import { html, css, nothing, customElement, state, query } from '@umbraco-cms/backoffice/external/lit';
import type { TemplateResult } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import type {
  VerbInfo,
  RoleInfo,
  TreeNodeState,
  PermissionEntry,
  PermissionState,
  PermissionScope,
} from '../models/permission.models.js';
import { getRoles, getVerbs, getTreeRoot, getTreeChildren, getPermissions, savePermissions } from '../api/advanced-security.api.js';
import { clearEffectivePermissionCache } from '../conditions/document-user-permission.condition.js';

/** Pending entries for a verb: empty array means "inherit" (clear all entries for this verb). */
type PendingVerbEntries = Array<{ state: PermissionState; scope: PermissionScope }>;

/** Map of verb → pending entries for a single node. */
type PendingNodeChanges = Map<string, PendingVerbEntries>;

const SCOPE_OPTIONS: ReadonlyArray<{
  value: string;
  label: string;
  state?: PermissionState;
  scope?: PermissionScope;
}> = [
  { value: 'inherit', label: 'Inherit (remove entry)' },
  { value: 'allow-nd', label: 'Allow — This node and descendants', state: 'Allow', scope: 'ThisNodeAndDescendants' },
  { value: 'allow-n', label: 'Allow — This node only', state: 'Allow', scope: 'ThisNodeOnly' },
  { value: 'allow-d', label: 'Allow — Descendants only', state: 'Allow', scope: 'DescendantsOnly' },
  { value: 'deny-nd', label: 'Deny — This node and descendants', state: 'Deny', scope: 'ThisNodeAndDescendants' },
  { value: 'deny-n', label: 'Deny — This node only', state: 'Deny', scope: 'ThisNodeOnly' },
  { value: 'deny-d', label: 'Deny — Descendants only', state: 'Deny', scope: 'DescendantsOnly' },
];

/** Simplified options for the virtual root "Content" node — no scope variants since it always applies to all descendants. */
const VIRTUAL_ROOT_OPTIONS: ReadonlyArray<{
  value: string;
  label: string;
  state?: PermissionState;
  scope?: PermissionScope;
}> = [
  { value: 'inherit', label: 'Not set (remove entry)' },
  { value: 'allow-nd', label: 'Allow (all content)', state: 'Allow', scope: 'ThisNodeAndDescendants' },
  { value: 'deny-nd', label: 'Deny (all content)', state: 'Deny', scope: 'ThisNodeAndDescendants' },
];

function scopeAbbr(scope: string): string {
  if (scope === 'ThisNodeAndDescendants') return 'N+D';
  if (scope === 'ThisNodeOnly') return 'N';
  return 'D';
}

/**
 * Security Editor workspace element.
 * Allows administrators to view and edit raw permission entries per role and content node.
 */
@customElement('uas-security-editor-root')
export class UasSecurityEditorRootElement extends UmbLitElement {
  // ── Metadata ────────────────────────────────────────────────────────────
  @state() private _roles: RoleInfo[] = [];
  @state() private _verbs: VerbInfo[] = [];

  // ── Selection & tree ────────────────────────────────────────────────────
  @state() private _selectedRole = '';
  @state() private _treeNodes: TreeNodeState[] = [];
  @state() private _loading = false;
  @state() private _saving = false;
  @state() private _error: string | null = null;

  /**
   * Pending changes: nodeKey → (verb → PendingVerbEntries).
   * Empty PendingVerbEntries means "inherit" (delete all entries for that verb on save).
   */
  @state() private _pendingChanges: Map<string, PendingNodeChanges> = new Map();

  // ── Scope picker dialog ──────────────────────────────────────────────────
  @state() private _pickerNode: TreeNodeState | null = null;
  @state() private _pickerVerb: string | null = null;
  @state() private _pickerValue = 'inherit';

  @query('.scope-dialog') private _scopeDialog!: HTMLDialogElement;

  #notificationContext: typeof UMB_NOTIFICATION_CONTEXT.TYPE | undefined = undefined;

  constructor() {
    super();
    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (ctx) => {
      this.#notificationContext = ctx ?? undefined;
    });
  }

  override connectedCallback(): void {
    super.connectedCallback();
    void this.#loadMeta();
  }

  // ── Computed ─────────────────────────────────────────────────────────────

  get #selectedRoleDefaultVerbs(): ReadonlySet<string> {
    const role = this._roles.find((r) => r.alias === this._selectedRole);
    return new Set(role?.defaultVerbs ?? []);
  }

  get #roleOptions() {
    return [
      { name: '— Select a role —', value: '' },
      ...this._roles.map((r) => ({
        name: `${r.name}${r.isEveryone ? ' (Everyone)' : ''}`,
        value: r.alias,
        selected: r.alias === this._selectedRole,
      })),
    ];
  }

  /** Scope options to show in the picker — simplified for the virtual root, full for real nodes. */
  get #currentScopeOptions() {
    if (this._pickerNode?.key === 'virtual-root') {
      return VIRTUAL_ROOT_OPTIONS;
    }
    return SCOPE_OPTIONS;
  }

  // ── Data loading ────────────────────────────────────────────────────────

  async #loadMeta(): Promise<void> {
    try {
      const [roles, verbs] = await Promise.all([getRoles(), getVerbs()]);
      this._roles = roles;
      this._verbs = verbs;
    } catch (err) {
      this._error = String(err);
    }
  }

  async #loadTree(): Promise<void> {
    if (!this._selectedRole) return;
    this._loading = true;
    this._error = null;
    this._treeNodes = [];
    try {
      const [virtualEntries, nodes] = await Promise.all([
        getPermissions(null, this._selectedRole),
        getTreeRoot(this._selectedRole),
      ]);
      const virtualRoot: TreeNodeState = {
        key: 'virtual-root',
        name: 'Content',
        icon: 'icon-folder',
        hasChildren: false,
        entries: virtualEntries,
        expanded: false,
        loading: false,
      };
      this._treeNodes = [virtualRoot, ...nodes.map((n) => ({ ...n, expanded: false, loading: false }))];
    } catch (err) {
      this._error = String(err);
    } finally {
      this._loading = false;
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
      const children = await getTreeChildren(node.key, this._selectedRole);
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

  // ── Scope picker ─────────────────────────────────────────────────────────

  #openPicker(node: TreeNodeState, verb: string): void {
    this._pickerNode = node;
    this._pickerVerb = verb;
    this._pickerValue = this.#entriesToPickerValue(this.#getCellEntries(node, verb));
    void this.updateComplete.then(() => this._scopeDialog.showModal());
  }

  #applyPicker(): void {
    if (!this._pickerNode || !this._pickerVerb) return;
    const option = this.#currentScopeOptions.find((o) => o.value === this._pickerValue);
    const newEntries: PendingVerbEntries =
      option?.state != null ? [{ state: option.state, scope: option.scope! }] : [];

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
        const apiKey = nodeKey === 'virtual-root' ? null : nodeKey;
        await savePermissions(apiKey, this._selectedRole, allEntries);

        const saved: PermissionEntry[] = allEntries.map((e, idx) => ({
          id: idx,
          nodeKey: apiKey,
          roleAlias: this._selectedRole,
          verb: e.verb,
          state: e.state,
          scope: e.scope,
        }));
        this.#updateNode(nodeKey, { entries: saved });
      }
      this._pendingChanges = new Map();
      clearEffectivePermissionCache();
      this.#notificationContext?.peek('positive', { data: { message: 'Permissions saved.' } });
    } catch (err) {
      this.#notificationContext?.peek('danger', { data: { message: `Save failed: ${String(err)}` } });
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

  #entriesToPickerValue(entries: PendingVerbEntries | PermissionEntry[]): string {
    if (entries.length === 0) return 'inherit';
    const first = entries[0];
    const stateKey = first.state === 'Deny' ? 'deny' : 'allow';
    const scopeMap: Record<string, string> = {
      ThisNodeAndDescendants: 'nd',
      ThisNodeOnly: 'n',
      DescendantsOnly: 'd',
    };
    return `${stateKey}-${scopeMap[first.scope] ?? 'nd'}`;
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
        <td class="node-cell" style="--depth: ${depth}">
          ${node.hasChildren || node.children
            ? html`<uui-button compact look="default" label=${node.expanded ? 'Collapse' : 'Expand'} @click=${() => void this.#toggleExpand(node)}>
                ${node.loading ? html`<uui-loader-circle></uui-loader-circle>` : node.expanded ? '▾' : '▸'}
              </uui-button>`
            : html`<span class="expand-spacer"></span>`}
          <umb-icon name=${node.icon ?? 'icon-document'}></umb-icon>
          <span class="node-name">${node.name}</span>
        </td>
        ${this._verbs.map((v) => this.#renderCell(node, v.verb))}
      </tr>
    `;
  }

  #renderCell(node: TreeNodeState, verb: string) {
    const entries = this.#getCellEntries(node, verb);
    const isPending = this._pendingChanges.get(node.key)?.has(verb) ?? false;

    if (entries.length === 0) {
      const isDefault = node.key === 'virtual-root' && this.#selectedRoleDefaultVerbs.has(verb);
      return html`
        <td
          class="perm-cell ${isDefault ? 'default-allow' : 'inherit'} ${isPending ? 'cell-pending' : ''}"
          title=${verb}
          @click=${() => this.#openPicker(node, verb)}>
          ${isDefault ? 'Allow' : '—'}
        </td>
      `;
    }

    const hasDeny = entries.some((e) => e.state === 'Deny');
    const scopes = [...new Set(entries.map((e) => scopeAbbr(e.scope)))].join('/');
    const label = `${hasDeny ? 'D' : 'A'} ${scopes}`;

    return html`
      <td
        class="perm-cell ${hasDeny ? 'deny' : 'allow'} ${isPending ? 'cell-pending' : ''}"
        title=${verb}
        @click=${() => this.#openPicker(node, verb)}>
        ${label}
      </td>
    `;
  }

  override render() {
    const hasPending = this._pendingChanges.size > 0;

    return html`
      <umb-body-layout headline="Security Editor">
        <div class="toolbar">
          <div class="role-picker">
            <label>Role:</label>
            <uui-select
              label="Role"
              placeholder="— Select a role —"
              .options=${this.#roleOptions}
              @change=${(e: Event) => {
                this._selectedRole = (e.target as HTMLInputElement).value;
                this._pendingChanges = new Map();
                void this.#loadTree();
              }}>
            </uui-select>
          </div>
          ${hasPending
            ? html`
                <uui-button look="primary" color="positive" ?loading=${this._saving} @click=${() => void this.#saveChanges()}>
                  Save Changes
                </uui-button>
                <uui-button look="outline" @click=${() => { this._pendingChanges = new Map(); }}>
                  Discard
                </uui-button>
              `
            : nothing}
        </div>

        ${this._error ? html`<p class="error-msg">⚠ ${this._error}</p>` : nothing}
        ${this._loading ? html`<div class="loading"><uui-loader></uui-loader></div>` : nothing}
        ${!this._selectedRole ? html`<p class="empty-msg">Select a role above to manage its permissions.</p>` : nothing}

        ${this._selectedRole && !this._loading && this._treeNodes.length > 0
          ? html`
              <div class="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th class="node-header">Content Node</th>
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

      <!-- Scope picker modal — rendered outside umb-body-layout so it always layers on top -->
      <dialog
        class="scope-dialog"
        @close=${() => {
          this._pickerNode = null;
          this._pickerVerb = null;
        }}>
        <uui-dialog-layout
          headline="Set Permission: ${this._pickerVerb?.split('.').pop() ?? ''}">
          <p class="dialog-node">Node: <strong>${this._pickerNode?.name ?? ''}</strong></p>
          <div class="dialog-options">
            <uui-radio-group
              .value=${this._pickerValue}
              @change=${(e: Event) => { this._pickerValue = (e.target as HTMLInputElement).value; }}>
              ${this.#currentScopeOptions.map(
                (opt) => html`
                  <uui-radio
                    value=${opt.value}
                    label=${opt.label}
                    class=${opt.state === 'Allow' ? 'opt-allow' : opt.state === 'Deny' ? 'opt-deny' : 'opt-inherit'}>
                  </uui-radio>
                `,
              )}
            </uui-radio-group>
          </div>
          <div slot="actions">
            <uui-button look="outline" @click=${() => this._scopeDialog.close()}>Cancel</uui-button>
            <uui-button look="primary" color="positive" @click=${() => this.#applyPicker()}>Apply</uui-button>
          </div>
        </uui-dialog-layout>
      </dialog>
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

    .role-picker {
      display: flex;
      align-items: center;
      gap: 8px;
    }

    .role-picker label {
      font-weight: 600;
      white-space: nowrap;
    }

    .role-picker uui-select {
      min-width: 240px;
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
      overflow: auto;
    }

    table {
      border-collapse: collapse;
      width: 100%;
      min-width: max-content;
      font-size: 13px;
    }

    thead {
      position: sticky;
      top: 0;
      z-index: 2;
    }

    th {
      padding: 8px 10px;
      text-align: left;
      border-bottom: 2px solid var(--uui-color-border, #ddd);
      white-space: nowrap;
      font-weight: 600;
      background: var(--uui-color-surface-alt, #f5f5f5);
    }

    th.node-header {
      min-width: 240px;
      position: sticky;
      left: 0;
      z-index: 3;
    }

    th.verb-header {
      min-width: 72px;
      text-align: center;
    }

    td {
      border-bottom: 1px solid var(--uui-color-border, #eee);
    }

    tr:hover td {
      background-color: var(--uui-color-surface-emphasis, #f8f8f8);
    }

    tr.row-pending td {
      background-color: color-mix(in srgb, oklch(85% 0.15 90) 15%, transparent);
    }

    /* ── Node cell ────────────────────────────────────────────── */
    td.node-cell {
      padding: 5px 8px 5px calc(var(--depth, 0) * 20px + 8px);
      display: flex;
      align-items: center;
      gap: 6px;
      min-width: 240px;
      position: sticky;
      left: 0;
      background: inherit;
    }

    .expand-spacer {
      width: 16px;
      flex-shrink: 0;
    }

    .node-name {
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
      max-width: 180px;
    }

    /* ── Permission cells ─────────────────────────────────────── */
    .perm-cell {
      text-align: center;
      padding: 5px 4px;
      cursor: pointer;
      font-size: 11px;
      font-weight: 700;
      min-width: 72px;
      user-select: none;
    }

    .perm-cell:hover {
      opacity: 0.8;
    }

    .perm-cell.inherit {
      color: var(--uui-color-text-alt, #aaa);
    }

    .perm-cell.allow {
      background: color-mix(in srgb, var(--uui-color-positive, #34a853) 22%, transparent);
      color: color-mix(in srgb, var(--uui-color-positive, #1e7e34) 90%, #000);
    }

    .perm-cell.deny {
      background: color-mix(in srgb, var(--uui-color-danger, #ea4335) 18%, transparent);
      color: color-mix(in srgb, var(--uui-color-danger, #c5221f) 90%, #000);
    }

    .perm-cell.default-allow {
      background: color-mix(in srgb, var(--uui-color-positive, #34a853) 10%, transparent);
      color: color-mix(in srgb, var(--uui-color-positive, #1e7e34) 60%, #888);
      border: 1px dashed color-mix(in srgb, var(--uui-color-positive, #34a853) 40%, transparent);
      font-style: italic;
    }

    .perm-cell.cell-pending {
      outline: 2px dashed var(--uui-color-warning-standalone, #f59e0b);
      outline-offset: -2px;
    }

    /* ── Scope dialog ─────────────────────────────────────────── */
    .scope-dialog {
      border: none;
      border-radius: 8px;
      box-shadow: 0 8px 32px rgba(0, 0, 0, 0.25);
      padding: 0;
      min-width: 320px;
      max-width: 480px;
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

    uui-radio.opt-allow {
      --uui-radio-border-color: color-mix(in srgb, var(--uui-color-positive, #34a853) 50%, transparent);
    }

    uui-radio.opt-deny {
      --uui-radio-border-color: color-mix(in srgb, var(--uui-color-danger, #ea4335) 50%, transparent);
    }
  `;
}

export default UasSecurityEditorRootElement;

declare global {
  interface HTMLElementTagNameMap {
    'uas-security-editor-root': UasSecurityEditorRootElement;
  }
}
