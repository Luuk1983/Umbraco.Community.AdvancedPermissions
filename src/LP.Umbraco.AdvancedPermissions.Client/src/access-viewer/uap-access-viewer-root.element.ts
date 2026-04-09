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
import { VIRTUAL_ROOT_NODE_KEY } from '../models/permission.models.js';
import { getVerbs, getRoles, getTreeRoot, getTreeChildren, getEffectiveForUser, getEffectiveForRole, getPermissionsForPath } from '../api/advanced-permissions.api.js';

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

  async #openReasoning(node: ViewerTreeNode, verb: string): Promise<void> {
    this._reasoningNode = node;
    this._reasoningVerb = verb;
    this._dialogPath = [];
    this._dialogEntriesByNode = new Map();
    this._dialogLoading = true;
    this._dialogShowStars = false;

    void this.updateComplete.then(() => this._reasoningDialog.showModal());

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
        // These come from the reasoning steps already loaded for this node+verb.
        relevantRoles = new Set(['$everyone']);
        const ep = node.effectivePerms?.get(verb);
        if (ep) {
          for (const step of ep.reasoning) {
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

  // ── Dialog rendering helpers ────────────────────────────────────────────

  /** Renders the effective permission summary banner above the inheritance tree. */
  #renderEffectiveBanner(): TemplateResult {
    const node = this._reasoningNode;
    const verb = this._reasoningVerb;
    if (!node || !verb) return html``;

    const perm = node.effectivePerms?.get(verb);
    if (!perm) return html``;

    const isAllowed = perm.isAllowed;
    const cls = isAllowed ? 'result-allow' : 'result-deny';
    const icon = isAllowed ? '\u2713' : '\u2717';

    const subjectName = this._activeSubject === 'role'
      ? this._selectedRole?.name ?? ''
      : this._selectedUser?.name ?? '';

    const verbLabel = verb.split('.').pop() ?? '';

    // Build the full content path (skip virtual root)
    const contentPath = this._dialogPath
      .filter((n) => n.key !== VIRTUAL_ROOT_NODE_KEY)
      .map((n) => n.name)
      .join(' > ');
    const nodePath = contentPath || node.name;

    const message = isAllowed
      ? this.#localize.term('uap_effectiveAllowed', subjectName, verbLabel, nodePath)
      : this.#localize.term('uap_effectiveDenied', subjectName, verbLabel, nodePath);

    return html`
      <div class="effective-banner ${cls}">
        <span class="banner-icon">${icon}</span>
        <span class="banner-text">${message}</span>
      </div>
    `;
  }

  #stateIcon(cls: string): string {
    if (cls === 'allow') return '\u2713'; // ✓
    if (cls === 'deny') return '\u2717';  // ✗
    return '\u2014';                       // —
  }

  /**
   * Renders the "Security" column content for a node in the reasoning dialog.
   * Shows raw stored entries using security-editor visual style (split cells + role names).
   */
  #renderSecurityCell(nodeKey: string): TemplateResult {
    const roleEntries = this._dialogEntriesByNode.get(nodeKey);

    // No entries at this node → inherit indicator
    if (!roleEntries || roleEntries.length === 0) {
      return html`
        <td class="dialog-security-cell">
          <div class="security-entry">
            <div class="d-perm-block uniform inherit">${this.#stateIcon('inherit')}</div>
          </div>
        </td>
      `;
    }

    // Show entries with role display names; star on deny-only roles when they override allow roles
    return html`
      <td class="dialog-security-cell">
        ${roleEntries.map(({ role, entries }) => {
          // Star goes on roles that have deny entries (and no allow entries) when there's a cross-role conflict
          const roleHasDeny = entries.some((e) => e.state === 'Deny');
          const roleHasAllow = entries.some((e) => e.state === 'Allow');
          const showStar = this._dialogShowStars && roleHasDeny && !roleHasAllow;
          return html`
            <div class="security-entry">
              ${this.#renderPermBlock(entries)}
              <span class="security-role">${this.#roleName(role)}</span>
              ${showStar ? html`<span class="winner-star" title=${this.#localize.term('uap_determiningEntry')}>\u2605</span>` : nothing}
            </div>
          `;
        })}
      </td>
    `;
  }

  /**
   * Renders a single permission block for a set of entries.
   * After scope filtering, entries always resolve to a single uniform state (no splits).
   */
  #renderPermBlock(entries: PermissionEntry[]): TemplateResult {
    // After scope filtering, all remaining entries for a role should agree.
    // Use the first entry's state as the display state.
    const state = entries[0]?.state === 'Allow' ? 'allow' : entries[0]?.state === 'Deny' ? 'deny' : 'inherit';
    return html`<div class="d-perm-block uniform ${state}">${this.#stateIcon(state)}</div>`;
  }

  /** Renders all path rows for the dialog table. */
  #renderDialogPathRows(): TemplateResult[] {
    return this._dialogPath.map((node, i) => {
      const isVirtualRoot = node.key === VIRTUAL_ROOT_NODE_KEY;
      return html`
        <tr>
          <td class="dialog-node-cell">
            <div class="dialog-node-inner" style="--depth: ${isVirtualRoot ? 0 : i}">
              <umb-icon name=${node.icon ?? 'icon-document'}></umb-icon>
              <span class="node-name">${isVirtualRoot ? this.#localize.term('uap_defaultPermissions') : node.name}</span>
            </div>
          </td>
          ${this.#renderSecurityCell(node.key)}
        </tr>
      `;
    });
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
          this._dialogPath = [];
          this._dialogEntriesByNode = new Map();
          this._dialogShowStars = false;
        }}>
        <uui-dialog-layout
          headline=${this.#localize.term('uap_reasoningHeadline', this._reasoningVerb?.split('.').pop() ?? '', this._reasoningNode?.name ?? '')}>

          ${this.#renderEffectiveBanner()}

          ${this._dialogLoading
            ? html`<div class="dialog-loading"><uui-loader-circle></uui-loader-circle></div>`
            : this._dialogPath.length > 0
              ? html`
                  <div class="dialog-table-wrap">
                    <table class="dialog-table">
                      <thead>
                        <tr>
                          <th class="dialog-node-header">${this.#localize.term('uap_contentNodeHeader')}</th>
                          <th class="dialog-security-header">${this.#localize.term('uap_dialogSecurityHeader')}</th>
                        </tr>
                      </thead>
                      <tbody>
                        ${this.#renderDialogPathRows()}
                      </tbody>
                    </table>
                  </div>
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
      min-width: 420px;
      max-width: 700px;
      width: max-content;
    }

    .reasoning-dialog::backdrop {
      background: rgba(0, 0, 0, 0.4);
    }

    .dialog-loading {
      display: flex;
      justify-content: center;
      padding: 24px;
    }

    /* Effective permission banner */
    .effective-banner {
      display: flex;
      align-items: flex-start;
      gap: 8px;
      padding: 10px 14px;
      border-radius: 6px;
      margin-bottom: 16px;
      font-size: 13px;
      line-height: 1.4;
    }

    .effective-banner.result-allow {
      background: color-mix(in srgb, var(--uui-color-positive, #34a853) 12%, transparent);
      border-left: 4px solid var(--uui-color-positive, #34a853);
    }

    .effective-banner.result-deny {
      background: color-mix(in srgb, var(--uui-color-danger, #ea4335) 10%, transparent);
      border-left: 4px solid var(--uui-color-danger, #ea4335);
    }

    .banner-icon {
      font-size: 16px;
      font-weight: 700;
      flex-shrink: 0;
    }

    .result-allow .banner-icon { color: var(--uui-color-positive, #34a853); }
    .result-deny .banner-icon { color: var(--uui-color-danger, #ea4335); }

    /* Dialog table */
    .dialog-table-wrap {
      overflow-x: auto;
      max-height: 400px;
      overflow-y: auto;
      margin-bottom: 12px;
    }

    .dialog-table {
      width: 100%;
      border-collapse: collapse;
    }

    .dialog-table thead {
      position: sticky;
      top: 0;
      z-index: 1;
    }

    .dialog-table th {
      padding: 6px 8px;
      text-align: left;
      border-bottom: 1px solid var(--uui-color-border, #ddd);
      font-weight: 600;
      font-size: 12px;
      background: var(--uui-color-surface, #fff);
      color: var(--uui-color-text-alt, #666);
      white-space: nowrap;
    }

    .dialog-table td {
      border-bottom: 1px solid var(--uui-color-border, #f0f0f0);
      vertical-align: middle;
    }

    .dialog-table tr:hover td {
      background-color: var(--uui-color-surface-emphasis, #fafafa);
    }

    .dialog-node-header {
      width: 50%;
    }

    .dialog-security-header {
      width: auto;
    }

    /* Dialog node cell */
    .dialog-node-cell {
      padding: 0;
    }

    .dialog-node-inner {
      display: flex;
      align-items: center;
      gap: 4px;
      padding: 0 8px 0 calc(var(--depth, 0) * 18px + 8px);
      height: 30px;
      white-space: nowrap;
      overflow: hidden;
    }

    .dialog-node-inner umb-icon {
      font-size: 16px;
      flex-shrink: 0;
      color: var(--uui-color-text-alt, #666);
    }

    /* Dialog security cell */
    .dialog-security-cell {
      padding: 4px 8px;
    }

    .security-entry {
      display: flex;
      align-items: center;
      gap: 6px;
      padding: 2px 0;
    }

    .security-role {
      font-size: 12px;
      color: var(--uui-color-text-alt, #666);
      font-family: monospace;
    }

    .winner-star {
      color: var(--uui-color-warning, #f59e0b);
      font-size: 14px;
      flex-shrink: 0;
    }

    /* Dialog permission blocks (security-editor style) */
    .d-perm-block {
      display: flex;
      align-items: center;
      justify-content: center;
      height: 24px;
      min-width: 36px;
      border: 1px solid var(--uui-color-border, #ddd);
      border-radius: 4px;
      user-select: none;
      overflow: hidden;
      flex-shrink: 0;
    }

    .d-perm-block.uniform {
      font-size: 13px;
      font-weight: 700;
    }

    .d-perm-block.inherit {
      color: var(--uui-color-text-alt, #ccc);
      border-color: var(--uui-color-border, #e8e8e8);
    }

    .d-perm-block.allow {
      background: color-mix(in srgb, var(--uui-color-positive, #34a853) 14%, transparent);
      color: color-mix(in srgb, var(--uui-color-positive, #1e7e34) 80%, #000);
      border-color: color-mix(in srgb, var(--uui-color-positive, #34a853) 30%, transparent);
    }

    .d-perm-block.deny {
      background: color-mix(in srgb, var(--uui-color-danger, #ea4335) 12%, transparent);
      color: color-mix(in srgb, var(--uui-color-danger, #c5221f) 80%, #000);
      border-color: color-mix(in srgb, var(--uui-color-danger, #ea4335) 25%, transparent);
    }

    .no-reasoning {
      color: var(--uui-color-text-alt, #888);
      font-size: 13px;
    }
  `;
}

export default UapAccessViewerRootElement;

declare global {
  interface HTMLElementTagNameMap {
    'uap-access-viewer-root': UapAccessViewerRootElement;
  }
}
