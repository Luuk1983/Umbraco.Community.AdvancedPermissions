import { html, css, nothing, customElement, state, query } from '@umbraco-cms/backoffice/external/lit';
import type { TemplateResult } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import { UMB_MODAL_MANAGER_CONTEXT } from '@umbraco-cms/backoffice/modal';
import type {
  RoleInfo,
  TreeNodeState,
  PermissionEntry,
} from '../models/permission.models.js';
import { VIRTUAL_ROOT_NODE_KEY } from '../models/permission.models.js';
import { getTreeRoot, getTreeChildren } from '../api/advanced-permissions.api.js';
import { getDocTypes, getDocTypePermissions, saveDocTypePermissions } from '../api/doc-type-permissions.api.js';
import type { DocTypeListItem, DocTypePermissionEntry } from '../models/doc-type-permission.models.js';
import { decomposeEntries } from '../utils/decompose-entries.js';
import { type PendingVerbEntries } from '../utils/compose-entries.js';
import { getCellInfo, type CellInfo } from '../utils/cell-info.js';
import { updateNode } from '../utils/tree-ops.js';
import { UAP_ROLE_PICKER_MODAL } from '../access-viewer/role-picker-modal.token.js';
import { UMB_DOCUMENT_TYPE_PICKER_MODAL } from '@umbraco-cms/backoffice/document-type';
import '../components/uap-picker-button.element.js';
import '../shared/components/uap-perm-block.element.js';
import '../shared/components/uap-permission-scope-dialog.element.js';
import type { UapPermissionScopeDialogElement } from '../shared/components/uap-permission-scope-dialog.element.js';

/** The single verb v1 ships for doc-type permissions. */
const VERB = 'Umb.Document.CreateOfType';

/** Sentinel local key for the virtual-root row in the tree. Mapped back to `VIRTUAL_ROOT_NODE_KEY` on save. */
const VIRTUAL_ROOT_LOCAL_KEY = 'virtual-root';

/**
 * Document-type Permissions Editor workspace.
 *
 * Mirrors the layout of the existing Permissions Editor with two differences:
 * 1. Two pickers in the toolbar — role *and* doc-type — both required before the tree loads.
 * 2. One "Allowed" column instead of N verb columns. Each row's cell shows the resolved entry
 *    state for the chosen (role, doc-type, node) triple.
 *
 * The virtual-root row defaults to a visible Allow when no entry exists, reflecting that the
 * doc-type resolver uses default-Allow semantics. The shared scope dialog handles editing;
 * saves go through the typed SDK's `DocTypePermissionsService.save`.
 */
@customElement('uap-doc-type-permissions-editor-root')
export class UapDocTypePermissionsEditorRootElement extends UmbLitElement {
  #localize = new UmbLocalizationController(this);

  @state() private _docTypes: DocTypeListItem[] = [];

  @state() private _selectedRole: RoleInfo | null = null;
  @state() private _selectedDocType: DocTypeListItem | null = null;
  @state() private _treeNodes: TreeNodeState[] = [];
  @state() private _loading = false;
  @state() private _saving = false;
  @state() private _error: string | null = null;

  /** nodeKey → pending entries for this (role, doc-type). Empty list = clear. */
  @state() private _pendingChanges: Map<string, PendingVerbEntries> = new Map();

  // Scope-dialog state — mirrors the existing editor.
  @state() private _pickerNode: TreeNodeState | null = null;
  @state() private _pickerIsVirtualRoot = false;
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
    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (ctx) => {
      this.#notificationContext = ctx ?? undefined;
    });
    this.consumeContext(UMB_MODAL_MANAGER_CONTEXT, (ctx) => {
      this.#modalManager = ctx ?? undefined;
    });
  }

  override connectedCallback(): void {
    super.connectedCallback();
    void this.#loadMeta();
  }

  override disconnectedCallback(): void {
    super.disconnectedCallback();
    this.#loadAbortController?.abort();
  }

  // ── Data loading ────────────────────────────────────────────────────────

  async #loadMeta(): Promise<void> {
    try {
      this._docTypes = await getDocTypes();
    } catch (err) {
      this._error = String(err);
    }
  }

  async #openRolePicker(): Promise<void> {
    if (!this.#modalManager) return;
    const modal = this.#modalManager.open(this, UAP_ROLE_PICKER_MODAL, {
      data: {
        ...(this._selectedRole ? { currentRole: this._selectedRole.alias } : {}),
      },
    });
    const result = await modal.onSubmit().catch(() => undefined);
    if (!result) return;

    const hadTree = this._treeNodes.length > 0 && this._selectedRole !== null && this._selectedDocType !== null;
    this._selectedRole = result.role;
    this._pendingChanges = new Map();
    if (hadTree) {
      void this.#reloadEntries();
    } else if (this._selectedDocType) {
      void this.#loadTree();
    }
  }

  /**
   * Opens Umbraco's built-in document-type tree picker, excluding element types and folders.
   * The picked GUID is mapped back to a `DocTypeListItem` (for its name/icon) via the already
   * loaded `_docTypes` list, then the existing reload/load flow runs.
   */
  async #openDocTypePicker(): Promise<void> {
    if (!this.#modalManager) return;
    const modal = this.#modalManager.open(this, UMB_DOCUMENT_TYPE_PICKER_MODAL, {
      data: {
        hideTreeRoot: true,
        pickableFilter: (item) => !item.isFolder && item.isElement === false,
      },
      value: { selection: this._selectedDocType ? [this._selectedDocType.key] : [] },
    });
    const value = await modal.onSubmit().catch(() => undefined);
    if (!value) return;

    const key = value.selection?.[0];
    const picked = key
      ? (this._docTypes.find((dt) => dt.key.toLowerCase() === key.toLowerCase()) ?? null)
      : null;

    const hadTree = this._treeNodes.length > 0 && this._selectedRole !== null && this._selectedDocType !== null;
    this._selectedDocType = picked;
    this._pendingChanges = new Map();
    if (!this._selectedDocType) {
      this._treeNodes = [];
      return;
    }
    if (hadTree) {
      void this.#reloadEntries();
    } else if (this._selectedRole) {
      void this.#loadTree();
    }
  }

  async #loadTree(): Promise<void> {
    if (!this._selectedRole || !this._selectedDocType) return;

    this.#loadAbortController?.abort();
    const controller = new AbortController();
    this.#loadAbortController = controller;

    this._loading = true;
    this._error = null;
    this._treeNodes = [];

    try {
      const [entries, nodes] = await Promise.all([
        getDocTypePermissions(this._selectedRole.alias, this._selectedDocType.key, controller.signal),
        getTreeRoot('$everyone', controller.signal),
      ]);
      if (controller.signal.aborted) return;

      const entriesByNode = this.#groupEntriesByNode(entries);

      const virtualRoot: TreeNodeState = {
        key: VIRTUAL_ROOT_LOCAL_KEY,
        name: this.#localize.term('uap_contentRoot'),
        icon: 'icon-folder',
        hasChildren: false,
        entries: entriesByNode.get(VIRTUAL_ROOT_NODE_KEY) ?? [],
        expanded: false,
        loading: false,
      };
      this._treeNodes = [
        virtualRoot,
        ...nodes.map((n) => ({
          ...n,
          entries: entriesByNode.get(n.key) ?? [],
          expanded: false,
          loading: false,
        })),
      ];
    } catch (err) {
      if (controller.signal.aborted) return;
      this._error = String(err);
    } finally {
      if (!controller.signal.aborted) this._loading = false;
    }
  }

  /**
   * Reloads only the entries for the current tree on (role, doc-type) change. Preserves
   * expanded state and existing children. Re-applies entries to all loaded tree nodes.
   */
  async #reloadEntries(): Promise<void> {
    if (!this._selectedRole || !this._selectedDocType || this._treeNodes.length === 0) return;

    this.#loadAbortController?.abort();
    const controller = new AbortController();
    this.#loadAbortController = controller;

    this._loading = true;
    this._error = null;

    try {
      const entries = await getDocTypePermissions(
        this._selectedRole.alias,
        this._selectedDocType.key,
        controller.signal,
      );
      if (controller.signal.aborted) return;

      const entriesByNode = this.#groupEntriesByNode(entries);
      this.#applyEntriesRecursive(this._treeNodes, entriesByNode);
      this._treeNodes = [...this._treeNodes];
    } catch (err) {
      if (controller.signal.aborted) return;
      this._error = String(err);
    } finally {
      if (!controller.signal.aborted) this._loading = false;
    }
  }

  /**
   * Bins flat entries by their node key for O(1) tree-walk lookups.
   */
  #groupEntriesByNode(entries: DocTypePermissionEntry[]): Map<string, PermissionEntry[]> {
    const map = new Map<string, PermissionEntry[]>();
    for (const e of entries) {
      const list = map.get(e.nodeKey) ?? [];
      list.push({
        id: e.id,
        nodeKey: e.nodeKey,
        roleAlias: e.roleAlias,
        verb: e.verb,
        state: e.state,
        scope: e.scope,
        isPriorityOverride: e.isPriorityOverride,
      });
      map.set(e.nodeKey, list);
    }
    return map;
  }

  /**
   * Walks the loaded tree and sets each node's `entries` from the supplied map. Virtual-root
   * row is keyed locally; map lookup uses `VIRTUAL_ROOT_NODE_KEY` for it.
   */
  #applyEntriesRecursive(
    nodes: TreeNodeState[],
    entriesByNode: Map<string, PermissionEntry[]>,
  ): void {
    for (const node of nodes) {
      const lookupKey = node.key === VIRTUAL_ROOT_LOCAL_KEY ? VIRTUAL_ROOT_NODE_KEY : node.key;
      node.entries = entriesByNode.get(lookupKey) ?? [];
      if (node.children) this.#applyEntriesRecursive(node.children, entriesByNode);
    }
  }

  async #toggleExpand(node: TreeNodeState): Promise<void> {
    if (node.expanded) {
      this.#updateNode(node.key, { expanded: false });
      return;
    }
    if (node.children) {
      this.#updateNode(node.key, { expanded: true });
      return;
    }
    this.#updateNode(node.key, { loading: true });
    try {
      const children = await getTreeChildren(node.key, '$everyone');
      const entries = await getDocTypePermissions(
        this._selectedRole!.alias,
        this._selectedDocType!.key,
      );
      const entriesByNode = this.#groupEntriesByNode(entries);
      this.#updateNode(node.key, {
        expanded: true,
        loading: false,
        children: children.map((c) => ({
          ...c,
          entries: entriesByNode.get(c.key) ?? [],
          expanded: false,
          loading: false,
        })),
      });
    } catch (err) {
      this.#updateNode(node.key, { loading: false });
      this._error = String(err);
    }
  }

  #updateNode(key: string, changes: Partial<TreeNodeState>): void {
    this._treeNodes = updateNode(this._treeNodes, key, changes);
  }

  // ── Scope dialog ─────────────────────────────────────────────────────────

  #openPicker(node: TreeNodeState): void {
    this._pickerNode = node;
    this._pickerIsVirtualRoot = node.key === VIRTUAL_ROOT_LOCAL_KEY;

    const entries = this.#getCellEntries(node);

    if (this._pickerIsVirtualRoot) {
      const first = entries[0];
      this._pickerNodeState = first ? (first.state === 'Allow' ? 'allow' : 'deny') : 'inherit';
      this._pickerDescState = 'inherit';
      this._pickerSameAsNode = true;
      this._pickerNodeIsPriorityOverride = first?.isPriorityOverride === true;
      this._pickerDescIsPriorityOverride = false;
    } else {
      const decomposed = decomposeEntries(entries);
      this._pickerNodeState = decomposed.nodeState;
      this._pickerDescState = decomposed.descState;
      this._pickerSameAsNode = decomposed.sameAsNode;
      this._pickerNodeIsPriorityOverride = decomposed.nodeIsPriorityOverride;
      this._pickerDescIsPriorityOverride = decomposed.descIsPriorityOverride;
    }

    void this.updateComplete.then(() => this._scopeDialog.open());
  }

  /**
   * Receives the composed entries from the shared scope dialog and writes them into the
   * pending-changes map for the cell that opened the dialog.
   */
  #handleScopeApply(e: CustomEvent<{ entries: PendingVerbEntries }>): void {
    if (!this._pickerNode) return;
    this._pendingChanges = new Map(this._pendingChanges).set(this._pickerNode.key, e.detail.entries);
  }

  // ── Save ─────────────────────────────────────────────────────────────────

  async #saveChanges(): Promise<void> {
    if (!this._pendingChanges.size || !this._selectedRole || !this._selectedDocType || this._saving) return;
    this._saving = true;
    const role = this._selectedRole.alias;
    const ctKey = this._selectedDocType.key;
    try {
      for (const [nodeKey, pending] of this._pendingChanges) {
        const apiKey = nodeKey === VIRTUAL_ROOT_LOCAL_KEY ? VIRTUAL_ROOT_NODE_KEY : nodeKey;
        const entriesToSave = pending.map((p) => ({
          verb: VERB,
          state: p.state,
          scope: p.scope,
          isPriorityOverride: p.isPriorityOverride,
        }));
        await saveDocTypePermissions(apiKey, role, ctKey, entriesToSave);

        const saved: PermissionEntry[] = entriesToSave.map((e, idx) => ({
          id: String(idx),
          nodeKey: apiKey,
          roleAlias: role,
          verb: e.verb,
          state: e.state,
          scope: e.scope,
          isPriorityOverride: e.isPriorityOverride,
        }));
        this.#updateNode(nodeKey, { entries: saved });
      }
      this._pendingChanges = new Map();
      this.#notificationContext?.peek('positive', {
        data: { message: this.#localize.term('uap_permissionsSaved') },
      });
    } catch (err) {
      this.#notificationContext?.peek('danger', {
        data: { message: this.#localize.term('uap_saveFailed', String(err)) },
      });
    } finally {
      this._saving = false;
    }
  }

  // ── Helpers ──────────────────────────────────────────────────────────────

  /**
   * Returns either pending entries (if any) or stored entries for the given node. The doc-type
   * editor has only one verb so we don't filter by verb the way the existing editor does.
   */
  #getCellEntries(node: TreeNodeState): PendingVerbEntries | PermissionEntry[] {
    const pending = this._pendingChanges.get(node.key);
    if (pending !== undefined) return pending;
    return node.entries;
  }

  /**
   * Computes the cell's display info. For the virtual-root row, an empty entry list defaults
   * to a visible Allow (rather than the usual Inherit dash) to reflect default-Allow semantics.
   */
  #getDisplayInfo(node: TreeNodeState): CellInfo {
    const entries = this.#getCellEntries(node);
    if (entries.length === 0 && node.key === VIRTUAL_ROOT_LOCAL_KEY) {
      return { split: false, nodeClass: 'allow', descClass: 'allow' };
    }
    return getCellInfo(entries);
  }

  // ── Rendering ────────────────────────────────────────────────────────────

  #renderRows(nodes: TreeNodeState[], depth: number): TemplateResult[] {
    return nodes.flatMap((node) => [
      this.#renderRow(node, depth),
      ...(node.expanded && node.children ? this.#renderRows(node.children, depth + 1) : []),
    ]);
  }

  #renderRow(node: TreeNodeState, depth: number): TemplateResult {
    const hasPending = this._pendingChanges.has(node.key);
    const info = this.#getDisplayInfo(node);
    const isPending = this._pendingChanges.has(node.key);
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
            <umb-icon name=${node.icon ?? 'icon-document'}></umb-icon>
            <span class="node-name">${node.name}</span>
          </div>
        </td>
        <td class="perm-td" @click=${() => this.#openPicker(node)}>
          <uap-perm-block
            .info=${info}
            ?pending=${isPending}
            priority-override-title=${this.#localize.term('uap_priorityOverrideBadgeTitle')}></uap-perm-block>
        </td>
      </tr>
    `;
  }

  override render(): TemplateResult {
    const hasPending = this._pendingChanges.size > 0;
    const docTypeName = this._selectedDocType?.name ?? '';
    const insertLabel = this.#localize.term('uap_docTypePermissions_verbInsert');
    const pickerVerbLabel = docTypeName ? `${insertLabel} – ${docTypeName}` : insertLabel;
    const pickerNodeName = this._pickerIsVirtualRoot
      ? this.#localize.term('uap_contentRoot')
      : (this._pickerNode?.name ?? '');

    return html`
      <umb-body-layout headline=${this.#localize.term('uap_docTypePermissions_workspaceTitle')}>
        <div class="toolbar">
          <uap-picker-button
            label=${this.#localize.term('uap_chooseRole')}
            .selectedName=${this._selectedRole?.name ?? ''}
            icon="icon-users"
            @click=${() => void this.#openRolePicker()}>
          </uap-picker-button>

          <uap-picker-button
            label=${this.#localize.term('uap_chooseDocType')}
            .selectedName=${this._selectedDocType?.name ?? ''}
            icon=${this._selectedDocType?.icon ?? 'icon-document'}
            @click=${() => void this.#openDocTypePicker()}>
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
        ${!this._selectedRole || !this._selectedDocType
          ? html`<p class="empty-msg">${this.#localize.term('uap_docTypePermissions_pickToStart')}</p>`
          : nothing}

        ${this._selectedRole && this._selectedDocType && !this._loading && this._treeNodes.length > 0
          ? html`
              <div class="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th class="node-header">${this.#localize.term('uap_contentNodeHeader')}</th>
                      <th class="verb-header">${this.#localize.term('uap_docTypePermissions_verbInsert')}</th>
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

      <uap-permission-scope-dialog
        .verb=${pickerVerbLabel}
        .nodeName=${pickerNodeName}
        .isVirtualRoot=${this._pickerIsVirtualRoot}
        .inheritLabel=${this._pickerIsVirtualRoot ? '' : this.#localize.term('uap_docTypePermissions_notSet')}
        .initialNodeState=${this._pickerNodeState}
        .initialDescState=${this._pickerDescState}
        .initialSameAsNode=${this._pickerSameAsNode}
        .initialNodeIsPriorityOverride=${this._pickerNodeIsPriorityOverride}
        .initialDescIsPriorityOverride=${this._pickerDescIsPriorityOverride}
        @uap-scope-apply=${(e: CustomEvent<{ entries: PendingVerbEntries }>) => this.#handleScopeApply(e)}>
      </uap-permission-scope-dialog>
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
      color: var(--uui-color-text-alt, #666);
    }
    th.node-header {
      width: 70%;
      text-align: left;
      padding-left: 8px;
      position: sticky;
      left: 0;
      z-index: 3;
      color: var(--uui-color-text, #333);
    }

    td { border-bottom: 1px solid var(--uui-color-border, #f0f0f0); }
    tr:hover td { background-color: var(--uui-color-surface-emphasis, #fafafa); }
    tr.row-pending td {
      background-color: color-mix(in srgb, oklch(85% 0.15 90) 12%, transparent);
    }

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

    .perm-td {
      padding: 3px;
      text-align: center;
      vertical-align: middle;
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'uap-doc-type-permissions-editor-root': UapDocTypePermissionsEditorRootElement;
  }
}

export default UapDocTypePermissionsEditorRootElement;
