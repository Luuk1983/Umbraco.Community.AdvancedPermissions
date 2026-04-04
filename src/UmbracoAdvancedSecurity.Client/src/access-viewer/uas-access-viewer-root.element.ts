import { html, css, nothing, customElement, state, query } from '@umbraco-cms/backoffice/external/lit';
import type { TemplateResult } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import type {
  VerbInfo,
  RoleInfo,
  EffectivePermission,
  ReasoningStep,
} from '../models/permission.models.js';
import { getRoles, getVerbs, getTreeRoot, getTreeChildren, getEffectiveForUser, getEffectiveForRole } from '../api/advanced-security.api.js';

/** Client-side tree node with effective permissions for all verbs. */
interface ViewerTreeNode {
  key: string;
  name: string;
  icon: string | null;
  hasChildren: boolean;
  expanded: boolean;
  loading: boolean;
  /** Map of verb → resolved effective permission (null = not yet loaded). */
  effectivePerms: Map<string, EffectivePermission> | null;
  children?: ViewerTreeNode[];
}

type ViewerMode = 'role' | 'user';

/**
 * Access Viewer workspace element.
 * Shows fully resolved (effective) permissions for a user or role at each content node,
 * including reasoning that traces inherited permissions back to their source.
 */
@customElement('uas-access-viewer-root')
export class UasAccessViewerRootElement extends UmbLitElement {
  // ── Metadata ────────────────────────────────────────────────────────────
  @state() private _roles: RoleInfo[] = [];
  @state() private _verbs: VerbInfo[] = [];

  // ── Mode & subject ───────────────────────────────────────────────────────
  @state() private _mode: ViewerMode = 'role';
  @state() private _selectedRole = '';
  @state() private _resolvedUserKey = '';

  // ── Tree ─────────────────────────────────────────────────────────────────
  @state() private _treeNodes: ViewerTreeNode[] = [];
  @state() private _loading = false;
  @state() private _error: string | null = null;

  // ── Reasoning dialog ─────────────────────────────────────────────────────
  @state() private _reasoningNode: ViewerTreeNode | null = null;
  @state() private _reasoningVerb: string | null = null;
  @state() private _reasoningPerm: EffectivePermission | null = null;

  @query('.reasoning-dialog') private _reasoningDialog!: HTMLDialogElement;

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

  // ── Data loading ─────────────────────────────────────────────────────────

  async #loadMeta(): Promise<void> {
    try {
      const [roles, verbs] = await Promise.all([getRoles(), getVerbs()]);
      this._roles = roles;
      this._verbs = verbs;
    } catch (err) {
      this._error = String(err);
    }
  }

  get #subject(): string {
    return this._mode === 'role' ? this._selectedRole : this._resolvedUserKey;
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

  async #loadTree(): Promise<void> {
    if (!this.#subject) return;
    this._loading = true;
    this._error = null;
    this._treeNodes = [];

    try {
      // Use security editor tree endpoint to get root nodes (entries not needed, just structure)
      const nodes = await getTreeRoot('$everyone'); // any valid role to get tree structure
      this._treeNodes = nodes.map((n) => ({
        key: n.key,
        name: n.name,
        icon: n.icon,
        hasChildren: n.hasChildren,
        expanded: false,
        loading: false,
        effectivePerms: null,
      }));
      // Load effective permissions for each root node
      await Promise.all(this._treeNodes.map((n) => this.#loadEffective(n)));
    } catch (err) {
      this._error = String(err);
    } finally {
      this._loading = false;
    }
  }

  async #loadEffective(node: ViewerTreeNode): Promise<void> {
    if (!this.#subject) return;
    try {
      const result =
        this._mode === 'role'
          ? await getEffectiveForRole(this._selectedRole, node.key)
          : await getEffectiveForUser(this._resolvedUserKey, node.key);

      const permsMap = new Map<string, EffectivePermission>();
      for (const p of result.permissions) {
        permsMap.set(p.verb, p);
      }
      node.effectivePerms = permsMap;
      this._treeNodes = [...this._treeNodes]; // trigger re-render
    } catch {
      // Non-fatal: leave effectivePerms null
    }
  }

  async #toggleExpand(node: ViewerTreeNode): Promise<void> {
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
      const childNodes: ViewerTreeNode[] = children.map((c) => ({
        key: c.key,
        name: c.name,
        icon: c.icon,
        hasChildren: c.hasChildren,
        expanded: false,
        loading: false,
        effectivePerms: null,
      }));
      this.#updateNode(node.key, { expanded: true, loading: false, children: childNodes });
      await Promise.all(childNodes.map((cn) => this.#loadEffective(cn)));
    } catch (err) {
      this.#updateNode(node.key, { loading: false });
      this.#notificationContext?.peek('danger', { data: { message: String(err) } });
    }
  }

  #updateNode(key: string, changes: Partial<ViewerTreeNode>, nodes: ViewerTreeNode[] = this._treeNodes): boolean {
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

  // ── Reasoning dialog ──────────────────────────────────────────────────────

  #openReasoning(node: ViewerTreeNode, verb: string): void {
    const perm = node.effectivePerms?.get(verb) ?? null;
    this._reasoningNode = node;
    this._reasoningVerb = verb;
    this._reasoningPerm = perm;
    void this.updateComplete.then(() => this._reasoningDialog.showModal());
  }

  // ── Rendering ─────────────────────────────────────────────────────────────

  #renderRows(nodes: ViewerTreeNode[], depth: number): TemplateResult[] {
    return nodes.flatMap((node) => [
      this.#renderRow(node, depth),
      ...(node.expanded && node.children ? this.#renderRows(node.children, depth + 1) : []),
    ]);
  }

  #renderRow(node: ViewerTreeNode, depth: number): TemplateResult {
    return html`
      <tr>
        <td class="node-cell" style="--depth: ${depth}">
          ${node.hasChildren || node.children
            ? html`<uui-button compact look="default" label=${node.expanded ? 'Collapse' : 'Expand'} @click=${() => void this.#toggleExpand(node)}>
                ${node.loading ? html`<uui-loader-circle></uui-loader-circle>` : node.expanded ? '▾' : '▸'}
              </uui-button>`
            : html`<span class="expand-spacer"></span>`}
          <umb-icon name=${node.icon ?? 'icon-document'}></umb-icon>
          <span class="node-name">${node.name}</span>
        </td>
        ${this._verbs.map((v) => this.#renderEffectiveCell(node, v.verb))}
      </tr>
    `;
  }

  #renderEffectiveCell(node: ViewerTreeNode, verb: string) {
    if (!node.effectivePerms) {
      return html`<td class="perm-cell loading-cell" title=${verb}>…</td>`;
    }

    const perm = node.effectivePerms.get(verb);
    const isAllowed = perm?.isAllowed ?? false;
    const colorClass = isAllowed ? 'allow' : 'deny';
    const label = isAllowed ? 'Allow' : 'Deny';

    return html`
      <td
        class="perm-cell ${colorClass}"
        title="${label} — click for reasoning"
        @click=${() => this.#openReasoning(node, verb)}>
        ${label}
      </td>
    `;
  }

  #renderReasoningStep(step: ReasoningStep) {
    const stateClass = step.state === 'Allow' ? 'step-allow' : 'step-deny';
    return html`
      <li class="reasoning-step ${stateClass}">
        <span class="step-state">${step.state}</span>
        <span class="step-role">${step.contributingRole}</span>
        ${step.isFromGroupDefault
          ? html`<span class="step-source">group default</span>`
          : step.sourceNodeKey
            ? html`<span class="step-source">
                from node · ${step.sourceScope ?? ''}
                ${step.isExplicit ? '' : '(inherited)'}
              </span>`
            : nothing}
        ${!step.isExplicit ? html`<span class="step-implicit">implicit</span>` : nothing}
      </li>
    `;
  }

  override render() {
    return html`
      <umb-body-layout headline="Access Viewer">
        <div class="toolbar">
          <!-- Mode toggle -->
          <uui-button-group>
            <uui-button
              look=${this._mode === 'role' ? 'primary' : 'secondary'}
              label="By Role"
              @click=${() => { this._mode = 'role'; this._treeNodes = []; }}>
              By Role
            </uui-button>
            <uui-button
              look=${this._mode === 'user' ? 'primary' : 'secondary'}
              label="By User"
              @click=${() => { this._mode = 'user'; this._treeNodes = []; }}>
              By User
            </uui-button>
          </uui-button-group>

          ${this._mode === 'role'
            ? html`
                <div class="subject-picker">
                  <label>Role:</label>
                  <uui-select
                    label="Role"
                    placeholder="— Select a role —"
                    .options=${this.#roleOptions}
                    @change=${(e: Event) => {
                      this._selectedRole = (e.target as HTMLInputElement).value;
                      void this.#loadTree();
                    }}>
                  </uui-select>
                </div>
              `
            : html`
                <div class="subject-picker">
                  <label>User:</label>
                  <umb-user-input
                    max="1"
                    @change=${(e: Event) => {
                      const value = (e.target as HTMLInputElement).value ?? '';
                      this._resolvedUserKey = value;
                      if (value) {
                        void this.#loadTree();
                      } else {
                        this._treeNodes = [];
                      }
                    }}>
                  </umb-user-input>
                </div>
              `}
        </div>

        <div class="legend">
          <span class="legend-item allow">Allow</span>
          <span class="legend-item deny">Deny</span>
        </div>

        ${this._error ? html`<p class="error-msg">⚠ ${this._error}</p>` : nothing}
        ${this._loading ? html`<div class="loading"><uui-loader></uui-loader></div>` : nothing}
        ${!this.#subject ? html`<p class="empty-msg">Select a role or user to view effective permissions.</p>` : nothing}

        ${this.#subject && !this._loading && this._treeNodes.length > 0
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

      <!-- Reasoning modal dialog -->
      <dialog
        class="reasoning-dialog"
        @close=${() => {
          this._reasoningNode = null;
          this._reasoningVerb = null;
          this._reasoningPerm = null;
        }}>
        <uui-dialog-layout
          headline="Permission Reasoning: ${this._reasoningVerb?.split('.').pop() ?? ''}">
          <p class="dialog-node">Node: <strong>${this._reasoningNode?.name ?? ''}</strong></p>

          ${this._reasoningPerm
            ? html`
                <div class="reasoning-result ${this._reasoningPerm.isAllowed ? 'result-allow' : 'result-deny'}">
                  <strong>${this._reasoningPerm.isAllowed ? 'Allowed' : 'Denied'}</strong>
                  — ${this._reasoningPerm.isExplicit ? 'explicit (set directly on this node)' : 'implicit (inherited or from group defaults)'}
                </div>
                <h3 class="reasoning-list-title">Contributing factors:</h3>
                ${this._reasoningPerm.reasoning.length > 0
                  ? html`<ul class="reasoning-list">
                      ${this._reasoningPerm.reasoning.map((step) => this.#renderReasoningStep(step))}
                    </ul>`
                  : html`<p class="no-reasoning">No explicit entries found — effective permission comes from system defaults.</p>`}
              `
            : html`<p class="no-reasoning">No effective permission data available for this verb.</p>`}

          <div slot="actions">
            <uui-button look="primary" @click=${() => this._reasoningDialog.close()}>Close</uui-button>
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

    .subject-picker {
      display: flex;
      align-items: center;
      gap: 8px;
    }

    .subject-picker label {
      font-weight: 600;
      white-space: nowrap;
    }

    .subject-picker uui-select {
      min-width: 240px;
    }

    /* ── Legend ───────────────────────────────────────────────── */
    .legend {
      display: flex;
      gap: 16px;
      flex-wrap: wrap;
      padding: 8px 18px;
      font-size: 11px;
      background: var(--uui-color-surface-alt, #f8f8f8);
      border-bottom: 1px solid var(--uui-color-border, #eee);
    }

    .legend-item {
      padding: 2px 8px;
      border-radius: 4px;
      font-weight: 600;
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

    /* ── Effective permission cells ───────────────────────────── */
    .perm-cell {
      text-align: center;
      padding: 5px 4px;
      cursor: pointer;
      font-size: 13px;
      font-weight: 700;
      min-width: 72px;
      user-select: none;
    }

    .perm-cell.loading-cell {
      color: var(--uui-color-text-alt, #aaa);
      font-size: 11px;
      font-weight: 400;
    }

    .perm-cell.allow {
      background: color-mix(in srgb, var(--uui-color-positive, #34a853) 30%, transparent);
      color: color-mix(in srgb, var(--uui-color-positive, #1e7e34) 90%, #000);
    }

    .perm-cell.deny {
      background: color-mix(in srgb, var(--uui-color-danger, #ea4335) 25%, transparent);
      color: color-mix(in srgb, var(--uui-color-danger, #c5221f) 90%, #000);
    }

    .perm-cell:hover {
      opacity: 0.8;
    }

    /* ── Legend items (reuse cell colors) ─────────────────────── */
    .legend-item.allow {
      background: color-mix(in srgb, var(--uui-color-positive, #34a853) 30%, transparent);
      color: color-mix(in srgb, var(--uui-color-positive, #1e7e34) 90%, #000);
    }
    .legend-item.deny {
      background: color-mix(in srgb, var(--uui-color-danger, #ea4335) 25%, transparent);
      color: color-mix(in srgb, var(--uui-color-danger, #c5221f) 90%, #000);
    }

    /* ── Reasoning dialog ─────────────────────────────────────── */
    .reasoning-dialog {
      border: none;
      border-radius: 8px;
      box-shadow: 0 8px 32px rgba(0, 0, 0, 0.25);
      padding: 0;
      min-width: 360px;
      max-width: 540px;
    }

    .reasoning-dialog::backdrop {
      background: rgba(0, 0, 0, 0.4);
    }

    .dialog-node {
      margin: 0 0 16px;
      font-size: 12px;
      color: var(--uui-color-text-alt, #666);
    }

    .reasoning-result {
      padding: 10px 14px;
      border-radius: 6px;
      margin-bottom: 16px;
      font-size: 14px;
    }

    .reasoning-result.result-allow {
      background: color-mix(in srgb, var(--uui-color-positive, #34a853) 20%, transparent);
      border-left: 4px solid var(--uui-color-positive, #34a853);
    }

    .reasoning-result.result-deny {
      background: color-mix(in srgb, var(--uui-color-danger, #ea4335) 15%, transparent);
      border-left: 4px solid var(--uui-color-danger, #ea4335);
    }

    .reasoning-list-title {
      font-size: 13px;
      font-weight: 600;
      margin: 0 0 8px;
    }

    .reasoning-list {
      list-style: none;
      padding: 0;
      margin: 0 0 20px;
      display: flex;
      flex-direction: column;
      gap: 6px;
    }

    .reasoning-step {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 6px 10px;
      border-radius: 4px;
      font-size: 12px;
      flex-wrap: wrap;
    }

    .reasoning-step.step-allow {
      background: color-mix(in srgb, var(--uui-color-positive, #34a853) 12%, transparent);
    }

    .reasoning-step.step-deny {
      background: color-mix(in srgb, var(--uui-color-danger, #ea4335) 10%, transparent);
    }

    .step-state {
      font-weight: 700;
      min-width: 36px;
    }

    .step-role {
      font-weight: 600;
      font-family: monospace;
    }

    .step-source,
    .step-implicit {
      font-size: 11px;
      color: var(--uui-color-text-alt, #777);
    }

    .no-reasoning {
      color: var(--uui-color-text-alt, #888);
      font-size: 13px;
    }

    .dialog-actions {
      display: flex;
      justify-content: flex-end;
    }
  `;
}

export default UasAccessViewerRootElement;

declare global {
  interface HTMLElementTagNameMap {
    'uas-access-viewer-root': UasAccessViewerRootElement;
  }
}
