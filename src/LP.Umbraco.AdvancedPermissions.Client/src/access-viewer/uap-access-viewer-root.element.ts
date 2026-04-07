import { html, css, nothing, customElement, state, query } from '@umbraco-cms/backoffice/external/lit';
import type { TemplateResult } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import { UMB_MODAL_MANAGER_CONTEXT } from '@umbraco-cms/backoffice/modal';
import type {
  VerbInfo,
  RoleInfo,
  UserItem,
  EffectivePermission,
  ReasoningStep,
} from '../models/permission.models.js';
import { getVerbs, getTreeRoot, getTreeChildren, getEffectiveForUser, getEffectiveForRole } from '../api/advanced-permissions.api.js';
import { UAP_ROLE_PICKER_MODAL } from './role-picker-modal.token.js';
import { UAP_USER_PICKER_MODAL } from './user-picker-modal.token.js';
import '../components/uap-picker-button.element.js';

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

/**
 * Access Viewer workspace element.
 * Shows fully resolved (effective) permissions for a user or role at each content node,
 * including reasoning that traces inherited permissions back to their source.
 */
@customElement('uap-access-viewer-root')
export class UapAccessViewerRootElement extends UmbLitElement {
  #localize = new UmbLocalizationController(this);

  // ── Metadata ────────────────────────────────────────────────────────────
  @state() private _verbs: VerbInfo[] = [];

  // ── Subject selection ────────────────────────────────────────────────────
  @state() private _selectedRole: RoleInfo | null = null;
  @state() private _selectedUser: UserItem | null = null;
  /** Which subject was most recently picked; determines the tree view mode. */
  @state() private _activeSubject: 'role' | 'user' | null = null;

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

  // ── Data loading ─────────────────────────────────────────────────────────

  async #loadMeta(): Promise<void> {
    try {
      this._verbs = await getVerbs();
    } catch (err) {
      this._error = String(err);
    }
  }

  get #subject(): string {
    if (this._activeSubject === 'role') return this._selectedRole?.alias ?? '';
    if (this._activeSubject === 'user') return this._selectedUser?.unique ?? '';
    return '';
  }

  async #loadTree(): Promise<void> {
    if (!this.#subject) return;

    // Cancel any in-flight load from a previous selection
    this.#loadAbortController?.abort();
    const controller = new AbortController();
    this.#loadAbortController = controller;

    this._loading = true;
    this._error = null;
    this._treeNodes = [];

    try {
      // Use security editor tree endpoint to get root nodes (entries not needed, just structure)
      const nodes = await getTreeRoot('$everyone', controller.signal);
      if (controller.signal.aborted) return;

      this._treeNodes = nodes.map((n) => ({
        key: n.key,
        name: n.name,
        icon: n.icon,
        hasChildren: n.hasChildren,
        expanded: false,
        loading: false,
        effectivePerms: null,
      }));
      // Load effective permissions for root nodes in batches to avoid request flooding
      await this.#loadEffectiveBatch(this._treeNodes, controller.signal);
    } catch (err) {
      if (controller.signal.aborted) return;
      this._error = String(err);
    } finally {
      if (!controller.signal.aborted) this._loading = false;
    }
  }

  /**
   * Reloads effective permissions for all loaded nodes without rebuilding the tree.
   * Preserves expanded state and children.
   */
  async #reloadEffective(): Promise<void> {
    if (!this.#subject || this._treeNodes.length === 0) return;

    // Cancel any in-flight load
    this.#loadAbortController?.abort();
    const controller = new AbortController();
    this.#loadAbortController = controller;

    this._loading = true;
    this._error = null;

    // Clear effective permissions on all loaded nodes
    this.#clearEffectiveRecursive(this._treeNodes);
    this._treeNodes = [...this._treeNodes]; // trigger re-render to show loading state

    try {
      // Reload effective permissions for root nodes
      await this.#loadEffectiveBatch(this._treeNodes, controller.signal);
      if (controller.signal.aborted) return;

      // Reload for any expanded children
      await this.#reloadExpandedChildren(this._treeNodes, controller.signal);
    } catch (err) {
      if (controller.signal.aborted) return;
      this._error = String(err);
    } finally {
      if (!controller.signal.aborted) this._loading = false;
    }
  }

  #clearEffectiveRecursive(nodes: ViewerTreeNode[]): void {
    for (const node of nodes) {
      node.effectivePerms = null;
      if (node.children) this.#clearEffectiveRecursive(node.children);
    }
  }

  async #reloadExpandedChildren(nodes: ViewerTreeNode[], signal: AbortSignal): Promise<void> {
    for (const node of nodes) {
      if (node.children && node.expanded) {
        await this.#loadEffectiveBatch(node.children, signal);
        if (signal.aborted) return;
        await this.#reloadExpandedChildren(node.children, signal);
        if (signal.aborted) return;
      }
    }
  }

  /**
   * Loads effective permissions for a batch of nodes, throttled to avoid
   * flooding the server with too many simultaneous requests.
   */
  async #loadEffectiveBatch(nodes: ViewerTreeNode[], signal: AbortSignal): Promise<void> {
    const batchSize = 8;
    for (let i = 0; i < nodes.length; i += batchSize) {
      if (signal.aborted) return;
      const batch = nodes.slice(i, i + batchSize);
      await Promise.all(batch.map((n) => this.#loadEffective(n, signal)));
    }
  }

  async #loadEffective(node: ViewerTreeNode, signal?: AbortSignal): Promise<void> {
    if (!this.#subject) return;
    try {
      const result =
        this._activeSubject === 'role'
          ? await getEffectiveForRole(this._selectedRole!.alias, node.key, signal)
          : await getEffectiveForUser(this._selectedUser!.unique, node.key, signal);

      if (signal?.aborted) return;

      const permsMap = new Map<string, EffectivePermission>();
      for (const p of result.permissions) {
        permsMap.set(p.verb, p);
      }
      node.effectivePerms = permsMap;
      this._treeNodes = [...this._treeNodes]; // trigger re-render
    } catch {
      // Non-fatal: leave effectivePerms null (shows loading indicator)
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
      // Load effective permissions in batches
      await this.#loadEffectiveBatch(childNodes, this.#loadAbortController?.signal ?? new AbortController().signal);
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

  // ── Picker methods ────────────────────────────────────────────────────────

  async #openRolePicker(): Promise<void> {
    if (!this.#modalManager) return;

    const modal = this.#modalManager.open(this, UAP_ROLE_PICKER_MODAL, {
      data: {
        ...(this._selectedRole ? { currentRole: this._selectedRole.alias } : {}),
      },
    });

    const result = await modal.onSubmit().catch(() => undefined);
    if (!result) return;

    const hadTree = this._treeNodes.length > 0 && this._activeSubject === 'role';
    this._selectedRole = result.role;
    this._selectedUser = null;
    this._activeSubject = 'role';

    if (hadTree) {
      void this.#reloadEffective();
    } else {
      void this.#loadTree();
    }
  }

  async #openUserPicker(): Promise<void> {
    if (!this.#modalManager) return;

    const modal = this.#modalManager.open(this, UAP_USER_PICKER_MODAL, {
      data: {
        ...(this._selectedUser ? { currentUser: this._selectedUser.unique } : {}),
      },
    });

    const result = await modal.onSubmit().catch(() => undefined);
    if (!result) return;

    const hadTree = this._treeNodes.length > 0 && this._activeSubject === 'user';
    this._selectedUser = result.user;
    this._selectedRole = null;
    this._activeSubject = 'user';

    if (hadTree) {
      void this.#reloadEffective();
    } else {
      void this.#loadTree();
    }
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
        ${this._verbs.map((v) => this.#renderEffectiveCell(node, v.verb))}
      </tr>
    `;
  }

  #renderEffectiveCell(node: ViewerTreeNode, verb: string) {
    if (!node.effectivePerms) {
      return html`<td class="perm-td" title=${verb}><div class="perm-block loading">\u2026</div></td>`;
    }

    const perm = node.effectivePerms.get(verb);
    const isAllowed = perm?.isAllowed ?? false;
    const cls = isAllowed ? 'allow' : 'deny';
    const icon = isAllowed ? '\u2713' : '\u2717';

    return html`
      <td class="perm-td" title=${this.#localize.term('uap_clickForReasoning', isAllowed ? this.#localize.term('uap_allow') : this.#localize.term('uap_deny'))}
        @click=${() => this.#openReasoning(node, verb)}>
        <div class="perm-block ${cls}">${icon}</div>
      </td>
    `;
  }

  #renderReasoningStep(step: ReasoningStep) {
    const stateClass = step.state === 'Allow' ? 'step-allow' : 'step-deny';
    return html`
      <li class="reasoning-step ${stateClass}">
        <span class="step-state">${step.state === 'Allow' ? this.#localize.term('uap_allow') : this.#localize.term('uap_deny')}</span>
        <span class="step-role">${step.contributingRole}</span>
        ${step.isFromGroupDefault
          ? html`<span class="step-source">${this.#localize.term('uap_groupDefault')}</span>`
          : step.sourceNodeKey
            ? html`<span class="step-source">
                ${this.#localize.term('uap_fromNode')} \u00b7 ${step.sourceScope ?? ''}
                ${step.isExplicit ? '' : this.#localize.term('uap_inherited')}
              </span>`
            : nothing}
        ${!step.isExplicit ? html`<span class="step-implicit">${this.#localize.term('uap_resultImplicit')}</span>` : nothing}
      </li>
    `;
  }

  override render() {
    return html`
      <umb-body-layout headline=${this.#localize.term('uap_viewerHeadline')}>
        <div class="toolbar">
          <uap-picker-button
            label=${this.#localize.term('uap_chooseRole')}
            .selectedName=${this._selectedRole?.name ?? ''}
            icon="icon-users"
            @click=${() => void this.#openRolePicker()}>
          </uap-picker-button>
          <uap-picker-button
            label=${this.#localize.term('uap_chooseUser')}
            .selectedName=${this._selectedUser?.name ?? ''}
            icon="icon-user"
            @click=${() => void this.#openUserPicker()}>
          </uap-picker-button>
        </div>

${this._error ? html`<p class="error-msg">\u26a0 ${this._error}</p>` : nothing}
        ${this._loading ? html`<div class="loading"><uui-loader></uui-loader></div>` : nothing}
        ${!this.#subject ? html`<p class="empty-msg">${this.#localize.term('uap_selectSubjectPrompt')}</p>` : nothing}

        ${this.#subject && !this._loading && this._treeNodes.length > 0
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

      <!-- Reasoning modal dialog -->
      <dialog
        class="reasoning-dialog"
        @close=${() => {
          this._reasoningNode = null;
          this._reasoningVerb = null;
          this._reasoningPerm = null;
        }}>
        <uui-dialog-layout
          headline=${this.#localize.term('uap_reasoningHeadline', this._reasoningVerb?.split('.').pop() ?? '')}>
          <p class="dialog-node">
            ${this.#localize.term('uap_reasoningNodeLabel')}: <strong>${this._reasoningNode?.name ?? ''}</strong>
          </p>

          ${this._reasoningPerm
            ? html`
                <div class="reasoning-result ${this._reasoningPerm.isAllowed ? 'result-allow' : 'result-deny'}">
                  <strong>${this._reasoningPerm.isAllowed ? this.#localize.term('uap_resultAllowed') : this.#localize.term('uap_resultDenied')}</strong>
                  \u2014 ${this._reasoningPerm.isExplicit ? this.#localize.term('uap_resultExplicit') : this.#localize.term('uap_resultImplicit')}
                </div>
                <h3 class="reasoning-list-title">${this.#localize.term('uap_contributingFactors')}</h3>
                ${this._reasoningPerm.reasoning.length > 0
                  ? html`<ul class="reasoning-list">
                      ${this._reasoningPerm.reasoning.map((step) => this.#renderReasoningStep(step))}
                    </ul>`
                  : html`<p class="no-reasoning">${this.#localize.term('uap_noReasoningEntries')}</p>`}
              `
            : html`<p class="no-reasoning">${this.#localize.term('uap_noReasoningData')}</p>`}

          <div slot="actions">
            <uui-button look="primary" @click=${() => this._reasoningDialog.close()}>
              ${this.#localize.term('uap_close')}
            </uui-button>
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
      cursor: pointer;
    }

    .perm-block {
      display: flex;
      align-items: center;
      justify-content: center;
      height: 26px;
      border: 1px solid var(--uui-color-border, #ddd);
      border-radius: 4px;
      user-select: none;
      overflow: hidden;
      font-size: 13px;
      font-weight: 700;
    }

    .perm-block:hover {
      border-color: var(--uui-color-border-emphasis, #bbb);
    }

    .perm-block.loading {
      color: var(--uui-color-text-alt, #aaa);
      font-size: 11px;
      font-weight: 400;
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

export default UapAccessViewerRootElement;

declare global {
  interface HTMLElementTagNameMap {
    'uap-access-viewer-root': UapAccessViewerRootElement;
  }
}
