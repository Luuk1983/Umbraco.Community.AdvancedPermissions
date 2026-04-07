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
import '../components/uap-picker-button.element.js';

/** Pending entries for a verb: empty array means "inherit" (clear all entries for this verb). */
type PendingVerbEntries = Array<{ state: PermissionState; scope: PermissionScope }>;

/** Map of verb → pending entries for a single node. */
type PendingNodeChanges = Map<string, PendingVerbEntries>;

/**
 * Compose dialog state back into stored entries (using backend scope model).
 * Returns 0, 1, or 2 entries depending on the combination.
 */
function composeEntries(
  nodeState: 'inherit' | 'allow' | 'deny',
  descState: 'inherit' | 'allow' | 'deny',
  sameAsNode: boolean,
): PendingVerbEntries {
  const effectiveDesc = sameAsNode ? nodeState : descState;

  // Both inherit → no entries
  if (nodeState === 'inherit' && effectiveDesc === 'inherit') return [];

  // Both same → single ThisNodeAndDescendants entry
  if (nodeState === effectiveDesc) {
    const state: PermissionState = nodeState === 'allow' ? 'Allow' : 'Deny';
    return [{ state, scope: 'ThisNodeAndDescendants' }];
  }

  // Different states → up to 2 entries
  const result: PendingVerbEntries = [];
  if (nodeState !== 'inherit') {
    result.push({ state: nodeState === 'allow' ? 'Allow' : 'Deny', scope: 'ThisNodeOnly' });
  }
  if (effectiveDesc !== 'inherit') {
    result.push({ state: effectiveDesc === 'allow' ? 'Allow' : 'Deny', scope: 'DescendantsOnly' });
  }
  return result;
}

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
  @state() private _pickerIsVirtualRoot = false;
  @state() private _pickerNodeState: 'inherit' | 'allow' | 'deny' = 'inherit';
  @state() private _pickerDescState: 'inherit' | 'allow' | 'deny' = 'inherit';
  @state() private _pickerSameAsNode = true;

  @query('.scope-dialog') private _scopeDialog!: HTMLDialogElement;

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
        icon: 'icon-folder',
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

  #updateNode(key: string, changes: Partial<TreeNodeState>, nodes: TreeNodeState[] = this._treeNodes): boolean {
    for (let i = 0; i < nodes.length; i++) {
      if (nodes[i].key === key) {
        nodes[i] = { ...nodes[i], ...changes };
        this._treeNodes = [...this._treeNodes];
        return true;
      }
      if (nodes[i].children && this.#updateNode(key, changes, nodes[i].children!)) {
        return true;
      }
    }
    return false;
  }

  // ── Permission dialog ───────────────────────────────────────────────────

  #openPicker(node: TreeNodeState, verb: string): void {
    this._pickerNode = node;
    this._pickerVerb = verb;
    this._pickerIsVirtualRoot = node.key === 'virtual-root';

    const entries = this.#getCellEntries(node, verb);

    if (this._pickerIsVirtualRoot) {
      // Virtual root: simple mode — only nodeState matters
      const first = entries[0];
      this._pickerNodeState = first ? (first.state === 'Allow' ? 'allow' : 'deny') : 'inherit';
      this._pickerDescState = 'inherit';
      this._pickerSameAsNode = true;
    } else {
      const decomposed = decomposeEntries(entries);
      this._pickerNodeState = decomposed.nodeState;
      this._pickerDescState = decomposed.descState;
      this._pickerSameAsNode = decomposed.sameAsNode;
    }

    void this.updateComplete.then(() => this._scopeDialog.showModal());
  }

  #applyPicker(): void {
    if (!this._pickerNode || !this._pickerVerb) return;

    let newEntries: PendingVerbEntries;

    if (this._pickerIsVirtualRoot) {
      // Virtual root always uses ThisNodeAndDescendants
      if (this._pickerNodeState === 'inherit') {
        newEntries = [];
      } else {
        const state: PermissionState = this._pickerNodeState === 'allow' ? 'Allow' : 'Deny';
        newEntries = [{ state, scope: 'ThisNodeAndDescendants' }];
      }
    } else {
      newEntries = composeEntries(this._pickerNodeState, this._pickerDescState, this._pickerSameAsNode);
    }

    const nodeKey = this._pickerNode.key;
    const verb = this._pickerVerb;
    const nodeChanges: PendingNodeChanges = this._pendingChanges.get(nodeKey) ?? new Map();
    nodeChanges.set(verb, newEntries);
    this._pendingChanges = new Map(this._pendingChanges).set(nodeKey, nodeChanges);
    this._scopeDialog.close();
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
        const byVerb = new Map<string, Array<{ verb: string; state: PermissionState; scope: PermissionScope }>>();
        for (const e of node.entries) {
          const list = byVerb.get(e.verb) ?? [];
          list.push({ verb: e.verb, state: e.state, scope: e.scope });
          byVerb.set(e.verb, list);
        }
        for (const [verb, pending] of verbChanges) {
          if (pending.length === 0) {
            byVerb.delete(verb);
          } else {
            byVerb.set(verb, pending.map((pe) => ({ verb, state: pe.state, scope: pe.scope })));
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

  #findNode(key: string, nodes: TreeNodeState[] = this._treeNodes): TreeNodeState | null {
    for (const node of nodes) {
      if (node.key === key) return node;
      if (node.children) {
        const found = this.#findNode(key, node.children);
        if (found) return found;
      }
    }
    return null;
  }

  #getCellEntries(node: TreeNodeState, verb: string): PendingVerbEntries | PermissionEntry[] {
    const pending = this._pendingChanges.get(node.key);
    if (pending?.has(verb)) return pending.get(verb)!;
    return node.entries.filter((e) => e.verb === verb);
  }

  /**
   * Get the cell rendering info for a set of entries.
   * Returns either a uniform cell (same state for node+desc) or a split cell.
   */
  #getCellInfo(entries: ReadonlyArray<{ state: PermissionState; scope: PermissionScope }>): {
    split: boolean;
    nodeClass: string;
    descClass: string;
  } {
    if (entries.length === 0) {
      return { split: false, nodeClass: 'inherit', descClass: 'inherit' };
    }

    const d = decomposeEntries(entries);
    const toClass = (s: 'inherit' | 'allow' | 'deny') => s;

    if (d.sameAsNode) {
      return { split: false, nodeClass: toClass(d.nodeState), descClass: toClass(d.nodeState) };
    }

    return { split: true, nodeClass: toClass(d.nodeState), descClass: toClass(d.descState) };
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
              : html`<span class="expand-spacer"></span>`}
            <umb-icon name=${node.icon ?? 'icon-document'}></umb-icon>
            <span class="node-name">${node.name}</span>
          </div>
        </td>
        ${this._verbs.map((v) => this.#renderCell(node, v.verb))}
      </tr>
    `;
  }

  #stateIcon(cls: string): string {
    if (cls === 'allow') return '\u2713';  // ✓
    if (cls === 'deny') return '\u2717';   // ✗
    return '\u2014';                        // —
  }

  #renderCell(node: TreeNodeState, verb: string) {
    const entries = this.#getCellEntries(node, verb);
    const isPending = this._pendingChanges.get(node.key)?.has(verb) ?? false;
    const pendingCls = isPending ? ' pending' : '';

    if (entries.length === 0) {
      return html`
        <td class="perm-td" title=${verb} @click=${() => this.#openPicker(node, verb)}>
          <div class="perm-block uniform inherit${pendingCls}">\u2014</div>
        </td>
      `;
    }

    const info = this.#getCellInfo(entries);

    if (!info.split) {
      return html`
        <td class="perm-td" title=${verb} @click=${() => this.#openPicker(node, verb)}>
          <div class="perm-block uniform ${info.nodeClass}${pendingCls}">${this.#stateIcon(info.nodeClass)}</div>
        </td>
      `;
    }

    return html`
      <td class="perm-td" title=${verb} @click=${() => this.#openPicker(node, verb)}>
        <div class="perm-block split${pendingCls}">
          <span class="half ${info.nodeClass}">${this.#stateIcon(info.nodeClass)}</span>
          <span class="half ${info.descClass}">${this.#stateIcon(info.descClass)}</span>
        </div>
      </td>
    `;
  }

  #renderDialog(): TemplateResult {
    const verbName = this._pickerVerb?.split('.').pop() ?? '';

    return html`
      <dialog
        class="scope-dialog"
        @close=${() => {
          this._pickerNode = null;
          this._pickerVerb = null;
        }}>
        <uui-dialog-layout
          headline=${this.#localize.term('uap_dialogHeadline', verbName)}>
          <p class="dialog-node">
            ${this.#localize.term('uap_dialogNodeLabel')}: <strong>${this._pickerNode?.name ?? ''}</strong>
          </p>

          ${this._pickerIsVirtualRoot ? this.#renderVirtualRootOptions() : this.#renderNodeOptions()}

          <div slot="actions">
            <uui-button look="outline" @click=${() => this._scopeDialog.close()}>
              ${this.#localize.term('uap_cancel')}
            </uui-button>
            <uui-button look="primary" color="positive" @click=${() => this.#applyPicker()}>
              ${this.#localize.term('uap_apply')}
            </uui-button>
          </div>
        </uui-dialog-layout>
      </dialog>
    `;
  }

  #renderVirtualRootOptions(): TemplateResult {
    return html`
      <div class="dialog-options">
        <uui-radio-group
          .value=${this._pickerNodeState}
          @change=${(e: Event) => { this._pickerNodeState = (e.target as HTMLInputElement).value as 'inherit' | 'allow' | 'deny'; }}>
          <uui-radio value="inherit" label=${this.#localize.term('uap_virtualRootInherit')} class="opt-inherit"></uui-radio>
          <uui-radio value="allow" label=${this.#localize.term('uap_virtualRootAllow')} class="opt-allow"></uui-radio>
          <uui-radio value="deny" label=${this.#localize.term('uap_virtualRootDeny')} class="opt-deny"></uui-radio>
        </uui-radio-group>
      </div>
    `;
  }

  #renderNodeOptions(): TemplateResult {
    return html`
      <div class="dialog-sections">
        <!-- This node -->
        <div class="dialog-section">
          <h4>${this.#localize.term('uap_thisNodeSection')}</h4>
          <uui-radio-group
            .value=${this._pickerNodeState}
            @change=${(e: Event) => { this._pickerNodeState = (e.target as HTMLInputElement).value as 'inherit' | 'allow' | 'deny'; }}>
            <uui-radio value="inherit" label=${this.#localize.term('uap_inherit')} class="opt-inherit"></uui-radio>
            <uui-radio value="allow" label=${this.#localize.term('uap_allow')} class="opt-allow"></uui-radio>
            <uui-radio value="deny" label=${this.#localize.term('uap_deny')} class="opt-deny"></uui-radio>
          </uui-radio-group>
        </div>

        <!-- Descendants -->
        <div class="dialog-section">
          <h4>${this.#localize.term('uap_descendantsSection')}</h4>
          <uui-toggle
            label=${this.#localize.term('uap_sameAsNode')}
            ?checked=${this._pickerSameAsNode}
            @change=${(e: Event) => {
              this._pickerSameAsNode = (e.target as HTMLInputElement).checked;
              if (this._pickerSameAsNode) {
                this._pickerDescState = this._pickerNodeState;
              }
            }}>${this.#localize.term('uap_sameAsNode')}</uui-toggle>
          <uui-radio-group
            class=${this._pickerSameAsNode ? 'radio-disabled' : ''}
            .value=${this._pickerSameAsNode ? this._pickerNodeState : this._pickerDescState}
            @change=${(e: Event) => {
              if (!this._pickerSameAsNode) {
                this._pickerDescState = (e.target as HTMLInputElement).value as 'inherit' | 'allow' | 'deny';
              }
            }}>
            <uui-radio value="inherit" label=${this.#localize.term('uap_inherit')} class="opt-inherit" ?disabled=${this._pickerSameAsNode}></uui-radio>
            <uui-radio value="allow" label=${this.#localize.term('uap_allow')} class="opt-allow" ?disabled=${this._pickerSameAsNode}></uui-radio>
            <uui-radio value="deny" label=${this.#localize.term('uap_deny')} class="opt-deny" ?disabled=${this._pickerSameAsNode}></uui-radio>
          </uui-radio-group>
        </div>
      </div>
    `;
  }

  override render() {
    const hasPending = this._pendingChanges.size > 0;

    return html`
      <umb-body-layout headline=${this.#localize.term('uap_editorHeadline')}>
        <div class="toolbar">
          <uap-picker-button
            label=${this.#localize.term('uap_chooseRole')}
            .selectedName=${this._selectedRole?.name ?? ''}
            icon="icon-users"
            @click=${() => void this.#openRolePicker()}>
          </uap-picker-button>
          ${hasPending
            ? html`
                <uui-button look="primary" color="positive" ?loading=${this._saving} @click=${() => void this.#saveChanges()}>
                  ${this.#localize.term('uap_saveChanges')}
                </uui-button>
                <uui-button look="outline" @click=${() => { this._pendingChanges = new Map(); }}>
                  ${this.#localize.term('uap_discard')}
                </uui-button>
              `
            : nothing}
        </div>

        ${this._error ? html`<p class="error-msg">\u26a0 ${this._error}</p>` : nothing}
        ${this._loading ? html`<div class="loading"><uui-loader></uui-loader></div>` : nothing}
        ${!this._selectedRole ? html`<p class="empty-msg">${this.#localize.term('uap_selectRolePrompt')}</p>` : nothing}

        ${this._selectedRole && !this._loading && this._treeNodes.length > 0
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
                    ${this.#renderRows(this._treeNodes, 0)}
                  </tbody>
                </table>
              </div>
            `
          : nothing}
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

    /* ── Toolbar ──────────────────────────────────────────────── */
    .toolbar {
      display: flex;
      align-items: center;
      gap: var(--uui-size-4, 12px);
      padding: var(--uui-size-3, 9px) var(--uui-size-6, 18px);
      background: var(--uui-color-surface, #fff);
      border-bottom: 1px solid var(--uui-color-border, #e0e0e0);
      flex-wrap: wrap;
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

    .empty-msg {
      padding: 32px 18px;
      color: var(--uui-color-text-alt, #888);
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

    .expand-spacer {
      width: 16px;
      flex-shrink: 0;
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

    .perm-block {
      display: flex;
      align-items: center;
      justify-content: center;
      height: 26px;
      border: 1px solid var(--uui-color-border, #ddd);
      border-radius: 4px;
      cursor: pointer;
      user-select: none;
      overflow: hidden;
    }

    .perm-block:hover {
      border-color: var(--uui-color-border-emphasis, #bbb);
    }

    .perm-block.pending {
      border-color: var(--uui-color-warning-standalone, #f59e0b);
      border-style: dashed;
      border-width: 2px;
    }

    /* ── Uniform block ────────────────────────────────────────── */
    .perm-block.uniform {
      font-size: 13px;
      font-weight: 700;
    }

    .perm-block.inherit {
      color: var(--uui-color-text-alt, #ccc);
      border-color: var(--uui-color-border, #e8e8e8);
    }

    .perm-block.allow {
      background: color-mix(in srgb, var(--uui-color-positive, #34a853) 14%, transparent);
      color: color-mix(in srgb, var(--uui-color-positive, #1e7e34) 80%, #000);
      border-color: color-mix(in srgb, var(--uui-color-positive, #34a853) 30%, transparent);
    }

    .perm-block.deny {
      background: color-mix(in srgb, var(--uui-color-danger, #ea4335) 12%, transparent);
      color: color-mix(in srgb, var(--uui-color-danger, #c5221f) 80%, #000);
      border-color: color-mix(in srgb, var(--uui-color-danger, #ea4335) 25%, transparent);
    }

    /* ── Split block — two halves ─────────────────────────────── */
    .perm-block.split {
      padding: 0;
    }

    .perm-block.split > .half {
      display: flex;
      align-items: center;
      justify-content: center;
      flex: 1;
      height: 100%;
      font-size: 11px;
      font-weight: 700;
    }

    .half.inherit {
      color: var(--uui-color-text-alt, #ccc);
    }

    .half.allow {
      background: color-mix(in srgb, var(--uui-color-positive, #34a853) 14%, transparent);
      color: color-mix(in srgb, var(--uui-color-positive, #1e7e34) 80%, #000);
    }

    .half.deny {
      background: color-mix(in srgb, var(--uui-color-danger, #ea4335) 12%, transparent);
      color: color-mix(in srgb, var(--uui-color-danger, #c5221f) 80%, #000);
    }

    /* ── Permission dialog ────────────────────────────────────── */
    .scope-dialog {
      border: none;
      border-radius: 8px;
      box-shadow: 0 8px 32px rgba(0, 0, 0, 0.25);
      padding: 0;
      min-width: 420px;
      max-width: 540px;
    }

    .scope-dialog::backdrop {
      background: rgba(0, 0, 0, 0.4);
    }

    .dialog-node {
      margin: 0 0 16px;
      font-size: 12px;
      color: var(--uui-color-text-alt, #666);
    }

    .dialog-options {
      margin-bottom: 8px;
    }

    .dialog-sections {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 24px;
      margin-bottom: 8px;
    }

    .dialog-section h4 {
      margin: 0 0 8px;
      font-size: 13px;
      font-weight: 600;
      color: var(--uui-color-text, #333);
    }

    .dialog-section uui-toggle {
      margin-bottom: 8px;
    }

    .radio-disabled {
      opacity: 0.4;
      pointer-events: none;
    }

    uui-radio.opt-allow {
      --uui-radio-border-color: color-mix(in srgb, var(--uui-color-positive, #34a853) 50%, transparent);
    }

    uui-radio.opt-deny {
      --uui-radio-border-color: color-mix(in srgb, var(--uui-color-danger, #ea4335) 50%, transparent);
    }
  `;
}

export default UapPermissionsEditorRootElement;

declare global {
  interface HTMLElementTagNameMap {
    'uap-permissions-editor-root': UapPermissionsEditorRootElement;
  }
}
