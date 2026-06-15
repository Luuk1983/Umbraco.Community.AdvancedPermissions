import { html, css, nothing, customElement, state, query } from '@umbraco-cms/backoffice/external/lit';
import type { TemplateResult } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import { UMB_MODAL_MANAGER_CONTEXT } from '@umbraco-cms/backoffice/modal';
import type {
  RoleInfo,
  PermissionEntry,
  PermissionState,
  PermissionScope,
} from '../models/permission.models.js';
import { VIRTUAL_ROOT_NODE_KEY } from '../models/permission.models.js';
import type { ElementTreeNodeState } from '../models/element-permission.models.js';
import {
  getElementTreeRoot,
  getElementTreeChildren,
  getElementPermissions,
  saveElementPermissions,
} from '../api/element-permissions.api.js';
import { clearElementEffectivePermissionCache } from '../conditions/element-permission-condition.base.js';
import { UAP_ROLE_PICKER_MODAL } from '../access-viewer/role-picker-modal.token.js';
import { decomposeEntries } from '../utils/decompose-entries.js';
import { type PendingVerbEntries } from '../utils/compose-entries.js';
import type { CellInfo } from '../utils/cell-info.js';
import { getCellInfo } from '../utils/cell-info.js';
import { updateNode, findNode } from '../utils/tree-ops.js';
import { LIBRARY_VERB_METAS, libraryApplicability } from './library-permission.descriptor.js';
import '../components/uap-picker-button.element.js';
import '../shared/components/uap-perm-block.element.js';
import '../shared/components/uap-permission-scope-dialog.element.js';
import type { UapPermissionScopeDialogElement } from '../shared/components/uap-permission-scope-dialog.element.js';

/** Map of verb → pending entries for a single node. */
type PendingNodeChanges = Map<string, PendingVerbEntries>;

const VIRTUAL_ROOT_KEY = 'virtual-root';

/**
 * Library permissions editor. Mirrors the content Permissions Editor but operates on the element tree
 * (folders + elements) using the canonical element verbs, with per-node-kind applicability supplied by
 * {@link libraryApplicability}: element-only verbs are descendants-only on folders, and leaf elements
 * have no descendants side.
 */
@customElement('uap-library-permissions-editor-root')
export class UapLibraryPermissionsEditorRootElement extends UmbLitElement {
  #localize = new UmbLocalizationController(this);

  @state() private _selectedRole: RoleInfo | null = null;
  @state() private _treeNodes: ElementTreeNodeState[] = [];
  @state() private _loading = false;
  @state() private _saving = false;
  @state() private _error: string | null = null;

  /** Pending changes: nodeKey → (verb → PendingVerbEntries). */
  @state() private _pendingChanges: Map<string, PendingNodeChanges> = new Map();

  // ── Permission dialog state ─────────────────────────────────────────────
  @state() private _pickerNode: ElementTreeNodeState | null = null;
  @state() private _pickerVerb: string | null = null;
  @state() private _pickerIsVirtualRoot = false;
  @state() private _pickerNodeApplicable = true;
  @state() private _pickerDescApplicable = true;
  @state() private _pickerNodeState: 'inherit' | 'allow' | 'deny' = 'inherit';
  @state() private _pickerDescState: 'inherit' | 'allow' | 'deny' = 'inherit';
  @state() private _pickerSameAsNode = true;
  @state() private _pickerNodeIsPriorityOverride = false;
  @state() private _pickerDescIsPriorityOverride = false;

  @query('uap-permission-scope-dialog') private _scopeDialog!: UapPermissionScopeDialogElement;

  #notificationContext: typeof UMB_NOTIFICATION_CONTEXT.TYPE | undefined = undefined;
  #modalManager: typeof UMB_MODAL_MANAGER_CONTEXT.TYPE | undefined = undefined;
  #loadAbortController: AbortController | null = null;

  constructor() {
    super();
    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (ctx) => { this.#notificationContext = ctx ?? undefined; });
    this.consumeContext(UMB_MODAL_MANAGER_CONTEXT, (ctx) => { this.#modalManager = ctx ?? undefined; });
  }

  override disconnectedCallback(): void {
    super.disconnectedCallback();
    this.#loadAbortController?.abort();
  }

  // ── Role selection + tree loading ───────────────────────────────────────

  async #openRolePicker(): Promise<void> {
    if (!this.#modalManager) return;
    const modal = this.#modalManager.open(this, UAP_ROLE_PICKER_MODAL, {
      data: { ...(this._selectedRole ? { currentRole: this._selectedRole.alias } : {}) },
    });
    const result = await modal.onSubmit().catch(() => undefined);
    if (!result) return;
    this._selectedRole = result.role;
    this._pendingChanges = new Map();
    void this.#loadTree();
  }

  async #loadTree(): Promise<void> {
    if (!this._selectedRole) return;
    this.#loadAbortController?.abort();
    const controller = new AbortController();
    this.#loadAbortController = controller;

    this._loading = true;
    this._error = null;
    this._treeNodes = [];
    try {
      const [virtualEntries, nodes] = await Promise.all([
        getElementPermissions(VIRTUAL_ROOT_NODE_KEY, this._selectedRole.alias, controller.signal),
        getElementTreeRoot(this._selectedRole.alias, controller.signal),
      ]);
      if (controller.signal.aborted) return;

      const virtualRoot: ElementTreeNodeState = {
        key: VIRTUAL_ROOT_KEY,
        name: this.#localize.term('uap_library_root'),
        icon: 'icon-globe',
        hasChildren: false,
        isFolder: false,
        entries: virtualEntries,
        expanded: false,
        loading: false,
      };
      this._treeNodes = [virtualRoot, ...nodes.map((n) => ({ ...n, expanded: false, loading: false }))];
    } catch (err) {
      if (controller.signal.aborted) return;
      this._error = String(err);
    } finally {
      if (!controller.signal.aborted) this._loading = false;
    }
  }

  async #toggleExpand(node: ElementTreeNodeState): Promise<void> {
    if (node.expanded) { this.#updateNode(node.key, { expanded: false }); return; }
    if (node.children) { this.#updateNode(node.key, { expanded: true }); return; }
    this.#updateNode(node.key, { loading: true });
    try {
      const children = await getElementTreeChildren(node.key, this._selectedRole!.alias);
      this.#updateNode(node.key, {
        expanded: true,
        loading: false,
        children: children.map((c) => ({ ...c, expanded: false, loading: false })),
      });
    } catch (err) {
      this.#updateNode(node.key, { loading: false });
      this._error = String(err);
    }
  }

  #updateNode(key: string, changes: Partial<ElementTreeNodeState>): void {
    this._treeNodes = updateNode(this._treeNodes, key, changes);
  }

  // ── Permission dialog ───────────────────────────────────────────────────

  #openPicker(node: ElementTreeNodeState, verb: string): void {
    const isVirtualRoot = node.key === VIRTUAL_ROOT_KEY;
    const entries = this.#getCellEntries(node, verb);

    this._pickerNode = node;
    this._pickerVerb = verb;
    this._pickerIsVirtualRoot = isVirtualRoot;

    if (isVirtualRoot) {
      this._pickerNodeApplicable = true;
      this._pickerDescApplicable = true;
      const first = entries[0];
      this._pickerNodeState = first ? (first.state === 'Allow' ? 'allow' : 'deny') : 'inherit';
      this._pickerDescState = 'inherit';
      this._pickerSameAsNode = true;
      this._pickerNodeIsPriorityOverride = first?.isPriorityOverride === true;
      this._pickerDescIsPriorityOverride = false;
    } else {
      const app = libraryApplicability(verb, node.isFolder);
      this._pickerNodeApplicable = app.nodeApplicable;
      this._pickerDescApplicable = app.descApplicable;
      const d = decomposeEntries(entries);

      if (app.nodeApplicable && app.descApplicable) {
        this._pickerNodeState = d.nodeState;
        this._pickerDescState = d.descState;
        this._pickerSameAsNode = d.sameAsNode;
        this._pickerNodeIsPriorityOverride = d.nodeIsPriorityOverride;
        this._pickerDescIsPriorityOverride = d.descIsPriorityOverride;
      } else if (app.nodeApplicable) {
        // Leaf element: single this-node choice (dialog single mode uses _pickerNodeState).
        this._pickerNodeState = d.nodeState;
        this._pickerNodeIsPriorityOverride = d.nodeIsPriorityOverride;
      } else {
        // Element-only verb on a folder: single descendants choice — surface the descendant state.
        this._pickerNodeState = d.descState;
        this._pickerNodeIsPriorityOverride = d.descIsPriorityOverride;
      }
    }

    void this.updateComplete.then(() => this._scopeDialog.open());
  }

  #handleScopeApply(e: CustomEvent<{ entries: PendingVerbEntries }>): void {
    if (!this._pickerNode || !this._pickerVerb) return;
    const nodeKey = this._pickerNode.key;
    const verb = this._pickerVerb;
    const nodeChanges: PendingNodeChanges = this._pendingChanges.get(nodeKey) ?? new Map();
    nodeChanges.set(verb, e.detail.entries);
    this._pendingChanges = new Map(this._pendingChanges).set(nodeKey, nodeChanges);
  }

  // ── Save ─────────────────────────────────────────────────────────────────

  async #saveChanges(): Promise<void> {
    if (!this._pendingChanges.size || !this._selectedRole || this._saving) return;
    this._saving = true;
    try {
      for (const [nodeKey, verbChanges] of this._pendingChanges) {
        const node = this.#findNode(nodeKey);
        if (!node) continue;

        const byVerb = new Map<string, Array<{ verb: string; state: PermissionState; scope: PermissionScope; isPriorityOverride: boolean }>>();
        for (const en of node.entries) {
          const list = byVerb.get(en.verb) ?? [];
          list.push({ verb: en.verb, state: en.state, scope: en.scope, isPriorityOverride: en.isPriorityOverride });
          byVerb.set(en.verb, list);
        }
        for (const [verb, pending] of verbChanges) {
          if (pending.length === 0) byVerb.delete(verb);
          else byVerb.set(verb, pending.map((pe) => ({ verb, state: pe.state, scope: pe.scope, isPriorityOverride: pe.isPriorityOverride })));
        }

        const allEntries = [...byVerb.values()].flat();
        const apiKey = nodeKey === VIRTUAL_ROOT_KEY ? VIRTUAL_ROOT_NODE_KEY : nodeKey;
        await saveElementPermissions(apiKey, this._selectedRole.alias, allEntries);

        const saved: PermissionEntry[] = allEntries.map((en, idx) => ({
          id: String(idx),
          nodeKey: apiKey,
          roleAlias: this._selectedRole!.alias,
          verb: en.verb,
          state: en.state,
          scope: en.scope,
          isPriorityOverride: en.isPriorityOverride,
        }));
        this.#updateNode(nodeKey, { entries: saved });
      }
      this._pendingChanges = new Map();
      clearElementEffectivePermissionCache();
      this.#notificationContext?.peek('positive', { data: { message: this.#localize.term('uap_library_permissionsSaved') } });
    } catch (err) {
      this.#notificationContext?.peek('danger', { data: { message: this.#localize.term('uap_saveFailed', String(err)) } });
    } finally {
      this._saving = false;
    }
  }

  // ── Helpers ──────────────────────────────────────────────────────────────

  #findNode(key: string): ElementTreeNodeState | null {
    return findNode(this._treeNodes, key);
  }

  #getCellEntries(node: ElementTreeNodeState, verb: string): PendingVerbEntries | PermissionEntry[] {
    const pending = this._pendingChanges.get(node.key);
    if (pending?.has(verb)) return pending.get(verb)!;
    return node.entries.filter((e) => e.verb === verb);
  }

  /**
   * Computes the cell rendering shape honouring applicability:
   * - fully N/A (Create on a leaf) → hatched N/A block, not clickable;
   * - leaf node-only → uniform block of the node state;
   * - folder descendants-only (element-only verbs) → split block with an N/A node half;
   * - otherwise → the standard split/uniform from {@link getCellInfo}.
   */
  #cellInfo(node: ElementTreeNodeState, verb: string): { info: CellInfo | null; na: boolean } {
    const entries = this.#getCellEntries(node, verb);
    if (node.key === VIRTUAL_ROOT_KEY) {
      return { info: getCellInfo(entries), na: false };
    }
    const app = libraryApplicability(verb, node.isFolder);
    if (!app.nodeApplicable && !app.descApplicable) {
      return { info: null, na: true };
    }
    const d = decomposeEntries(entries);
    if (app.nodeApplicable && app.descApplicable) {
      return { info: getCellInfo(entries), na: false };
    }
    if (app.nodeApplicable) {
      // Leaf element — uniform of the node state.
      return {
        info: { split: false, nodeClass: d.nodeState, descClass: d.nodeState, nodeOverride: d.nodeIsPriorityOverride },
        na: false,
      };
    }
    // Folder, element-only verb — N/A node half + descendant state.
    return {
      info: { split: true, nodeNa: true, nodeClass: 'inherit', descClass: d.descState, descOverride: d.descIsPriorityOverride },
      na: false,
    };
  }

  // ── Rendering ─────────────────────────────────────────────────────────────

  #renderRows(nodes: ElementTreeNodeState[], depth: number): TemplateResult[] {
    return nodes.flatMap((node) => [
      this.#renderRow(node, depth),
      ...(node.expanded && node.children ? this.#renderRows(node.children, depth + 1) : []),
    ]);
  }

  #renderRow(node: ElementTreeNodeState, depth: number): TemplateResult {
    const hasPending = this._pendingChanges.has(node.key);
    return html`
      <tr class=${hasPending ? 'row-pending' : ''}>
        <td class="node-cell">
          <div class="node-inner" style="--depth: ${depth}">
            ${node.hasChildren || node.children
              ? html`<uui-button compact look="default"
                  label=${node.expanded ? this.#localize.term('uap_collapse') : this.#localize.term('uap_expand')}
                  @click=${() => void this.#toggleExpand(node)}>
                  ${node.loading ? html`<uui-loader-circle></uui-loader-circle>` : node.expanded ? '▾' : '▸'}
                </uui-button>`
              : html`<span class="expand-spacer"></span>`}
            <umb-icon name=${node.icon ?? (node.isFolder ? 'icon-folder' : 'icon-document')}></umb-icon>
            <span class="node-name">${node.name}</span>
          </div>
        </td>
        ${LIBRARY_VERB_METAS.map((v) => this.#renderCell(node, v.verb))}
      </tr>
    `;
  }

  #renderCell(node: ElementTreeNodeState, verb: string): TemplateResult {
    const { info, na } = this.#cellInfo(node, verb);
    const isPending = this._pendingChanges.get(node.key)?.has(verb) ?? false;

    if (na) {
      return html`<td class="perm-td na-td" title=${this.#localize.term('uap_library_notApplicableTitle', verb.split('.').pop() ?? '')}>
        <uap-perm-block na ?pending=${isPending}></uap-perm-block>
      </td>`;
    }

    return html`
      <td class="perm-td" title=${verb} @click=${() => this.#openPicker(node, verb)}>
        <uap-perm-block
          .info=${info}
          ?pending=${isPending}
          priority-override-title=${this.#localize.term('uap_priorityOverrideBadgeTitle')}></uap-perm-block>
      </td>
    `;
  }

  #renderDialog(): TemplateResult {
    const verbName = this._pickerVerb?.split('.').pop() ?? '';
    const nodeName = this._pickerIsVirtualRoot
      ? this.#localize.term('uap_library_root')
      : (this._pickerNode?.name ?? '');

    return html`
      <uap-permission-scope-dialog
        .verb=${verbName}
        .nodeName=${nodeName}
        .isVirtualRoot=${this._pickerIsVirtualRoot}
        .nodeApplicable=${this._pickerNodeApplicable}
        .descApplicable=${this._pickerDescApplicable}
        .initialNodeState=${this._pickerNodeState}
        .initialDescState=${this._pickerDescState}
        .initialSameAsNode=${this._pickerSameAsNode}
        .initialNodeIsPriorityOverride=${this._pickerNodeIsPriorityOverride}
        .initialDescIsPriorityOverride=${this._pickerDescIsPriorityOverride}
        @uap-scope-apply=${(e: CustomEvent<{ entries: PendingVerbEntries }>) => this.#handleScopeApply(e)}>
      </uap-permission-scope-dialog>
    `;
  }

  override render() {
    const hasPending = this._pendingChanges.size > 0;
    return html`
      <umb-body-layout headline=${this.#localize.term('uap_library_editorHeadline')}>
        <div class="toolbar">
          <uap-picker-button
            label=${this.#localize.term('uap_chooseRole')}
            .selectedName=${this._selectedRole?.name ?? ''}
            icon="icon-users"
            @click=${() => void this.#openRolePicker()}>
          </uap-picker-button>
          ${hasPending
            ? html`
                <uui-button label=${this.#localize.term('uap_saveChanges')} look="primary" color="positive" ?loading=${this._saving} @click=${() => void this.#saveChanges()}>
                  ${this.#localize.term('uap_saveChanges')}
                </uui-button>
                <uui-button label=${this.#localize.term('uap_discard')} look="outline" @click=${() => { this._pendingChanges = new Map(); }}>
                  ${this.#localize.term('uap_discard')}
                </uui-button>
              `
            : nothing}
        </div>

        ${this._error ? html`<p class="error-msg">⚠ ${this._error}</p>` : nothing}
        ${this._loading ? html`<div class="loading"><uui-loader></uui-loader></div>` : nothing}
        ${!this._selectedRole ? html`<p class="empty-msg">${this.#localize.term('uap_library_selectRolePrompt')}</p>` : nothing}

        ${this._selectedRole && !this._loading && this._treeNodes.length > 0
          ? html`
              <div class="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th class="node-header">${this.#localize.term('uap_library_nodeHeader')}</th>
                      ${LIBRARY_VERB_METAS.map((v) => html`<th class="verb-header" title=${v.verb}>${v.displayName}</th>`)}
                    </tr>
                  </thead>
                  <tbody>
                    ${this.#renderRows(this._treeNodes, 0)}
                  </tbody>
                </table>
              </div>
            `
          : nothing}
      </umb-body-layout>

      ${this.#renderDialog()}
    `;
  }

  static override styles = css`
    :host { display: block; height: 100%; }

    .toolbar {
      display: flex;
      align-items: center;
      gap: var(--uui-size-4, 12px);
      padding: var(--uui-size-3, 9px) var(--uui-size-6, 18px);
      background: var(--uui-color-surface, #fff);
      border-bottom: 1px solid var(--uui-color-border, #e0e0e0);
      flex-wrap: wrap;
    }

    .loading { display: flex; justify-content: center; padding: 32px; }
    .error-msg { padding: 12px 18px; color: var(--uui-color-danger, #b91c1c); }
    .empty-msg { padding: 32px 18px; color: var(--uui-color-text-alt, #888); }

    .table-wrap { overflow-x: auto; }
    table { width: 100%; border-collapse: collapse; table-layout: fixed; }
    thead { position: sticky; top: 0; z-index: 2; }

    th {
      padding: 6px 4px;
      text-align: center;
      border-bottom: 1px solid var(--uui-color-border, #ddd);
      font-weight: 600;
      line-height: 1.3;
      background: var(--uui-color-surface, #fff);
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
      color: var(--uui-color-text-alt, #666);
    }

    th.node-header {
      width: 40%;
      text-align: left;
      padding-left: 8px;
      position: sticky;
      left: 0;
      z-index: 3;
      white-space: nowrap;
      color: var(--uui-color-text, #333);
    }

    td { border-bottom: 1px solid var(--uui-color-border, #f0f0f0); }
    tr:hover td { background-color: var(--uui-color-surface-emphasis, #fafafa); }
    tr.row-pending td { background-color: color-mix(in srgb, oklch(85% 0.15 90) 12%, transparent); }

    td.node-cell {
      padding: 0;
      position: sticky;
      left: 0;
      background: inherit;
      vertical-align: middle;
    }

    .node-inner {
      display: flex;
      align-items: center;
      gap: 4px;
      padding: 0 8px 0 calc(var(--depth, 0) * 18px + 8px);
      height: 32px;
      white-space: nowrap;
      overflow: hidden;
    }

    .expand-spacer { width: 16px; flex-shrink: 0; }
    .node-name { overflow: hidden; text-overflow: ellipsis; }

    .perm-td { padding: 3px; text-align: center; vertical-align: middle; }
    .na-td { cursor: default; }
  `;
}

export default UapLibraryPermissionsEditorRootElement;

declare global {
  interface HTMLElementTagNameMap {
    'uap-library-permissions-editor-root': UapLibraryPermissionsEditorRootElement;
  }
}
