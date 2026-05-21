import { html, css, nothing, customElement, state, query } from '@umbraco-cms/backoffice/external/lit';
import type { TemplateResult } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import { UMB_MODAL_MANAGER_CONTEXT } from '@umbraco-cms/backoffice/modal';
import type {
  RoleInfo,
  UserItem,
  PathNode,
  PermissionEntry,
} from '../models/permission.models.js';
import { getRoles, getTreeRoot, getTreeChildren } from '../api/advanced-permissions.api.js';
import {
  getDocTypes,
  getDocTypeAuditForNode,
  getDocTypePathEntries,
} from '../api/doc-type-permissions.api.js';
import type {
  DocTypeListItem,
  DocTypeAuditForNodeRow,
} from '../models/doc-type-permission.models.js';
import { updateNode } from '../utils/tree-ops.js';
import type { CellInfo } from '../utils/cell-info.js';
import { UAP_ROLE_PICKER_MODAL } from '../access-viewer/role-picker-modal.token.js';
import { UAP_USER_PICKER_MODAL } from '../access-viewer/user-picker-modal.token.js';
import '../components/uap-picker-button.element.js';
import '../shared/components/uap-perm-block.element.js';
import '../shared/components/uap-reasoning-dialog.element.js';
import type {
  UapReasoningDialogElement,
  ReasoningRoleEntries,
} from '../shared/components/uap-reasoning-dialog.element.js';

/**
 * Verb columns shown by the audit. v1 ships only one verb; the list exists so future of-type
 * verbs become additional columns by simply appending here.
 */
const VERBS: ReadonlyArray<{ verb: string; labelKey: string }> = [
  { verb: 'Umb.Document.CreateOfType', labelKey: 'uap_docTypePermissions_verbInsert' },
];

/**
 * Tree node augmented with audit data: a map of doc-type-key → audit row, plus the standard
 * lazy-load flags. Mirrors the Access Viewer's `ViewerTreeNode`.
 */
interface AuditTreeNode {
  key: string;
  name: string;
  icon: string | null;
  hasChildren: boolean;
  expanded: boolean;
  loading: boolean;
  /** Map of contentTypeKey → audit row (null = not yet loaded). */
  auditResults: Map<string, DocTypeAuditForNodeRow> | null;
  children?: AuditTreeNode[];
}

/**
 * Document-type Create Audit workspace.
 *
 * Mirrors the Access Viewer: dual subject picker (role OR user), lazy-loaded tree, and a
 * reasoning dialog. The columns are non-element doc types (instead of verbs) and the cells
 * show `allow` / `deny` / `n/a` (when the doc type isn't in the parent's allowed-children list)
 * / `loading`.
 */
@customElement('uap-doc-type-create-audit-root')
export class UapDocTypeCreateAuditRootElement extends UmbLitElement {
  #localize = new UmbLocalizationController(this);

  @state() private _docTypes: DocTypeListItem[] = [];
  /** Maps role alias → display name (for the reasoning dialog). */
  #roleNames = new Map<string, string>();

  @state() private _selectedRole: RoleInfo | null = null;
  @state() private _selectedUser: UserItem | null = null;
  /** Whichever subject was picked last drives the tree. */
  @state() private _activeSubject: 'role' | 'user' | null = null;

  /**
   * The doc-type whose effective permissions to audit. Required: the audit table only renders
   * once both a subject and a doc-type are picked. Columns are verbs (currently just "Insert");
   * cells show the effective state for (subject, node, doc-type, verb).
   */
  @state() private _selectedDocType: DocTypeListItem | null = null;

  @state() private _treeNodes: AuditTreeNode[] = [];
  @state() private _loading = false;
  @state() private _error: string | null = null;

  // Reasoning dialog state
  @state() private _reasoningNode: AuditTreeNode | null = null;
  @state() private _reasoningContentTypeKey: string | null = null;
  @state() private _dialogPath: PathNode[] = [];
  @state() private _dialogEntriesByNode: Map<string, ReasoningRoleEntries[]> = new Map();
  @state() private _dialogLoading = false;
  @state() private _dialogShowStars = false;

  @query('uap-reasoning-dialog') private _reasoningDialog!: UapReasoningDialogElement;

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
      const [docTypes, roles] = await Promise.all([getDocTypes(), getRoles()]);
      this._docTypes = docTypes;
      for (const r of roles) {
        this.#roleNames.set(r.alias, r.name);
      }
    } catch (err) {
      this._error = String(err);
    }
  }

  /** Returns the audited subject as a discriminated object for the API helpers. */
  get #subject(): { userKey: string } | { roleAlias: string } | null {
    if (this._activeSubject === 'role' && this._selectedRole) return { roleAlias: this._selectedRole.alias };
    if (this._activeSubject === 'user' && this._selectedUser) return { userKey: this._selectedUser.unique };
    return null;
  }

  /** Returns the role display name (used by the reasoning dialog). */
  #roleName = (alias: string): string => this.#roleNames.get(alias) ?? alias;

  async #loadTree(): Promise<void> {
    const subject = this.#subject;
    if (!subject) return;

    this.#loadAbortController?.abort();
    const controller = new AbortController();
    this.#loadAbortController = controller;

    this._loading = true;
    this._error = null;
    this._treeNodes = [];

    try {
      // Tree structure only — entries come per node via audit-for-node
      const nodes = await getTreeRoot('$everyone', controller.signal);
      if (controller.signal.aborted) return;

      this._treeNodes = nodes.map((n) => ({
        key: n.key,
        name: n.name,
        icon: n.icon,
        hasChildren: n.hasChildren,
        expanded: false,
        loading: false,
        auditResults: null,
      }));

      await this.#loadAuditBatch(this._treeNodes, controller.signal);
    } catch (err) {
      if (controller.signal.aborted) return;
      this._error = String(err);
    } finally {
      if (!controller.signal.aborted) this._loading = false;
    }
  }

  /** Reloads audit results for all loaded nodes when the subject changes. */
  async #reloadAudit(): Promise<void> {
    if (!this.#subject || this._treeNodes.length === 0) return;

    this.#loadAbortController?.abort();
    const controller = new AbortController();
    this.#loadAbortController = controller;

    this._loading = true;
    this._error = null;
    this.#clearAuditRecursive(this._treeNodes);
    this._treeNodes = [...this._treeNodes];

    try {
      await this.#loadAuditBatch(this._treeNodes, controller.signal);
      if (controller.signal.aborted) return;
      await this.#reloadExpandedChildren(this._treeNodes, controller.signal);
    } catch (err) {
      if (controller.signal.aborted) return;
      this._error = String(err);
    } finally {
      if (!controller.signal.aborted) this._loading = false;
    }
  }

  #clearAuditRecursive(nodes: AuditTreeNode[]): void {
    for (const node of nodes) {
      node.auditResults = null;
      if (node.children) this.#clearAuditRecursive(node.children);
    }
  }

  async #reloadExpandedChildren(nodes: AuditTreeNode[], signal: AbortSignal): Promise<void> {
    for (const node of nodes) {
      if (node.children && node.expanded) {
        await this.#loadAuditBatch(node.children, signal);
        if (signal.aborted) return;
        await this.#reloadExpandedChildren(node.children, signal);
        if (signal.aborted) return;
      }
    }
  }

  /** Batched audit loading — caps simultaneous in-flight requests to avoid overwhelming the server. */
  async #loadAuditBatch(nodes: AuditTreeNode[], signal: AbortSignal): Promise<void> {
    const batchSize = 8;
    for (let i = 0; i < nodes.length; i += batchSize) {
      if (signal.aborted) return;
      const batch = nodes.slice(i, i + batchSize);
      await Promise.all(batch.map((n) => this.#loadAuditForNode(n, signal)));
    }
  }

  async #loadAuditForNode(node: AuditTreeNode, signal?: AbortSignal): Promise<void> {
    const subject = this.#subject;
    if (!subject) return;
    try {
      const result = await getDocTypeAuditForNode(subject, node.key, signal);
      if (signal?.aborted) return;

      const map = new Map<string, DocTypeAuditForNodeRow>();
      for (const row of result.results) {
        map.set(row.contentTypeKey, row);
      }
      node.auditResults = map;
      this._treeNodes = [...this._treeNodes];
    } catch {
      // Non-fatal: leave auditResults null (loading indicator stays)
    }
  }

  async #toggleExpand(node: AuditTreeNode): Promise<void> {
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
      const childNodes: AuditTreeNode[] = children.map((c) => ({
        key: c.key,
        name: c.name,
        icon: c.icon,
        hasChildren: c.hasChildren,
        expanded: false,
        loading: false,
        auditResults: null,
      }));
      this.#updateNode(node.key, { expanded: true, loading: false, children: childNodes });
      await this.#loadAuditBatch(childNodes, this.#loadAbortController?.signal ?? new AbortController().signal);
    } catch (err) {
      this.#updateNode(node.key, { loading: false });
      this.#notificationContext?.peek('danger', { data: { message: String(err) } });
    }
  }

  #updateNode(key: string, changes: Partial<AuditTreeNode>): void {
    this._treeNodes = updateNode(this._treeNodes, key, changes);
  }

  // ── Subject pickers ──────────────────────────────────────────────────────

  async #openRolePicker(): Promise<void> {
    if (!this.#modalManager) return;
    const modal = this.#modalManager.open(this, UAP_ROLE_PICKER_MODAL, {
      data: { ...(this._selectedRole ? { currentRole: this._selectedRole.alias } : {}) },
    });
    const result = await modal.onSubmit().catch(() => undefined);
    if (!result) return;

    const hadTree = this._treeNodes.length > 0 && this._activeSubject === 'role';
    this._selectedRole = result.role;
    this._selectedUser = null;
    this._activeSubject = 'role';
    if (hadTree) void this.#reloadAudit();
    else void this.#loadTree();
  }

  async #openUserPicker(): Promise<void> {
    if (!this.#modalManager) return;
    const modal = this.#modalManager.open(this, UAP_USER_PICKER_MODAL, {
      data: { ...(this._selectedUser ? { currentUser: this._selectedUser.unique } : {}) },
    });
    const result = await modal.onSubmit().catch(() => undefined);
    if (!result) return;

    const hadTree = this._treeNodes.length > 0 && this._activeSubject === 'user';
    this._selectedUser = result.user;
    this._selectedRole = null;
    this._activeSubject = 'user';
    if (hadTree) void this.#reloadAudit();
    else void this.#loadTree();
  }

  // ── Reasoning dialog ─────────────────────────────────────────────────────

  /**
   * Opens the reasoning dialog for a (node, content-type) pair. Fetches the inheritance path
   * + doc-type entries along it, filters to roles relevant for the current subject, and shapes
   * the result for `<uap-reasoning-dialog>`.
   */
  async #openReasoning(node: AuditTreeNode, contentTypeKey: string): Promise<void> {
    this._reasoningNode = node;
    this._reasoningContentTypeKey = contentTypeKey;
    this._dialogPath = [];
    this._dialogEntriesByNode = new Map();
    this._dialogLoading = true;
    this._dialogShowStars = false;

    void this.updateComplete.then(() => this._reasoningDialog.open());

    try {
      const result = await getDocTypePathEntries(node.key, contentTypeKey);

      this._dialogPath = result.path;
      const targetKey = node.key;

      // Determine which roles are relevant for the current subject — same logic as the Access Viewer.
      let relevantRoles: Set<string>;
      if (this._activeSubject === 'role') {
        relevantRoles = new Set([this._selectedRole!.alias]);
      } else {
        relevantRoles = new Set(['$everyone']);
        const row = node.auditResults?.get(contentTypeKey);
        if (row) {
          for (const step of row.reasoning) {
            relevantRoles.add(step.contributingRole);
          }
        }
      }

      // Group entries by nodeKey → role, applying the same scope-filtering rule used by the
      // node-level reasoning dialog: ancestors contribute via ThisNodeAndDescendants /
      // DescendantsOnly; the target contributes via ThisNodeOnly / ThisNodeAndDescendants.
      const byNode = new Map<string, Map<string, PermissionEntry[]>>();
      for (const entry of result.entries) {
        if (!relevantRoles.has(entry.roleAlias)) continue;
        const isTarget = entry.nodeKey === targetKey;
        if (isTarget && entry.scope === 'DescendantsOnly') continue;
        if (!isTarget && entry.scope === 'ThisNodeOnly') continue;

        const roleMap = byNode.get(entry.nodeKey) ?? new Map<string, PermissionEntry[]>();
        const list = roleMap.get(entry.roleAlias) ?? [];
        list.push({
          id: entry.id,
          nodeKey: entry.nodeKey,
          roleAlias: entry.roleAlias,
          verb: entry.verb,
          state: entry.state,
          scope: entry.scope,
        });
        roleMap.set(entry.roleAlias, list);
        byNode.set(entry.nodeKey, roleMap);
      }

      const display = new Map<string, ReasoningRoleEntries[]>();
      for (const [nodeKey, roleMap] of byNode) {
        const rows: ReasoningRoleEntries[] = [];
        for (const [role, entries] of roleMap) {
          rows.push({ role, entries });
        }
        display.set(nodeKey, rows);
      }
      this._dialogEntriesByNode = display;

      // Star calculation — same approach as the Access Viewer.
      let showStars = false;
      for (const [, roleMap] of byNode) {
        if (roleMap.size < 2) continue;
        let allow = false;
        let deny = false;
        for (const [, entries] of roleMap) {
          const a = entries.some((e) => e.state === 'Allow');
          const d = entries.some((e) => e.state === 'Deny');
          if (a) allow = true;
          if (d) deny = true;
        }
        if (allow && deny) { showStars = true; break; }
      }
      this._dialogShowStars = showStars;
    } catch {
      // Non-fatal: dialog shows the loading-cleared empty state
    } finally {
      this._dialogLoading = false;
    }
  }

  #onReasoningClose = (): void => {
    this._reasoningNode = null;
    this._reasoningContentTypeKey = null;
    this._dialogPath = [];
    this._dialogEntriesByNode = new Map();
    this._dialogShowStars = false;
  };

  // ── Rendering ────────────────────────────────────────────────────────────

  #renderRows(nodes: AuditTreeNode[], depth: number): TemplateResult[] {
    return nodes.flatMap((node) => [
      this.#renderRow(node, depth),
      ...(node.expanded && node.children ? this.#renderRows(node.children, depth + 1) : []),
    ]);
  }

  #renderRow(node: AuditTreeNode, depth: number): TemplateResult {
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
        ${VERBS.map((v) => this.#renderCell(node, v.verb))}
      </tr>
    `;
  }

  /**
   * Renders one cell for the (node, verb) pair given the currently selected doc-type. v1 has
   * only one verb so `verb` is unused for the lookup but still flows through for future-proofing
   * when more of-type verbs land.
   */
  #renderCell(node: AuditTreeNode, _verb: string): TemplateResult {
    const ctKey = this._selectedDocType?.key;
    if (!ctKey) return html`<td class="perm-td"></td>`;

    if (!node.auditResults) {
      return html`
        <td class="perm-td">
          <uap-perm-block loading></uap-perm-block>
        </td>
      `;
    }

    const row = node.auditResults.get(ctKey);
    if (!row || !row.isInAllowedChildren) {
      return html`
        <td class="perm-td" title=${this.#localize.term('uap_docTypePermissions_notInAllowedChildren')}>
          <uap-perm-block na></uap-perm-block>
        </td>
      `;
    }

    const cls: 'allow' | 'deny' = row.isAllowed ? 'allow' : 'deny';
    const info: CellInfo = { split: false, nodeClass: cls, descClass: cls };
    return html`
      <td class="perm-td"
        title=${this.#localize.term('uap_clickForReasoning', row.isAllowed ? this.#localize.term('uap_allow') : this.#localize.term('uap_deny'))}
        @click=${() => void this.#openReasoning(node, ctKey)}>
        <uap-perm-block .info=${info}></uap-perm-block>
      </td>
    `;
  }

  /** The effective permission shown in the reasoning dialog banner. */
  #currentEffective() {
    if (!this._reasoningNode || !this._reasoningContentTypeKey) return null;
    const row = this._reasoningNode.auditResults?.get(this._reasoningContentTypeKey);
    if (!row) return null;
    return {
      verb: 'Umb.Document.CreateOfType',
      isAllowed: row.isAllowed,
      isExplicit: row.isExplicit,
      reasoning: row.reasoning,
    };
  }

  #currentSubjectName(): string {
    if (this._activeSubject === 'role') return this._selectedRole?.name ?? '';
    if (this._activeSubject === 'user') return this._selectedUser?.name ?? '';
    return '';
  }

  /**
   * Verb label shown in the reasoning dialog banner. v1 always returns "Insert" because there's
   * only one of-type verb; when future verbs land this should return the verb the user clicked.
   */
  #currentVerbLabel(): string {
    return this.#localize.term('uap_docTypePermissions_verbInsert');
  }

  /**
   * Reacts to the doc-type dropdown changing. Empty value clears the selection so the audit
   * table hides; selecting a specific doc-type shows the verb columns for it.
   */
  #onDocTypeChange(event: Event): void {
    const select = event.target as HTMLSelectElement;
    const key = select.value;
    this._selectedDocType = key
      ? (this._docTypes.find((d) => d.key === key) ?? null)
      : null;
  }

  override render(): TemplateResult {
    const subject = this.#subject;
    const docTypeSelected = this._selectedDocType !== null;
    return html`
      <umb-body-layout headline=${this.#localize.term('uap_docTypePermissions_auditTitle')}>
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

          <label class="doctype-filter">
            <span>${this.#localize.term('uap_docTypePermissions_documentType')}:</span>
            <select
              @change=${(e: Event) => this.#onDocTypeChange(e)}
              .value=${this._selectedDocType?.key ?? ''}>
              <option value="">${this.#localize.term('uap_docTypePermissions_pickDocType')}</option>
              ${this._docTypes.map((dt) => html`
                <option value=${dt.key} ?selected=${dt.key === this._selectedDocType?.key}>${dt.name}</option>
              `)}
            </select>
          </label>
        </div>

        ${this._error ? html`<p class="error-msg">⚠ ${this._error}</p>` : nothing}
        ${this._loading ? html`<div class="loading"><uui-loader></uui-loader></div>` : nothing}
        ${!subject
          ? html`<p class="empty-msg">${this.#localize.term('uap_selectSubjectPrompt')}</p>`
          : !docTypeSelected
            ? html`<p class="empty-msg">${this.#localize.term('uap_docTypePermissions_pickToStart')}</p>`
            : nothing}

        ${subject && docTypeSelected && !this._loading && this._treeNodes.length > 0
          ? html`
              <div class="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th class="node-header">${this.#localize.term('uap_contentNodeHeader')}</th>
                      ${VERBS.map((v) =>
                        html`<th class="verb-header" title=${v.verb}>${this.#localize.term(v.labelKey)}</th>`,
                      )}
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

      <uap-reasoning-dialog
        .path=${this._dialogPath}
        .entriesByNode=${this._dialogEntriesByNode}
        .showStars=${this._dialogShowStars}
        .effectivePerm=${this.#currentEffective()}
        .subjectName=${this.#currentSubjectName()}
        .verbLabel=${this.#currentVerbLabel()}
        .nodeName=${this._reasoningNode?.name ?? ''}
        .loading=${this._dialogLoading}
        .roleNameLookup=${this.#roleName}
        @uap-reasoning-close=${this.#onReasoningClose}>
      </uap-reasoning-dialog>
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

    .doctype-filter {
      display: flex;
      align-items: center;
      gap: 6px;
      font-size: 13px;
    }
    .doctype-filter select {
      padding: 4px 8px;
      border: 1px solid var(--uui-color-border, #ddd);
      border-radius: 4px;
      background: var(--uui-color-surface, #fff);
      min-width: 200px;
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
      color: var(--uui-color-text, #333);
    }

    td { border-bottom: 1px solid var(--uui-color-border, #f0f0f0); }
    tr:hover td { background-color: var(--uui-color-surface-emphasis, #fafafa); }

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
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'uap-doc-type-create-audit-root': UapDocTypeCreateAuditRootElement;
  }
}

export default UapDocTypeCreateAuditRootElement;
