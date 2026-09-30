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
import { loadSelection, saveSelection, clearSelection } from '../utils/selection-store.js';
import { clearElementEffectivePermissionCache } from '../conditions/element-permission-condition.base.js';
import { UapSecurityEventsController, UAP_EVENT_SOURCES } from '../live/security-events.controller.js';
import type { UapRefreshPhase } from '../live/refresh-phase.js';
import { pruneCollapsedChildren } from '../live/tree-cache.js';
import '../live/uap-live-refresh.element.js';
import '../shared/components/uap-perm-block.element.js';
import '../shared/components/uap-reasoning-dialog.element.js';
import '../help/uap-page-intro.element.js';
import '../help/uap-selection-panel.element.js';
import type { UapSelectorGroup } from '../help/uap-selection-panel.element.js';
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
/** Stable identifier used to key this surface's remembered selection (issue #46). */
const SURFACE_ID = 'library-access-viewer';

@customElement('uap-library-access-viewer-root')
export class UapLibraryAccessViewerRootElement extends UmbLitElement {
  #localize = new UmbLocalizationController(this);

  // ── Metadata ────────────────────────────────────────────────────────────
  /** The element permission verbs, one per grid column; empty until the metadata request lands. */
  @state() private _verbs: VerbInfo[] = [];
  /** Maps role alias → display name for the reasoning dialog. */
  #roleNames = new Map<string, string>();

  // ── Subject selection ──────────────────────────────────────────────────────
  /** The user group being inspected, when the subject is a group. */
  @state() private _selectedRole: RoleInfo | null = null;
  /** The user being inspected, when the subject is a user. */
  @state() private _selectedUser: UserItem | null = null;
  /** Which subject was most recently picked; determines which effective-permission endpoint is asked. */
  @state() private _activeSubject: 'role' | 'user' | null = null;

  // ── Tree ─────────────────────────────────────────────────────────────────
  /** The loaded library tree, with each node's effective permissions; rows swap their data in place on a refresh. */
  @state() private _treeNodes: ViewerTreeNode[] = [];
  /** True only for a first load or a selection change, which hides the grid behind the loader. A live refresh never sets it. */
  @state() private _loading = false;
  /** The last load failure, shown above the grid. */
  @state() private _error: string | null = null;

  // ── Reasoning dialog ─────────────────────────────────────────────────────
  /** The node whose cell opened the reasoning dialog. */
  @state() private _reasoningNode: ViewerTreeNode | null = null;
  /** The verb whose cell opened the reasoning dialog. */
  @state() private _reasoningVerb: string | null = null;
  /** The ancestor chain, root first, that the dialog walks through. */
  @state() private _dialogPath: PathNode[] = [];
  /** nodeKey → array of {roleAlias, entries for the verb} for roles that have entries at that node. */
  @state() private _dialogEntriesByNode: Map<string, Array<{ role: string; entries: PermissionEntry[] }>> = new Map();
  /** True while the dialog's inheritance path is being fetched. */
  @state() private _dialogLoading = false;
  /** Whether the dialog should show stars on deny entries (deny trumping allow). */
  @state() private _dialogShowStars = false;

  /** Where the refresh control is: idle, refreshing, or briefly confirming an update. */
  @state() private _refreshPhase: UapRefreshPhase = 'idle';

  @query('uap-reasoning-dialog') private _reasoningDialog!: UapReasoningDialogElement;

  #notificationContext: typeof UMB_NOTIFICATION_CONTEXT.TYPE | undefined = undefined;
  #modalManager: typeof UMB_MODAL_MANAGER_CONTEXT.TYPE | undefined = undefined;
  #loadAbortController: AbortController | null = null;

  /** Live-event subscription; also the entry point for the manual refresh control. */
  #liveEvents: UapSecurityEventsController;

  constructor() {
    super();
    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (ctx) => { this.#notificationContext = ctx ?? undefined; });
    this.consumeContext(UMB_MODAL_MANAGER_CONTEXT, (ctx) => { this.#modalManager = ctx ?? undefined; });

    // A viewer holds nothing of the user's, so it never asks and never flags — it just becomes
    // correct again. The keys are ignored deliberately: a permission written on an ancestor moves
    // what every descendant on screen resolves to, so there is no subset worth refetching.
    // Sources: Shows effective permissions for library elements and folders: `elementPermissions` for writes
    // to their entries, `access` for group and membership changes and for elements or folders
    // moving or being deleted. Content-node and type-level sources cannot change what is shown.
    this.#liveEvents = new UapSecurityEventsController(
      this,
      async () => {
        if (!this._activeSubject) return;
        // The client-side effective-permission cache would otherwise hand the refetch back the
        // value it already had.
        clearElementEffectivePermissionCache();
        // Background: the grid stays on screen and each row swaps in place as its answer arrives.
        await this.#reloadEffective(true);
      },
      (phase) => { this._refreshPhase = phase; },
      [
        UAP_EVENT_SOURCES.elementPermissions,
        UAP_EVENT_SOURCES.access,
      ],
    );
  }

  /** Starts the surface: loads what it needs and restores the remembered selection. Paired with {@link disconnectedCallback}, which undoes everything started here. */
  override connectedCallback(): void {
    super.connectedCallback();
    void this.#loadMeta();
    this.#restoreSelection();
  }

  /** Cancels any in-flight load, and detaches any navigation guard, so a dismounted surface does nothing on behalf of a page that is gone. */
  override disconnectedCallback(): void {
    super.disconnectedCallback();
    this.#loadAbortController?.abort();
  }

  /** Restores the last-used subject (user group or user) from per-user storage and loads its tree. */
  #restoreSelection(): void {
    const stored = loadSelection(SURFACE_ID);
    if (stored?.subjectKind === 'role' && stored.role) {
      this._selectedRole = stored.role;
      this._activeSubject = 'role';
      void this.#loadTree();
    } else if (stored?.subjectKind === 'user' && stored.user) {
      this._selectedUser = stored.user;
      this._activeSubject = 'user';
      void this.#loadTree();
    }
  }

  /** Persists the current subject selection to per-user storage. */
  #persistSelection(): void {
    if (this._activeSubject === 'role' && this._selectedRole) {
      saveSelection(SURFACE_ID, { subjectKind: 'role', role: this._selectedRole });
    } else if (this._activeSubject === 'user' && this._selectedUser) {
      saveSelection(SURFACE_ID, { subjectKind: 'user', user: this._selectedUser });
    }
  }

  /** Clears the current selection, resets the view, and forgets the stored selection. */
  #onClearSelection(): void {
    this.#loadAbortController?.abort();
    this._selectedRole = null;
    this._selectedUser = null;
    this._activeSubject = null;
    this._treeNodes = [];
    this._error = null;
    // A load aborted here never reaches its own `finally`, so the loader is cleared here.
    this._loading = false;
    clearSelection(SURFACE_ID);
  }

  // ── Data loading ─────────────────────────────────────────────────────────

  /** Loads the verb list (grid columns) and the user-group display names used by the reasoning dialog. */
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

  /** The key of the current subject (group alias or user key), or an empty string when nothing is selected. */
  get #subject(): string {
    if (this._activeSubject === 'role') return this._selectedRole?.alias ?? '';
    if (this._activeSubject === 'user') return this._selectedUser?.unique ?? '';
    return '';
  }

  /**
   * Builds the tree from scratch for the current subject and loads effective permissions for the
   * root level. Used for the first load and whenever there is no tree to reload into.
   */
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

  /**
   * Reloads effective permissions for all loaded nodes without rebuilding the tree.
   * Preserves expanded state and children.
   *
   * Two modes, because they answer different questions. A selection change (`background` false)
   * blanks the grid and shows the loader: the rows on screen describe somebody else, so leaving
   * them up would be wrong. A live or manual refresh (`background` true) is the same subject
   * asked again, so the current values stay on screen, the loader stays away, and each row swaps
   * in place only when its new answer has arrived. That keeps the reader's place on the page
   * instead of flashing the whole grid for a refresh they never asked for.
   *
   * Either way the cached children of collapsed nodes are dropped first. Only expanded rows are
   * reloaded, but expanding a node reuses whatever children it already holds, so a collapsed
   * subtree would otherwise come back showing the previous subject's values, or a placeholder that
   * never resolves, or values from before the change that caused this refresh. A viewer holds no
   * edits, so nothing needs protecting from the prune; the next expand fetches fresh, exactly as a
   * first expand does.
   * @param background True to refresh in place without any loading state.
   * @returns A promise that resolves when done. In background mode it rejects if the refresh did
   * not complete, so the refresh control does not claim the data was updated.
   */
  async #reloadEffective(background = false): Promise<void> {
    if (!this.#subject || this._treeNodes.length === 0) return;

    this.#loadAbortController?.abort();
    const controller = new AbortController();
    this.#loadAbortController = controller;

    let complete = true;
    this._treeNodes = pruneCollapsedChildren(this._treeNodes, () => false);
    if (!background) {
      this._loading = true;
      this._error = null;

      this.#clearEffectiveRecursive(this._treeNodes);
      this._treeNodes = [...this._treeNodes];
    }

    try {
      complete = (await this.#loadEffectiveBatch(this._treeNodes, controller.signal)) && complete;
      if (controller.signal.aborted) return;
      complete = (await this.#reloadExpandedChildren(this._treeNodes, controller.signal)) && complete;
    } catch (err) {
      if (controller.signal.aborted) return;
      this._error = String(err);
      if (background) throw err;
    } finally {
      // Cleared by whichever load finishes un-superseded, background included. A background
      // refresh aborts a selection load that was still showing the loader, and that load's own
      // `finally` then skips clearing it, so leaving this to foreground loads would strand the
      // grid behind the loader for good.
      if (!controller.signal.aborted) this._loading = false;
    }

    if (background && !complete && !controller.signal.aborted) {
      throw new Error('Refreshing effective permissions failed for one or more rows.');
    }
  }

  /**
   * Blanks the effective permissions of every loaded node so a selection change never shows the
   * previous subject's values while the new ones load.
   *
   * Recursive and private: the return type is annotated explicitly (CLAUDE.md #4).
   * @param nodes The nodes to clear, recursing into their loaded children.
   */
  #clearEffectiveRecursive(nodes: ViewerTreeNode[]): void {
    for (const node of nodes) {
      node.effectivePerms = null;
      if (node.children) this.#clearEffectiveRecursive(node.children);
    }
  }

  /**
   * Reloads the effective permissions of every expanded node's children, depth first.
   * @param nodes The nodes to walk.
   * @param signal Aborts the walk when the load it belongs to is superseded.
   * @returns True if every row loaded.
   */
  async #reloadExpandedChildren(nodes: ViewerTreeNode[], signal: AbortSignal): Promise<boolean> {
    let ok = true;
    for (const node of nodes) {
      if (node.children && node.expanded) {
        ok = (await this.#loadEffectiveBatch(node.children, signal)) && ok;
        if (signal.aborted) return ok;
        ok = (await this.#reloadExpandedChildren(node.children, signal)) && ok;
        if (signal.aborted) return ok;
      }
    }
    return ok;
  }

  /**
   * Loads effective permissions for a batch of nodes, throttled to avoid request flooding.
   * @param nodes The nodes to load.
   * @param signal Aborts the loading when the load it belongs to is superseded.
   * @returns True if every node loaded.
   */
  async #loadEffectiveBatch(nodes: ViewerTreeNode[], signal: AbortSignal): Promise<boolean> {
    const batchSize = 8;
    let ok = true;
    for (let i = 0; i < nodes.length; i += batchSize) {
      if (signal.aborted) return ok;
      const batch = nodes.slice(i, i + batchSize);
      const results = await Promise.all(batch.map((n) => this.#loadEffective(n, signal)));
      ok = results.every(Boolean) && ok;
    }
    return ok;
  }

  /**
   * Loads one node's effective permissions and swaps them in when they arrive.
   * @param node The node to load.
   * @param signal Aborts the request when the load it belongs to is superseded.
   * @returns False if the request failed; true otherwise (including when superseded).
   */
  async #loadEffective(node: ViewerTreeNode, signal?: AbortSignal): Promise<boolean> {
    if (!this.#subject) return true;
    try {
      const result =
        this._activeSubject === 'role'
          ? await getElementEffectiveForRole(this._selectedRole!.alias, node.key, signal)
          : await getElementEffectiveForUser(this._selectedUser!.unique, node.key, signal);

      if (signal?.aborted) return true;

      const permsMap = new Map<string, EffectivePermission>();
      for (const p of result.permissions) {
        permsMap.set(p.verb, p);
      }
      node.effectivePerms = permsMap;
      this._treeNodes = [...this._treeNodes];
      return true;
    } catch {
      // Non-fatal: a first load leaves effectivePerms null (shows loading indicator); a
      // background refresh leaves the previous values in place. Reported so a refresh can say so.
      return false;
    }
  }

  /**
   * Expands or collapses a node, fetching its children and their effective permissions the first time.
   * @param node The node whose toggle was clicked.
   */
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

  /**
   * Wrapper over the shared `updateNode` tree-op helper.
   * @param key The key of the node to change.
   * @param changes The fields to merge into it.
   */
  #updateNode(key: string, changes: Partial<ViewerTreeNode>): void {
    this._treeNodes = updateNode(this._treeNodes, key, changes);
  }

  // ── Picker methods ────────────────────────────────────────────────────────

  /** Lets the user pick a user group as the subject, then loads (or reloads) the grid for it. */
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
    this.#persistSelection();
    if (hadTree) void this.#reloadEffective();
    else void this.#loadTree();
  }

  /** Lets the user pick a user as the subject, then loads (or reloads) the grid for them. */
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
    this.#persistSelection();
    if (hadTree) void this.#reloadEffective();
    else void this.#loadTree();
  }

  // ── Reasoning dialog ──────────────────────────────────────────────────────

  /**
   * Opens the reasoning dialog for one cell and fills it with the inheritance path and the
   * entries that contributed to the result.
   * @param node The node whose cell was clicked.
   * @param verb The verb whose cell was clicked.
   */
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

  // ── Selection panel ──────────────────────────────────────────────────────

  /** The selector options for the selection panel: a user-group picker and a user picker, mutually exclusive. */
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

  /**
   * Routes a click on one of the selection panel's controls to its picker.
   * @param id The id of the selector option that was clicked.
   */
  #onSelectorClick(id: string): void {
    if (id === 'group') void this.#openRolePicker();
    else if (id === 'user') void this.#openUserPicker();
  }

  // ── Rendering ─────────────────────────────────────────────────────────────

  /**
   * Renders a level of the tree and, for expanded nodes, everything beneath it.
   *
   * Recursive and private, so the return type is annotated explicitly (CLAUDE.md #4).
   * @param nodes The sibling nodes to render.
   * @param depth The nesting depth, used for indentation.
   * @returns One row per visible node, parents before children.
   */
  #renderRows(nodes: ViewerTreeNode[], depth: number): TemplateResult[] {
    return nodes.flatMap((node) => [
      this.#renderRow(node, depth),
      ...(node.expanded && node.children ? this.#renderRows(node.children, depth + 1) : []),
    ]);
  }

  /**
   * Renders one node's row: its expander, icon and name, then one effective-permission cell per verb.
   * @param node The node to render.
   * @param depth The nesting depth, used for indentation.
   * @returns The table row.
   */
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

  /**
   * Renders one effective-permission cell: hatched when the verb does not apply to the node's kind,
   * a loading block until the node's answer has arrived, the resolved allow or deny after that.
   * @param node The node the cell belongs to.
   * @param verb The verb the cell is for.
   * @returns The table cell.
   */
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

  /** Renders the surface: the selection panel with its actions and grid, plus the dialogs that must layer above it. */
  override render(): TemplateResult {
    return html`
      <umb-body-layout headline=${this.#localize.term('uap_library_accessViewerHeadline')}>
        <uap-page-intro surface="uap-library-access-viewer" headline=${this.#localize.term('uap_library_accessViewerHeadline')}></uap-page-intro>
        <uap-selection-panel
          .groups=${this.#selectionGroups}
          promptText=${this.#localize.term('uap_selectSubjectPrompt')}
          ctaIcon="icon-globe"
          orLabel=${this.#localize.term('uap_subjectOr')}
          ?clearable=${true}
          clearLabel=${this.#localize.term('uap_clearSelection')}
          @uap-selector-click=${(e: CustomEvent<{ id: string }>) => this.#onSelectorClick(e.detail.id)}
          @uap-selection-clear=${() => this.#onClearSelection()}>
          <uap-live-refresh
            slot="actions"
            .phase=${this._refreshPhase}
            @uap-live-refresh=${() => void this.#liveEvents.refresh()}>
          </uap-live-refresh>
          ${this._error ? html`<p class="error-msg">⚠ ${this._error}</p>` : nothing}
          ${this._loading ? html`<div class="loading"><uui-loader></uui-loader></div>` : nothing}
          ${!this._loading && this._treeNodes.length > 0
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

  /**
   * The effective permission shown in the reasoning dialog banner.
   * @returns The permission for the open cell, or `null` when no cell is open.
   */
  #currentEffectivePerm(): EffectivePermission | null {
    if (!this._reasoningNode || !this._reasoningVerb) return null;
    return this._reasoningNode.effectivePerms?.get(this._reasoningVerb) ?? null;
  }

  /**
   * The subject's display name for the reasoning dialog banner.
   * @returns The group or user name, or an empty string when nothing is selected.
   */
  #currentSubjectName(): string {
    if (this._activeSubject === 'role') return this._selectedRole?.name ?? '';
    if (this._activeSubject === 'user') return this._selectedUser?.name ?? '';
    return '';
  }

  /** Resets the dialog state when the user closes the reasoning dialog. */
  #onReasoningClose = (): void => {
    this._reasoningNode = null;
    this._reasoningVerb = null;
    this._dialogPath = [];
    this._dialogEntriesByNode = new Map();
    this._dialogShowStars = false;
  };

  static override styles = css`
    :host { display: block; height: 100%; }

    .loading { display: flex; justify-content: center; padding: 32px; }
    .error-msg { padding: 12px 18px; color: var(--uui-color-danger, #b91c1c); }

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
