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
  PathNode,
  PermissionEntry,
} from '../models/permission.models.js';
import { getRoles } from '../api/advanced-permissions.api.js';
import {
  getElementVerbs,
  getElementTreeRoot,
  getElementTreeChildren,
  getElementEffectiveForUser,
  getElementEffectiveForRole,
  getElementPermissionsForPath,
} from '../api/element-permissions.api.js';
import { libraryApplicability } from './library-permission.descriptor.js';
import { UAP_ROLE_PICKER_MODAL } from '../access-viewer/role-picker-modal.token.js';
import { UAP_USER_PICKER_MODAL } from '../access-viewer/user-picker-modal.token.js';
import type { CellInfo } from '../utils/cell-info.js';
import { updateNode } from '../utils/tree-ops.js';
import '../components/uap-picker-button.element.js';
import '../shared/components/uap-perm-block.element.js';
import '../shared/components/uap-reasoning-dialog.element.js';
import type { UapReasoningDialogElement } from '../shared/components/uap-reasoning-dialog.element.js';

/** Client-side library tree node with effective permissions for all element verbs. */
interface ViewerTreeNode {
  key: string;
  name: string;
  icon: string | null;
  hasChildren: boolean;
  /** Whether this node is an element folder (container) rather than an element. */
  isFolder: boolean;
  expanded: boolean;
  loading: boolean;
  /** Map of verb → resolved effective permission (null = not yet loaded). */
  effectivePerms: Map<string, EffectivePermission> | null;
  children?: ViewerTreeNode[];
}

/**
 * Library Access Viewer. The element analogue of the content {@link UapAccessViewerRootElement}: shows
 * fully resolved (effective) element permissions for a user or role at each library node (folders and
 * elements), with reasoning that traces inherited permissions back to their source.
 *
 * Cells that don't apply to a node kind render as N/A rather than allow/deny — element-only verbs
 * (Publish/Unpublish/Duplicate/Rollback) can't be performed on a folder, and Create has no meaning on a
 * leaf element — matching the applicability the Library editor uses.
 */
@customElement('uap-library-access-viewer-root')
export class UapLibraryAccessViewerRootElement extends UmbLitElement {
  #localize = new UmbLocalizationController(this);

  // ── Metadata ────────────────────────────────────────────────────────────
  @state() private _verbs: VerbInfo[] = [];
  /** Maps role alias → display name for the reasoning dialog. */
  #roleNames = new Map<string, string>();

  // ── Subject selection ──────────────────────────────────────────────────────
  @state() private _selectedRole: RoleInfo | null = null;
  @state() private _selectedUser: UserItem | null = null;
  @state() private _activeSubject: 'role' | 'user' | null = null;

  // ── Tree ─────────────────────────────────────────────────────────────────
  @state() private _treeNodes: ViewerTreeNode[] = [];
  @state() private _loading = false;
  @state() private _error: string | null = null;

  // ── Reasoning dialog ─────────────────────────────────────────────────────
  @state() private _reasoningNode: ViewerTreeNode | null = null;
  @state() private _reasoningVerb: string | null = null;
  @state() private _dialogPath: PathNode[] = [];
  @state() private _dialogEntriesByNode: Map<string, Array<{ role: string; entries: PermissionEntry[] }>> = new Map();
  @state() private _dialogLoading = false;
  @state() private _dialogShowStars = false;

  @query('uap-reasoning-dialog') private _reasoningDialog!: UapReasoningDialogElement;

  #notificationContext: typeof UMB_NOTIFICATION_CONTEXT.TYPE | undefined = undefined;
  #modalManager: typeof UMB_MODAL_MANAGER_CONTEXT.TYPE | undefined = undefined;
  #loadAbortController: AbortController | null = null;

  constructor() {
    super();
    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (ctx) => { this.#notificationContext = ctx ?? undefined; });
    this.consumeContext(UMB_MODAL_MANAGER_CONTEXT, (ctx) => { this.#modalManager = ctx ?? undefined; });
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
      const [verbs, roles] = await Promise.all([getElementVerbs(), getRoles()]);
      this._verbs = verbs;
      for (const r of roles) {
        this.#roleNames.set(r.alias, r.name);
      }
    } catch (err) {
      this._error = String(err);
    }
  }

  /** Returns the display name for a role alias, falling back to the alias itself. */
  #roleName(alias: string): string {
    return this.#roleNames.get(alias) ?? alias;
  }

  get #subject(): string {
    if (this._activeSubject === 'role') return this._selectedRole?.alias ?? '';
    if (this._activeSubject === 'user') return this._selectedUser?.unique ?? '';
    return '';
  }

  async #loadTree(): Promise<void> {
    if (!this.#subject) return;

    this.#loadAbortController?.abort();
    const controller = new AbortController();
    this.#loadAbortController = controller;

    this._loading = true;
    this._error = null;
    this._treeNodes = [];

    try {
      // Structure comes from the element tree ($everyone — entries aren't needed here, just shape).
      const nodes = await getElementTreeRoot('$everyone', controller.signal);
      if (controller.signal.aborted) return;

      this._treeNodes = nodes.map((n) => ({
        key: n.key,
        name: n.name,
        icon: n.icon,
        hasChildren: n.hasChildren,
        isFolder: n.isFolder,
        expanded: false,
        loading: false,
        effectivePerms: null,
      }));
      await this.#loadEffectiveBatch(this._treeNodes, controller.signal);
    } catch (err) {
      if (controller.signal.aborted) return;
      this._error = String(err);
    } finally {
      if (!controller.signal.aborted) this._loading = false;
    }
  }

  /** Reloads effective permissions for all loaded nodes without rebuilding the tree. */
  async #reloadEffective(): Promise<void> {
    if (!this.#subject || this._treeNodes.length === 0) return;

    this.#loadAbortController?.abort();
    const controller = new AbortController();
    this.#loadAbortController = controller;

    this._loading = true;
    this._error = null;

    this.#clearEffectiveRecursive(this._treeNodes);
    this._treeNodes = [...this._treeNodes];

    try {
      await this.#loadEffectiveBatch(this._treeNodes, controller.signal);
      if (controller.signal.aborted) return;
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

  /** Loads effective permissions for a batch of nodes, throttled to avoid request flooding. */
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
          ? await getElementEffectiveForRole(this._selectedRole!.alias, node.key, signal)
          : await getElementEffectiveForUser(this._selectedUser!.unique, node.key, signal);

      if (signal?.aborted) return;

      const permsMap = new Map<string, EffectivePermission>();
      for (const p of result.permissions) {
        permsMap.set(p.verb, p);
      }
      node.effectivePerms = permsMap;
      this._treeNodes = [...this._treeNodes];
    } catch {
      // Non-fatal: leave effectivePerms null (shows loading indicator).
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
      const children = await getElementTreeChildren(node.key, '$everyone');
      const childNodes: ViewerTreeNode[] = children.map((c) => ({
        key: c.key,
        name: c.name,
        icon: c.icon,
        hasChildren: c.hasChildren,
        isFolder: c.isFolder,
        expanded: false,
        loading: false,
        effectivePerms: null,
      }));
      this.#updateNode(node.key, { expanded: true, loading: false, children: childNodes });
      await this.#loadEffectiveBatch(childNodes, this.#loadAbortController?.signal ?? new AbortController().signal);
    } catch (err) {
      this.#updateNode(node.key, { loading: false });
      this.#notificationContext?.peek('danger', { data: { message: String(err) } });
    }
  }

  /** Wrapper over the shared `updateNode` tree-op helper. */
  #updateNode(key: string, changes: Partial<ViewerTreeNode>): void {
    this._treeNodes = updateNode(this._treeNodes, key, changes);
  }

  // ── Picker methods ────────────────────────────────────────────────────────

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
    if (hadTree) void this.#reloadEffective();
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
    if (hadTree) void this.#reloadEffective();
    else void this.#loadTree();
  }

  // ── Reasoning dialog ──────────────────────────────────────────────────────

  async #openReasoning(node: ViewerTreeNode, verb: string): Promise<void> {
    this._reasoningNode = node;
    this._reasoningVerb = verb;
    this._dialogPath = [];
    this._dialogEntriesByNode = new Map();
    this._dialogLoading = true;
    this._dialogShowStars = false;

    void this.updateComplete.then(() => this._reasoningDialog.open());

    try {
      const result = await getElementPermissionsForPath(node.key, verb);
      this._dialogPath = result.path;
      const targetKey = node.key;

      // Which roles are relevant for the current subject.
      let relevantRoles: Set<string>;
      if (this._activeSubject === 'role') {
        relevantRoles = new Set([this._selectedRole!.alias]);
      } else {
        relevantRoles = new Set(['$everyone']);
        const ep = node.effectivePerms?.get(verb);
        if (ep) {
          for (const step of ep.reasoning) relevantRoles.add(step.contributingRole);
          for (const step of ep.suppressedReasoning ?? []) relevantRoles.add(step.contributingRole);
        }
      }

      // Group entries by nodeKey → roleAlias, filtering by applicable scope.
      const byNode = new Map<string, Map<string, PermissionEntry[]>>();
      for (const entry of result.entries) {
        if (!relevantRoles.has(entry.roleAlias)) continue;

        const isTarget = entry.nodeKey === targetKey;
        const scope = entry.scope;
        if (isTarget && scope === 'DescendantsOnly') continue;
        if (!isTarget && scope === 'ThisNodeOnly') continue;

        const nodeKey = entry.nodeKey;
        if (!byNode.has(nodeKey)) byNode.set(nodeKey, new Map());
        const roleMap = byNode.get(nodeKey)!;
        if (!roleMap.has(entry.roleAlias)) roleMap.set(entry.roleAlias, []);
        roleMap.get(entry.roleAlias)!.push(entry);
      }

      const display = new Map<string, Array<{ role: string; entries: PermissionEntry[] }>>();
      for (const [nodeKey, roleMap] of byNode) {
        const roleEntries: Array<{ role: string; entries: PermissionEntry[] }> = [];
        for (const [role, entries] of roleMap) {
          roleEntries.push({ role, entries });
        }
        display.set(nodeKey, roleEntries);
      }
      this._dialogEntriesByNode = display;

      // Stars when different ROLES at the same node carry conflicting states.
      let showStars = false;
      for (const [, roleMap] of byNode) {
        if (roleMap.size < 2) continue;
        let nodeHasRoleAllow = false;
        let nodeHasRoleDeny = false;
        for (const [, entries] of roleMap) {
          const roleHasAllow = entries.some((e) => e.state === 'Allow');
          const roleHasDeny = entries.some((e) => e.state === 'Deny');
          if (roleHasAllow && !roleHasDeny) nodeHasRoleAllow = true;
          if (roleHasDeny && !roleHasAllow) nodeHasRoleDeny = true;
          if (roleHasAllow && roleHasDeny) { nodeHasRoleAllow = true; nodeHasRoleDeny = true; }
        }
        if (nodeHasRoleAllow && nodeHasRoleDeny) { showStars = true; break; }
      }
      this._dialogShowStars = showStars;
    } catch {
      // Non-fatal: dialog shows without entries.
    } finally {
      this._dialogLoading = false;
    }
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
              : html`<uui-button compact look="default" class="expand-spacer" disabled aria-hidden="true" label="">▸</uui-button>`}
            <umb-icon name=${node.icon ?? (node.isFolder ? 'icon-folder' : 'icon-document')}></umb-icon>
            <span class="node-name">${node.name}</span>
          </div>
        </td>
        ${this._verbs.map((v) => this.#renderEffectiveCell(node, v.verb))}
      </tr>
    `;
  }

  #renderEffectiveCell(node: ViewerTreeNode, verb: string): TemplateResult {
    // Verbs that can't apply to this node kind render as a hatched N/A cell, matching the editor.
    if (!libraryApplicability(verb, node.isFolder).nodeApplicable) {
      return html`<td class="perm-td na-td" title=${this.#localize.term('uap_library_notApplicableTitle', verb.split('.').pop() ?? '')}>
        <uap-perm-block na></uap-perm-block>
      </td>`;
    }

    if (!node.effectivePerms) {
      return html`<td class="perm-td" title=${verb}><uap-perm-block loading></uap-perm-block></td>`;
    }

    const perm = node.effectivePerms.get(verb);
    const isAllowed = perm?.isAllowed ?? false;
    const cls: 'allow' | 'deny' = isAllowed ? 'allow' : 'deny';
    const wasOverride = perm?.wasPriorityOverrideActive === true;
    const info: CellInfo = { split: false, nodeClass: cls, descClass: cls, nodeOverride: wasOverride, descOverride: wasOverride };

    return html`
      <td class="perm-td" title=${this.#localize.term('uap_clickForReasoning', isAllowed ? this.#localize.term('uap_allow') : this.#localize.term('uap_deny'))}
        @click=${() => this.#openReasoning(node, verb)}>
        <uap-perm-block
          .info=${info}
          priority-override-title=${this.#localize.term('uap_priorityOverrideWonTitle')}></uap-perm-block>
      </td>
    `;
  }

  override render(): TemplateResult {
    return html`
      <umb-body-layout headline=${this.#localize.term('uap_library_accessViewerHeadline')}>
        <div class="toolbar">
          <uap-picker-button
            label=${this.#localize.term('uap_chooseRole')}
            .selectedName=${this._selectedRole?.name ?? ''}
            icon="icon-users"
            @click=${() => void this.#openRolePicker()}>
          </uap-picker-button>
          <span class="picker-or">${this.#localize.term('uap_subjectOr')}</span>
          <uap-picker-button
            label=${this.#localize.term('uap_chooseUser')}
            .selectedName=${this._selectedUser?.name ?? ''}
            icon="icon-user"
            @click=${() => void this.#openUserPicker()}>
          </uap-picker-button>
        </div>

        ${this._error ? html`<p class="error-msg">⚠ ${this._error}</p>` : nothing}
        ${this._loading ? html`<div class="loading"><uui-loader></uui-loader></div>` : nothing}
        ${!this.#subject ? html`<p class="empty-msg">${this.#localize.term('uap_selectSubjectPrompt')}</p>` : nothing}

        ${this.#subject && !this._loading && this._treeNodes.length > 0
          ? html`
              <div class="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th class="node-header">${this.#localize.term('uap_library_nodeHeader')}</th>
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

      <uap-reasoning-dialog
        .path=${this._dialogPath}
        .entriesByNode=${this._dialogEntriesByNode}
        .showStars=${this._dialogShowStars}
        .effectivePerm=${this.#currentEffectivePerm()}
        .subjectName=${this.#currentSubjectName()}
        .verbLabel=${this._reasoningVerb?.split('.').pop() ?? ''}
        .nodeName=${this._reasoningNode?.name ?? ''}
        .loading=${this._dialogLoading}
        .defaultState=${'deny'}
        .roleNameLookup=${(alias: string) => this.#roleName(alias)}
        @uap-reasoning-close=${this.#onReasoningClose}>
      </uap-reasoning-dialog>
    `;
  }

  /** Returns the effective permission shown in the reasoning dialog banner. */
  #currentEffectivePerm() {
    if (!this._reasoningNode || !this._reasoningVerb) return null;
    return this._reasoningNode.effectivePerms?.get(this._reasoningVerb) ?? null;
  }

  /** Returns the subject's display name for the reasoning dialog banner. */
  #currentSubjectName(): string {
    if (this._activeSubject === 'role') return this._selectedRole?.name ?? '';
    if (this._activeSubject === 'user') return this._selectedUser?.name ?? '';
    return '';
  }

  /** Resets dialog state when the user closes the reasoning dialog. */
  #onReasoningClose = (): void => {
    this._reasoningNode = null;
    this._reasoningVerb = null;
    this._dialogPath = [];
    this._dialogEntriesByNode = new Map();
    this._dialogShowStars = false;
  };

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
    .picker-or { font-size: 12px; color: var(--uui-color-text-alt, #888); text-align: center; align-self: center; }

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

    td.node-cell { padding: 0; position: sticky; left: 0; background: inherit; vertical-align: middle; }
    .node-inner {
      display: flex;
      align-items: center;
      gap: 4px;
      padding: 0 8px 0 calc(var(--depth, 0) * 18px + 8px);
      height: 32px;
      white-space: nowrap;
      overflow: hidden;
    }
    /* Invisible clone of the expand toggle; reserves equal width so leaf icons stay aligned. */
    .expand-spacer { visibility: hidden; }
    .node-name { overflow: hidden; text-overflow: ellipsis; }

    .perm-td { padding: 3px; text-align: center; vertical-align: middle; cursor: pointer; }
    .na-td { cursor: default; }
  `;
}

export default UapLibraryAccessViewerRootElement;

declare global {
  interface HTMLElementTagNameMap {
    'uap-library-access-viewer-root': UapLibraryAccessViewerRootElement;
  }
}
