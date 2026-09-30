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
import { flattenReconciled } from '../live/flatten-reconciled.js';
import { pruneCollapsedChildren } from '../live/tree-cache.js';
import { applyFreshReads } from '../live/apply-fresh.js';
import { withVerbEdit, withoutVerbs } from '../live/pending-changes.js';
import { createMissedEvents } from '../live/missed-events.js';
import { planSaveConflicts, type SaveConflictNode } from '../live/save-conflicts.js';
import { expectedStampFor } from '../live/stamp.js';
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
 * How many times one click on Save may be refused and retried on its own before giving up. A retry
 * only happens when nothing the user edited was contested, so this is a backstop against a
 * colleague writing to the same node continuously, not something a person should ever meet.
 */
const MAX_SAVE_ATTEMPTS = 5;

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

  /**
   * Whether a reload failed, so the grid may not match the server: after a save whose follow-up
   * read failed, or a user-group switch whose read failed. While set, the grid is hidden and saving
   * is refused, because a payload built from entries of unknown vintage, under stamps that may
   * match, is how a node's other verbs get wiped. Cleared only by a read that lands (the refresh
   * control, a live event, or a fresh load), which adopts entries and stamps together.
   */
  @state() private _treeStale = false;

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

  /**
   * Records that a live event, or a read that could not be reconciled, was set aside because a save
   * was in flight. Replayed when the save ends: the coalescer that delivered the event has already settled,
   * so nothing else would ever retry it, and the grid would stay stale until some unrelated event
   * arrived.
   */
  #missedWhileSaving = createMissedEvents();

  /**
   * What the server held for each node the last refused save named, keyed by local node key: its
   * entries and the stamp that describes them. Kept so "Overwrite anyway" can retry under exactly
   * the stamps the user was shown a dialog about, rather than under none at all: a colleague's write
   * that lands after the dialog then produces a second dialog instead of being overwritten unseen.
   */
  #overwriteSnapshots = new Map<string, { entries: PermissionEntry[]; stamp: string }>();

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

    // Every event is answered by a reconcile, whether or not there are unsaved edits. With none,
    // `reconcileNode` classifies every cell as a refresh and adopts the server's value and stamp,
    // which is what a plain reload would have done — but it compares first. A reload applies its
    // responses across several awaits, so an edit begun while one was in flight, on a cell somebody
    // else also changed, would have that change absorbed as the new baseline with no flag, and the
    // next save would overwrite it under a fresh stamp. The keys are ignored deliberately — a
    // permission written on an ancestor moves what every descendant on screen resolves to, so
    // there is no subset worth refetching.
    this.#liveEvents = new UapSecurityEventsController(
      this,
      async () => {
        if (!this._selectedRole) return;
        // Nothing is reconciled while this editor's own save is in flight. The write raises an
        // event of its own, and the event carries no identity, so it cannot be told from a
        // colleague's; reconciling against it would flag the user's own save as a conflict. This
        // replaces what comparing values used to do (see `CellVerdict`). It is not simply
        // dropped: a finished save reloads everything, but a colleague's write can land after
        // that read and before `_saving` clears, so the event is remembered and replayed by
        // `#saveChanges` once the save is over.
        if (this._saving) {
          this.#missedWhileSaving.defer();
          return;
        }
        // Nothing here is a selection change, so the grid stays on screen and only the values
        // (and conflict flags) change underneath.
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
    this.#overwriteSnapshots = new Map();
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
   * to watch the page reload. It goes through the live-event controller, like every other
   * background refresh, so it reconciles rather than overwriting: with nothing pending and nothing
   * flagged, that adopts the server's entries and stamps exactly as a reload would, but it cannot
   * absorb somebody else's change to a cell the user starts editing again while it is in flight.
   */
  #discardChanges(): void {
    this._pendingChanges = new Map();
    this.#resetLiveState();
    void this.#liveEvents.refresh();
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
    // The load just aborted would have cleared this itself, but an aborted load never does.
    this._loading = false;
    this._selectedRole = null;
    this._treeNodes = [];
    this._pendingChanges = new Map();
    this.#resetLiveState();
    this._treeStale = false;
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
    // The tree is being replaced from scratch, so whatever made the old one untrustworthy is gone.
    this._treeStale = false;
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
   * Reloads the permission entries and stamps for the current tree structure without rebuilding the
   * tree. Preserves expanded state and children.
   *
   * Used for a user-group switch and for the refresh after a save. It shows the loader, because what
   * is on screen belongs to the previous group (or, after a save, is about to be replaced by what the
   * server now holds). Live events, the manual refresh control and discarding do not come through
   * here: they go through `#reconcileWithServer`, which keeps the grid on screen and compares before
   * it adopts anything.
   *
   * Everything is read first and applied in one step afterwards (`applyFreshReads`), so nothing is
   * blanked while the requests are in flight. This used to clear every node's entries, refetch, and
   * swallow a failure; a failed refetch then left the grid showing all-inherit next to stamps that
   * were still valid, and the next save on any node built its payload from the blanked entries and
   * wiped that node's other verbs. Now a failed read leaves the tree exactly as it was and marks it
   * stale (`_treeStale`), which hides the grid and refuses to save until a read lands.
   * @returns `true` when fresh entries and stamps were adopted; `false` when the read failed, or was
   * superseded by another load, in which case the tree is marked stale until one of them lands.
   */
  async #reloadPermissions(): Promise<boolean> {
    if (!this._selectedRole || this._treeNodes.length === 0) return true;
    const roleAlias = this._selectedRole.alias;

    // Cancel any in-flight load from a previous role selection
    this.#loadAbortController?.abort();
    const controller = new AbortController();
    this.#loadAbortController = controller;

    this._loading = true;
    this._error = null;

    // Collapsed nodes keep their cached children through a reload, and nothing below refetches
    // them, so re-expanding would show them blank against the previous group's stamps. Dropping
    // the cache makes the next expand fetch them, exactly as the first one did.
    this.#pruneStaleCaches();

    try {
      const fresh = await this.#fetchFreshTree(roleAlias, controller.signal);
      if (controller.signal.aborted || this._selectedRole?.alias !== roleAlias) return false;

      this._treeNodes = applyFreshReads(this._treeNodes, fresh);
      this._treeStale = false;
      return true;
    } catch (err) {
      if (controller.signal.aborted) return false;
      this._error = String(err);
      this._treeStale = true;
      return false;
    } finally {
      // Cleared by whichever load finishes un-superseded. A live refresh aborts a reload that was
      // still showing the loader, and that reload's own `finally` then skips clearing it, so
      // leaving the clearing to the load that set the flag would strand the grid behind the loader
      // for good. `#reconcileWithServer` does the same for the loads it aborts.
      if (!controller.signal.aborted) this._loading = false;
    }
  }

  /**
   * Drops the cached children of collapsed nodes, so expanding one again refetches them instead of
   * showing what they held when it was collapsed. The tree is only refreshed where it is on screen,
   * so anything cached below a collapsed node is stale from the moment of the next change.
   *
   * Nodes holding an unsaved edit or a flagged cell are kept, whatever they sit under: removing one
   * would leave its pending change pointing at a node the tree can no longer find, and the save
   * would skip it without saying so. The fetches below descend into every cached node, expanded or
   * not, so whatever survives here is refreshed along with what is visible.
   */
  #pruneStaleCaches(): void {
    const flagged = new Set([...this._conflicts].map((cell) => cell.split('|')[0]!));
    this._treeNodes = pruneCollapsedChildren(
      this._treeNodes,
      (key) => this._pendingChanges.has(key) || flagged.has(key),
    );
  }

  /**
   * Expands or collapses a node, fetching its children the first time.
   *
   * Cached children are reused on a re-expand, which is only correct because every reload and
   * reconcile drops the cache of collapsed nodes (see `#pruneStaleCaches`). A node that still has
   * children while collapsed is therefore either one collapsed since the last refresh, whose
   * children were refreshed with the visible tree while it was still open, or one holding an
   * unsaved edit, which every refresh keeps fresh. Either way what is reused is current.
   * @param node The node whose expander was clicked.
   */
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
   * Handles `uap-scope-apply` from the shared scope dialog: records the composed entries as the
   * pending change for the cell that opened the dialog, unless they equal what the cell already
   * holds.
   *
   * The dialog's Apply is unconditional, so a user can apply the value a cell already has, or change
   * it and change it back. Recording that as an edit leaves a pending entry equal to the baseline,
   * which a colleague's later change then makes dangerous: the refresh adopts their value and stamp,
   * and the stale entry goes out on the next save under a matching stamp, deleting their change with
   * no flag and no dialog. `withVerbEdit` drops it instead (see there for the flagged-cell
   * exception), and `reconcileNode` reports any that get through anyway.
   * @param e The dialog's apply event, carrying the composed entries.
   */
  #handleScopeApply(e: CustomEvent<{ entries: PendingVerbEntries }>): void {
    if (!this._pickerNode || !this._pickerVerb) return;
    const nodeKey = this._pickerNode.key;
    const verb = this._pickerVerb;
    this._pendingChanges = withVerbEdit(
      this._pendingChanges,
      nodeKey,
      verb,
      e.detail.entries,
      this.#cellOf(this._pickerNode.entries, verb),
      this._conflicts.has(`${nodeKey}|${verb}`),
    );
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
   *
   * Also the only path for a refresh that leaves the grid usable — live events, the manual control,
   * discarding and resolving conflicts all come through here, with or without unsaved edits. That
   * is deliberate. With nothing pending, every cell classifies as a refresh and the server's
   * entries and stamp are adopted, which is what a plain reload does; but this compares before it
   * adopts, and reads the pending edits after the fetch lands rather than before it is issued. A
   * reload that applied its responses across several awaits could instead absorb somebody else's
   * change to a cell the user began editing while it was in flight, as the new baseline with no
   * flag.
   *
   * It aborts whatever load was in flight, including a foreground one that was showing the loader,
   * so it clears the loader itself when it finishes un-aborted; see `#reloadPermissions`.
   * @returns A promise that resolves when the pass is done and rejects if the fetch failed, so
   * the refresh control does not claim the data was updated.
   */
  async #reconcileWithServer(): Promise<void> {
    // No tree yet means the first load is still in flight (`#loadTree` empties the tree before it
    // starts). There is nothing to reconcile, and going on would abort that load — it is the one
    // that builds the tree — leaving the grid empty for good.
    if (!this._selectedRole || this._treeNodes.length === 0) return;
    const roleAlias = this._selectedRole.alias;

    this.#loadAbortController?.abort();
    const controller = new AbortController();
    this.#loadAbortController = controller;

    // Collapsed nodes' cached children are not refetched, so anything left under one would keep
    // showing (and being edited against) values and stamps from before this event. They are
    // dropped so a re-expand asks the server; the ones holding an unsaved edit or a flag are kept
    // and refreshed below, because dropping those would orphan the edit.
    this.#pruneStaleCaches();

    try {
      const fresh = await this.#fetchFreshTree(roleAlias, controller.signal);
      // `_saving` too: a tree read that finished after the save began may predate its write, and
      // would be reconciled against as if it were somebody else's change.
      if (controller.signal.aborted || this._selectedRole?.alias !== roleAlias) return;
      // Not silently: the read was dropped, and nothing else will retry it. See `#missedWhileSaving`.
      if (this._saving) {
        this.#missedWhileSaving.defer();
        return;
      }

      // Starts from what is already flagged and only ever grows: a flag is removed by the user
      // acting on it, never by a later pass that happens to read the cell as unchanged.
      const conflicts = new Set(this._conflicts);
      let pendingAfter = this._pendingChanges;
      let droppedStale = false;

      for (const node of this.#flattenNodes(this._treeNodes)) {
        const theirsForNode = fresh.get(node.key);
        // Not in this pass's read: the node entered the tree while the read was in flight (a user
        // expanded its parent, and that expand fetched it fresh), or the server no longer returns
        // it. Either way there is nothing to compare against, so it is left as it is. Collapsed
        // nodes are not a case here: their caches were dropped or fetched above.
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
        // The server moved and the user's entry is just the value that was loaded, so they hold
        // nothing here. The server's value has been adopted below; an entry left behind would go
        // out on the next save under the fresh stamp and silently put the old value back.
        if (result.stalePendingVerbs.length > 0) {
          pendingAfter = withoutVerbs(pendingAfter, node.key, result.stalePendingVerbs);
          droppedStale = true;
        }

        // Rebuilt from the per-verb merge: a conflicted verb keeps the entries it had before
        // this event (what the next pass needs as `base`, so the same disagreement is not lost
        // the moment an unrelated node fires the next event); a clean verb takes the fresh ones.
        const mergedEntries = flattenReconciled(apiKey, roleAlias, result.nextBase, theirsForNode.entries);

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

      if (controller.signal.aborted || this._selectedRole?.alias !== roleAlias) return;
      if (this._saving) {
        this.#missedWhileSaving.defer();
        return;
      }

      if (droppedStale) this._pendingChanges = pendingAfter;
      this.#adoptConflicts(conflicts);
      // A pass that ran to the end adopted entries and stamps together for every node it covers,
      // which is what a failed reload was waiting for.
      if (this._treeStale) {
        this._treeStale = false;
        this._error = null;
      }
    } catch (err) {
      if (controller.signal.aborted) return;
      this._error = String(err);
      // Rethrown so the refresh control does not claim the data was updated.
      throw err;
    } finally {
      // This pass aborted whatever load was in flight, and an aborted load never clears the
      // loader it raised. If this one was not itself superseded, nothing else is going to.
      if (!controller.signal.aborted) this._loading = false;
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
   * the same way a role switch batches its reads: one call for the virtual
   * root, one for the whole root level, and one per parent with cached children — never one call per node.
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
   * Recursive companion to `#fetchFreshTree`: descends into every node with cached children, fetching one
   * `getTreeChildren` call per parent rather than one per node. After `#pruneStaleCaches` the cached
   * children left are those of expanded nodes and of nodes holding an unsaved edit or a flag, so
   * "cached" and "worth refreshing" coincide.
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
      if (node.children) {
        const childNodes = await getTreeChildren(node.key, roleAlias, signal);
        for (const c of childNodes) {
          out.set(c.key, { entries: c.entries, stamp: c.stamp });
        }
        await this.#fetchFreshChildren(node.children, roleAlias, signal, out);
      }
    }
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
   * through the live-event controller, exactly as the manual refresh control does, which always
   * runs `#reconcileWithServer`, whether or not other edits survive. A plain reload replaces every
   * node's baseline, and for a cell the user is still editing that would quietly absorb somebody
   * else's write that had landed but not yet been reconciled — the pending change would then
   * overwrite it under a fresh stamp. Reconciling compares first and only adopts what is not in
   * dispute (and with nothing pending, adopts everything, as a reload would). It also queues
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
    let next = this._pendingChanges;

    for (const cell of this._conflicts) {
      const [nodeKey, verb] = cell.split('|');
      next = withoutVerbs(next, nodeKey!, [verb!]);
    }

    this._pendingChanges = next;
    this.#resetLiveState();
    void this.#liveEvents.refresh();
  }

  // ── Save ─────────────────────────────────────────────────────────────────

  /**
   * Writes every pending change in one all-or-nothing request, and stops to ask only about cells
   * somebody else actually changed.
   *
   * One request rather than a loop of one save per node: a rejection partway through a loop would
   * leave some nodes written and some not, with nothing afterwards able to tell which.
   *
   * A refusal is not the end. The server refuses a node as a whole, so it refuses a save that only
   * touches verb A when a colleague changed verb B on the same node. `#absorbSaveConflicts` works
   * out which cells are genuinely contested; when none are, the node adopts what is stored and the
   * save is retried here, under the fresh stamp, without bothering anybody. The retry is never
   * `force`: it always carries the stamps the server just reported, so a second collision is refused
   * and asked about like the first.
   *
   * Refused outright while the tree is stale (`_treeStale`): the entries and stamps a payload would
   * be built from are of unknown vintage.
   */
  async #saveChanges(): Promise<void> {
    if (!this._pendingChanges.size || !this._selectedRole || this._saving || this._treeStale) return;
    this._saving = true;
    const roleAlias = this._selectedRole.alias;

    try {
      for (let attempt = 0; attempt < MAX_SAVE_ATTEMPTS; attempt++) {
        const nodes = this.#buildSaveBatch(roleAlias);
        // Every pending edit turned out to be stale bookkeeping and was dropped.
        if (nodes.length === 0) return;

        const result = await savePermissionsBatch(nodes);
        // The user group changed while the request was out; what came back is about another tree.
        if (this._selectedRole?.alias !== roleAlias) return;

        if (result.ok) {
          await this.#finishSave();
          return;
        }

        // Not an error path. A conflict is the answer this feature exists to produce, and what
        // happens next is the user's decision, not this method's, unless nothing is contested.
        const outcome = this.#absorbSaveConflicts(result.conflicts, roleAlias);
        if (outcome === 'dialog') return;
        if (outcome === 'stalled') break;
      }

      this.#notificationContext?.peek('danger', {
        data: { message: this.#localize.term('uap_saveFailed', 'the stored permissions keep changing') },
      });
    } catch (err) {
      this.#notificationContext?.peek('danger', { data: { message: this.#localize.term('uap_saveFailed', String(err)) } });
    } finally {
      this._saving = false;
      // Whatever arrived while the save was in flight was set aside rather than answered, and the
      // coalescer that delivered it has long since settled. A failed reload has the same need: the
      // read it was waiting for is still owed.
      // Always taken, so the record is cleared even when a stale tree is what asks for the replay.
      const missed = this.#missedWhileSaving.take();
      if (missed || this._treeStale) void this.#liveEvents.refresh();
    }
  }

  /**
   * Builds the batch a save sends: for each node with pending edits, its complete new entry list.
   *
   * Starts from what is stored and applies the pending verbs on top, because the server replaces a
   * node's whole entry set and a payload of only the edited verbs would delete the rest.
   * @param roleAlias The user group being saved.
   * @returns One item per node the tree can still find.
   */
  #buildSaveBatch(roleAlias: string): BatchSaveNode[] {
    const nodes: BatchSaveNode[] = [];

    for (const [nodeKey, verbChanges] of this._pendingChanges) {
      const node = this.#findNode(nodeKey);
      if (!node) continue;

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

      nodes.push({
        nodeKey: nodeKey === 'virtual-root' ? VIRTUAL_ROOT_NODE_KEY : nodeKey,
        roleAlias,
        entries: [...byVerb.values()].flat(),
        // Empty only for a stamp the read could not supply because the response carried no ETag, so
        // there is genuinely nothing to compare against. Every node here has a real stamp: the tree
        // endpoints hash an empty set on the server, and the virtual root's comes from its ETag.
        expectedStamp: expectedStampFor(node.stamp),
      });
    }

    return nodes;
  }

  /**
   * Completes a save the server accepted: clears the edits and flags, re-reads everything, and
   * tells the user.
   *
   * Entries and stamps are adopted only from the re-read (`#reloadPermissions`), never from the
   * stamps the save returned. Adopting those first left a window where the grid held the old entries
   * under new stamps, and a failed re-read left it there. The edits are cleared before the re-read
   * because they are written; the grid is behind the loader for its duration, and if the re-read
   * fails the tree is marked stale and hidden rather than shown out of date.
   */
  async #finishSave(): Promise<void> {
    this._pendingChanges = new Map();
    this.#resetLiveState();
    clearEffectivePermissionCache();
    // Not landing (failed or superseded) means the grid is not known to match the server.
    if (!(await this.#reloadPermissions())) this._treeStale = true;
    this.#notificationContext?.peek('positive', { data: { message: this.#localize.term('uap_permissionsSaved') } });
  }

  /**
   * Reduces a node's entries to one cell per verb the editor compares, keyed by verb.
   * @param entries The node's entries.
   * @param extraVerbs Verbs to include beyond the grid's columns, so a pending verb is never skipped.
   * @returns A cell for every verb, empty where nothing is stored.
   */
  #cellsByVerb(entries: PermissionEntry[], extraVerbs: Iterable<string> = []): Map<string, CellEntry[]> {
    const cells = new Map<string, CellEntry[]>();
    for (const v of this._verbs) cells.set(v.verb, this.#cellOf(entries, v.verb));
    for (const verb of extraVerbs) if (!cells.has(verb)) cells.set(verb, this.#cellOf(entries, verb));
    return cells;
  }

  /**
   * Decides what a refused save means and acts on it. See `planSaveConflicts` for the rules.
   *
   * Every refused node is compared verb by verb with the same `reconcileNode` a live event uses:
   * the editor's loaded values are the baseline, the refusal's `currentEntries` are the server's.
   * Only verbs both sides changed are flagged. Flagging every pending verb on a refused node, as
   * this used to, made the dialog claim a conflict on verb A when a colleague had changed verb B,
   * and "Load stored values" then discarded an edit nobody contested.
   *
   * Applied for every node, contested or not: entries advance to the stored values for every verb
   * that is not flagged, and the stamp advances for a node with nothing flagged. A flagged verb keeps
   * its old baseline and the node keeps its stamp, so a plain save of it is refused again. The other
   * verbs have to advance because a save rebuilds the whole node from `node.entries` plus the pending
   * verbs; a flagged verb's own baseline does not matter to that rebuild, because its pending value
   * replaces it. Pending entries that are just the old loaded value are dropped.
   *
   * The snapshot kept for each contested node is what "Overwrite anyway" retries under; see
   * `#overwriteConflicts`.
   * @param conflicts The refused node and user group pairs.
   * @param roleAlias The user group that was being saved.
   * @returns `retry` when nothing is contested and the save should go again, `dialog` when the
   * confirmation dialog was opened, `stalled` when the refusal carried nothing new to retry with.
   */
  #absorbSaveConflicts(conflicts: BatchSaveConflict[], roleAlias: string): 'retry' | 'dialog' | 'stalled' {
    const inputs: SaveConflictNode[] = [];
    const currentByKey = new Map<string, BatchSaveConflict>();

    for (const conflict of conflicts) {
      const localKey = conflict.nodeKey === VIRTUAL_ROOT_NODE_KEY ? 'virtual-root' : conflict.nodeKey;
      const node = this.#findNode(localKey);
      if (!node) continue;

      const pendingForNode = this._pendingChanges.get(localKey);
      const prefix = `${localKey}|`;
      const flagged = new Set<string>();
      for (const cell of this._conflicts) if (cell.startsWith(prefix)) flagged.add(cell.slice(prefix.length));

      currentByKey.set(localKey, conflict);
      inputs.push({
        key: localKey,
        base: this.#cellsByVerb(node.entries, pendingForNode?.keys()),
        pending: pendingForNode
          ? new Map([...pendingForNode].map(([verb, entries]) => [
              verb,
              entries.map((p) => ({ state: p.state, scope: p.scope, isPriorityOverride: p.isPriorityOverride })),
            ]))
          : undefined,
        theirs: this.#cellsByVerb(conflict.currentEntries, pendingForNode?.keys()),
        flagged,
        heldStamp: node.stamp,
        currentStamp: conflict.currentStamp,
      });
    }

    const plan = planSaveConflicts(inputs);
    const flags = new Set(this._conflicts);
    let pendingAfter = this._pendingChanges;
    const snapshots = new Map<string, { entries: PermissionEntry[]; stamp: string }>();
    const lines: UapConflictLine[] = [];

    for (const planned of plan.nodes) {
      const conflict = currentByKey.get(planned.key)!;
      const apiKey = planned.key === 'virtual-root' ? VIRTUAL_ROOT_NODE_KEY : planned.key;
      const merged = flattenReconciled(apiKey, roleAlias, planned.nextBase, conflict.currentEntries);
      this.#updateNode(planned.key, {
        entries: merged,
        ...(planned.adoptedStamp !== undefined ? { stamp: planned.adoptedStamp } : {}),
      });

      if (planned.stalePendingVerbs.length > 0) {
        pendingAfter = withoutVerbs(pendingAfter, planned.key, planned.stalePendingVerbs);
      }

      if (planned.conflictedVerbs.length === 0) continue;
      snapshots.set(planned.key, { entries: conflict.currentEntries, stamp: planned.currentStamp });

      const isVirtualRoot = planned.key === 'virtual-root';
      const nodeName = isVirtualRoot
        ? this.#localize.term('uap_contentRoot')
        : (this.#findNode(planned.key)?.name ?? planned.key);
      for (const verb of planned.conflictedVerbs) {
        flags.add(`${planned.key}|${verb}`);
        const mine = pendingAfter.get(planned.key)?.get(verb);
        if (!mine) continue;
        lines.push({
          nodeName,
          verbName: this._verbs.find((v) => v.verb === verb)?.displayName ?? verb,
          stored: this.#describeCell(conflict.currentEntries.filter((e) => e.verb === verb), isVirtualRoot),
          mine: this.#describeCell(mine, isVirtualRoot),
        });
      }
    }

    this._pendingChanges = pendingAfter;
    if (plan.retry) return 'retry';
    if (plan.stalled) return 'stalled';

    this._conflicts = flags;
    this.#overwriteSnapshots = snapshots;
    this._conflictDialog.lines = lines;
    this._conflictDialog.open();
    return 'dialog';
  }

  /**
   * Confirms "Overwrite anyway" from the save-time dialog: accepts what the server reported in that
   * refusal as the baseline, resolves the flags it named, and saves again, without `force`.
   *
   * The retry goes out under the stamps the user was just shown, not under none. The old code
   * skipped the check for the whole batch, which overwrote nodes nobody was contesting, and any
   * change a colleague made after the dialog appeared. Now that colleague's write makes the server
   * refuse again, `#absorbSaveConflicts` finds the new disagreement, and the user is asked a second
   * time. A colleague's change to some other verb of the node is not a new disagreement and does not
   * ask anything.
   */
  #overwriteConflicts(): void {
    const snapshots = this.#overwriteSnapshots;
    for (const [key, snapshot] of snapshots) {
      this.#updateNode(key, { entries: snapshot.entries, stamp: snapshot.stamp });
    }
    this._conflicts = new Set([...this._conflicts].filter((cell) => !snapshots.has(cell.split('|')[0]!)));
    this.#overwriteSnapshots = new Map();
    void this.#saveChanges();
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
                <uui-button label=${this.#localize.term('uap_saveChanges')} look="primary" color="positive" ?loading=${this._saving} ?disabled=${this._treeStale} @click=${() => void this.#saveChanges()}>
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
          ${!this._loading && !this._treeStale && this._treeNodes.length > 0
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
        @uap-conflict-overwrite=${() => { this.#closeConflictDialog(); this.#overwriteConflicts(); }}>
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
