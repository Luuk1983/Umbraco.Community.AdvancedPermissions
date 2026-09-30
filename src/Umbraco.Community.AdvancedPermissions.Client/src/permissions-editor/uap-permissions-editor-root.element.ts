import { html, css, nothing, customElement, state, query } from '@umbraco-cms/backoffice/external/lit';
import type { TemplateResult } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import { UMB_MODAL_MANAGER_CONTEXT } from '@umbraco-cms/backoffice/modal';
import type {
  VerbInfo,
  RoleInfo,
  TreeNode,
  TreeNodeState,
  PermissionEntry,
  PermissionState,
  PermissionScope,
  BatchSaveNode,
  BatchSaveConflict,
} from '../models/permission.models.js';
import { VIRTUAL_ROOT_NODE_KEY } from '../models/permission.models.js';
import { getVerbs, getTreeRoot, getTreeChildren, getPermissionsWithStamp, savePermissionsBatch } from '../api/advanced-permissions.api.js';
import { clearEffectivePermissionCache } from '../conditions/document-user-permission.condition.js';
import { UAP_ROLE_PICKER_MODAL } from '../access-viewer/role-picker-modal.token.js';
import { decomposeEntries } from '../utils/decompose-entries.js';
import { type PendingVerbEntries } from '../utils/compose-entries.js';
import { getCellInfo } from '../utils/cell-info.js';
import { updateNode, findNode } from '../utils/tree-ops.js';
import { loadSelection, saveSelection, clearSelection } from '../utils/selection-store.js';
import { UapSecurityEventsController } from '../live/security-events.controller.js';
import { UapUnsavedChangesGuard, confirmDiscardUnsavedChanges } from '../live/unsaved-changes-guard.js';
import type { UapRefreshPhase } from '../live/refresh-phase.js';
import '../live/uap-live-refresh.element.js';
import type { CellEntry } from '../live/conflict.js';
import { reconcileNode } from '../live/reconcile.js';
import '../shared/components/uap-perm-block.element.js';
import '../shared/components/uap-permission-scope-dialog.element.js';
import '../live/uap-live-banner.element.js';
import '../live/uap-conflict-dialog.element.js';
import '../help/uap-page-intro.element.js';
import '../help/uap-selection-panel.element.js';
import type { UapSelectorGroup } from '../help/uap-selection-panel.element.js';
import type { UapPermissionScopeDialogElement } from '../shared/components/uap-permission-scope-dialog.element.js';
import type { UapConflictDialogElement, UapConflictLine } from '../live/uap-conflict-dialog.element.js';

/** Map of verb → pending entries for a single node. */
type PendingNodeChanges = Map<string, PendingVerbEntries>;

/** Stable identifier used to key this surface's remembered selection (issue #46). */
const SURFACE_ID = 'permissions-editor';

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

  // ── Live updates & conflicts ────────────────────────────────────────────
  /** Cells the server changed under an unsaved edit, keyed `nodeKey|verb`. */
  @state() private _conflicts: Set<string> = new Set();

  /** Whether the banner has been dismissed for the current set of conflicts. */
  @state() private _bannerDismissed = false;

  /** Where the refresh control is: idle, refreshing, or briefly confirming an update. */
  @state() private _refreshPhase: UapRefreshPhase = 'idle';

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
  @query('uap-conflict-dialog') private _conflictDialog!: UapConflictDialogElement;

  #notificationContext: typeof UMB_NOTIFICATION_CONTEXT.TYPE | undefined = undefined;
  #modalManager: typeof UMB_MODAL_MANAGER_CONTEXT.TYPE | undefined = undefined;
  #loadAbortController: AbortController | null = null;

  /** Live-event subscription; also the entry point for the manual refresh control. */
  #liveEvents: UapSecurityEventsController;

  /**
   * Vetoes a router navigation, browser close or reload while there are unsaved edits, and asks
   * first. In-page changes (group switch, clearing the selection) are guarded by their own
   * handlers through the same `#confirmDiscard`, because they are not router navigations.
   */
  #unsavedGuard = new UapUnsavedChangesGuard({
    hasChanges: () => this._pendingChanges.size > 0,
    confirm: () => this.#confirmDiscard(),
    onDiscard: () => this.#discardChanges(),
  });

  constructor() {
    super();
    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (ctx) => {
      this.#notificationContext = ctx ?? undefined;
    });
    this.consumeContext(UMB_MODAL_MANAGER_CONTEXT, (ctx) => {
      this.#modalManager = ctx ?? undefined;
    });

    // A clean editor takes the server's version in place; a dirty one is classified cell by cell
    // and nothing of the user's is touched. The keys are ignored deliberately — a permission
    // written on an ancestor moves what every descendant on screen resolves to, so there is no
    // subset worth refetching.
    this.#liveEvents = new UapSecurityEventsController(
      this,
      async () => {
        if (!this._selectedRole) return;
        // Nothing is reconciled while this editor's own save is in flight. The write raises an
        // event of its own, and the event carries no identity, so it cannot be told from a
        // colleague's; reconciling against it would flag the user's own save as a conflict. This
        // replaces what comparing values used to do (see `CellVerdict`). It is safe to drop
        // because a finished save reloads everything and clears the pending and conflict state,
        // and a refused one hands back the server's current values itself.
        if (this._saving) return;
        // Both paths refresh in the background: nothing here is a selection change, so the grid
        // stays on screen and only the values (and conflict flags) change underneath.
        if (this._pendingChanges.size === 0) {
          await this.#reloadPermissions(true);
          return;
        }
        await this.#reconcileWithServer();
      },
      (phase) => { this._refreshPhase = phase; },
    );
  }

  /**
   * Clears every trace of an unresolved live conflict: the flagged cells and the dismissed-banner
   * flag.
   *
   * Called wherever the tree itself is being replaced or abandoned — switching user group or
   * clearing the selection — so a conflict against the previous group's data cannot outlive the
   * data it was about.
   */
  #resetLiveState(): void {
    this._conflicts = new Set();
    this._bannerDismissed = false;
  }

  /**
   * Throws away every pending change and returns the grid to whatever the server currently holds.
   *
   * Discarding resolves any outstanding conflict by definition: with nothing of the user's left,
   * there is nothing left to contest, so the flags and the banner have to go with the edits. They
   * used to survive, leaving a warning on screen about changes that no longer existed.
   *
   * The reload is not cosmetic. A conflicted node's entries are frozen at what they were before
   * the other person's write and its stamp was never advanced, so leaving it alone would keep both
   * out of date and the next save would be refused against a value nobody is editing any more — a
   * conflict dialog with no conflict behind it. Re-reading adopts the server's current stamps along
   * with its entries.
   *
   * Runs in the background so the grid stays on screen: the user asked to drop their edits, not
   * to watch the page reload.
   */
  #discardChanges(): void {
    this._pendingChanges = new Map();
    this.#resetLiveState();
    void this.#reloadPermissions(true);
  }

  /**
   * The one place the unsaved-changes question is asked, so every path (leaving the page,
   * switching user group, clearing the selection) words it identically.
   * @returns `true` when it is fine to proceed: either nothing is pending, or the user chose to
   * discard what is. `false` when they chose to keep editing.
   */
  async #confirmDiscard(): Promise<boolean> {
    if (this._pendingChanges.size === 0) return true;
    return confirmDiscardUnsavedChanges(this, this.#localize);
  }

  override connectedCallback(): void {
    super.connectedCallback();
    // Attached here and detached in disconnectedCallback: this element mounts and unmounts
    // repeatedly, so a listener left behind would guard navigation on behalf of a dead editor.
    this.#unsavedGuard.attach();
    void this.#loadMeta();
    this.#restoreSelection();
  }

  /** Restores the last-used user group (if any) from per-user storage and loads its tree. */
  #restoreSelection(): void {
    const stored = loadSelection(SURFACE_ID);
    if (stored?.role) {
      this._selectedRole = stored.role;
      void this.#loadTree();
    }
  }

  /**
   * Clears the current selection, resets the view, and forgets the stored selection.
   *
   * Asks first when there are unsaved edits, since clearing drops them along with the tree they
   * belong to. Cancelling leaves the selection, the grid and the edits exactly as they were.
   */
  async #onClearSelection(): Promise<void> {
    if (!(await this.#confirmDiscard())) return;
    this.#loadAbortController?.abort();
    this._selectedRole = null;
    this._treeNodes = [];
    this._pendingChanges = new Map();
    this.#resetLiveState();
    this._error = null;
    clearSelection(SURFACE_ID);
  }

  override disconnectedCallback(): void {
    super.disconnectedCallback();
    this.#unsavedGuard.detach();
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

    // Picking the group that is already selected changes nothing, so it must not cost the user
    // their edits (it used to reset them silently).
    if (this._pendingChanges.size > 0 && result.role.alias === this._selectedRole?.alias) return;
    // Asked after the pick, not before opening the picker: cancelling the picker then costs nothing.
    if (!(await this.#confirmDiscard())) return;

    const hadTree = this._treeNodes.length > 0 && this._selectedRole !== null;
    this._selectedRole = result.role;
    this._pendingChanges = new Map();
    this.#resetLiveState();
    saveSelection(SURFACE_ID, { subjectKind: 'role', role: result.role });
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
      // The virtual root is synthesised here rather than returned by the tree endpoint, so it
      // needs its stamp fetched explicitly — getPermissionsWithStamp is the only place that
      // stamp can come from. A placeholder would make every save touching it a false conflict;
      // omitting it would leave it with no concurrency protection at all.
      const [virtualResult, nodes] = await Promise.all([
        getPermissionsWithStamp(VIRTUAL_ROOT_NODE_KEY, this._selectedRole!.alias, controller.signal),
        getTreeRoot(this._selectedRole!.alias, controller.signal),
      ]);
      if (controller.signal.aborted) return;

      const virtualRoot: TreeNodeState = {
        key: 'virtual-root',
        name: this.#localize.term('uap_contentRoot'),
        icon: 'icon-globe',
        hasChildren: false,
        entries: virtualResult.entries,
        stamp: virtualResult.stamp,
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
   * rebuilding the tree. Preserves expanded state and children.
   *
   * Used both when switching user group and for a live or manual refresh of a clean editor. A
   * switch (`background` false) blanks the entries and shows the loader, because what is on screen
   * belongs to the previous group. A refresh (`background` true) is the same group asked again:
   * the current entries stay on screen and are replaced in one step once every response is in,
   * so the grid neither flashes nor loses the reader's place.
   * @param background True to refresh in place without any loading state.
   * @returns A promise that resolves when done. In background mode it rejects on failure, so the
   * refresh control does not claim the data was updated.
   */
  async #reloadPermissions(background = false): Promise<void> {
    if (!this._selectedRole || this._treeNodes.length === 0) return;

    // Cancel any in-flight load from a previous role selection
    this.#loadAbortController?.abort();
    const controller = new AbortController();
    this.#loadAbortController = controller;

    if (!background) {
      this._loading = true;
      this._error = null;
    }

    try {
      // Reload virtual root entries and its stamp together — same reasoning as #loadTree, this
      // is the one place that stamp is refreshed from.
      const virtualResult = await getPermissionsWithStamp(VIRTUAL_ROOT_NODE_KEY, this._selectedRole!.alias, controller.signal);
      if (controller.signal.aborted) return;

      // A group switch clears entries on all existing nodes first; a refresh leaves them until
      // the fresh values are assigned over them, so there is no empty frame in between.
      if (!background) this.#clearEntriesRecursive(this._treeNodes);

      // Set virtual root entries
      const virtualRoot = this._treeNodes.find((n) => n.key === 'virtual-root');
      if (virtualRoot) {
        virtualRoot.entries = virtualResult.entries;
        virtualRoot.stamp = virtualResult.stamp;
      }

      // Reload entries for visible (loaded) nodes
      await this.#reloadNodeEntries(this._treeNodes, controller.signal);
      if (controller.signal.aborted) return;

      this._treeNodes = [...this._treeNodes]; // trigger re-render
    } catch (err) {
      if (controller.signal.aborted) return;
      this._error = String(err);
      if (background) throw err;
    } finally {
      if (!background && !controller.signal.aborted) this._loading = false;
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

    // Build a map from key → the freshly-loaded node. Entries and stamp travel together: a
    // stamp left behind here would still describe whatever was stored before this reload (or,
    // after a role switch, another role's entries entirely), and the next save for that node
    // would be checked against a value that is no longer real.
    const entryMap = new Map<string, TreeNode>();
    for (const n of rootNodes) {
      entryMap.set(n.key, n);
    }

    // Apply entries and stamp to existing tree nodes at root level
    for (const node of nodes) {
      if (node.key === 'virtual-root') continue;
      const fresh = entryMap.get(node.key);
      if (fresh) {
        node.entries = fresh.entries;
        node.stamp = fresh.stamp;
      }
    }

    // For expanded children, reload their entries
    for (const node of nodes) {
      if (node.children && node.expanded) {
        const childEntries = await getTreeChildren(node.key, this._selectedRole!.alias, signal);
        if (signal.aborted) return;
        const childMap = new Map(childEntries.map((c) => [c.key, c]));
        for (const child of node.children) {
          const fresh = childMap.get(child.key);
          if (fresh) {
            child.entries = fresh.entries;
            child.stamp = fresh.stamp;
          }
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
        const childMap = new Map(childEntries.map((c) => [c.key, c]));
        for (const child of node.children) {
          const fresh = childMap.get(child.key);
          if (fresh) {
            child.entries = fresh.entries;
            child.stamp = fresh.stamp;
          }
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

  // ── Live reconciliation ─────────────────────────────────────────────────

  /**
   * Refetches the stored values and works out, node by node and verb by verb, what a live
   * change means for an editor holding unsaved work.
   *
   * The decision itself is delegated to the pure, unit-tested `reconcileNode` — this method's
   * job is only to fetch, translate the shapes it needs, and apply what comes back. Cells the
   * user has not touched take the server's value silently — there is nothing of theirs to lose,
   * and leaving them stale would show two vintages of data in one grid. Cells where both sides
   * moved are flagged, and nothing else happens to them: the pending map is never written over
   * here, only ever by an explicit choice afterwards.
   *
   * Cancellable and role-guarded like the other loaders in this file: the fetch is assigned to
   * `#loadAbortController` (so a role switch or a cleared selection cancels it, and it cancels
   * them), and the selected user group's alias is captured once up front and checked again after
   * the one await this method makes. Without that, switching user group mid-fetch would let a
   * response for the *new* group land on the *old* group's tree and conflict state.
   */
  async #reconcileWithServer(): Promise<void> {
    if (!this._selectedRole) return;
    const roleAlias = this._selectedRole.alias;

    this.#loadAbortController?.abort();
    const controller = new AbortController();
    this.#loadAbortController = controller;

    try {
      const fresh = await this.#fetchFreshTree(roleAlias, controller.signal);
      // `_saving` too: a tree read that finished after the save began may predate its write, and
      // would be reconciled against as if it were somebody else's change.
      if (controller.signal.aborted || this._selectedRole?.alias !== roleAlias || this._saving) return;

      // Starts from what is already flagged and only ever grows: a flag is removed by the user
      // acting on it, never by a later pass that happens to read the cell as unchanged.
      const conflicts = new Set(this._conflicts);

      for (const node of this.#flattenNodes(this._treeNodes)) {
        const theirsForNode = fresh.get(node.key);
        // Not fetched this pass — a collapsed node whose children are still only cached from an
        // earlier expand, the same gap `#reloadNodeEntries` already has. Nothing on screen is
        // showing it, so there is nothing to reconcile yet; it catches up if it is re-expanded.
        if (!theirsForNode) continue;

        const apiKey = node.key === 'virtual-root' ? VIRTUAL_ROOT_NODE_KEY : node.key;
        const pendingForNode = this._pendingChanges.get(node.key);

        const base = new Map<string, ReadonlyArray<CellEntry>>();
        const theirs = new Map<string, ReadonlyArray<CellEntry>>();
        for (const verbInfo of this._verbs) {
          base.set(verbInfo.verb, this.#cellOf(node.entries, verbInfo.verb));
          theirs.set(verbInfo.verb, this.#cellOf(theirsForNode.entries, verbInfo.verb));
        }
        const pending = pendingForNode
          ? new Map<string, ReadonlyArray<CellEntry>>(
              [...pendingForNode].map(([verb, entries]) => [
                verb,
                entries.map((p) => ({ state: p.state, scope: p.scope, isPriorityOverride: p.isPriorityOverride })),
              ]),
            )
          : undefined;

        const flaggedPrefix = `${node.key}|`;
        const alreadyFlagged = new Set<string>();
        for (const cell of this._conflicts) {
          if (cell.startsWith(flaggedPrefix)) alreadyFlagged.add(cell.slice(flaggedPrefix.length));
        }

        const result = reconcileNode({ base, pending, theirs, conflicted: alreadyFlagged });
        for (const verb of result.conflictedVerbs) {
          conflicts.add(`${node.key}|${verb}`);
        }

        // Rebuilt from the per-verb merge: a conflicted verb keeps the entries it had before
        // this event (what the next pass needs as `base`, so the same disagreement is not lost
        // the moment an unrelated node fires the next event); a clean verb takes the fresh ones.
        const mergedEntries = this.#entriesFromNextBase(apiKey, roleAlias, result.nextBase);

        // A node with a flagged verb keeps the stamp it has. Adopting the server's would let the
        // next save for that node sail through the concurrency check on a value nobody has agreed
        // to overwrite. Nothing remembers the stamp that was skipped: resolving the conflict
        // re-reads the server (see `#loadStoredForConflicts`), which supplies a current one.
        if (result.adoptStamp) {
          this.#updateNode(node.key, { entries: mergedEntries, stamp: theirsForNode.stamp });
        } else {
          this.#updateNode(node.key, { entries: mergedEntries });
        }
      }

      if (controller.signal.aborted || this._selectedRole?.alias !== roleAlias || this._saving) return;

      this.#adoptConflicts(conflicts);
    } catch (err) {
      if (controller.signal.aborted) return;
      this._error = String(err);
      // Rethrown so the refresh control does not claim the data was updated.
      throw err;
    }
  }

  /**
   * Adopts the flagged-cell set a reconcile pass just computed, without disturbing what is
   * already on screen. The set is a superset of the previous one by construction (flags are
   * sticky), so this only ever adds.
   *
   * A pass that finds exactly the cells already flagged leaves the state alone: swapping in an
   * equal set would re-render the grid and, worse, bring back a banner the person had dismissed
   * for those very cells. The banner returns only when a pass flags a cell that was not flagged
   * before, which is genuinely new news.
   * @param next The previous flags plus any this pass found, keyed `nodeKey|verb`.
   */
  #adoptConflicts(next: Set<string>): void {
    const previous = this._conflicts;
    const unchanged = next.size === previous.size && [...next].every((c) => previous.has(c));
    if (unchanged) return;
    if ([...next].some((c) => !previous.has(c))) this._bannerDismissed = false;
    this._conflicts = next;
  }

  /**
   * Fetches the current stored entries and stamp for every node this editor has loaded, batched
   * the same way `#reloadNodeEntries` already batches a role switch: one call for the virtual
   * root, one for the whole root level, and one per expanded parent — never one call per node.
   * @param roleAlias The user group to fetch for.
   * @param signal Aborts the fetch when the load it belongs to is superseded.
   * @returns Freshly-read entries and stamp, keyed by local node key (`'virtual-root'` for the
   * virtual root, the content key for everything else).
   */
  async #fetchFreshTree(
    roleAlias: string,
    signal: AbortSignal,
  ): Promise<Map<string, { entries: PermissionEntry[]; stamp: string }>> {
    const fresh = new Map<string, { entries: PermissionEntry[]; stamp: string }>();

    const [virtualResult, rootNodes] = await Promise.all([
      getPermissionsWithStamp(VIRTUAL_ROOT_NODE_KEY, roleAlias, signal),
      getTreeRoot(roleAlias, signal),
    ]);
    fresh.set('virtual-root', virtualResult);
    for (const n of rootNodes) {
      fresh.set(n.key, { entries: n.entries, stamp: n.stamp });
    }

    await this.#fetchFreshChildren(this._treeNodes, roleAlias, signal, fresh);
    return fresh;
  }

  /**
   * Recursive companion to `#fetchFreshTree`: descends into expanded parents only, fetching one
   * `getTreeChildren` call per level rather than one per node.
   * @param nodes The nodes to walk.
   * @param roleAlias The user group to fetch for.
   * @param signal Aborts the fetch when the load it belongs to is superseded.
   * @param out Accumulates the freshly-read entries and stamp, keyed by node key.
   */
  async #fetchFreshChildren(
    nodes: TreeNodeState[],
    roleAlias: string,
    signal: AbortSignal,
    out: Map<string, { entries: PermissionEntry[]; stamp: string }>,
  ): Promise<void> {
    for (const node of nodes) {
      if (node.children && node.expanded) {
        const childNodes = await getTreeChildren(node.key, roleAlias, signal);
        for (const c of childNodes) {
          out.set(c.key, { entries: c.entries, stamp: c.stamp });
        }
        await this.#fetchFreshChildren(node.children, roleAlias, signal, out);
      }
    }
  }

  /**
   * Flattens a per-verb `nextBase` merge (as `reconcileNode` returns it) back into the flat
   * entry list `TreeNodeState.entries` holds. The synthesised `id` is display-only, the same
   * convention the rest of this file uses when it fabricates entries client-side.
   * @param nodeKey The API node key (real key, or `VIRTUAL_ROOT_NODE_KEY`).
   * @param roleAlias The user group these entries belong to.
   * @param nextBase The per-verb entries to flatten.
   * @returns The flat entry list.
   */
  #entriesFromNextBase(
    nodeKey: string,
    roleAlias: string,
    nextBase: ReadonlyMap<string, ReadonlyArray<CellEntry>>,
  ): PermissionEntry[] {
    const result: PermissionEntry[] = [];
    for (const [verb, cellEntries] of nextBase) {
      for (const e of cellEntries) {
        result.push({
          id: `${nodeKey}-${verb}-${result.length}`,
          nodeKey,
          roleAlias,
          verb,
          state: e.state,
          scope: e.scope,
          isPriorityOverride: e.isPriorityOverride,
        });
      }
    }
    return result;
  }

  /**
   * Reduces a node's stored entries for one verb to the fields a comparison cares about.
   * @param entries The node's entries.
   * @param verb The verb to extract.
   * @returns The cell's entries.
   */
  #cellOf(entries: PermissionEntry[], verb: string): CellEntry[] {
    return entries
      .filter((e) => e.verb === verb)
      .map((e) => ({ state: e.state, scope: e.scope, isPriorityOverride: e.isPriorityOverride }));
  }

  /**
   * Walks the loaded tree depth-first.
   *
   * Recursive and private: the return type is annotated explicitly, otherwise TypeScript infers
   * `any` for a recursive private method (CLAUDE.md #4).
   * @param nodes The nodes to walk.
   * @returns Every loaded node, parents before children.
   */
  #flattenNodes(nodes: TreeNodeState[]): TreeNodeState[] {
    const out: TreeNodeState[] = [];
    for (const node of nodes) {
      out.push(node);
      if (node.children?.length) out.push(...this.#flattenNodes(node.children));
    }
    return out;
  }

  /**
   * Resolves every currently-flagged cell to its stored value, keeping every other pending
   * change untouched, then re-reads the server so the grid actually shows that stored value.
   *
   * Scoped to the flagged cells on purpose. Discarding everything would be simpler and would
   * throw away edits elsewhere in the tree that nobody is contesting — which is the outcome this
   * whole feature exists to avoid.
   *
   * THE RE-READ IS NOT OPTIONAL — do not remove it as a redundant round trip. A conflicted verb's
   * baseline is deliberately frozen (see `reconcileNode`): `node.entries` for that verb still holds
   * the value from *before* the other person's write, and the node's stamp was held back to match.
   * Dropping the pending change only removes the user's edit; it does not make the entries current.
   * Without the re-read the cell keeps showing the pre-conflict value, and — if anything were to
   * advance the stamp without the entries (which is what this method used to do, by applying a
   * stamp it had withheld) — `#saveChanges`, which builds its request from `node.entries` plus
   * pending changes, would send that stale value under a stamp the server accepts. The other
   * person's change would be silently reverted: no 409, no dialog, nothing to notice.
   *
   * The re-read fetches entries and stamp in the same response, so they cannot disagree. It goes
   * through the live-event controller, exactly as the manual refresh control does: that runs
   * `#reloadPermissions(true)` when nothing is pending any more, and `#reconcileWithServer` when
   * other edits survive. The split matters. A plain reload replaces every node's baseline, and
   * for a cell the user is still editing that would quietly absorb somebody else's write that had
   * landed but not yet been reconciled — the pending change would then overwrite it under a fresh
   * stamp. Reconciling compares first and only adopts what is not in dispute. It also queues
   * behind any run already in flight instead of aborting it.
   *
   * Until that read lands, the nodes still hold their old stamp, so a save made in the meantime is
   * refused by the server rather than written stale. No withheld stamp is applied here: it is
   * older than what the re-read fetches, and applying it early is exactly the drift described above.
   *
   * Used for both the banner's "Load stored values" and the save-time conflict dialog's, so the
   * two cannot end in different states.
   */
  #loadStoredForConflicts(): void {
    const next = new Map(this._pendingChanges);

    for (const cell of this._conflicts) {
      const [nodeKey, verb] = cell.split('|');
      const forNode = next.get(nodeKey!);
      if (!forNode) continue;
      forNode.delete(verb!);
      if (forNode.size === 0) next.delete(nodeKey!);
    }

    this._pendingChanges = next;
    this.#resetLiveState();
    void this.#liveEvents.refresh();
  }

  // ── Save ─────────────────────────────────────────────────────────────────

  /**
   * Writes every pending change in one all-or-nothing request, and stops to ask if the stored
   * values moved since this editor read them.
   *
   * One request rather than a loop of one save per node: a rejection partway through a loop would
   * leave some nodes written and some not, with nothing afterwards able to tell which.
   * @param force Whether to write through a stale stamp. Only true after the user has confirmed
   * via the conflict dialog.
   */
  async #saveChanges(force = false): Promise<void> {
    if (!this._pendingChanges.size || !this._selectedRole || this._saving) return;
    this._saving = true;

    try {
      const nodes: BatchSaveNode[] = [];

      for (const [nodeKey, verbChanges] of this._pendingChanges) {
        const node = this.#findNode(nodeKey);
        if (!node) continue;

        // Build the complete new entry list: start from stored, apply pending verb changes.
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

        // An empty stamp means the read that produced it found no ETag to report — "no stamp
        // available", not a real value. Sending it as `''` would compare as a stamp that never
        // matches; omitting it lets the server skip the check instead, same as if this node's
        // entries had never been read (see getPermissionsWithStamp's doc comment).
        nodes.push({
          nodeKey: nodeKey === 'virtual-root' ? VIRTUAL_ROOT_NODE_KEY : nodeKey,
          roleAlias: this._selectedRole.alias,
          entries: [...byVerb.values()].flat(),
          expectedStamp: node.stamp === '' ? undefined : node.stamp,
        });
      }

      const result = await savePermissionsBatch(nodes, force);

      if (!result.ok) {
        // Not an error path. A conflict is the answer this feature exists to produce, and what
        // happens next is the user's decision, not this method's.
        this.#openConflictDialog(result.conflicts);
        return;
      }

      // Adopt the stamps the server just handed back before reloading, so the nodes this save
      // touched are immediately correct even though the reload below re-derives the same thing
      // from the tree endpoints a moment later.
      for (const [key, stamp] of result.stamps) {
        const [savedNodeKey, savedRoleAlias] = key.split('|');
        if (savedRoleAlias !== this._selectedRole.alias) continue;
        const localKey = savedNodeKey === VIRTUAL_ROOT_NODE_KEY ? 'virtual-root' : savedNodeKey!;
        this.#updateNode(localKey, { stamp });
      }

      await this.#reloadPermissions();
      this._pendingChanges = new Map();
      this.#resetLiveState();
      clearEffectivePermissionCache();
      this.#notificationContext?.peek('positive', { data: { message: this.#localize.term('uap_permissionsSaved') } });
    } catch (err) {
      this.#notificationContext?.peek('danger', { data: { message: this.#localize.term('uap_saveFailed', String(err)) } });
    } finally {
      this._saving = false;
    }
  }

  /**
   * Turns the server's node-level conflict list into per-verb lines for the confirmation dialog,
   * flags the same cells on the grid, and opens the dialog.
   *
   * The batch endpoint reports a conflict per node+role pair — its unit of write — but the
   * dialog has to name what would be lost per verb, the granularity a person actually edited at.
   * Only verbs this editor has a pending change for become a line: `currentEntries` carries every
   * verb stored at that node, most of them untouched by this save. The affected nodes' entries
   * are also advanced to what the server just reported, but their stamp is left alone, exactly as
   * for a live-detected conflict — `#loadStoredForConflicts` resolves both the same way, by
   * re-reading the server rather than trusting either half of what is held here.
   * @param conflicts The conflicting node+role pairs the server refused to overwrite.
   */
  #openConflictDialog(conflicts: BatchSaveConflict[]): void {
    const lines: UapConflictLine[] = [];
    const flagged = new Set(this._conflicts);

    for (const conflict of conflicts) {
      const localKey = conflict.nodeKey === VIRTUAL_ROOT_NODE_KEY ? 'virtual-root' : conflict.nodeKey;
      const isVirtualRoot = localKey === 'virtual-root';
      const nodeName = isVirtualRoot
        ? this.#localize.term('uap_contentRoot')
        : (this.#findNode(localKey)?.name ?? localKey);

      this.#updateNode(localKey, { entries: conflict.currentEntries });

      const pending = this._pendingChanges.get(localKey);
      if (!pending) continue;

      for (const [verb, pendingEntries] of pending) {
        flagged.add(`${localKey}|${verb}`);
        const verbName = this._verbs.find((v) => v.verb === verb)?.displayName ?? verb;
        const storedForVerb = conflict.currentEntries.filter((e) => e.verb === verb);
        lines.push({
          nodeName,
          verbName,
          stored: this.#describeCell(storedForVerb, isVirtualRoot),
          mine: this.#describeCell(pendingEntries, isVirtualRoot),
        });
      }
    }

    this._conflicts = flagged;
    this._conflictDialog.lines = lines;
    this._conflictDialog.open();
  }

  /** Closes the save-time conflict dialog without changing anything. */
  #closeConflictDialog(): void {
    this._conflictDialog.close();
  }

  /**
   * Renders a set of stored/pending entries for one verb as the short human-readable phrase the
   * conflict dialog shows for "stored" and "yours" — the same phrasing `uap-permission-scope-dialog`
   * uses for its live preview, reused here so both surfaces describe a cell the same way.
   * @param entries The cell's entries.
   * @param isVirtualRoot Whether the cell belongs to the virtual root, which has no descendant side.
   * @returns The localized description.
   */
  #describeCell(
    entries: ReadonlyArray<{ state: PermissionState; scope: PermissionScope; isPriorityOverride: boolean }>,
    isVirtualRoot: boolean,
  ): string {
    const stateLabel = (s: 'allow' | 'deny'): string =>
      s === 'allow' ? this.#localize.term('uap_allow') : this.#localize.term('uap_deny');
    const d = decomposeEntries(entries);
    const nodeState = d.nodeState;
    const descState = d.descState;

    if (isVirtualRoot) {
      return nodeState === 'inherit'
        ? this.#localize.term('uap_previewVirtualInherit')
        : this.#localize.term('uap_previewVirtualSet', stateLabel(nodeState));
    }

    if (nodeState === 'inherit' && descState === 'inherit') {
      return this.#localize.term('uap_previewBothInherit');
    }
    // Neither side is 'inherit' from here on: the both-inherit case returned above, and
    // `sameAsNode` (nodeState === descState) together with that rules it out for this branch.
    if (d.sameAsNode) {
      return this.#localize.term('uap_previewUniform', stateLabel(nodeState as 'allow' | 'deny'));
    }
    if (nodeState !== 'inherit' && descState === 'inherit') {
      return this.#localize.term('uap_previewNodeOnly', stateLabel(nodeState));
    }
    if (nodeState === 'inherit' && descState !== 'inherit') {
      return this.#localize.term('uap_previewDescOnly', stateLabel(descState));
    }
    return this.#localize.term(
      'uap_previewSplit',
      stateLabel(nodeState as 'allow' | 'deny'),
      stateLabel(descState as 'allow' | 'deny'),
    );
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
      <td
        class="perm-td ${this._conflicts.has(`${node.key}|${verb}`) ? 'conflicted' : ''}"
        title=${verb}
        @click=${() => this.#openPicker(node, verb)}>
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
          ctaIcon="icon-lock"
          orLabel=${this.#localize.term('uap_subjectOr')}
          ?clearable=${true}
          clearLabel=${this.#localize.term('uap_clearSelection')}
          @uap-selector-click=${(e: CustomEvent<{ id: string }>) => this.#onSelectorClick(e.detail.id)}
          @uap-selection-clear=${() => void this.#onClearSelection()}>
          ${this._pendingChanges.size > 0
            ? html`<div slot="actions">
                <uui-button label=${this.#localize.term('uap_saveChanges')} look="primary" color="positive" ?loading=${this._saving} @click=${() => void this.#saveChanges()}>
                  ${this.#localize.term('uap_saveChanges')}
                </uui-button>
                <uui-button label=${this.#localize.term('uap_discard')} look="outline" @click=${() => this.#discardChanges()}>
                  ${this.#localize.term('uap_discard')}
                </uui-button>
              </div>`
            : nothing}
          <uap-live-refresh
            slot="actions"
            .phase=${this._refreshPhase}
            @uap-live-refresh=${() => void this.#liveEvents.refresh()}>
          </uap-live-refresh>
          ${this._error ? html`<p class="error-msg">⚠ ${this._error}</p>` : nothing}
          ${this._loading ? html`<div class="loading"><uui-loader></uui-loader></div>` : nothing}
          ${this._conflicts.size > 0 && !this._bannerDismissed
            ? html`<uap-live-banner
                .conflictCount=${this._conflicts.size}
                @uap-live-load-stored=${() => this.#loadStoredForConflicts()}
                @uap-live-keep-mine=${() => { this._bannerDismissed = true; }}>
              </uap-live-banner>`
            : nothing}
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

      <!-- Save-time conflict confirmation — same reasoning as the permission dialog above -->
      <uap-conflict-dialog
        @uap-conflict-cancel=${() => this.#closeConflictDialog()}
        @uap-conflict-load-stored=${() => { this.#closeConflictDialog(); this.#loadStoredForConflicts(); }}
        @uap-conflict-overwrite=${() => { this.#closeConflictDialog(); void this.#saveChanges(true); }}>
      </uap-conflict-dialog>
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

    .perm-td.conflicted uap-perm-block {
      outline: 2px solid var(--uui-color-danger);
      outline-offset: 2px;
    }
  `;
}

export default UapPermissionsEditorRootElement;

declare global {
  interface HTMLElementTagNameMap {
    'uap-permissions-editor-root': UapPermissionsEditorRootElement;
  }
}
