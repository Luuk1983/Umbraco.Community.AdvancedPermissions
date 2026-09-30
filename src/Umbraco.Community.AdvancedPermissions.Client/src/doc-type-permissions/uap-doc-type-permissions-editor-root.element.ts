import { html, css, nothing, customElement, state, query } from '@umbraco-cms/backoffice/external/lit';
import type { TemplateResult } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import { UMB_MODAL_MANAGER_CONTEXT } from '@umbraco-cms/backoffice/modal';
import type {
  RoleInfo,
  TreeNodeState,
  PermissionEntry,
  PermissionState,
  PermissionScope,
} from '../models/permission.models.js';
import { VIRTUAL_ROOT_NODE_KEY } from '../models/permission.models.js';
import { getTreeRoot, getTreeChildren } from '../api/advanced-permissions.api.js';
import {
  getDocTypes,
  getDocTypePermissionsForEditor,
  saveDocTypePermissionsBatch,
} from '../api/doc-type-permissions.api.js';
import type {
  DocTypeListItem,
  DocTypeEditorNode,
  BatchSaveDocTypeNode,
  BatchSaveDocTypeConflict,
} from '../models/doc-type-permission.models.js';
import { decomposeEntries } from '../utils/decompose-entries.js';
import { type PendingVerbEntries } from '../utils/compose-entries.js';
import { getCellInfo, type CellInfo } from '../utils/cell-info.js';
import { updateNode, findNode } from '../utils/tree-ops.js';
import { loadSelection, saveSelection, clearSelection } from '../utils/selection-store.js';
import { UAP_ROLE_PICKER_MODAL } from '../access-viewer/role-picker-modal.token.js';
import { UMB_DOCUMENT_TYPE_PICKER_MODAL } from '@umbraco-cms/backoffice/document-type';
import { UapSecurityEventsController } from '../live/security-events.controller.js';
import { UapUnsavedChangesGuard, confirmDiscardUnsavedChanges } from '../live/unsaved-changes-guard.js';
import type { CellEntry } from '../live/conflict.js';
import type { UapRefreshPhase } from '../live/refresh-phase.js';
import '../live/uap-live-refresh.element.js';
import { reconcileNode } from '../live/reconcile.js';
import { applyFreshReads, type FreshRead } from '../live/apply-fresh.js';
import { withCellEdit } from '../live/pending-changes.js';
import { createMissedEvents } from '../live/missed-events.js';
import { planSaveConflicts, type SaveConflictNode } from '../live/save-conflicts.js';
import { EMPTY_SET_STAMP, expectedStampFor, stampOf } from '../live/stamp.js';
import '../shared/components/uap-perm-block.element.js';
import '../shared/components/uap-permission-scope-dialog.element.js';
import '../live/uap-live-banner.element.js';
import '../live/uap-conflict-dialog.element.js';
import '../help/uap-page-intro.element.js';
import '../help/uap-selection-panel.element.js';
import type { UapSelectorGroup } from '../help/uap-selection-panel.element.js';
import type { UapPermissionScopeDialogElement } from '../shared/components/uap-permission-scope-dialog.element.js';
import type { UapConflictDialogElement, UapConflictLine } from '../live/uap-conflict-dialog.element.js';

/** The single verb v1 ships for doc-type permissions. */
const VERB = 'Umb.Document.CreateOfType';

/** Sentinel local key for the virtual-root row in the tree. Mapped back to `VIRTUAL_ROOT_NODE_KEY` on save. */
const VIRTUAL_ROOT_LOCAL_KEY = 'virtual-root';

/** Stable identifier used to key this surface's remembered selection (issue #46). */
const SURFACE_ID = 'doc-type-permissions-editor';

/**
 * How many times one click on Save may be refused and retried on its own before giving up. A retry
 * only happens when nothing the user edited was contested, so this is a backstop against a
 * colleague writing to the same node continuously, not something a person should ever meet.
 */
const MAX_SAVE_ATTEMPTS = 5;

/**
 * Document-type Permissions Editor workspace.
 *
 * Mirrors the layout of the existing Permissions Editor with two differences:
 * 1. Two pickers in the toolbar — role *and* doc-type — both required before the tree loads.
 * 2. One "Allowed" column instead of N verb columns. Each row's cell shows the resolved entry
 *    state for the chosen (role, doc-type, node) triple.
 *
 * The virtual-root row defaults to a visible Allow when no entry exists, reflecting that the
 * doc-type resolver uses default-Allow semantics. The shared scope dialog handles editing;
 * saves go through the batch endpoint, all or nothing, with the same live-update reconciliation
 * and save-time conflict handling as the node Permissions Editor — see `#reconcileWithServer` and
 * `#saveChanges` below.
 *
 * A doc-type permission is keyed by node **plus** user group **plus** document type. The role
 * and document type are fixed for the whole editor session (there is one tree per pair), so every
 * per-cell key elsewhere in this file only needs the node key; only the conflict set — reported to
 * the user in terms that must survive a role or doc-type switch conceptually, and matched against
 * `BatchSaveDocTypeConflict.contentTypeKey` from the server — carries the content type explicitly.
 */
@customElement('uap-doc-type-permissions-editor-root')
export class UapDocTypePermissionsEditorRootElement extends UmbLitElement {
  #localize = new UmbLocalizationController(this);

  @state() private _docTypes: DocTypeListItem[] = [];

  @state() private _selectedRole: RoleInfo | null = null;
  @state() private _selectedDocType: DocTypeListItem | null = null;
  @state() private _treeNodes: TreeNodeState[] = [];
  @state() private _loading = false;
  @state() private _saving = false;
  @state() private _error: string | null = null;

  /** nodeKey → pending entries for this (role, doc-type). Empty list = clear. */
  @state() private _pendingChanges: Map<string, PendingVerbEntries> = new Map();

  // ── Live updates & conflicts ────────────────────────────────────────────
  /** Cells the server changed under an unsaved edit, keyed `nodeKey|contentTypeKey|verb`. */
  @state() private _conflicts: Set<string> = new Set();

  /** Whether the banner has been dismissed for the current set of conflicts. */
  @state() private _bannerDismissed = false;

  /** Where the refresh control is: idle, refreshing, or briefly confirming an update. */
  @state() private _refreshPhase: UapRefreshPhase = 'idle';

  /**
   * Whether a reload failed, so the grid may not match the server: after a save whose follow-up
   * read failed, or a selection change whose read failed. While set, the grid is hidden and saving
   * is refused, because entries and stamps of unknown vintage are what a save is checked against.
   * Cleared only by a read that lands (the refresh control, a live event, or a fresh load), which
   * adopts entries and stamps together.
   */
  @state() private _treeStale = false;

  // Scope-dialog state — mirrors the existing editor.
  @state() private _pickerNode: TreeNodeState | null = null;
  @state() private _pickerIsVirtualRoot = false;
  @state() private _pickerNodeState: 'inherit' | 'allow' | 'deny' = 'inherit';
  @state() private _pickerDescState: 'inherit' | 'allow' | 'deny' = 'inherit';
  @state() private _pickerSameAsNode = true;
  @state() private _pickerNodeIsPriorityOverride = false;
  @state() private _pickerDescIsPriorityOverride = false;

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
   * first. In-page changes (switching group or document type, clearing the selection) are guarded
   * by their own handlers through the same `#confirmDiscard`, because they are not router
   * navigations.
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

    // Every event is answered by a reconcile, clean editor or not: with nothing pending it adopts
    // the server's value and stamp for every node, as a plain reload would, but it compares first,
    // so an edit begun while the read was in flight cannot have somebody else's change to the same
    // cell absorbed as its baseline. Mirrors the node Permissions Editor exactly — see its
    // constructor for the full reasoning.
    this.#liveEvents = new UapSecurityEventsController(
      this,
      async () => {
        if (!this._selectedRole || !this._selectedDocType) return;
        // Nothing is reconciled while this editor's own save is in flight. The write raises an
        // event of its own, and the event carries no identity, so it cannot be told from a
        // colleague's; reconciling against it would flag the user's own save as a conflict. This
        // replaces what comparing values used to do (see `CellVerdict`). It is safe to drop
        // because a finished save reloads everything and clears the pending and conflict state,
        // and a refused one hands back the server's current values itself. It is not simply
        // dropped: a colleague's write can land after that reload and before `_saving` clears, so
        // the event is remembered and replayed by `#saveChanges` once the save is over.
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
   * document type, or clearing the selection — so a conflict against the previous pair's data
   * cannot outlive the data it was about.
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
   * switching user group or document type, clearing the selection) words it identically.
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

  override disconnectedCallback(): void {
    super.disconnectedCallback();
    this.#unsavedGuard.detach();
    this.#loadAbortController?.abort();
  }

  /** Restores the last-used user group and document type; loads the tree once both are present. */
  #restoreSelection(): void {
    const stored = loadSelection(SURFACE_ID);
    if (stored?.role) this._selectedRole = stored.role;
    if (stored?.docType) this._selectedDocType = stored.docType;
    if (this._selectedRole && this._selectedDocType) void this.#loadTree();
  }

  /** Persists the selection only when complete (user group + document type); otherwise forgets it. */
  #persistSelection(): void {
    if (this._selectedRole && this._selectedDocType) {
      saveSelection(SURFACE_ID, { subjectKind: 'role', role: this._selectedRole, docType: this._selectedDocType });
    } else {
      clearSelection(SURFACE_ID);
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
    this._selectedDocType = null;
    this._treeNodes = [];
    this._pendingChanges = new Map();
    this.#resetLiveState();
    this._treeStale = false;
    this._error = null;
    clearSelection(SURFACE_ID);
  }

  // ── Data loading ────────────────────────────────────────────────────────

  async #loadMeta(): Promise<void> {
    try {
      this._docTypes = await getDocTypes();
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

    const hadTree = this._treeNodes.length > 0 && this._selectedRole !== null && this._selectedDocType !== null;
    this._selectedRole = result.role;
    this._pendingChanges = new Map();
    this.#resetLiveState();
    this.#persistSelection();
    if (hadTree) {
      void this.#reloadEntries();
    } else if (this._selectedDocType) {
      void this.#loadTree();
    }
  }

  /**
   * Opens Umbraco's built-in document-type tree picker, excluding element types and folders.
   * The picked GUID is mapped back to a `DocTypeListItem` (for its name/icon) via the already
   * loaded `_docTypes` list, then the existing reload/load flow runs.
   */
  async #openDocTypePicker(): Promise<void> {
    if (!this.#modalManager) return;
    const modal = this.#modalManager.open(this, UMB_DOCUMENT_TYPE_PICKER_MODAL, {
      data: {
        hideTreeRoot: true,
        pickableFilter: (item) => !item.isFolder && item.isElement === false,
      },
      value: { selection: this._selectedDocType ? [this._selectedDocType.key] : [] },
    });
    const value = await modal.onSubmit().catch(() => undefined);
    if (!value) return;

    const key = value.selection?.[0];
    const picked = key
      ? (this._docTypes.find((dt) => dt.key.toLowerCase() === key.toLowerCase()) ?? null)
      : null;

    // Same document type again changes nothing, so it must not cost the user their edits.
    if (this._pendingChanges.size > 0 && picked?.key === this._selectedDocType?.key) return;
    // Asked after the pick, not before opening the picker: cancelling the picker then costs nothing.
    if (!(await this.#confirmDiscard())) return;

    const hadTree = this._treeNodes.length > 0 && this._selectedRole !== null && this._selectedDocType !== null;
    this._selectedDocType = picked;
    this._pendingChanges = new Map();
    this.#resetLiveState();
    this.#persistSelection();
    if (!this._selectedDocType) {
      this._treeNodes = [];
      return;
    }
    if (hadTree) {
      void this.#reloadEntries();
    } else if (this._selectedRole) {
      void this.#loadTree();
    }
  }

  async #loadTree(): Promise<void> {
    if (!this._selectedRole || !this._selectedDocType) return;

    this.#loadAbortController?.abort();
    const controller = new AbortController();
    this.#loadAbortController = controller;

    this._loading = true;
    this._error = null;
    // The tree is being replaced from scratch, so whatever made the old one untrustworthy is gone.
    this._treeStale = false;
    this._treeNodes = [];

    try {
      const [editorNodes, nodes] = await Promise.all([
        getDocTypePermissionsForEditor(this._selectedRole.alias, this._selectedDocType.key, controller.signal),
        getTreeRoot('$everyone', controller.signal),
      ]);
      if (controller.signal.aborted) return;

      const byNode = this.#groupByNode(editorNodes);

      const virtualRootFresh = byNode.get(VIRTUAL_ROOT_NODE_KEY);
      const virtualRoot: TreeNodeState = {
        key: VIRTUAL_ROOT_LOCAL_KEY,
        name: this.#localize.term('uap_contentRoot'),
        icon: 'icon-globe',
        hasChildren: false,
        entries: virtualRootFresh?.entries ?? [],
        stamp: stampOf(virtualRootFresh),
        expanded: false,
        loading: false,
      };
      this._treeNodes = [
        virtualRoot,
        ...nodes.map((n) => {
          const fresh = byNode.get(n.key);
          return {
            ...n,
            entries: fresh?.entries ?? [],
            // Overrides the node-permission stamp `getTreeRoot` carries — irrelevant here, since
            // this tree call is only used for its content structure (name/icon/hasChildren). The
            // doc-type concurrency stamp always comes from `getDocTypePermissionsForEditor`. A node
            // the read has no bucket for has nothing stored and holds the real empty-set stamp, so
            // its first save is concurrency-checked like any other.
            stamp: stampOf(fresh),
            expanded: false,
            loading: false,
          };
        }),
      ];
    } catch (err) {
      if (controller.signal.aborted) return;
      this._error = String(err);
    } finally {
      if (!controller.signal.aborted) this._loading = false;
    }
  }

  /**
   * Reloads only the entries (and stamps) for the current tree on a (role, doc-type) change or
   * after a save. Preserves expanded state and existing children — the one response covers every
   * node the tree holds, expanded or not, so there are no stale cached children to worry about.
   *
   * Always shows the loader, because what is on screen belongs to the previous pair (or, after a
   * save, is about to be replaced). Live events, the manual refresh control and discarding do not
   * come through here: they go through `#reconcileWithServer`, which keeps the grid on screen and
   * compares before it adopts anything. Applying a response over a grid the user can still edit
   * would absorb, as the new baseline with no flag, a change somebody else made to a cell that
   * user began editing while the request was in flight.
   *
   * Entries and stamps are swapped in together, in one step, after the response has landed
   * (`applyFreshReads`). A failed read leaves the tree exactly as it was and marks it stale
   * (`_treeStale`), which hides the grid and refuses to save until a read lands.
   * @returns `true` when fresh entries and stamps were adopted; `false` when the read failed, or was
   * superseded by another load, in which case the tree is marked stale until one of them lands.
   */
  async #reloadEntries(): Promise<boolean> {
    if (!this._selectedRole || !this._selectedDocType || this._treeNodes.length === 0) return true;
    const roleAlias = this._selectedRole.alias;
    const contentTypeKey = this._selectedDocType.key;

    this.#loadAbortController?.abort();
    const controller = new AbortController();
    this.#loadAbortController = controller;

    this._loading = true;
    this._error = null;

    try {
      const editorNodes = await getDocTypePermissionsForEditor(roleAlias, contentTypeKey, controller.signal);
      if (
        controller.signal.aborted ||
        this._selectedRole?.alias !== roleAlias ||
        this._selectedDocType?.key !== contentTypeKey
      ) {
        return false;
      }

      this._treeNodes = applyFreshReads(this._treeNodes, this.#freshForTree(this.#groupByNode(editorNodes)));
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
   * Builds the fresh read for every node the tree holds from the one response, keyed by local node
   * key. The response only carries a bucket for a node with something stored, so a node without one
   * is not "unread": it has nothing stored, and takes an empty list with the real empty-set stamp.
   * @param byNode The response, binned by API node key.
   * @returns Entries and stamp for every loaded node.
   */
  #freshForTree(byNode: Map<string, { entries: PermissionEntry[]; stamp: string }>): Map<string, FreshRead> {
    const fresh = new Map<string, FreshRead>();
    for (const node of this.#flattenNodes(this._treeNodes)) {
      const bucket = byNode.get(node.key === VIRTUAL_ROOT_LOCAL_KEY ? VIRTUAL_ROOT_NODE_KEY : node.key);
      fresh.set(node.key, { entries: bucket?.entries ?? [], stamp: stampOf(bucket) });
    }
    return fresh;
  }

  /**
   * Bins the editor's grouped-by-node response into a lookup keyed by node key, translating each
   * node's `DocTypePermissionEntry` list into the generic `PermissionEntry` shape the tree state
   * holds (the content-type is implied by the whole editor's selection, so it is dropped here the
   * same way the rest of this file already drops it).
   */
  #groupByNode(nodes: DocTypeEditorNode[]): Map<string, { entries: PermissionEntry[]; stamp: string }> {
    const map = new Map<string, { entries: PermissionEntry[]; stamp: string }>();
    for (const n of nodes) {
      map.set(n.nodeKey, {
        entries: n.entries.map((e) => ({
          id: e.id,
          nodeKey: e.nodeKey,
          roleAlias: e.roleAlias,
          verb: e.verb,
          state: e.state,
          scope: e.scope,
          isPriorityOverride: e.isPriorityOverride,
        })),
        stamp: n.stamp,
      });
    }
    return map;
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
      const children = await getTreeChildren(node.key, '$everyone');
      const editorNodes = await getDocTypePermissionsForEditor(
        this._selectedRole!.alias,
        this._selectedDocType!.key,
      );
      const byNode = this.#groupByNode(editorNodes);
      this.#updateNode(node.key, {
        expanded: true,
        loading: false,
        children: children.map((c) => {
          const fresh = byNode.get(c.key);
          return {
            ...c,
            entries: fresh?.entries ?? [],
            stamp: stampOf(fresh),
            expanded: false,
            loading: false,
          };
        }),
      });
    } catch (err) {
      this.#updateNode(node.key, { loading: false });
      this._error = String(err);
    }
  }

  #updateNode(key: string, changes: Partial<TreeNodeState>): void {
    this._treeNodes = updateNode(this._treeNodes, key, changes);
  }

  // ── Scope dialog ─────────────────────────────────────────────────────────

  #openPicker(node: TreeNodeState): void {
    this._pickerNode = node;
    this._pickerIsVirtualRoot = node.key === VIRTUAL_ROOT_LOCAL_KEY;

    const entries = this.#getCellEntries(node);

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
   * Receives the composed entries from the shared scope dialog and records them as the pending
   * change for the cell that opened the dialog, unless they equal what the cell already holds.
   *
   * The dialog's Apply is unconditional, so a user can apply the value a cell already has, or change
   * it and change it back. Recording that as an edit leaves a pending entry equal to the baseline,
   * which a colleague's later change then makes dangerous: the refresh adopts their value and stamp,
   * and the stale entry goes out on the next save under a matching stamp, deleting their change with
   * no flag and no dialog. `withCellEdit` drops it instead (see there for the flagged-cell
   * exception), and `reconcileNode` reports any that get through anyway.
   * @param e The dialog's apply event, carrying the composed entries.
   */
  #handleScopeApply(e: CustomEvent<{ entries: PendingVerbEntries }>): void {
    if (!this._pickerNode || !this._selectedDocType) return;
    const nodeKey = this._pickerNode.key;
    this._pendingChanges = withCellEdit(
      this._pendingChanges,
      nodeKey,
      e.detail.entries,
      this.#cellOf(this._pickerNode.entries),
      this._conflicts.has(`${nodeKey}|${this._selectedDocType.key}|${VERB}`),
    );
  }

  // ── Live reconciliation ─────────────────────────────────────────────────

  /**
   * Refetches the stored values and works out, node by node, what a live change means for an
   * editor holding unsaved work.
   *
   * The decision itself is delegated to the pure, unit-tested `reconcileNode` — the same one the
   * node Permissions Editor uses — with a single verb (`VERB`) standing in for the verb dimension
   * that editor iterates over. There is only one HTTP call to make: unlike the node-permission
   * tree, `getDocTypePermissionsForEditor` already answers for the whole (role, content-type)
   * combination regardless of tree depth, so there is no per-level batching to do and no "not
   * fetched this pass" gap to skip around — a node absent from the response has nothing stored,
   * full stop, and reconciles against an empty cell exactly like any other stored state.
   *
   * Cancellable and selection-guarded like the other loaders in this file: the fetch is assigned
   * to `#loadAbortController`, and the selected role and document type are both captured once up
   * front and checked again after the one await this method makes. Without that, switching either
   * selection mid-fetch would let a response for the *new* pair land on the *old* pair's tree and
   * conflict state.
   *
   * Also the only path for a refresh that leaves the grid usable — live events, the manual control,
   * discarding and resolving conflicts all come through here, with or without unsaved edits. With
   * nothing pending every node classifies as a refresh and the server's entries and stamp are
   * adopted, which is what a plain reload does; but this compares before it adopts, and reads the
   * pending edits after the fetch lands rather than before it is issued, so a change somebody else
   * made to a cell the user began editing while the request was in flight is flagged instead of
   * absorbed as the new baseline.
   *
   * It aborts whatever load was in flight, including a foreground one that was showing the loader,
   * so it clears the loader itself when it finishes un-aborted; see `#reloadEntries`.
   * @returns A promise that resolves when the pass is done and rejects if the fetch failed, so
   * the refresh control does not claim the data was updated.
   */
  async #reconcileWithServer(): Promise<void> {
    // No tree yet means the first load is still in flight (`#loadTree` empties the tree before it
    // starts). There is nothing to reconcile, and going on would abort that load — it is the one
    // that builds the tree — leaving the grid empty for good.
    if (!this._selectedRole || !this._selectedDocType || this._treeNodes.length === 0) return;
    const roleAlias = this._selectedRole.alias;
    const contentTypeKey = this._selectedDocType.key;

    this.#loadAbortController?.abort();
    const controller = new AbortController();
    this.#loadAbortController = controller;

    try {
      const editorNodes = await getDocTypePermissionsForEditor(roleAlias, contentTypeKey, controller.signal);
      // `_saving` too: a read that finished after the save began may predate its write, and would
      // be reconciled against as if it were somebody else's change.
      if (
        controller.signal.aborted ||
        this._selectedRole?.alias !== roleAlias ||
        this._selectedDocType?.key !== contentTypeKey
      ) {
        return;
      }
      // Not silently: the read was dropped, and nothing else will retry it. See `#missedWhileSaving`.
      if (this._saving) {
        this.#missedWhileSaving.defer();
        return;
      }

      const fresh = this.#groupByNode(editorNodes);
      // Starts from what is already flagged and only ever grows: a flag is removed by the user
      // acting on it, never by a later pass that happens to read the cell as unchanged.
      const conflicts = new Set(this._conflicts);
      const pendingAfter = new Map(this._pendingChanges);
      let droppedStale = false;

      for (const node of this.#flattenNodes(this._treeNodes)) {
        const apiKey = node.key === VIRTUAL_ROOT_LOCAL_KEY ? VIRTUAL_ROOT_NODE_KEY : node.key;
        // A node with no bucket has nothing stored, and holds the real empty-set stamp so that its
        // next save is concurrency-checked (see `stampOf`).
        const theirsForNode = fresh.get(apiKey) ?? { entries: [] as PermissionEntry[], stamp: EMPTY_SET_STAMP };
        const pendingForNode = this._pendingChanges.get(node.key);

        const base = new Map<string, ReadonlyArray<CellEntry>>([[VERB, this.#cellOf(node.entries)]]);
        const theirs = new Map<string, ReadonlyArray<CellEntry>>([[VERB, this.#cellOf(theirsForNode.entries)]]);
        const pending = pendingForNode
          ? new Map<string, ReadonlyArray<CellEntry>>([
              [
                VERB,
                pendingForNode.map((p) => ({ state: p.state, scope: p.scope, isPriorityOverride: p.isPriorityOverride })),
              ],
            ])
          : undefined;

        const alreadyFlagged = new Set<string>(
          this._conflicts.has(`${node.key}|${contentTypeKey}|${VERB}`) ? [VERB] : [],
        );

        const result = reconcileNode({ base, pending, theirs, conflicted: alreadyFlagged });
        for (const verb of result.conflictedVerbs) {
          conflicts.add(`${node.key}|${contentTypeKey}|${verb}`);
        }
        // The server moved and the user's entry is just the value that was loaded, so they hold
        // nothing here. The server's value is adopted below; an entry left behind would go out on
        // the next save under the fresh stamp and silently put the old value back.
        if (result.stalePendingVerbs.includes(VERB)) {
          pendingAfter.delete(node.key);
          droppedStale = true;
        }

        // Rebuilt from the per-verb merge, exactly as the node Permissions Editor does: a
        // conflicted verb keeps the entries it had before this event, a clean verb takes the
        // fresh ones. Never the raw server response — see reconcileNode's doc comment for why
        // that distinction is the entire point.
        const mergedEntries = this.#entriesFromCells(apiKey, roleAlias, result.nextBase.get(VERB) ?? []);

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

      if (
        controller.signal.aborted ||
        this._selectedRole?.alias !== roleAlias ||
        this._selectedDocType?.key !== contentTypeKey
      ) {
        return;
      }
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
   * Adopts the flagged-cell set a reconcile pass just computed, without disturbing
   * what is already on screen.
   *
   * A pass that finds exactly the cells already flagged leaves the state alone: swapping in an
   * equal set would re-render the grid and, worse, bring back a banner the person had dismissed
   * for those very cells. The banner returns only when a pass flags a cell that was not flagged
   * before, which is genuinely new news.
   * @param next The previous flags plus any this pass found, keyed `nodeKey|contentTypeKey|verb`.
   */
  #adoptConflicts(next: Set<string>): void {
    const previous = this._conflicts;
    const unchanged = next.size === previous.size && [...next].every((c) => previous.has(c));
    if (unchanged) return;
    if ([...next].some((c) => !previous.has(c))) this._bannerDismissed = false;
    this._conflicts = next;
  }

  /**
   * Reduces a node's stored entries to the fields a comparison cares about. Filters to `VERB`
   * defensively — every entry this editor deals with already carries that verb by construction —
   * so a comparison never silently includes something that does not belong to this editor's cell.
   */
  #cellOf(entries: PermissionEntry[]): CellEntry[] {
    return entries
      .filter((e) => e.verb === VERB)
      .map((e) => ({ state: e.state, scope: e.scope, isPriorityOverride: e.isPriorityOverride }));
  }

  /**
   * Expands a per-verb cell (as `reconcileNode` returns it) back into the flat entry list
   * `TreeNodeState.entries` holds. The synthesised `id` is display-only, the same convention the
   * node Permissions Editor uses when it fabricates entries client-side.
   *
   * Only `VERB` entries are rebuilt, and that loses nothing: a save here is built from the pending
   * entries alone and never from `node.entries`, so an entry for another verb dropped from a node
   * cannot be deleted from the server by a later save. (The node Permissions Editor, whose saves
   * do rebuild from `node.entries`, carries entries for unlisted verbs over instead — see
   * `flattenReconciled`.)
   */
  #entriesFromCells(nodeKey: string, roleAlias: string, cells: ReadonlyArray<CellEntry>): PermissionEntry[] {
    return cells.map((e, idx) => ({
      id: `${nodeKey}-${VERB}-${idx}`,
      nodeKey,
      roleAlias,
      verb: VERB,
      state: e.state,
      scope: e.scope,
      isPriorityOverride: e.isPriorityOverride,
    }));
  }

  /**
   * Walks the loaded tree depth-first.
   *
   * Recursive and private: the return type is annotated explicitly, otherwise TypeScript infers
   * `any` for a recursive private method (CLAUDE.md #4).
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
   * Resolves every currently-flagged node to its stored value, keeping every other pending change
   * untouched, then re-reads the server so the grid actually shows that stored value.
   *
   * Scoped to the flagged nodes on purpose — discarding everything would throw away edits
   * elsewhere in the tree that nobody is contesting. Because there is only one verb in this
   * editor, a flagged cell and its node coincide, so resolving the cell is exactly deleting that
   * node's whole pending entry.
   *
   * THE RE-READ IS NOT OPTIONAL — do not remove it as a redundant round trip. A conflicted node's
   * baseline is deliberately frozen (see `reconcileNode`): `node.entries` still holds the value
   * from *before* the other person's write, and the node's stamp was held back to match. Dropping
   * the pending change only removes the user's edit; it does not make the entries current. Without
   * the re-read the cell keeps showing the pre-conflict value, and — if anything were to advance
   * the stamp without the entries (which is what this method used to do, by applying a stamp it
   * had withheld) — the next save would send that stale value under a stamp the server accepts. The
   * other person's change would be silently reverted: no 409, no dialog, nothing to notice.
   *
   * The re-read fetches entries and stamp in the same response, so they cannot disagree. It goes
   * through the live-event controller, exactly as the manual refresh control does, which always
   * runs `#reconcileWithServer`, whether or not other edits survive. A plain reload replaces every
   * node's baseline, and for a node the user is still editing that would quietly absorb somebody
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
    const next = new Map(this._pendingChanges);

    for (const cell of this._conflicts) {
      const [nodeKey] = cell.split('|');
      next.delete(nodeKey!);
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
   * A refusal is not the end. When `#absorbSaveConflicts` finds nothing genuinely contested, the node
   * adopts what is stored and the save is retried here under the fresh stamp, without bothering
   * anybody. The retry is never `force`: it always carries the stamps the server just reported, so a
   * second collision is refused and asked about like the first.
   *
   * Refused outright while the tree is stale (`_treeStale`): the entries and stamps a comparison
   * would be made against are of unknown vintage.
   */
  async #saveChanges(): Promise<void> {
    if (!this._pendingChanges.size || !this._selectedRole || !this._selectedDocType || this._saving || this._treeStale) {
      return;
    }
    this._saving = true;
    const roleAlias = this._selectedRole.alias;
    const contentTypeKey = this._selectedDocType.key;

    try {
      for (let attempt = 0; attempt < MAX_SAVE_ATTEMPTS; attempt++) {
        const nodes = this.#buildSaveBatch(roleAlias, contentTypeKey);
        // Every pending edit turned out to be stale bookkeeping and was dropped.
        if (nodes.length === 0) return;

        const result = await saveDocTypePermissionsBatch(nodes);
        // The selection changed while the request was out; what came back is about another tree.
        if (this._selectedRole?.alias !== roleAlias || this._selectedDocType?.key !== contentTypeKey) return;

        if (result.ok) {
          await this.#finishSave();
          return;
        }

        // Not an error path. A conflict is the answer this feature exists to produce, and what
        // happens next is the user's decision, not this method's, unless nothing is contested.
        const outcome = this.#absorbSaveConflicts(result.conflicts, roleAlias, contentTypeKey);
        if (outcome === 'dialog') return;
        if (outcome === 'stalled') break;
      }

      this.#notificationContext?.peek('danger', {
        data: { message: this.#localize.term('uap_saveFailed', 'the stored permissions keep changing') },
      });
    } catch (err) {
      this.#notificationContext?.peek('danger', {
        data: { message: this.#localize.term('uap_saveFailed', String(err)) },
      });
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
   * Builds the batch a save sends: one item per node with a pending edit.
   *
   * Only one verb exists per node here, so the pending list already is the complete new entry list;
   * unlike the node Permissions Editor there is nothing else to merge it with.
   * @param roleAlias The user group being saved.
   * @param contentTypeKey The document type being saved.
   * @returns One item per node the tree can still find.
   */
  #buildSaveBatch(roleAlias: string, contentTypeKey: string): BatchSaveDocTypeNode[] {
    const nodes: BatchSaveDocTypeNode[] = [];

    for (const [nodeKey, pending] of this._pendingChanges) {
      const node = this.#findNode(nodeKey);
      if (!node) continue;

      nodes.push({
        nodeKey: nodeKey === VIRTUAL_ROOT_LOCAL_KEY ? VIRTUAL_ROOT_NODE_KEY : nodeKey,
        roleAlias,
        contentTypeKey,
        entries: pending.map((p) => ({
          verb: VERB,
          state: p.state,
          scope: p.scope,
          isPriorityOverride: p.isPriorityOverride,
        })),
        // Empty only for a stamp the read could not supply, which no read in this file produces:
        // a node with nothing stored holds the real empty-set stamp (`stampOf`), not `''`. Omitting
        // it would make the server skip the check for exactly the nodes that most need it.
        expectedStamp: expectedStampFor(node.stamp),
      });
    }

    return nodes;
  }

  /**
   * Completes a save the server accepted: clears the edits and flags, re-reads everything, and
   * tells the user.
   *
   * Entries and stamps are adopted only from the re-read (`#reloadEntries`), never from the stamps
   * the save returned. Adopting those first left the grid holding the old entries under new stamps
   * for as long as the re-read took, and for good if it failed. If the re-read fails the tree is
   * marked stale, which hides the grid and refuses to save until a read lands.
   */
  async #finishSave(): Promise<void> {
    this._pendingChanges = new Map();
    this.#resetLiveState();
    // Not landing (failed or superseded) means the grid is not known to match the server.
    if (!(await this.#reloadEntries())) this._treeStale = true;
    this.#notificationContext?.peek('positive', {
      data: { message: this.#localize.term('uap_permissionsSaved') },
    });
  }

  /**
   * Decides what a refused save means and acts on it. See `planSaveConflicts` for the rules.
   *
   * Every refused node is compared with the same `reconcileNode` a live event uses: the editor's
   * loaded value is the baseline, the refusal's `currentEntries` are the server's. The cell is
   * flagged only if both sides changed it, so a save-time refusal and a live event can never
   * disagree about what counts as a conflict. A refusal where the user's entry is just the value
   * that was loaded is not a conflict at all: the entry is dropped and the stored value adopted.
   *
   * Applied for every node, contested or not: entries advance to the stored values unless the cell
   * is flagged, and the stamp advances for a node that is not. A flagged cell keeps its old baseline
   * and the node keeps its stamp, so a plain save of it is refused again. A pending entry that is
   * just the old loaded value is dropped.
   *
   * The snapshot kept for each contested node is what "Overwrite anyway" retries under; see
   * `#overwriteConflicts`.
   * @param conflicts The refused node, user group and document type triples.
   * @param roleAlias The user group that was being saved.
   * @param contentTypeKey The document type that was being saved.
   * @returns `retry` when nothing is contested and the save should go again, `dialog` when the
   * confirmation dialog was opened, `stalled` when the refusal carried nothing new to retry with.
   */
  #absorbSaveConflicts(
    conflicts: BatchSaveDocTypeConflict[],
    roleAlias: string,
    contentTypeKey: string,
  ): 'retry' | 'dialog' | 'stalled' {
    const inputs: SaveConflictNode[] = [];
    const currentByKey = new Map<string, BatchSaveDocTypeConflict>();

    for (const conflict of conflicts) {
      const localKey = conflict.nodeKey === VIRTUAL_ROOT_NODE_KEY ? VIRTUAL_ROOT_LOCAL_KEY : conflict.nodeKey;
      const node = this.#findNode(localKey);
      if (!node) continue;

      const pendingForNode = this._pendingChanges.get(localKey);
      const flagged = new Set<string>(this._conflicts.has(`${localKey}|${contentTypeKey}|${VERB}`) ? [VERB] : []);

      currentByKey.set(localKey, conflict);
      inputs.push({
        key: localKey,
        base: new Map([[VERB, this.#cellOf(node.entries)]]),
        pending: pendingForNode
          ? new Map([[VERB, pendingForNode.map((p) => ({ state: p.state, scope: p.scope, isPriorityOverride: p.isPriorityOverride }))]])
          : undefined,
        theirs: new Map([[VERB, this.#cellOf(conflict.currentEntries)]]),
        flagged,
        heldStamp: node.stamp,
        currentStamp: conflict.currentStamp,
      });
    }

    const plan = planSaveConflicts(inputs);
    const flags = new Set(this._conflicts);
    const pendingAfter = new Map(this._pendingChanges);
    const snapshots = new Map<string, { entries: PermissionEntry[]; stamp: string }>();
    const lines: UapConflictLine[] = [];
    const verbName = this.#localize.term('uap_docTypePermissions_verbInsert');

    for (const planned of plan.nodes) {
      const conflict = currentByKey.get(planned.key)!;
      const apiKey = planned.key === VIRTUAL_ROOT_LOCAL_KEY ? VIRTUAL_ROOT_NODE_KEY : planned.key;
      this.#updateNode(planned.key, {
        entries: this.#entriesFromCells(apiKey, roleAlias, planned.nextBase.get(VERB) ?? []),
        ...(planned.adoptedStamp !== undefined ? { stamp: planned.adoptedStamp } : {}),
      });

      if (planned.stalePendingVerbs.includes(VERB)) pendingAfter.delete(planned.key);

      if (planned.conflictedVerbs.length === 0) continue;
      flags.add(`${planned.key}|${contentTypeKey}|${VERB}`);
      snapshots.set(planned.key, {
        entries: this.#entriesFromCells(apiKey, roleAlias, this.#cellOf(conflict.currentEntries)),
        stamp: planned.currentStamp,
      });

      const mine = pendingAfter.get(planned.key);
      if (!mine) continue;
      const isVirtualRoot = planned.key === VIRTUAL_ROOT_LOCAL_KEY;
      lines.push({
        nodeName: isVirtualRoot
          ? this.#localize.term('uap_contentRoot')
          : (this.#findNode(planned.key)?.name ?? planned.key),
        verbName,
        stored: this.#describeCell(conflict.currentEntries, isVirtualRoot),
        mine: this.#describeCell(mine, isVirtualRoot),
      });
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
   * time.
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
   * Renders a set of stored/pending entries as the short human-readable phrase the conflict
   * dialog shows for "stored" and "yours" — the same phrasing `uap-permission-scope-dialog` uses
   * for its live preview and the node Permissions Editor's conflict dialog reuses, reused here so
   * every surface describes a cell the same way.
   */
  #describeCell(
    entries: ReadonlyArray<{ state: PermissionState; scope: PermissionScope; isPriorityOverride: boolean }>,
    isVirtualRoot: boolean,
  ): string {
    const stateLabel = (s: 'allow' | 'deny'): string =>
      s === 'allow' ? this.#localize.term('uap_allow') : this.#localize.term('uap_deny');

    if (isVirtualRoot) {
      const first = entries[0];
      return first
        ? this.#localize.term('uap_previewVirtualSet', stateLabel(first.state === 'Allow' ? 'allow' : 'deny'))
        : this.#localize.term('uap_previewVirtualInherit');
    }

    const d = decomposeEntries(entries);
    const nodeState = d.nodeState;
    const descState = d.descState;

    if (nodeState === 'inherit' && descState === 'inherit') {
      return this.#localize.term('uap_previewBothInherit');
    }
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

  /**
   * Returns either pending entries (if any) or stored entries for the given node. The doc-type
   * editor has only one verb so we don't filter by verb the way the existing editor does.
   */
  #getCellEntries(node: TreeNodeState): PendingVerbEntries | PermissionEntry[] {
    const pending = this._pendingChanges.get(node.key);
    if (pending !== undefined) return pending;
    return node.entries;
  }

  /**
   * Computes the cell's display info. For the virtual-root row, an empty entry list defaults
   * to a visible Allow (rather than the usual Inherit dash) to reflect default-Allow semantics.
   */
  #getDisplayInfo(node: TreeNodeState): CellInfo {
    const entries = this.#getCellEntries(node);
    if (entries.length === 0 && node.key === VIRTUAL_ROOT_LOCAL_KEY) {
      return { split: false, nodeClass: 'allow', descClass: 'allow' };
    }
    return getCellInfo(entries);
  }

  // ── Selection panel ──────────────────────────────────────────────────────

  get #selectionGroups(): UapSelectorGroup[] {
    return [
      {
        options: [
          { id: 'group', label: this.#localize.term('uap_chooseRole'), icon: 'icon-users', ...(this._selectedRole ? { selectedName: this._selectedRole.name } : {}) },
        ],
      },
      {
        options: [
          { id: 'docType', label: this.#localize.term('uap_chooseDocType'), icon: this._selectedDocType?.icon ?? 'icon-document', ...(this._selectedDocType ? { selectedName: this._selectedDocType.name } : {}) },
        ],
      },
    ];
  }

  #onSelectorClick(id: string): void {
    if (id === 'group') void this.#openRolePicker();
    else if (id === 'docType') void this.#openDocTypePicker();
  }

  // ── Rendering ────────────────────────────────────────────────────────────

  #renderRows(nodes: TreeNodeState[], depth: number, contentTypeKey: string): TemplateResult[] {
    return nodes.flatMap((node) => [
      this.#renderRow(node, depth, contentTypeKey),
      ...(node.expanded && node.children ? this.#renderRows(node.children, depth + 1, contentTypeKey) : []),
    ]);
  }

  #renderRow(node: TreeNodeState, depth: number, contentTypeKey: string): TemplateResult {
    const hasPending = this._pendingChanges.has(node.key);
    const info = this.#getDisplayInfo(node);
    const conflicted = this._conflicts.has(`${node.key}|${contentTypeKey}|${VERB}`);
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
        <td class="perm-td ${conflicted ? 'conflicted' : ''}" @click=${() => this.#openPicker(node)}>
          <uap-perm-block
            .info=${info}
            ?pending=${hasPending}
            priority-override-title=${this.#localize.term('uap_priorityOverrideBadgeTitle')}></uap-perm-block>
        </td>
      </tr>
    `;
  }

  override render(): TemplateResult {
    const hasPending = this._pendingChanges.size > 0;
    const docTypeName = this._selectedDocType?.name ?? '';
    const contentTypeKey = this._selectedDocType?.key ?? '';
    const insertLabel = this.#localize.term('uap_docTypePermissions_verbInsert');
    const pickerVerbLabel = docTypeName ? `${insertLabel} – ${docTypeName}` : insertLabel;
    const pickerNodeName = this._pickerIsVirtualRoot
      ? this.#localize.term('uap_contentRoot')
      : (this._pickerNode?.name ?? '');

    return html`
      <umb-body-layout headline=${this.#localize.term('uap_docTypePermissions_workspaceTitle')}>
      <uap-page-intro
        surface="uap-doc-type-permissions"
        headline=${this.#localize.term('uap_docTypePermissions_workspaceTitle')}>
      </uap-page-intro>

      <uap-selection-panel
        .groups=${this.#selectionGroups}
        promptText=${this.#localize.term('uap_docTypePermissions_pickToStart')}
        ctaIcon="icon-document"
        orLabel=${this.#localize.term('uap_subjectOr')}
        ?clearable=${true}
        clearLabel=${this.#localize.term('uap_clearSelection')}
        @uap-selector-click=${(e: CustomEvent<{ id: string }>) => this.#onSelectorClick(e.detail.id)}
        @uap-selection-clear=${() => void this.#onClearSelection()}>

        ${hasPending
          ? html`
              <div slot="actions">
                <uui-button label=${this.#localize.term('uap_saveChanges')} look="primary" color="positive" ?loading=${this._saving} ?disabled=${this._treeStale} @click=${() => void this.#saveChanges()}>
                  ${this.#localize.term('uap_saveChanges')}
                </uui-button>
                <uui-button label=${this.#localize.term('uap_discard')} look="outline" @click=${() => this.#discardChanges()}>
                  ${this.#localize.term('uap_discard')}
                </uui-button>
              </div>
            `
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
                      <th class="verb-header">${this.#localize.term('uap_docTypePermissions_verbInsert')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    ${this.#renderRow(this._treeNodes[0], 0, contentTypeKey)}
                    ${this.#renderRows(this._treeNodes.slice(1), 1, contentTypeKey)}
                  </tbody>
                </table>
              </div>
            `
          : nothing}
      </uap-selection-panel>
      </umb-body-layout>

      <uap-permission-scope-dialog
        .verb=${pickerVerbLabel}
        .nodeName=${pickerNodeName}
        .isVirtualRoot=${this._pickerIsVirtualRoot}
        .inheritLabel=${this._pickerIsVirtualRoot ? '' : this.#localize.term('uap_docTypePermissions_notSet')}
        .initialNodeState=${this._pickerNodeState}
        .initialDescState=${this._pickerDescState}
        .initialSameAsNode=${this._pickerSameAsNode}
        .initialNodeIsPriorityOverride=${this._pickerNodeIsPriorityOverride}
        .initialDescIsPriorityOverride=${this._pickerDescIsPriorityOverride}
        @uap-scope-apply=${(e: CustomEvent<{ entries: PendingVerbEntries }>) => this.#handleScopeApply(e)}>
      </uap-permission-scope-dialog>

      <!-- Save-time conflict confirmation — same reasoning as the permission dialog above -->
      <uap-conflict-dialog
        @uap-conflict-cancel=${() => this.#closeConflictDialog()}
        @uap-conflict-load-stored=${() => { this.#closeConflictDialog(); this.#loadStoredForConflicts(); }}
        @uap-conflict-overwrite=${() => { this.#closeConflictDialog(); this.#overwriteConflicts(); }}>
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
      color: var(--uui-color-text-alt, #666);
    }
    th.node-header {
      width: 70%;
      text-align: left;
      padding-left: 8px;
      position: sticky;
      left: 0;
      z-index: 3;
      color: var(--uui-color-text, #333);
    }

    td { border-bottom: 1px solid var(--uui-color-border, #f0f0f0); }
    tr:hover td { background-color: var(--uui-color-surface-emphasis, #fafafa); }
    tr.row-pending td {
      background-color: color-mix(in srgb, oklch(85% 0.15 90) 12%, transparent);
    }

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

declare global {
  interface HTMLElementTagNameMap {
    'uap-doc-type-permissions-editor-root': UapDocTypePermissionsEditorRootElement;
  }
}

export default UapDocTypePermissionsEditorRootElement;
