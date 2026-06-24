import { html, css, nothing, customElement, state, query } from '@umbraco-cms/backoffice/external/lit';
import type { TemplateResult } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import { UMB_MODAL_MANAGER_CONTEXT } from '@umbraco-cms/backoffice/modal';
import type {
  VerbInfo,
  RoleInfo,
  TreeNodeState,
  PermissionEntry,
  PermissionState,
  PermissionScope,
} from '../models/permission.models.js';
import { VIRTUAL_ROOT_NODE_KEY } from '../models/permission.models.js';
import { getVerbs, getTreeRoot, getTreeChildren, getPermissions, savePermissions } from '../api/advanced-permissions.api.js';
import { clearEffectivePermissionCache } from '../conditions/document-user-permission.condition.js';
import { UAP_ROLE_PICKER_MODAL } from '../access-viewer/role-picker-modal.token.js';
import { decomposeEntries } from '../utils/decompose-entries.js';
import { type PendingVerbEntries } from '../utils/compose-entries.js';
import { getCellInfo } from '../utils/cell-info.js';
import { updateNode, findNode } from '../utils/tree-ops.js';
import '../shared/components/uap-perm-block.element.js';
import '../shared/components/uap-permission-scope-dialog.element.js';
import '../help/uap-page-intro.element.js';
import '../help/uap-selection-panel.element.js';
import type { UapSelectorGroup } from '../help/uap-selection-panel.element.js';
import type { UapPermissionScopeDialogElement } from '../shared/components/uap-permission-scope-dialog.element.js';

/** Map of verb → pending entries for a single node. */
type PendingNodeChanges = Map<string, PendingVerbEntries>;

/**
 * Security Editor workspace element.
 * Allows administrators to view and edit raw permission entries per role and content node.
 */
@customElement('uap-permissions-editor-root')
export class UapPermissionsEditorRootElement extends UmbLitElement {
  #localize = new UmbLocalizationController(this);

  // ── Metadata ────────────────────────────────────────────────────────────
  @state() private _verbs: VerbInfo[] = [];

  // ── Selection & tree ────────────────────────────────────────────────────
  @state() private _selectedRole: RoleInfo | null = null;
  @state() private _treeNodes: TreeNodeState[] = [];
  @state() private _loading = false;
  @state() private _saving = false;
  @state() private _error: string | null = null;

  /**
   * Pending changes: nodeKey → (verb → PendingVerbEntries).
   * Empty PendingVerbEntries means "inherit" (delete all entries for that verb on save).
   */
  @state() private _pendingChanges: Map<string, PendingNodeChanges> = new Map();

  // ── Permission dialog ───────────────────────────────────────────────────
  @state() private _pickerNode: TreeNodeState | null = null;
  @state() private _pickerVerb: string | null = null;
  @state() private _pickerNodeIsPriorityOverride = false;
  @state() private _pickerDescIsPriorityOverride = false;
  @state() private _pickerIsVirtualRoot = false;
  @state() private _pickerNodeState: 'inherit' | 'allow' | 'deny' = 'inherit';
  @state() private _pickerDescState: 'inherit' | 'allow' | 'deny' = 'inherit';
  @state() private _pickerSameAsNode = true;

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
      this._verbs = await getVerbs();
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

    const hadTree = this._treeNodes.length > 0 && this._selectedRole !== null;
    this._selectedRole = result.role;
    this._pendingChanges = new Map();
    if (hadTree) {
      void this.#reloadPermissions();
    } else {
      void this.#loadTree();
    }
  }

  async #loadTree(): Promise<void> {
    if (!this._selectedRole) return;

    // Cancel any in-flight load from a previous role selection
    this.#loadAbortController?.abort();
    const controller = new AbortController();
    this.#loadAbortController = controller;

    this._loading = true;
    this._error = null;
    this._treeNodes = [];
    try {
      const [virtualEntries, nodes] = await Promise.all([
        getPermissions(VIRTUAL_ROOT_NODE_KEY, this._selectedRole!.alias, controller.signal),
        getTreeRoot(this._selectedRole!.alias, controller.signal),
      ]);
      if (controller.signal.aborted) return;

      const virtualRoot: TreeNodeState = {
        key: 'virtual-root',
        name: this.#localize.term('uap_contentRoot'),
        icon: 'icon-globe',
        hasChildren: false,
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

  /**
   * Reloads only the permission entries for the current tree structure without
   * rebuilding the tree. Preserves expanded state and children. Used when switching roles.
   */
  async #reloadPermissions(): Promise<void> {
    if (!this._selectedRole || this._treeNodes.length === 0) return;

    // Cancel any in-flight load from a previous role selection
    this.#loadAbortController?.abort();
    const controller = new AbortController();
    this.#loadAbortController = controller;

    this._loading = true;
    this._error = null;

    try {
      // Reload virtual root entries
      const virtualEntries = await getPermissions(VIRTUAL_ROOT_NODE_KEY, this._selectedRole!.alias, controller.signal);
      if (controller.signal.aborted) return;

      // Clear entries on all existing nodes and reload their entries
      this.#clearEntriesRecursive(this._treeNodes);

      // Set virtual root entries
      const virtualRoot = this._treeNodes.find((n) => n.key === 'virtual-root');
      if (virtualRoot) {
        virtualRoot.entries = virtualEntries;
      }

      // Reload entries for visible (loaded) nodes
      await this.#reloadNodeEntries(this._treeNodes, controller.signal);
      if (controller.signal.aborted) return;

      this._treeNodes = [...this._treeNodes]; // trigger re-render
    } catch (err) {
      if (controller.signal.aborted) return;
      this._error = String(err);
    } finally {
      if (!controller.signal.aborted) this._loading = false;
    }
  }

  #clearEntriesRecursive(nodes: TreeNodeState[]): void {
    for (const node of nodes) {
      node.entries = [];
      if (node.children) {
        this.#clearEntriesRecursive(node.children);
      }
    }
  }

  async #reloadNodeEntries(nodes: TreeNodeState[], signal: AbortSignal): Promise<void> {
    // Collect all non-virtual node keys that have been loaded (root level + expanded children)
    const keys: string[] = [];
    this.#collectLoadedKeys(nodes, keys);

    if (keys.length === 0) return;

    // Use the tree endpoint which batch-loads entries per role
    const [rootNodes] = await Promise.all([
      getTreeRoot(this._selectedRole!.alias, signal),
    ]);
    if (signal.aborted) return;

    // Build a map from key → entries
    const entryMap = new Map<string, typeof rootNodes[0]['entries']>();
    for (const n of rootNodes) {
      entryMap.set(n.key, n.entries);
    }

    // Apply entries to existing tree nodes at root level
    for (const node of nodes) {
      if (node.key === 'virtual-root') continue;
      const entries = entryMap.get(node.key);
      if (entries) node.entries = entries;
    }

    // For expanded children, reload their entries
    for (const node of nodes) {
      if (node.children && node.expanded) {
        const childEntries = await getTreeChildren(node.key, this._selectedRole!.alias, signal);
        if (signal.aborted) return;
        const childMap = new Map(childEntries.map((c) => [c.key, c.entries]));
        for (const child of node.children) {
          const entries = childMap.get(child.key);
          if (entries) child.entries = entries;
        }
        // Recurse for deeply expanded subtrees
        await this.#reloadChildEntries(node.children, signal);
        if (signal.aborted) return;
      }
    }
  }

  async #reloadChildEntries(nodes: TreeNodeState[], signal: AbortSignal): Promise<void> {
    for (const node of nodes) {
      if (node.children && node.expanded) {
        const childEntries = await getTreeChildren(node.key, this._selectedRole!.alias, signal);
        if (signal.aborted) return;
        const childMap = new Map(childEntries.map((c) => [c.key, c.entries]));
        for (const child of node.children) {
          const entries = childMap.get(child.key);
          if (entries) child.entries = entries;
        }
        await this.#reloadChildEntries(node.children, signal);
        if (signal.aborted) return;
      }
    }
  }

  #collectLoadedKeys(nodes: TreeNodeState[], keys: string[]): void {
    for (const node of nodes) {
      if (node.key !== 'virtual-root') keys.push(node.key);
      if (node.children) this.#collectLoadedKeys(node.children, keys);
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
      const children = await getTreeChildren(node.key, this._selectedRole!.alias);
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

  /**
   * Immutably updates the node with the given key. Reassigns `_treeNodes` so Lit picks up the
   * change. Delegates the recursive walk to the shared `updateNode` helper.
   */
  #updateNode(key: string, changes: Partial<TreeNodeState>): void {
    this._treeNodes = updateNode(this._treeNodes, key, changes);
  }

  // ── Permission dialog ───────────────────────────────────────────────────

  #openPicker(node: TreeNodeState, verb: string): void {
    this._pickerNode = node;
    this._pickerVerb = verb;
    this._pickerIsVirtualRoot = node.key === 'virtual-root';

    const entries = this.#getCellEntries(node, verb);

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
   * Handles `uap-scope-apply` from the shared scope dialog: writes the composed entries into
   * the pending-changes map under the cell that opened the dialog.
   */
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

        // Build the complete new entry list: start from stored, apply pending verb changes
        const byVerb = new Map<string, Array<{ verb: string; state: PermissionState; scope: PermissionScope; isPriorityOverride: boolean }>>();
        for (const e of node.entries) {
          const list = byVerb.get(e.verb) ?? [];
          list.push({ verb: e.verb, state: e.state, scope: e.scope, isPriorityOverride: e.isPriorityOverride });
          byVerb.set(e.verb, list);
        }
        for (const [verb, pending] of verbChanges) {
          if (pending.length === 0) {
            byVerb.delete(verb);
          } else {
            byVerb.set(verb, pending.map((pe) => ({
              verb,
              state: pe.state,
              scope: pe.scope,
              isPriorityOverride: pe.isPriorityOverride,
            })));
          }
        }

        const allEntries = [...byVerb.values()].flat();
        const apiKey = nodeKey === 'virtual-root' ? VIRTUAL_ROOT_NODE_KEY : nodeKey;
        await savePermissions(apiKey, this._selectedRole!.alias, allEntries);

        const saved: PermissionEntry[] = allEntries.map((e, idx) => ({
          id: String(idx),
          nodeKey: apiKey,
          roleAlias: this._selectedRole!.alias,
          verb: e.verb,
          state: e.state,
          scope: e.scope,
          isPriorityOverride: e.isPriorityOverride,
        }));
        this.#updateNode(nodeKey, { entries: saved });
      }
      this._pendingChanges = new Map();
      clearEffectivePermissionCache();
      this.#notificationContext?.peek('positive', { data: { message: this.#localize.term('uap_permissionsSaved') } });
    } catch (err) {
      this.#notificationContext?.peek('danger', { data: { message: this.#localize.term('uap_saveFailed', String(err)) } });
    } finally {
      this._saving = false;
    }
  }

  // ── Helpers ──────────────────────────────────────────────────────────────

  /** Recursive lookup wrapper using the shared `findNode` helper. */
  #findNode(key: string): TreeNodeState | null {
    return findNode(this._treeNodes, key);
  }

  #getCellEntries(node: TreeNodeState, verb: string): PendingVerbEntries | PermissionEntry[] {
    const pending = this._pendingChanges.get(node.key);
    if (pending?.has(verb)) return pending.get(verb)!;
    return node.entries.filter((e) => e.verb === verb);
  }

  // ── Selection panel ──────────────────────────────────────────────────────

  get #selectionGroups(): UapSelectorGroup[] {
    return [
      {
        options: [
          {
            id: 'group',
            label: this.#localize.term('uap_chooseRole'),
            icon: 'icon-users',
            ...(this._selectedRole ? { selectedName: this._selectedRole.name } : {}),
          },
        ],
      },
    ];
  }

  #onSelectorClick(id: string): void {
    if (id === 'group') void this.#openRolePicker();
  }

  // ── Rendering ─────────────────────────────────────────────────────────────

  #renderRows(nodes: TreeNodeState[], depth: number): TemplateResult[] {
    return nodes.flatMap((node) => [
      this.#renderRow(node, depth),
      ...(node.expanded && node.children ? this.#renderRows(node.children, depth + 1) : []),
    ]);
  }

  #renderRow(node: TreeNodeState, depth: number): TemplateResult {
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
              : html`<uui-button compact look="default" class="expand-spacer" disabled aria-hidden="true" label="">▸</uui-button>`}
            <umb-icon name=${node.icon ?? 'icon-document'}></umb-icon>
            <span class="node-name">${node.name}</span>
          </div>
        </td>
        ${this._verbs.map((v) => this.#renderCell(node, v.verb))}
      </tr>
    `;
  }


  #renderCell(node: TreeNodeState, verb: string) {
    const entries = this.#getCellEntries(node, verb);
    const isPending = this._pendingChanges.get(node.key)?.has(verb) ?? false;
    // getCellInfo carries per-side priority-override flags, so the gold theme is applied
    // automatically by uap-perm-block (per half in split cells).
    const info = getCellInfo(entries);

    return html`
      <td class="perm-td" title=${verb} @click=${() => this.#openPicker(node, verb)}>
        <uap-perm-block
          .info=${info}
          ?pending=${isPending}
          priority-override-title=${this.#localize.term('uap_priorityOverrideBadgeTitle')}></uap-perm-block>
      </td>
    `;
  }

  /**
   * Renders the shared scope dialog instance. Initial state is pushed from `_pickerNodeState`
   * etc.; the dialog opens via `#openPicker` (which calls `_scopeDialog.open()` after the next
   * render). `uap-scope-apply` is handled by `#handleScopeApply`.
   */
  #renderDialog(): TemplateResult {
    const verbName = this._pickerVerb?.split('.').pop() ?? '';
    const nodeName = this._pickerIsVirtualRoot
      ? this.#localize.term('uap_contentRoot')
      : (this._pickerNode?.name ?? '');

    return html`
      <uap-permission-scope-dialog
        .verb=${verbName}
        .nodeName=${nodeName}
        .isVirtualRoot=${this._pickerIsVirtualRoot}
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
    return html`
      <umb-body-layout headline=${this.#localize.term('uap_editorHeadline')}>
        <uap-page-intro surface="uap-permissions-editor" headline=${this.#localize.term('uap_editorHeadline')}></uap-page-intro>
        <uap-selection-panel
          .groups=${this.#selectionGroups}
          promptText=${this.#localize.term('uap_selectRolePrompt')}
          ctaIcon="icon-document"
          orLabel=${this.#localize.term('uap_subjectOr')}
          @uap-selector-click=${(e: CustomEvent<{ id: string }>) => this.#onSelectorClick(e.detail.id)}>
          ${this._pendingChanges.size > 0
            ? html`<div slot="actions">
                <uui-button label=${this.#localize.term('uap_saveChanges')} look="primary" color="positive" ?loading=${this._saving} @click=${() => void this.#saveChanges()}>
                  ${this.#localize.term('uap_saveChanges')}
                </uui-button>
                <uui-button label=${this.#localize.term('uap_discard')} look="outline" @click=${() => { this._pendingChanges = new Map(); }}>
                  ${this.#localize.term('uap_discard')}
                </uui-button>
              </div>`
            : nothing}
          ${this._error ? html`<p class="error-msg">⚠ ${this._error}</p>` : nothing}
          ${this._loading ? html`<div class="loading"><uui-loader></uui-loader></div>` : nothing}
          ${!this._loading && this._treeNodes.length > 0
            ? html`
                <div class="table-wrap">
                  <table>
                    <thead>
                      <tr>
                        <th class="node-header">${this.#localize.term('uap_contentNodeHeader')}</th>
                        ${this._verbs.map((v) => html`<th class="verb-header" title=${v.verb}>${v.displayName}</th>`)}
                      </tr>
                    </thead>
                    <tbody>
                      ${this.#renderRow(this._treeNodes[0], 0)}
                      ${this.#renderRows(this._treeNodes.slice(1), 1)}
                    </tbody>
                  </table>
                </div>
              `
            : nothing}
        </uap-selection-panel>
      </umb-body-layout>

      <!-- Permission dialog — rendered outside umb-body-layout so it always layers on top -->
      ${this.#renderDialog()}
    `;
  }

  static override styles = css`
    :host {
      display: block;
      height: 100%;
    }

    .loading {
      display: flex;
      justify-content: center;
      padding: 32px;
    }

    .error-msg {
      padding: 12px 18px;
      color: var(--uui-color-danger, #b91c1c);
    }

    /* ── Table ────────────────────────────────────────────────── */
    .table-wrap {
      overflow-x: auto;
    }

    table {
      width: 100%;
      border-collapse: collapse;
      table-layout: fixed;
    }

    thead {
      position: sticky;
      top: 0;
      z-index: 2;
    }

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

    td {
      border-bottom: 1px solid var(--uui-color-border, #f0f0f0);
    }

    tr:hover td {
      background-color: var(--uui-color-surface-emphasis, #fafafa);
    }

    tr.row-pending td {
      background-color: color-mix(in srgb, oklch(85% 0.15 90) 12%, transparent);
    }

    /* ── Node cell ────────────────────────────────────────────── */
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

    /* Invisible clone of the expand toggle: leaf rows reserve the exact same width as expandable
       rows, so the node icon and label stay aligned whether or not a row has an expander. */
    .expand-spacer {
      visibility: hidden;
    }

    .node-name {
      overflow: hidden;
      text-overflow: ellipsis;
    }

    /* ── Permission blocks ────────────────────────────────────── */
    .perm-td {
      padding: 3px;
      text-align: center;
      vertical-align: middle;
    }


  `;
}

export default UapPermissionsEditorRootElement;

declare global {
  interface HTMLElementTagNameMap {
    'uap-permissions-editor-root': UapPermissionsEditorRootElement;
  }
}
