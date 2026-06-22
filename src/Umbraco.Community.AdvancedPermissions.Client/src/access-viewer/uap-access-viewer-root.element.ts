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
import { getVerbs, getRoles, getTreeRoot, getTreeChildren, getEffectiveForUser, getEffectiveForRole, getPermissionsForPath } from '../api/advanced-permissions.api.js';

import { UAP_ROLE_PICKER_MODAL } from './role-picker-modal.token.js';
import { UAP_USER_PICKER_MODAL } from './user-picker-modal.token.js';
import type { CellInfo } from '../utils/cell-info.js';
import { updateNode } from '../utils/tree-ops.js';
import '../shared/components/uap-perm-block.element.js';
import '../shared/components/uap-reasoning-dialog.element.js';
import '../help/uap-page-intro.element.js';
import '../help/uap-selection-panel.element.js';
import type { UapSelectorGroup } from '../help/uap-selection-panel.element.js';
import type { UapReasoningDialogElement } from '../shared/components/uap-reasoning-dialog.element.js';

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
  /** Maps role alias → display name for showing friendly role names in the dialog. */
  #roleNames = new Map<string, string>();

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
  @state() private _dialogPath: PathNode[] = [];
  /** nodeKey → array of {roleAlias, entries for the verb} for roles that have entries at that node. */
  @state() private _dialogEntriesByNode: Map<string, Array<{ role: string; entries: PermissionEntry[] }>> = new Map();
  @state() private _dialogLoading = false;
  /** Whether the dialog should show stars on deny entries (deny trumping allow). */
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

  // ── Data loading ─────────────────────────────────────────────────────────

  async #loadMeta(): Promise<void> {
    try {
      const [verbs, roles] = await Promise.all([getVerbs(), getRoles()]);
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

  /** Wrapper over the shared `updateNode` tree-op helper. */
  #updateNode(key: string, changes: Partial<ViewerTreeNode>): void {
    this._treeNodes = updateNode(this._treeNodes, key, changes);
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

  async #openReasoning(node: ViewerTreeNode, verb: string): Promise<void> {
    this._reasoningNode = node;
    this._reasoningVerb = verb;
    this._dialogPath = [];
    this._dialogEntriesByNode = new Map();
    this._dialogLoading = true;
    this._dialogShowStars = false;

    void this.updateComplete.then(() => this._reasoningDialog.open());

    try {
      const result = await getPermissionsForPath(node.key, verb);

      this._dialogPath = result.path;
      const targetKey = node.key;

      // Determine which roles are relevant for the current subject.
      // Only entries for these roles should appear in the reasoning dialog.
      let relevantRoles: Set<string>;
      if (this._activeSubject === 'role') {
        // Role mode: a role is self-contained — show only its own entries, never $everyone.
        relevantRoles = new Set([this._selectedRole!.alias]);
      } else {
        // User mode: show only roles that actually contributed to the effective permission.
        // These come from the reasoning steps already loaded for this node+verb. Include both the
        // winning reasoning AND the suppressed reasoning — when a priority override fires, the
        // overridden role (e.g. an Administrators Deny that lost to an All Users override) lives in
        // suppressedReasoning, and it must still appear in the chain so admins can see what was set.
        relevantRoles = new Set(['$everyone']);
        const ep = node.effectivePerms?.get(verb);
        if (ep) {
          for (const step of ep.reasoning) {
            relevantRoles.add(step.contributingRole);
          }
          for (const step of ep.suppressedReasoning ?? []) {
            relevantRoles.add(step.contributingRole);
          }
        }
      }

      // Group entries by nodeKey → roleAlias, filtering by applicable scope.
      // For ancestor nodes: only ThisNodeAndDescendants and DescendantsOnly apply to the target.
      // For the target node itself: only ThisNodeOnly and ThisNodeAndDescendants apply.
      // For the virtual root: entries always use ThisNodeAndDescendants, so they always apply.
      const byNode = new Map<string, Map<string, PermissionEntry[]>>();
      for (const entry of result.entries) {
        // Filter to only roles relevant for this subject.
        if (!relevantRoles.has(entry.roleAlias)) continue;

        const isTarget = entry.nodeKey === targetKey;
        const scope = entry.scope;

        // Filter: does this scope apply to the target from this node's position?
        if (isTarget && scope === 'DescendantsOnly') continue;      // doesn't apply to the node itself
        if (!isTarget && scope === 'ThisNodeOnly') continue;         // doesn't propagate to descendants

        const nodeKey = entry.nodeKey;
        if (!byNode.has(nodeKey)) byNode.set(nodeKey, new Map());
        const roleMap = byNode.get(nodeKey)!;
        if (!roleMap.has(entry.roleAlias)) roleMap.set(entry.roleAlias, []);
        roleMap.get(entry.roleAlias)!.push(entry);
      }

      // Convert to the display format
      const display = new Map<string, Array<{ role: string; entries: PermissionEntry[] }>>();
      for (const [nodeKey, roleMap] of byNode) {
        const roleEntries: Array<{ role: string; entries: PermissionEntry[] }> = [];
        for (const [role, entries] of roleMap) {
          roleEntries.push({ role, entries });
        }
        display.set(nodeKey, roleEntries);
      }
      this._dialogEntriesByNode = display;

      // Stars are shown when different ROLES at the same node have conflicting states
      // (one role allows, another denies). A single role with a split scope is NOT a conflict.
      let showStars = false;
      for (const [, roleMap] of byNode) {
        if (roleMap.size < 2) continue;
        // Check if different roles disagree: one role's entries contain Allow, another's contain Deny
        let nodeHasRoleAllow = false;
        let nodeHasRoleDeny = false;
        for (const [, entries] of roleMap) {
          const roleHasAllow = entries.some((e) => e.state === 'Allow');
          const roleHasDeny = entries.some((e) => e.state === 'Deny');
          // A role that only allows counts as "allow role"; a role that only denies counts as "deny role"
          // A role with both (split) doesn't trigger the conflict on its own
          if (roleHasAllow && !roleHasDeny) nodeHasRoleAllow = true;
          if (roleHasDeny && !roleHasAllow) nodeHasRoleDeny = true;
          // A role with both allow AND deny entries (split) — check the dominant state
          if (roleHasAllow && roleHasDeny) {
            nodeHasRoleAllow = true;
            nodeHasRoleDeny = true;
          }
        }
        if (nodeHasRoleAllow && nodeHasRoleDeny) {
          showStars = true;
          break;
        }
      }
      this._dialogShowStars = showStars;
    } catch {
      // Non-fatal: dialog shows without entries
    } finally {
      this._dialogLoading = false;
    }
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
            ...(this._activeSubject === 'role' && this._selectedRole ? { selectedName: this._selectedRole.name } : {}),
          },
          {
            id: 'user',
            label: this.#localize.term('uap_chooseUser'),
            icon: 'icon-user',
            ...(this._activeSubject === 'user' && this._selectedUser ? { selectedName: this._selectedUser.name } : {}),
          },
        ],
      },
    ];
  }

  #onSelectorClick(id: string): void {
    if (id === 'group') void this.#openRolePicker();
    else if (id === 'user') void this.#openUserPicker();
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
      return html`
        <td class="perm-td" title=${verb}>
          <uap-perm-block loading></uap-perm-block>
        </td>
      `;
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


  override render() {
    return html`
      <umb-body-layout headline=${this.#localize.term('uap_viewerHeadline')}>
        <uap-page-intro surface="uap-access-viewer" headline=${this.#localize.term('uap_viewerHeadline')}></uap-page-intro>
        <uap-selection-panel
          .groups=${this.#selectionGroups}
          promptText=${this.#localize.term('uap_selectSubjectPrompt')}
          ctaIcon="icon-eye"
          orLabel=${this.#localize.term('uap_subjectOr')}
          @uap-selector-click=${(e: CustomEvent<{ id: string }>) => this.#onSelectorClick(e.detail.id)}>
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
                      ${this.#renderRows(this._treeNodes, 0)}
                    </tbody>
                  </table>
                </div>
              `
            : nothing}
        </uap-selection-panel>
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

  /** Returns the effective permission to show in the reasoning dialog banner. */
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
      cursor: pointer;
    }

  `;
}

export default UapAccessViewerRootElement;

declare global {
  interface HTMLElementTagNameMap {
    'uap-access-viewer-root': UapAccessViewerRootElement;
  }
}
