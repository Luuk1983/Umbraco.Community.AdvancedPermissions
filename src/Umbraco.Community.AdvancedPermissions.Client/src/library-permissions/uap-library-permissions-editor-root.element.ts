import { html, css, nothing, customElement, state, query } from '@umbraco-cms/backoffice/external/lit';
import type { TemplateResult } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import { UMB_MODAL_MANAGER_CONTEXT } from '@umbraco-cms/backoffice/modal';
import type {
  RoleInfo,
  PermissionEntry,
  PermissionState,
  PermissionScope,
  BatchSaveNode,
  BatchSaveConflict,
} from '../models/permission.models.js';
import { VIRTUAL_ROOT_NODE_KEY } from '../models/permission.models.js';
import type { ElementTreeNodeState } from '../models/element-permission.models.js';
import {
  getElementTreeRoot,
  getElementTreeChildren,
  getElementPermissionsWithStamp,
  saveElementPermissionsBatch,
} from '../api/element-permissions.api.js';
import { clearElementEffectivePermissionCache } from '../conditions/element-permission-condition.base.js';
import { UAP_ROLE_PICKER_MODAL } from '../access-viewer/role-picker-modal.token.js';
import { decomposeEntries } from '../utils/decompose-entries.js';
import { type PendingVerbEntries } from '../utils/compose-entries.js';
import type { CellInfo } from '../utils/cell-info.js';
import { getCellInfo } from '../utils/cell-info.js';
import { updateNode, findNode } from '../utils/tree-ops.js';
import { loadSelection, saveSelection, clearSelection } from '../utils/selection-store.js';
import { UapSecurityEventsController, UAP_EVENT_SOURCES } from '../live/security-events.controller.js';
import { UapUnsavedChangesGuard, confirmDiscardUnsavedChanges } from '../live/unsaved-changes-guard.js';
import type { UapRefreshPhase } from '../live/refresh-phase.js';
import '../live/uap-live-refresh.element.js';
import type { CellEntry } from '../live/conflict.js';
import { reconcileNode } from '../live/reconcile.js';
import { pruneCollapsedChildren } from '../live/tree-cache.js';
import { sameCell, staleRefreshVerbs } from './pending-cells.js';
import { LIBRARY_VERB_METAS, libraryApplicability } from './library-permission.descriptor.js';
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

/** A node's freshly-read stored entries together with the stamp they were read under. */
type FreshNode = { entries: PermissionEntry[]; stamp: string };

/** The local key the virtual root carries in the tree; the API knows it as {@link VIRTUAL_ROOT_NODE_KEY}. */
const VIRTUAL_ROOT_KEY = 'virtual-root';

/** Stable identifier used to key this surface's remembered selection (issue #46). */
const SURFACE_ID = 'library-permissions-editor';

/**
 * Library permissions editor. Mirrors the content Permissions Editor but operates on the element tree
 * (folders + elements) using the canonical element verbs, with per-node-kind applicability supplied by
 * {@link libraryApplicability}: element-only verbs are descendants-only on folders, and leaf elements
 * have no descendants side.
 *
 * This is the element permission family: entries are keyed by node plus user group, read and written
 * through the element API, and its concurrency stamps are the element endpoints' stamps. It has no
 * document type in its key; the element-type editor next to it does, and belongs to a different
 * family.
 */
@customElement('uap-library-permissions-editor-root')
export class UapLibraryPermissionsEditorRootElement extends UmbLitElement {
  #localize = new UmbLocalizationController(this);

  // ── Selection & tree ────────────────────────────────────────────────────
  /** The user group being edited; `null` until one is picked or restored. */
  @state() private _selectedRole: RoleInfo | null = null;

  /** The loaded tree. Index 0 is always the synthesised virtual root once a group is loaded. */
  @state() private _treeNodes: ElementTreeNodeState[] = [];

  /** True while a first load or a user-group switch is running, which hides the grid behind the loader. */
  @state() private _loading = false;

  /**
   * True while a save is in flight. Doubles as the guard that stops the editor reacting to the
   * live-event echo of its own write, so it must stay set until the post-save reload has finished.
   */
  @state() private _saving = false;

  /** The last load or refresh failure, shown above the grid. */
  @state() private _error: string | null = null;

  /**
   * Pending changes: nodeKey → (verb → PendingVerbEntries).
   * Empty PendingVerbEntries means "inherit" (delete all entries for that verb on save).
   */
  @state() private _pendingChanges: Map<string, PendingNodeChanges> = new Map();

  // ── Live updates & conflicts ────────────────────────────────────────────
  /**
   * Cells the server changed under an unsaved edit, keyed `nodeKey|verb`.
   *
   * Sticky by design: reconciliation only ever adds to it. A flag leaves on exactly four
   * occasions — the user loads the stored value, a save completes, the edits are discarded, or the
   * selection changes — because a flag that a later pass could clear would let a save through
   * silently over somebody else's write.
   */
  @state() private _conflicts: Set<string> = new Set();

  /** Whether the banner has been dismissed for the current set of conflicts. */
  @state() private _bannerDismissed = false;

  /** Where the refresh control is: idle, refreshing, or briefly confirming an update. */
  @state() private _refreshPhase: UapRefreshPhase = 'idle';

  // ── Permission dialog state ─────────────────────────────────────────────
  /** The node whose cell opened the scope dialog. */
  @state() private _pickerNode: ElementTreeNodeState | null = null;

  /** The verb whose cell opened the scope dialog. */
  @state() private _pickerVerb: string | null = null;

  /** Whether the dialog is editing the virtual root, which has no descendant side. */
  @state() private _pickerIsVirtualRoot = false;

  /** Whether the dialog offers the "this node" side for the clicked cell's node kind. */
  @state() private _pickerNodeApplicable = true;

  /** Whether the dialog offers the "descendants" side for the clicked cell's node kind. */
  @state() private _pickerDescApplicable = true;

  /** The dialog's initial state for the node itself. */
  @state() private _pickerNodeState: 'inherit' | 'allow' | 'deny' = 'inherit';

  /** The dialog's initial state for the node's descendants. */
  @state() private _pickerDescState: 'inherit' | 'allow' | 'deny' = 'inherit';

  /** Whether the dialog starts with one state applied to both node and descendants. */
  @state() private _pickerSameAsNode = true;

  /** Whether the dialog's "this node" side starts as a priority override. */
  @state() private _pickerNodeIsPriorityOverride = false;

  /** Whether the dialog's "descendants" side starts as a priority override. */
  @state() private _pickerDescIsPriorityOverride = false;

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
    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (ctx) => { this.#notificationContext = ctx ?? undefined; });
    this.consumeContext(UMB_MODAL_MANAGER_CONTEXT, (ctx) => { this.#modalManager = ctx ?? undefined; });

    // Every event, live or manual, is answered by one reconcile pass. A clean editor is just the
    // degenerate case (nothing pending, so every cell takes the server's value); a dirty one is
    // classified cell by cell and nothing of the user's is touched. Going through the same path
    // for both closes a window a plain reload would leave: an edit started while a reload is in
    // flight would otherwise have its baseline replaced underneath it by whatever the reload
    // brought. The keys are ignored deliberately — a permission written on an ancestor moves what
    // every descendant on screen resolves to, so there is no subset worth refetching.
    // Sources: Listens for library element and folder permission writes, and for `access` because deleting a
    // user group, element or folder removes stored element entries directly, with no permission
    // notification of its own. Content-node and document-type writes cannot appear here.
    this.#liveEvents = new UapSecurityEventsController(
      this,
      async () => {
        if (!this._selectedRole) return;
        // Nothing is reconciled while this editor's own save is in flight. The write raises an
        // event of its own, and the event carries no identity, so it cannot be told from a
        // colleague's; reconciling against it would flag the user's own save as a conflict. It is
        // safe to drop because a finished save reloads everything and clears the pending and
        // conflict state, and a refused one hands back the server's current values itself.
        if (this._saving) return;
        await this.#refreshFromServer();
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
    // Attached here and detached in disconnectedCallback: this element mounts and unmounts
    // repeatedly, so a listener left behind would guard navigation on behalf of a dead editor.
    this.#unsavedGuard.attach();
    this.#restoreSelection();
  }

  /** Cancels any in-flight load, and detaches any navigation guard, so a dismounted surface does nothing on behalf of a page that is gone. */
  override disconnectedCallback(): void {
    super.disconnectedCallback();
    this.#unsavedGuard.detach();
    this.#loadAbortController?.abort();
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
   * Clears every trace of an unresolved live conflict: the flagged cells and the dismissed-banner
   * flag.
   *
   * Called wherever the tree itself is being replaced or abandoned — switching user group or
   * clearing the selection — and after a completed save or a resolved conflict, so a conflict
   * against data that has since been replaced cannot outlive it.
   */
  #resetLiveState(): void {
    this._conflicts = new Set();
    this._bannerDismissed = false;
  }

  /**
   * Throws away every pending change and returns the grid to whatever the server currently holds.
   *
   * Discarding resolves any outstanding conflict by definition: with nothing of the user's left,
   * there is nothing left to contest, so the flags and the banner have to go with the edits.
   *
   * The re-read is not cosmetic. A conflicted node's entries are frozen at what they were before
   * the other person's write and its stamp was never advanced, so leaving it alone would keep both
   * out of date and the next save would be refused against a value nobody is editing any more — a
   * conflict dialog with no conflict behind it. Re-reading adopts the server's current stamps along
   * with its entries. It runs in the background so the grid stays on screen: the user asked to drop
   * their edits, not to watch the page reload.
   *
   * Does nothing while a save is in flight. The save's own reload would be aborted by this one and
   * leave the grid blank, and the save is about to clear the pending state itself.
   */
  #discardChanges(): void {
    if (this._saving) return;
    this._pendingChanges = new Map();
    this.#resetLiveState();
    // Failure is already on screen through `_error`; catching here only stops an unhandled rejection.
    void this.#refreshFromServer().catch(() => undefined);
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
    // A load aborted here never reaches its own `finally`, so the loader is cleared here.
    this._loading = false;
    clearSelection(SURFACE_ID);
  }

  // ── Role selection + tree loading ───────────────────────────────────────

  /**
   * Lets the user pick a user group, then switches to it.
   *
   * Asks about unsaved edits only after a group has actually been chosen, so cancelling the picker
   * costs nothing, and picking the group that is already selected changes nothing at all.
   */
  async #openRolePicker(): Promise<void> {
    if (!this.#modalManager) return;
    const modal = this.#modalManager.open(this, UAP_ROLE_PICKER_MODAL, {
      data: { ...(this._selectedRole ? { currentRole: this._selectedRole.alias } : {}) },
    });
    const result = await modal.onSubmit().catch(() => undefined);
    if (!result) return;

    // Picking the group that is already selected changes nothing, so it must not cost the user
    // their edits.
    if (this._pendingChanges.size > 0 && result.role.alias === this._selectedRole?.alias) return;
    // Asked after the pick, not before opening the picker: cancelling the picker then costs nothing.
    if (!(await this.#confirmDiscard())) return;

    const hadTree = this._treeNodes.length > 0 && this._selectedRole !== null;
    this._selectedRole = result.role;
    this._pendingChanges = new Map();
    this.#resetLiveState();
    saveSelection(SURFACE_ID, { subjectKind: 'role', role: result.role });
    if (hadTree) void this.#reloadAll();
    else void this.#loadTree();
  }

  /**
   * Builds the tree from scratch for the selected user group: the virtual root plus the library
   * root level. Used for the first load and whenever there is no tree to reload into.
   */
  async #loadTree(): Promise<void> {
    if (!this._selectedRole) return;
    this.#loadAbortController?.abort();
    const controller = new AbortController();
    this.#loadAbortController = controller;

    this._loading = true;
    this._error = null;
    this._treeNodes = [];
    try {
      // The virtual root is synthesised here rather than returned by the tree endpoint, so it
      // needs its stamp fetched explicitly — getElementPermissionsWithStamp is the only place
      // that stamp can come from. A placeholder would make every save touching it a false
      // conflict (it holds real entries, so an empty stamp never matches); omitting it would
      // leave it the one node with no concurrency protection at all.
      const [virtualResult, nodes] = await Promise.all([
        getElementPermissionsWithStamp(VIRTUAL_ROOT_NODE_KEY, this._selectedRole.alias, controller.signal),
        getElementTreeRoot(this._selectedRole.alias, controller.signal),
      ]);
      if (controller.signal.aborted) return;

      const virtualRoot: ElementTreeNodeState = {
        key: VIRTUAL_ROOT_KEY,
        name: this.#localize.term('uap_library_root'),
        icon: 'icon-globe',
        hasChildren: false,
        isFolder: false,
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
   * Expands or collapses a node, fetching its children the first time.
   * @param node The node whose toggle was clicked.
   */
  async #toggleExpand(node: ElementTreeNodeState): Promise<void> {
    if (node.expanded) { this.#updateNode(node.key, { expanded: false }); return; }
    if (node.children) { this.#updateNode(node.key, { expanded: true }); return; }
    this.#updateNode(node.key, { loading: true });
    try {
      const children = await getElementTreeChildren(node.key, this._selectedRole!.alias);
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
   * @param key The local key of the node to change.
   * @param changes The fields to merge into it.
   */
  #updateNode(key: string, changes: Partial<ElementTreeNodeState>): void {
    this._treeNodes = updateNode(this._treeNodes, key, changes);
  }

  // ── Re-reading the server ───────────────────────────────────────────────

  /**
   * Fetches the current stored entries and stamp for every node this editor has loaded, batched
   * the same way the tree endpoints are: one call for the virtual root, one for the whole root
   * level, and one per loaded parent — never one call per node.
   *
   * Descends into every parent whose children are still loaded. Callers prune the caches of
   * collapsed parents first (see `#pruneStaleTreeCache`), so what remains is the expanded parents
   * and the collapsed ones holding an unsaved edit or a flagged cell, and all of those must be
   * refreshed: an edit against a stale stamp would be refused over a change nobody made.
   * @param roleAlias The user group to fetch for.
   * @param signal Aborts the fetch when the load it belongs to is superseded.
   * @returns Freshly-read entries and stamp, keyed by local node key (`'virtual-root'` for the
   * virtual root, the element or folder key for everything else).
   */
  async #fetchFreshTree(roleAlias: string, signal: AbortSignal): Promise<Map<string, FreshNode>> {
    const fresh = new Map<string, FreshNode>();

    const [virtualResult, rootNodes] = await Promise.all([
      getElementPermissionsWithStamp(VIRTUAL_ROOT_NODE_KEY, roleAlias, signal),
      getElementTreeRoot(roleAlias, signal),
    ]);
    fresh.set(VIRTUAL_ROOT_KEY, virtualResult);
    for (const n of rootNodes) {
      fresh.set(n.key, { entries: n.entries, stamp: n.stamp });
    }

    await this.#fetchFreshChildren(this._treeNodes, roleAlias, signal, fresh);
    return fresh;
  }

  /**
   * Recursive companion to `#fetchFreshTree`: descends into every parent whose children are
   * loaded, one `getElementTreeChildren` call per parent.
   * @param nodes The nodes to walk.
   * @param roleAlias The user group to fetch for.
   * @param signal Aborts the fetch when the load it belongs to is superseded.
   * @param out Accumulates the freshly-read entries and stamp, keyed by node key.
   */
  async #fetchFreshChildren(
    nodes: ElementTreeNodeState[],
    roleAlias: string,
    signal: AbortSignal,
    out: Map<string, FreshNode>,
  ): Promise<void> {
    for (const node of nodes) {
      if (!node.children) continue;
      const childNodes = await getElementTreeChildren(node.key, roleAlias, signal);
      for (const c of childNodes) {
        out.set(c.key, { entries: c.entries, stamp: c.stamp });
      }
      await this.#fetchFreshChildren(node.children, roleAlias, signal, out);
    }
  }

  /**
   * Whether a node is holding something the user has not finished with: an unsaved edit on it or
   * on a cell the server has changed under one. Such a node must not be dropped from the tree.
   * @param key The local node key.
   * @returns `true` when the node has an edit or a flagged cell.
   */
  #holdsWork(key: string): boolean {
    if (this._pendingChanges.has(key)) return true;
    const prefix = `${key}|`;
    for (const cell of this._conflicts) {
      if (cell.startsWith(prefix)) return true;
    }
    return false;
  }

  /**
   * Drops the cached children of every collapsed node, so expanding it again fetches them fresh.
   *
   * Without this a collapsed parent keeps its children, expanding reuses them, and the reader sees
   * values (and edits against stamps) from before every change since — or, after a user-group
   * switch, entries and stamps that belong to the previous group. Subtrees holding an unsaved edit
   * or a flagged cell are kept, because dropping a node an edit points at would make the save skip
   * that edit without saying so; the fetch that follows refreshes those along with the expanded ones.
   */
  #pruneStaleTreeCache(): void {
    this._treeNodes = pruneCollapsedChildren(this._treeNodes, (key) => this.#holdsWork(key));
  }

  /**
   * Re-reads every loaded node for the current user group and takes the answer as the truth, with
   * the loader up.
   *
   * For the cases where what is on screen belongs to something else or is about to be superseded
   * wholesale: a user-group switch, and the refresh after a successful save. Nothing of the
   * user's is pending in either, so nothing needs classifying. Preserves the tree's shape and its
   * expanded state.
   *
   * If the read fails the tree is emptied rather than left as it was. What is on screen would
   * either belong to the previous group or predate a save that has just landed, and a grid
   * showing that under an error is worse than no grid. Picking the group again reloads it.
   */
  async #reloadAll(): Promise<void> {
    if (!this._selectedRole || this._treeNodes.length === 0) return;
    const roleAlias = this._selectedRole.alias;

    this.#loadAbortController?.abort();
    const controller = new AbortController();
    this.#loadAbortController = controller;

    this._loading = true;
    this._error = null;
    this.#pruneStaleTreeCache();

    try {
      const fresh = await this.#fetchFreshTree(roleAlias, controller.signal);
      if (controller.signal.aborted) return;
      this._treeNodes = this.#withFreshAdopted(this._treeNodes, fresh);
    } catch (err) {
      if (controller.signal.aborted) return;
      this._error = String(err);
      this._treeNodes = [];
    } finally {
      // Cleared by whichever load finishes un-superseded. A live refresh aborts a reload that was
      // still showing the loader, and that reload's own `finally` then skips clearing it, so
      // leaving this to the foreground path alone would strand the grid behind the loader.
      if (!controller.signal.aborted) this._loading = false;
    }
  }

  /**
   * Replaces the entries and stamp of every loaded node with the freshly-read ones, wholesale.
   *
   * Recursive and private: the return type is annotated explicitly (CLAUDE.md #4).
   * @param nodes The nodes to update, recursing into their loaded children.
   * @param fresh The freshly-read entries and stamp, keyed by local node key.
   * @returns New nodes, leaving the inputs untouched. A node absent from `fresh` is kept as it was.
   */
  #withFreshAdopted(nodes: ElementTreeNodeState[], fresh: ReadonlyMap<string, FreshNode>): ElementTreeNodeState[] {
    return nodes.map((node) => {
      const theirs = fresh.get(node.key);
      return {
        ...node,
        ...(theirs ? { entries: theirs.entries, stamp: theirs.stamp } : {}),
        ...(node.children ? { children: this.#withFreshAdopted(node.children, fresh) } : {}),
      };
    });
  }

  // ── Live reconciliation ─────────────────────────────────────────────────

  /**
   * Re-reads the server in the background and works out, node by node and verb by verb, what the
   * change means for whatever the editor is holding — which may be nothing at all.
   *
   * The decision itself is delegated to the pure, unit-tested `reconcileNode` — this method's
   * job is only to fetch, translate the shapes it needs, and apply what comes back. Cells the
   * user has not touched take the server's value silently — there is nothing of theirs to lose,
   * and leaving them stale would show two vintages of data in one grid. Cells where both sides
   * moved are flagged, and nothing else happens to them: the pending map is never written over
   * here, only ever by an explicit choice afterwards.
   *
   * What is written back is always `reconcileNode`'s `nextBase`, never the raw server response:
   * a conflicted verb must keep the entries it had, or the next pass would read it as unchanged
   * and the flag would silently stop meaning anything.
   *
   * Cancellable and role-guarded like the other loaders in this file: the fetch is assigned to
   * `#loadAbortController` (so a role switch or a cleared selection cancels it, and it cancels
   * them), and the selected user group's alias is captured once up front and checked again after
   * the one await this method makes. Without that, switching user group mid-fetch would let a
   * response for the *new* group land on the *old* group's tree and conflict state.
   * @returns A promise that resolves when the pass is done and rejects if the fetch failed, so
   * the refresh control does not claim the data was updated.
   */
  async #refreshFromServer(): Promise<void> {
    if (!this._selectedRole || this._treeNodes.length === 0) return;
    const roleAlias = this._selectedRole.alias;

    this.#loadAbortController?.abort();
    const controller = new AbortController();
    this.#loadAbortController = controller;
    this.#pruneStaleTreeCache();

    try {
      const fresh = await this.#fetchFreshTree(roleAlias, controller.signal);
      // `_saving` too: a tree read that finished after the save began may predate its write, and
      // would be reconciled against as if it were somebody else's change.
      if (controller.signal.aborted || this._selectedRole?.alias !== roleAlias || this._saving) return;

      // Starts from what is already flagged and only ever grows: a flag is removed by the user
      // acting on it, never by a later pass that happens to read the cell as unchanged.
      const conflicts = new Set(this._conflicts);
      // Pending entries this pass proves are not edits at all; removed once the loop is done.
      const stalePending = new Map<string, string[]>();

      for (const node of this.#flattenNodes(this._treeNodes)) {
        const theirsForNode = fresh.get(node.key);
        // Not fetched this pass, so there is nothing to reconcile it against.
        if (!theirsForNode) continue;

        const apiKey = node.key === VIRTUAL_ROOT_KEY ? VIRTUAL_ROOT_NODE_KEY : node.key;
        const pendingForNode = this._pendingChanges.get(node.key);

        const base = new Map<string, ReadonlyArray<CellEntry>>();
        const theirs = new Map<string, ReadonlyArray<CellEntry>>();
        for (const meta of LIBRARY_VERB_METAS) {
          base.set(meta.verb, this.#cellOf(node.entries, meta.verb));
          theirs.set(meta.verb, this.#cellOf(theirsForNode.entries, meta.verb));
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

        // A `refresh` verdict means the user holds nothing of their own on that cell, so a
        // pending entry there is stale bookkeeping — and a dangerous one, because it is what the
        // grid would go on rendering and the save would go on sending, over the value adopted here.
        if (pending) {
          const stale = staleRefreshVerbs({ base, pending, theirs, flagged: new Set(result.conflictedVerbs) });
          if (stale.length > 0) stalePending.set(node.key, stale);
        }

        // Rebuilt from the per-verb merge: a conflicted verb keeps the entries it had before
        // this event (what the next pass needs as `base`, so the same disagreement is not lost
        // the moment an unrelated node fires the next event); a clean verb takes the fresh ones.
        const mergedEntries = this.#entriesFromNextBase(apiKey, roleAlias, result.nextBase, theirsForNode.entries);

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

      this.#dropStalePending(stalePending);
      this.#adoptConflicts(conflicts);
    } catch (err) {
      if (controller.signal.aborted) return;
      this._error = String(err);
      // Rethrown so the refresh control does not claim the data was updated.
      throw err;
    } finally {
      // Cleared by whichever load finishes un-superseded, background included. A live refresh
      // aborts a group-switch reload that was still showing the loader, and that reload's own
      // `finally` then skips clearing it, so leaving this to foreground loads would strand the
      // grid behind the loader for good.
      if (!controller.signal.aborted) this._loading = false;
    }
  }

  /**
   * Removes the pending entries a reconcile pass found to be stale, and a node's whole entry in
   * the pending map when that leaves it with nothing pending.
   * @param stale The verbs to drop, keyed by local node key.
   */
  #dropStalePending(stale: ReadonlyMap<string, readonly string[]>): void {
    if (stale.size === 0) return;
    const next = new Map<string, PendingNodeChanges>();
    for (const [nodeKey, verbs] of this._pendingChanges) {
      const drop = stale.get(nodeKey);
      if (!drop) {
        next.set(nodeKey, verbs);
        continue;
      }
      const kept: PendingNodeChanges = new Map([...verbs].filter(([verb]) => !drop.includes(verb)));
      if (kept.size > 0) next.set(nodeKey, kept);
    }
    this._pendingChanges = next;
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
   * Flattens a per-verb `nextBase` merge (as `reconcileNode` returns it) back into the flat
   * entry list `ElementTreeNodeState.entries` holds. The synthesised `id` is display-only, the
   * same convention the rest of this file uses when it fabricates entries client-side.
   *
   * Entries for verbs the grid has no column for are carried over from the fresh read rather than
   * dropped. `nextBase` only knows the verbs in the grid, and a save rebuilds a node's whole entry
   * list from `node.entries`, so an entry lost here would be deleted from the server by the next
   * save of a node the user never touched that verb on.
   * @param nodeKey The API node key (real key, or `VIRTUAL_ROOT_NODE_KEY`).
   * @param roleAlias The user group these entries belong to.
   * @param nextBase The per-verb entries to flatten.
   * @param freshEntries The node's freshly-read entries, the source for verbs `nextBase` lacks.
   * @returns The flat entry list.
   */
  #entriesFromNextBase(
    nodeKey: string,
    roleAlias: string,
    nextBase: ReadonlyMap<string, ReadonlyArray<CellEntry>>,
    freshEntries: ReadonlyArray<PermissionEntry>,
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
    for (const e of freshEntries) {
      if (!nextBase.has(e.verb)) result.push(e);
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
  #flattenNodes(nodes: ElementTreeNodeState[]): ElementTreeNodeState[] {
    const out: ElementTreeNodeState[] = [];
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
   * Without the re-read the cell keeps showing the pre-conflict value, and `#saveChanges`, which
   * builds its request from `node.entries` plus pending changes, could send that stale value under
   * a stamp the server accepts. The other person's change would be silently reverted: no 409, no
   * dialog, nothing to notice.
   *
   * The re-read fetches entries and stamp in the same response, so they cannot disagree. It goes
   * through the live-event controller, exactly as the manual refresh control does, so it queues
   * behind any run already in flight instead of aborting it and compares before it adopts. Until
   * that read lands, the nodes still hold their old stamp, so a save made in the meantime is
   * refused by the server rather than written stale.
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

  // ── Permission dialog ───────────────────────────────────────────────────

  /**
   * Opens the scope dialog for one cell, seeded from the cell's pending value if it has one and
   * its stored value otherwise, and restricted to the sides that apply to the node's kind.
   * @param node The node whose cell was clicked.
   * @param verb The verb whose cell was clicked.
   */
  #openPicker(node: ElementTreeNodeState, verb: string): void {
    const isVirtualRoot = node.key === VIRTUAL_ROOT_KEY;
    const entries = this.#getCellEntries(node, verb);

    this._pickerNode = node;
    this._pickerVerb = verb;
    this._pickerIsVirtualRoot = isVirtualRoot;

    if (isVirtualRoot) {
      this._pickerNodeApplicable = true;
      this._pickerDescApplicable = true;
      const first = entries[0];
      this._pickerNodeState = first ? (first.state === 'Allow' ? 'allow' : 'deny') : 'inherit';
      this._pickerDescState = 'inherit';
      this._pickerSameAsNode = true;
      this._pickerNodeIsPriorityOverride = first?.isPriorityOverride === true;
      this._pickerDescIsPriorityOverride = false;
    } else {
      const app = libraryApplicability(verb, node.isFolder);
      this._pickerNodeApplicable = app.nodeApplicable;
      this._pickerDescApplicable = app.descApplicable;
      const d = decomposeEntries(entries);

      if (app.nodeApplicable && app.descApplicable) {
        this._pickerNodeState = d.nodeState;
        this._pickerDescState = d.descState;
        this._pickerSameAsNode = d.sameAsNode;
        this._pickerNodeIsPriorityOverride = d.nodeIsPriorityOverride;
        this._pickerDescIsPriorityOverride = d.descIsPriorityOverride;
      } else if (app.nodeApplicable) {
        // Leaf element: single this-node choice (dialog single mode uses _pickerNodeState).
        this._pickerNodeState = d.nodeState;
        this._pickerNodeIsPriorityOverride = d.nodeIsPriorityOverride;
      } else {
        // Element-only verb on a folder: single descendants choice — surface the descendant state.
        this._pickerNodeState = d.descState;
        this._pickerNodeIsPriorityOverride = d.descIsPriorityOverride;
      }
    }

    void this.updateComplete.then(() => this._scopeDialog.open());
  }

  /**
   * Handles `uap-scope-apply` from the shared scope dialog: writes the composed entries into
   * the pending-changes map under the cell that opened the dialog.
   * @param e The apply event carrying the composed entries.
   */
  #handleScopeApply(e: CustomEvent<{ entries: PendingVerbEntries }>): void {
    if (!this._pickerNode || !this._pickerVerb) return;
    const nodeKey = this._pickerNode.key;
    const verb = this._pickerVerb;
    const nodeChanges: PendingNodeChanges = new Map(this._pendingChanges.get(nodeKey) ?? []);
    const stored = this.#cellOf(this.#findNode(nodeKey)?.entries ?? [], verb);
    const applied = e.detail.entries.map((p) => ({ state: p.state, scope: p.scope, isPriorityOverride: p.isPriorityOverride }));

    // Applying what is already stored is not an edit, so it is not recorded. The dialog's Apply is
    // unconditional; recording the no-op would leave a pending entry that a later reconcile treats
    // as "nothing of the user's here" while the save still sends it, reverting a colleague's change.
    // It also removes an earlier edit the user has just changed back.
    if (sameCell(applied, stored)) nodeChanges.delete(verb);
    else nodeChanges.set(verb, e.detail.entries);

    const next = new Map(this._pendingChanges);
    if (nodeChanges.size === 0) next.delete(nodeKey);
    else next.set(nodeKey, nodeChanges);
    this._pendingChanges = next;
  }

  // ── Save ─────────────────────────────────────────────────────────────────

  /**
   * Writes every pending change in one all-or-nothing request, and stops to ask if the stored
   * values moved since this editor read them.
   *
   * One request rather than a loop of one save per node: a rejection partway through a loop would
   * leave some nodes written and some not, with nothing afterwards able to tell which.
   * @param force Whether to write through a stale stamp. Only true after the user has confirmed
   * via the conflict dialog, and the dialog's overwrite handler is the only caller that passes it.
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
        for (const en of node.entries) {
          const list = byVerb.get(en.verb) ?? [];
          list.push({ verb: en.verb, state: en.state, scope: en.scope, isPriorityOverride: en.isPriorityOverride });
          byVerb.set(en.verb, list);
        }
        for (const [verb, pending] of verbChanges) {
          if (pending.length === 0) byVerb.delete(verb);
          else byVerb.set(verb, pending.map((pe) => ({ verb, state: pe.state, scope: pe.scope, isPriorityOverride: pe.isPriorityOverride })));
        }

        // An empty stamp is NOT "this node has nothing stored". A node with no entries carries the
        // real empty-set stamp from the server, so it is checked like any other. An empty string
        // arises only when a read came back with no ETag at all, which means "no stamp available":
        // sending it would be a stamp that can never match, so the check is omitted for that one
        // case and no other.
        nodes.push({
          nodeKey: nodeKey === VIRTUAL_ROOT_KEY ? VIRTUAL_ROOT_NODE_KEY : nodeKey,
          roleAlias: this._selectedRole.alias,
          entries: [...byVerb.values()].flat(),
          expectedStamp: node.stamp === '' ? undefined : node.stamp,
        });
      }

      const result = await saveElementPermissionsBatch(nodes, force);

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
        const localKey = savedNodeKey === VIRTUAL_ROOT_NODE_KEY ? VIRTUAL_ROOT_KEY : savedNodeKey!;
        this.#updateNode(localKey, { stamp });
      }

      await this.#reloadAll();
      this._pendingChanges = new Map();
      this.#resetLiveState();
      clearElementEffectivePermissionCache();
      this.#notificationContext?.peek('positive', { data: { message: this.#localize.term('uap_library_permissionsSaved') } });
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
   * The batch endpoint reports a conflict per node+user group pair — its unit of write — but the
   * dialog has to name what would be lost per verb, the granularity a person actually edited at.
   * Only verbs this editor has a pending change for become a line: `currentEntries` carries every
   * verb stored at that node, most of them untouched by this save. The affected nodes' entries
   * are also advanced to what the server just reported, but their stamp is left alone, exactly as
   * for a live-detected conflict — `#loadStoredForConflicts` resolves both the same way, by
   * re-reading the server rather than trusting either half of what is held here. The advanced
   * entries are what keeps a confirmed overwrite from reverting the node's other verbs; the flags
   * set here are sticky, so the advanced baseline cannot make a later pass read the cell as clean.
   * @param conflicts The conflicting node+user group pairs the server refused to overwrite.
   */
  #openConflictDialog(conflicts: BatchSaveConflict[]): void {
    const lines: UapConflictLine[] = [];
    const flagged = new Set(this._conflicts);

    for (const conflict of conflicts) {
      const localKey = conflict.nodeKey === VIRTUAL_ROOT_NODE_KEY ? VIRTUAL_ROOT_KEY : conflict.nodeKey;
      const isVirtualRoot = localKey === VIRTUAL_ROOT_KEY;
      const nodeName = isVirtualRoot
        ? this.#localize.term('uap_library_root')
        : (this.#findNode(localKey)?.name ?? localKey);

      this.#updateNode(localKey, { entries: conflict.currentEntries });

      const pending = this._pendingChanges.get(localKey);
      if (!pending) continue;

      for (const [verb, pendingEntries] of pending) {
        flagged.add(`${localKey}|${verb}`);
        const verbName = LIBRARY_VERB_METAS.find((m) => m.verb === verb)?.displayName ?? verb;
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

  /**
   * Recursive lookup wrapper using the shared `findNode` helper.
   * @param key The local key of the node to find.
   * @returns The node, or `null` when it is not in the loaded tree.
   */
  #findNode(key: string): ElementTreeNodeState | null {
    return findNode(this._treeNodes, key);
  }

  /**
   * The entries a cell should currently display: its pending value if it has one, otherwise what
   * is stored.
   * @param node The node the cell belongs to.
   * @param verb The verb the cell is for.
   * @returns The pending entries, or the stored entries for that verb.
   */
  #getCellEntries(node: ElementTreeNodeState, verb: string): PendingVerbEntries | PermissionEntry[] {
    const pending = this._pendingChanges.get(node.key);
    if (pending?.has(verb)) return pending.get(verb)!;
    return node.entries.filter((e) => e.verb === verb);
  }

  /**
   * Computes the cell rendering shape honouring applicability:
   * - fully N/A (Create on a leaf) → hatched N/A block, not clickable;
   * - leaf node-only → uniform block of the node state;
   * - folder descendants-only (element-only verbs) → split block with an N/A node half;
   * - otherwise → the standard split/uniform from {@link getCellInfo}.
   * @param node The node the cell belongs to.
   * @param verb The verb the cell is for.
   * @returns The block to draw, or `na` when the cell does not apply to this node kind.
   */
  #cellInfo(node: ElementTreeNodeState, verb: string): { info: CellInfo | null; na: boolean } {
    const entries = this.#getCellEntries(node, verb);
    if (node.key === VIRTUAL_ROOT_KEY) {
      return { info: getCellInfo(entries), na: false };
    }
    const app = libraryApplicability(verb, node.isFolder);
    if (!app.nodeApplicable && !app.descApplicable) {
      return { info: null, na: true };
    }
    const d = decomposeEntries(entries);
    if (app.nodeApplicable && app.descApplicable) {
      return { info: getCellInfo(entries), na: false };
    }
    if (app.nodeApplicable) {
      // Leaf element — uniform of the node state.
      return {
        info: { split: false, nodeClass: d.nodeState, descClass: d.nodeState, nodeOverride: d.nodeIsPriorityOverride },
        na: false,
      };
    }
    // Folder, element-only verb — N/A node half + descendant state.
    return {
      info: { split: true, nodeNa: true, nodeClass: 'inherit', descClass: d.descState, descOverride: d.descIsPriorityOverride },
      na: false,
    };
  }

  // ── Selection panel ──────────────────────────────────────────────────────

  /** The selector options for the selection panel: a single user-group picker, showing the current group when there is one. */
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

  /**
   * Routes a click on one of the selection panel's controls to its picker.
   * @param id The id of the selector option that was clicked.
   */
  #onSelectorClick(id: string): void {
    if (id === 'group') void this.#openRolePicker();
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
  #renderRows(nodes: ElementTreeNodeState[], depth: number): TemplateResult[] {
    return nodes.flatMap((node) => [
      this.#renderRow(node, depth),
      ...(node.expanded && node.children ? this.#renderRows(node.children, depth + 1) : []),
    ]);
  }

  /**
   * Renders one node's row: its expander, icon and name, then one permission cell per verb.
   * @param node The node to render.
   * @param depth The nesting depth, used for indentation.
   * @returns The table row.
   */
  #renderRow(node: ElementTreeNodeState, depth: number): TemplateResult {
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
            <umb-icon name=${node.icon ?? (node.isFolder ? 'icon-folder' : 'icon-document')}></umb-icon>
            <span class="node-name">${node.name}</span>
          </div>
        </td>
        ${LIBRARY_VERB_METAS.map((v) => this.#renderCell(node, v.verb))}
      </tr>
    `;
  }

  /**
   * Renders one permission cell: hatched when the verb does not apply to the node's kind, and
   * outlined when the server has changed it under an unsaved edit.
   * @param node The node the cell belongs to.
   * @param verb The verb the cell is for.
   * @returns The table cell.
   */
  #renderCell(node: ElementTreeNodeState, verb: string): TemplateResult {
    const { info, na } = this.#cellInfo(node, verb);
    const isPending = this._pendingChanges.get(node.key)?.has(verb) ?? false;

    if (na) {
      return html`<td class="perm-td na-td" title=${this.#localize.term('uap_library_notApplicableTitle', verb.split('.').pop() ?? '')}>
        <uap-perm-block na ?pending=${isPending}></uap-perm-block>
      </td>`;
    }

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
   * @returns The dialog element.
   */
  #renderDialog(): TemplateResult {
    const verbName = this._pickerVerb?.split('.').pop() ?? '';
    const nodeName = this._pickerIsVirtualRoot
      ? this.#localize.term('uap_library_root')
      : (this._pickerNode?.name ?? '');

    return html`
      <uap-permission-scope-dialog
        .verb=${verbName}
        .nodeName=${nodeName}
        .isVirtualRoot=${this._pickerIsVirtualRoot}
        .nodeApplicable=${this._pickerNodeApplicable}
        .descApplicable=${this._pickerDescApplicable}
        .initialNodeState=${this._pickerNodeState}
        .initialDescState=${this._pickerDescState}
        .initialSameAsNode=${this._pickerSameAsNode}
        .initialNodeIsPriorityOverride=${this._pickerNodeIsPriorityOverride}
        .initialDescIsPriorityOverride=${this._pickerDescIsPriorityOverride}
        @uap-scope-apply=${(e: CustomEvent<{ entries: PendingVerbEntries }>) => this.#handleScopeApply(e)}>
      </uap-permission-scope-dialog>
    `;
  }

  /** Renders the surface: the selection panel with its actions and grid, plus the dialogs that must layer above it. */
  override render(): TemplateResult {
    return html`
      <umb-body-layout headline=${this.#localize.term('uap_library_editorHeadline')}>
        <uap-page-intro surface="uap-library-permissions" headline=${this.#localize.term('uap_library_editorHeadline')}></uap-page-intro>
        <uap-selection-panel
          .groups=${this.#selectionGroups}
          promptText=${this.#localize.term('uap_library_selectRolePrompt')}
          ctaIcon="icon-globe"
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
                <uui-button label=${this.#localize.term('uap_discard')} look="outline" ?disabled=${this._saving} @click=${() => this.#discardChanges()}>
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
                        <th class="node-header">${this.#localize.term('uap_library_nodeHeader')}</th>
                        ${LIBRARY_VERB_METAS.map((v) => html`<th class="verb-header" title=${v.verb}>${v.displayName}</th>`)}
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
    tr.row-pending td { background-color: color-mix(in srgb, oklch(85% 0.15 90) 12%, transparent); }

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

    /* Invisible clone of the expand toggle; reserves equal width so leaf icons stay aligned. */
    .expand-spacer { visibility: hidden; }
    .node-name { overflow: hidden; text-overflow: ellipsis; }

    .perm-td { padding: 3px; text-align: center; vertical-align: middle; }
    .na-td { cursor: default; }

    .perm-td.conflicted uap-perm-block {
      outline: 2px solid var(--uui-color-danger);
      outline-offset: 2px;
    }
  `;
}

export default UapLibraryPermissionsEditorRootElement;

declare global {
  interface HTMLElementTagNameMap {
    'uap-library-permissions-editor-root': UapLibraryPermissionsEditorRootElement;
  }
}
