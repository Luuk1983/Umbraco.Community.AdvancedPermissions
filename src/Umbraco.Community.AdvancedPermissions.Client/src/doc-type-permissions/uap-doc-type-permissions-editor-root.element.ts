import { html, css, nothing, customElement, state, query } from '@umbraco-cms/backoffice/external/lit';
import type { TemplateResult } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import { UMB_MODAL_MANAGER_CONTEXT } from '@umbraco-cms/backoffice/modal';
import type {
  RoleInfo,
  TreeNode,
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
  DocTypePermissionEntry,
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
import { UapSecurityEventsController, UAP_EVENT_SOURCES } from '../live/security-events.controller.js';
import { UapUnsavedChangesGuard, confirmDiscardUnsavedChanges } from '../live/unsaved-changes-guard.js';
import type { UapRefreshPhase } from '../live/refresh-phase.js';
import '../live/uap-live-refresh.element.js';
import type { CellEntry } from '../live/conflict.js';
import { reconcileNode } from '../live/reconcile.js';
import { EMPTY_SET_STAMP, conflictKey, parseConflictKey, cellsEqual, isStalePending } from './doc-type-concurrency.js';
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

/** One node's stored entries and the stamp that describes them, as the document-type read reports them. */
type NodeBucket = { entries: PermissionEntry[]; stamp: string };

/**
 * What a node the server did not mention looks like: nothing stored, and the empty-set stamp.
 *
 * `GetForEditor` returns a bucket only for nodes that have entries, so an absent bucket means an
 * empty set, not an unknown one. Giving it the real empty-set stamp (see {@link EMPTY_SET_STAMP})
 * is what keeps a first write to such a node concurrency-checked.
 */
const EMPTY_BUCKET: Readonly<NodeBucket> = { entries: [], stamp: EMPTY_SET_STAMP };

/**
 * Document-type Permissions Editor workspace.
 *
 * Mirrors the layout of the existing Permissions Editor with two differences:
 * 1. Two pickers in the toolbar - user group *and* document type - both required before the tree loads.
 * 2. One "Allowed" column instead of N verb columns. Each row's cell shows the resolved entry
 *    state for the chosen (user group, document type, node) triple.
 *
 * The virtual-root row defaults to a visible Allow when no entry exists, reflecting that the
 * doc-type resolver uses default-Allow semantics. The shared scope dialog handles editing;
 * saves go through the batch endpoint, all or nothing, with the same live-update reconciliation
 * and save-time conflict handling as the node Permissions Editor - see `#reconcileWithServer` and
 * `#saveChanges` below.
 *
 * A doc-type permission is keyed by node **plus** user group **plus** document type. The user
 * group and document type are fixed for the whole tree (there is one tree per pair), so the
 * per-node maps in this file (pending changes, stamps) need only the node key, and are discarded
 * whenever either selection changes. The conflict set is the exception: it names the document type
 * explicitly (`nodeKey|contentTypeKey|verb`), so a flag can never be matched against a different
 * document type's cell, and it is what the server's per-triple conflicts are translated into.
 *
 * The tree structure is borrowed from the *content* tree endpoints, whose nodes carry a content
 * entries list and a content stamp. Both are meaningless here and are never copied across: every
 * node's entries and stamp come from `getDocTypePermissionsForEditor`.
 */
@customElement('uap-doc-type-permissions-editor-root')
export class UapDocTypePermissionsEditorRootElement extends UmbLitElement {
  #localize = new UmbLocalizationController(this);

  // ── Metadata ────────────────────────────────────────────────────────────
  /** Every non-element document type, used to map the picker's chosen key back to a name and icon. */
  @state() private _docTypes: DocTypeListItem[] = [];

  // ── Selection & tree ────────────────────────────────────────────────────
  /** The user group being edited; `null` until one is picked or restored. */
  @state() private _selectedRole: RoleInfo | null = null;

  /** The document type being edited; `null` until one is picked or restored. */
  @state() private _selectedDocType: DocTypeListItem | null = null;

  /** The loaded tree. Index 0 is always the synthesised virtual root once a selection is complete. */
  @state() private _treeNodes: TreeNodeState[] = [];

  /** True while a first load or a selection change is running, which hides the grid behind the loader. */
  @state() private _loading = false;

  /**
   * True while a save is in flight. Doubles as the guard that stops the editor reacting to the
   * live-event echo of its own write, so it must stay set until the post-save reload has finished.
   */
  @state() private _saving = false;

  /** The last load or refresh failure, shown above the grid. */
  @state() private _error: string | null = null;

  /**
   * Pending changes: nodeKey -> the entries this editor wants stored for the selected
   * (user group, document type) at that node. An empty list means "clear".
   *
   * Never holds an entry equal to what is stored: applying the stored value removes the entry (see
   * `#handleScopeApply`), and reconciliation removes one the server has since moved past (see
   * `#reconcileWithServer`).
   */
  @state() private _pendingChanges: Map<string, PendingVerbEntries> = new Map();

  // ── Live updates & conflicts ────────────────────────────────────────────
  /**
   * Cells the server changed under an unsaved edit, keyed `nodeKey|contentTypeKey|verb`.
   *
   * Sticky by design: reconciliation only ever adds to it. A flag leaves on exactly four
   * occasions - the user loads the stored value, a save completes, the edits are discarded, or the
   * selection changes - because a flag that a later pass could clear would let a save through
   * silently over somebody else's write.
   */
  @state() private _conflicts: Set<string> = new Set();

  /** Whether the banner has been dismissed for the current set of conflicts. */
  @state() private _bannerDismissed = false;

  /** Where the refresh control is: idle, refreshing, or briefly confirming an update. */
  @state() private _refreshPhase: UapRefreshPhase = 'idle';

  // ── Permission dialog ───────────────────────────────────────────────────
  /** The node whose cell opened the scope dialog. */
  @state() private _pickerNode: TreeNodeState | null = null;

  /** Whether the dialog is editing the virtual root, which has no descendant side. */
  @state() private _pickerIsVirtualRoot = false;

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

    // A clean editor takes the server's version in place; a dirty one is classified cell by cell
    // and nothing of the user's is touched. The keys are ignored deliberately - a permission
    // written on an ancestor moves what every descendant on screen resolves to, so there is no
    // subset worth refetching.
    // Sources: Listens for document-type create-permission writes, and for `access` because deleting a user
    // group or content node removes stored document-type entries directly, with no permission
    // notification of its own. It must NOT listen to `elementTypePermissions`: that table serves the
    // Element Type editor too, and the server splits the two sources so the editors do not wake for
    // each other's saves. Content-node and library writes are irrelevant here.
    this.#liveEvents = new UapSecurityEventsController(
      this,
      async () => {
        if (!this._selectedRole || !this._selectedDocType) return;
        // Nothing is reconciled while this editor's own save is in flight. The write raises an
        // event of its own, and the event carries no identity, so it cannot be told from a
        // colleague's; reconciling against it would flag the user's own save as a conflict. It is
        // safe to drop because a finished save reloads everything and clears the pending and
        // conflict state, and a refused one hands back the server's current values itself.
        if (this._saving) return;
        // Both paths refresh in the background: nothing here is a selection change, so the grid
        // stays on screen and only the values (and conflict flags) change underneath.
        if (this._pendingChanges.size === 0) {
          await this.#reloadEntries(true);
          return;
        }
        await this.#reconcileWithServer();
      },
      (phase) => { this._refreshPhase = phase; },
      [
        UAP_EVENT_SOURCES.docTypePermissions,
        UAP_EVENT_SOURCES.access,
      ],
    );
  }

  /**
   * Clears every trace of an unresolved live conflict: the flagged cells and the dismissed-banner
   * flag.
   *
   * Called wherever the tree itself is being replaced or abandoned - switching user group or
   * document type, or clearing the selection - and after a completed save or a resolved conflict,
   * so a conflict against data that has since been replaced cannot outlive it.
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
   * The reload is not cosmetic. A conflicted node's entries are frozen at what they were before
   * the other person's write and its stamp was never advanced, so leaving it alone would keep both
   * out of date and the next save would be refused against a value nobody is editing any more - a
   * conflict dialog with no conflict behind it. Re-reading adopts the server's current stamps along
   * with its entries.
   *
   * Runs in the background so the grid stays on screen: the user asked to drop their edits, not
   * to watch the page reload.
   */
  #discardChanges(): void {
    this._pendingChanges = new Map();
    this.#resetLiveState();
    void this.#reloadEntries(true);
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
    this._selectedRole = null;
    this._selectedDocType = null;
    this._treeNodes = [];
    this._pendingChanges = new Map();
    this.#resetLiveState();
    this._error = null;
    clearSelection(SURFACE_ID);
  }

  // ── Data loading ────────────────────────────────────────────────────────

  /** Loads the document-type list used to resolve the picker's chosen key to a name and icon. */
  async #loadMeta(): Promise<void> {
    try {
      this._docTypes = await getDocTypes();
    } catch (err) {
      this._error = String(err);
    }
  }

  /**
   * Lets the user pick a user group, then switches to it.
   *
   * Asks about unsaved edits only after a group has actually been chosen, so cancelling the picker
   * costs nothing, and picking the group that is already selected changes nothing at all.
   */
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
    // their edits.
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
   *
   * Asks about unsaved edits only after a document type has actually been chosen, and treats
   * picking the document type that is already selected as a no-op, for the same reasons as
   * {@link UapDocTypePermissionsEditorRootElement.#openRolePicker}.
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

    // The same document type again changes nothing, so it must not cost the user their edits.
    if (this._pendingChanges.size > 0 && picked?.key === this._selectedDocType?.key) return;
    // Asked after the pick, not before opening the picker: cancelling the picker then costs nothing.
    if (!(await this.#confirmDiscard())) return;

    const hadTree = this._treeNodes.length > 0 && this._selectedRole !== null && this._selectedDocType !== null;
    this._selectedDocType = picked;
    this._pendingChanges = new Map();
    this.#resetLiveState();
    this.#persistSelection();
    if (!this._selectedDocType) {
      // Whatever load was in flight belongs to the document type that has just been dropped.
      this.#loadAbortController?.abort();
      this._loading = false;
      this._treeNodes = [];
      return;
    }
    if (hadTree) {
      void this.#reloadEntries();
    } else if (this._selectedRole) {
      void this.#loadTree();
    }
  }

  /**
   * Builds the tree from scratch for the selected user group and document type: the virtual root
   * plus the content root level. Used for the first load and whenever there is no tree to reload
   * into.
   *
   * The content tree endpoint supplies only the structure. Its per-node entries and stamp describe
   * *content* permissions for `$everyone`, so both are replaced with the document-type ones from
   * `getDocTypePermissionsForEditor` (see {@link UapDocTypePermissionsEditorRootElement.#toTreeNodeState}).
   * That single read also carries the virtual root's entries and stamp, so it needs no read of
   * its own.
   */
  async #loadTree(): Promise<void> {
    if (!this._selectedRole || !this._selectedDocType) return;

    this.#loadAbortController?.abort();
    const controller = new AbortController();
    this.#loadAbortController = controller;

    this._loading = true;
    this._error = null;
    this._treeNodes = [];

    try {
      const [editorNodes, nodes] = await Promise.all([
        getDocTypePermissionsForEditor(this._selectedRole.alias, this._selectedDocType.key, controller.signal),
        getTreeRoot('$everyone', controller.signal),
      ]);
      if (controller.signal.aborted) return;

      const byNode = this.#groupByNode(editorNodes);

      // Synthesised here rather than returned by the tree endpoint. It holds real entries, so it
      // needs its real stamp: an empty one would either never match (a conflict dialog for a
      // change nobody made) or, if it made the save omit the check, leave it the one node with no
      // protection at all.
      const virtualBucket = byNode.get(VIRTUAL_ROOT_NODE_KEY) ?? EMPTY_BUCKET;
      const virtualRoot: TreeNodeState = {
        key: VIRTUAL_ROOT_LOCAL_KEY,
        name: this.#localize.term('uap_contentRoot'),
        icon: 'icon-globe',
        hasChildren: false,
        entries: virtualBucket.entries,
        stamp: virtualBucket.stamp,
        expanded: false,
        loading: false,
      };
      this._treeNodes = [virtualRoot, ...nodes.map((n) => this.#toTreeNodeState(n, byNode))];
    } catch (err) {
      if (controller.signal.aborted) return;
      this._error = String(err);
    } finally {
      if (!controller.signal.aborted) this._loading = false;
    }
  }

  /**
   * Reloads only the entries and stamps for the current tree, without rebuilding it. Preserves
   * expanded state and existing children.
   *
   * Used for a user-group or document-type switch, a live or manual refresh of a clean editor,
   * discarding, and the refresh after a save. A switch (`background` false) blanks the entries and
   * shows the loader, because what is on screen belongs to the previous pair. A refresh
   * (`background` true) is the same pair asked again: the current entries stay on screen and are
   * replaced when the response arrives, so the grid neither flashes nor loses the reader's place.
   *
   * One request covers every node, at any depth: `getDocTypePermissionsForEditor` answers for the
   * whole (user group, document type) pair, so there is no per-level batching to do here.
   * @param background True to refresh in place without any loading state.
   * @returns A promise that resolves when done. In background mode it rejects on failure, so the
   * refresh control does not claim the data was updated.
   */
  async #reloadEntries(background = false): Promise<void> {
    if (!this._selectedRole || !this._selectedDocType || this._treeNodes.length === 0) return;

    this.#loadAbortController?.abort();
    const controller = new AbortController();
    this.#loadAbortController = controller;

    if (!background) {
      this._loading = true;
      this._error = null;
      // Emptied first so that a failed read leaves an empty grid rather than the previous pair's
      // values under the new pair's name. The stamps are left as they are: they no longer match
      // anything, so a save made from that state is refused rather than written.
      this.#clearEntriesRecursive(this._treeNodes);
    }

    try {
      const editorNodes = await getDocTypePermissionsForEditor(
        this._selectedRole.alias,
        this._selectedDocType.key,
        controller.signal,
      );
      if (controller.signal.aborted) return;

      this.#applyBucketsRecursive(this._treeNodes, this.#groupByNode(editorNodes));
      this._treeNodes = [...this._treeNodes];
    } catch (err) {
      if (controller.signal.aborted) return;
      this._error = String(err);
      if (background) throw err;
    } finally {
      // Cleared by whichever load finishes un-superseded, background included. A live refresh
      // aborts a selection-change reload that was still showing the loader, and that reload's own
      // `finally` then skips clearing it, so leaving this to foreground loads would strand the
      // grid behind the loader for good.
      if (!controller.signal.aborted) this._loading = false;
    }
  }

  /**
   * Empties the entries of every loaded node so a selection change never shows the previous
   * pair's values while the new ones load.
   * @param nodes The nodes to clear, recursing into their loaded children.
   */
  #clearEntriesRecursive(nodes: TreeNodeState[]): void {
    for (const node of nodes) {
      node.entries = [];
      if (node.children) this.#clearEntriesRecursive(node.children);
    }
  }

  /**
   * Bins the editor's grouped-by-node response into a lookup keyed by node key, translating each
   * node's `DocTypePermissionEntry` list into the generic `PermissionEntry` shape the tree state
   * holds. Each bucket's stamp is the *document-type* stamp of that node's entries.
   * @param nodes The response of `getDocTypePermissionsForEditor`.
   * @returns The buckets, keyed by API node key (`VIRTUAL_ROOT_NODE_KEY` for the virtual root). A
   * node with nothing stored has no bucket; use {@link EMPTY_BUCKET} for it.
   */
  #groupByNode(nodes: DocTypeEditorNode[]): Map<string, NodeBucket> {
    const map = new Map<string, NodeBucket>();
    for (const n of nodes) {
      map.set(n.nodeKey, { entries: this.#toPermissionEntries(n.entries), stamp: n.stamp });
    }
    return map;
  }

  /**
   * Drops the document-type-specific fields from stored entries, leaving the generic shape the
   * tree state holds. The document type is implied by the whole editor's selection, so it is
   * carried by the selection and not by each entry.
   * @param entries The stored document-type entries.
   * @returns The same entries as `PermissionEntry` values.
   */
  #toPermissionEntries(entries: ReadonlyArray<DocTypePermissionEntry>): PermissionEntry[] {
    return entries.map((e) => ({
      id: e.id,
      nodeKey: e.nodeKey,
      roleAlias: e.roleAlias,
      verb: e.verb,
      state: e.state,
      scope: e.scope,
      isPriorityOverride: e.isPriorityOverride,
    }));
  }

  /**
   * Turns a node from the content tree endpoints into this editor's tree node, keeping only its
   * structure and taking entries and stamp from the document-type buckets.
   *
   * Deliberately picks fields rather than spreading the node: the content node's own `entries`
   * and `stamp` describe content permissions, and a stamp from there sent with a document-type
   * save would be compared against the wrong thing.
   * @param node The node as the content tree endpoint returned it.
   * @param byNode The document-type buckets for the selected pair.
   * @returns The node, collapsed, carrying its document-type entries and stamp.
   */
  #toTreeNodeState(node: TreeNode, byNode: ReadonlyMap<string, NodeBucket>): TreeNodeState {
    const bucket = byNode.get(node.key) ?? EMPTY_BUCKET;
    return {
      key: node.key,
      name: node.name,
      icon: node.icon,
      hasChildren: node.hasChildren,
      entries: bucket.entries,
      stamp: bucket.stamp,
      expanded: false,
      loading: false,
    };
  }

  /**
   * Walks the loaded tree and sets each node's `entries` and `stamp` from the supplied buckets. A
   * node without a bucket has nothing stored, so it takes an empty list and the empty-set stamp.
   * The virtual-root row is keyed locally; the lookup uses `VIRTUAL_ROOT_NODE_KEY` for it.
   * @param nodes The nodes to update, recursing into their loaded children.
   * @param byNode The buckets from {@link UapDocTypePermissionsEditorRootElement.#groupByNode}.
   */
  #applyBucketsRecursive(nodes: TreeNodeState[], byNode: ReadonlyMap<string, NodeBucket>): void {
    for (const node of nodes) {
      const lookupKey = node.key === VIRTUAL_ROOT_LOCAL_KEY ? VIRTUAL_ROOT_NODE_KEY : node.key;
      const bucket = byNode.get(lookupKey) ?? EMPTY_BUCKET;
      node.entries = bucket.entries;
      node.stamp = bucket.stamp;
      if (node.children) this.#applyBucketsRecursive(node.children, byNode);
    }
  }

  /**
   * Expands or collapses a node, fetching its children the first time.
   *
   * The children's entries and stamps come from the document-type read, not from the content tree
   * call that supplies their structure. The selection is captured before the awaits and checked
   * after them, so a response that arrives after the user switched user group or document type is
   * dropped rather than written into the new pair's tree.
   * @param node The node whose toggle was clicked.
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
    const roleAlias = this._selectedRole!.alias;
    const contentTypeKey = this._selectedDocType!.key;
    this.#updateNode(node.key, { loading: true });
    try {
      const children = await getTreeChildren(node.key, '$everyone');
      const editorNodes = await getDocTypePermissionsForEditor(roleAlias, contentTypeKey);
      if (this._selectedRole?.alias !== roleAlias || this._selectedDocType?.key !== contentTypeKey) return;

      const byNode = this.#groupByNode(editorNodes);
      this.#updateNode(node.key, {
        expanded: true,
        loading: false,
        children: children.map((c) => this.#toTreeNodeState(c, byNode)),
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
  #updateNode(key: string, changes: Partial<TreeNodeState>): void {
    this._treeNodes = updateNode(this._treeNodes, key, changes);
  }

  // ── Permission dialog ───────────────────────────────────────────────────

  /**
   * Opens the scope dialog for one cell, seeded from the cell's pending value if it has one and
   * its stored value otherwise.
   * @param node The node whose cell was clicked.
   */
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
   * Handles `uap-scope-apply` from the shared scope dialog: records the composed entries as the
   * pending change for the cell that opened the dialog - unless they are what is already stored,
   * in which case the cell's pending change is removed instead.
   *
   * The dialog's Apply is unconditional, so applying the stored value, or changing a cell and
   * changing it back, would otherwise leave a pending entry equal to the baseline. That entry is
   * not an edit, but it looks like one to everything downstream: a save would send it, and if a
   * colleague then changed the cell the entry would revert their write under a fresh stamp with
   * nothing to say so.
   * @param e The apply event carrying the composed entries.
   */
  #handleScopeApply(e: CustomEvent<{ entries: PendingVerbEntries }>): void {
    if (!this._pickerNode) return;
    const nodeKey = this._pickerNode.key;
    // Looked up afresh rather than read off the dialog's snapshot: the stored value may have moved
    // since the dialog opened.
    const stored = this.#cellOf(this.#findNode(nodeKey)?.entries ?? this._pickerNode.entries);
    const applied: CellEntry[] = e.detail.entries.map((p) => ({
      state: p.state,
      scope: p.scope,
      isPriorityOverride: p.isPriorityOverride,
    }));

    const next = new Map(this._pendingChanges);
    if (cellsEqual(stored, applied)) {
      next.delete(nodeKey);
    } else {
      next.set(nodeKey, e.detail.entries);
    }
    this._pendingChanges = next;
  }

  // ── Live reconciliation ─────────────────────────────────────────────────

  /**
   * Refetches the stored values and works out, node by node, what a live change means for an
   * editor holding unsaved work.
   *
   * The decision itself is delegated to the pure, unit-tested `reconcileNode`, with a single verb
   * (`VERB`) standing in for the verb dimension the node Permissions Editor iterates over. There
   * is one request to make: `getDocTypePermissionsForEditor` answers for the whole (user group,
   * document type) pair whatever the tree depth, so there is no "not fetched this pass" gap to
   * skip around. A node absent from the response has nothing stored, and reconciles against an
   * empty cell like any other value.
   *
   * What is written back is always `reconcileNode`'s `nextBase`, never the raw server response: a
   * conflicted verb must keep the entries it had, or the next pass would read it as unchanged and
   * the flag would silently stop meaning anything. A node that is flagged keeps its stamp for the
   * same reason.
   *
   * Where the verdict is that the server moved and the user's pending entry is just the value that
   * was loaded, the entry is dropped along with adopting the server's value. Adopting a value
   * while a pending entry still says otherwise would leave the grid, and the next save, showing the
   * old value under the new stamp - a silent revert of the other person's write.
   *
   * Cancellable and selection-guarded like the other loaders in this file: the fetch is assigned
   * to `#loadAbortController`, and the selected user group and document type are captured once up
   * front and checked again after the one await this method makes. Without that, switching either
   * selection mid-fetch would let a response for the *new* pair land on the *old* pair's tree and
   * conflict state.
   * @returns A promise that resolves when the pass is done and rejects if the fetch failed, so
   * the refresh control does not claim the data was updated.
   */
  async #reconcileWithServer(): Promise<void> {
    if (!this._selectedRole || !this._selectedDocType) return;
    const roleAlias = this._selectedRole.alias;
    const contentTypeKey = this._selectedDocType.key;

    this.#loadAbortController?.abort();
    const controller = new AbortController();
    this.#loadAbortController = controller;

    /** Whether this pass has been overtaken by a selection change, a newer load or the editor's own save. */
    const superseded = (): boolean =>
      controller.signal.aborted ||
      this._selectedRole?.alias !== roleAlias ||
      this._selectedDocType?.key !== contentTypeKey ||
      this._saving;

    try {
      const editorNodes = await getDocTypePermissionsForEditor(roleAlias, contentTypeKey, controller.signal);
      // `_saving` too: a read that finished after the save began may predate its write, and would
      // be reconciled against as if it were somebody else's change.
      if (superseded()) return;

      const fresh = this.#groupByNode(editorNodes);
      // Starts from what is already flagged and only ever grows: a flag is removed by the user
      // acting on it, never by a later pass that happens to read the cell as unchanged.
      const conflicts = new Set(this._conflicts);
      const stalePending = new Set<string>();

      for (const node of this.#flattenNodes(this._treeNodes)) {
        const apiKey = node.key === VIRTUAL_ROOT_LOCAL_KEY ? VIRTUAL_ROOT_NODE_KEY : node.key;
        const theirsForNode = fresh.get(apiKey) ?? EMPTY_BUCKET;
        const pendingForNode = this._pendingChanges.get(node.key);

        const baseCell = this.#cellOf(node.entries);
        const theirsCell = this.#cellOf(theirsForNode.entries);
        const pendingCell: CellEntry[] | undefined = pendingForNode?.map((p) => ({
          state: p.state,
          scope: p.scope,
          isPriorityOverride: p.isPriorityOverride,
        }));

        const base = new Map<string, ReadonlyArray<CellEntry>>([[VERB, baseCell]]);
        const theirs = new Map<string, ReadonlyArray<CellEntry>>([[VERB, theirsCell]]);
        const pending = pendingCell ? new Map<string, ReadonlyArray<CellEntry>>([[VERB, pendingCell]]) : undefined;

        const flagKey = conflictKey(node.key, contentTypeKey, VERB);
        const alreadyFlagged = new Set<string>(this._conflicts.has(flagKey) ? [VERB] : []);

        const result = reconcileNode({ base, pending, theirs, conflicted: alreadyFlagged });
        for (const verb of result.conflictedVerbs) {
          conflicts.add(conflictKey(node.key, contentTypeKey, verb));
        }
        // A flagged cell keeps its pending entry whatever else is true of it: that is a decision
        // for the user. Only an entry the verdict says is not an edit at all is dropped.
        if (pendingCell && !result.conflictedVerbs.includes(VERB) && isStalePending(baseCell, pendingCell, theirsCell)) {
          stalePending.add(node.key);
        }

        // Rebuilt from the per-verb merge: a conflicted verb keeps the entries it had before this
        // event, a clean verb takes the fresh ones.
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

      if (superseded()) return;

      if (stalePending.size > 0) {
        const next = new Map(this._pendingChanges);
        for (const key of stalePending) next.delete(key);
        this._pendingChanges = next;
      }
      this.#adoptConflicts(conflicts);
    } catch (err) {
      if (controller.signal.aborted) return;
      this._error = String(err);
      // Rethrown so the refresh control does not claim the data was updated.
      throw err;
    } finally {
      // This pass aborted whatever load was running when it started, and that load's own `finally`
      // skips clearing the loader for exactly that reason, so it has to be cleared here.
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
   * Flattens a per-verb `nextBase` merge (as `reconcileNode` returns it) back into the flat
   * entry list `TreeNodeState.entries` holds. The synthesised `id` is display-only, the same
   * convention the node Permissions Editor uses when it fabricates entries client-side.
   *
   * Entries for verbs `nextBase` does not cover are carried over from the fresh read rather than
   * dropped. `nextBase` only knows `VERB`, and a save rebuilds a node's whole entry list from
   * `node.entries`, so an entry lost here would be deleted from the server by the next save of a
   * node the user never touched that verb on.
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
   * Reduces a node's stored entries to the fields a comparison cares about. Filters to `VERB`
   * because the stored list carries every verb stored for the triple, and only this editor's
   * verb is a cell here.
   * @param entries The node's entries.
   * @returns The cell's entries.
   */
  #cellOf(entries: ReadonlyArray<PermissionEntry>): CellEntry[] {
    return entries
      .filter((e) => e.verb === VERB)
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
   * throw away edits elsewhere in the tree that nobody is contesting - which is the outcome this
   * whole feature exists to avoid. With one verb, a flagged cell and its node's pending change
   * coincide, so resolving the cell is dropping that node's pending entry.
   *
   * THE RE-READ IS NOT OPTIONAL - do not remove it as a redundant round trip. A conflicted verb's
   * baseline is deliberately frozen (see `reconcileNode`): `node.entries` still holds the value
   * from *before* the other person's write, and the node's stamp was held back to match. Dropping
   * the pending change only removes the user's edit; it does not make the entries current. Without
   * the re-read the cell keeps showing the pre-conflict value, and - if anything were to advance
   * the stamp without the entries - `#saveChanges`, which builds its request from `node.entries`
   * plus pending changes, would send that stale value under a stamp the server accepts. The other
   * person's change would be silently reverted: no 409, no dialog, nothing to notice.
   *
   * The re-read fetches entries and stamp in the same response, so they cannot disagree. It goes
   * through the live-event controller, exactly as the manual refresh control does: that runs
   * `#reloadEntries(true)` when nothing is pending any more, and `#reconcileWithServer` when other
   * edits survive. The split matters. A plain reload replaces every node's baseline, and for a
   * cell the user is still editing that would quietly absorb somebody else's write that had landed
   * but not yet been reconciled - the pending change would then overwrite it under a fresh stamp.
   * Reconciling compares first and only adopts what is not in dispute. It also queues behind any
   * run already in flight instead of aborting it.
   *
   * Until that read lands, the nodes still hold their old stamp, so a save made in the meantime is
   * refused by the server rather than written stale.
   *
   * Used for both the banner's "Load stored values" and the save-time conflict dialog's, so the
   * two cannot end in different states.
   */
  #loadStoredForConflicts(): void {
    const next = new Map(this._pendingChanges);
    const currentContentTypeKey = this._selectedDocType?.key.toLowerCase();

    for (const cell of this._conflicts) {
      const { nodeKey, contentTypeKey, verb } = parseConflictKey(cell);
      // A flag for another document type cannot name a cell of the tree on screen. Flags are reset
      // whenever the selection changes, so this is defence against a future path forgetting to.
      if (contentTypeKey !== currentContentTypeKey || verb !== VERB) continue;
      next.delete(nodeKey);
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
   *
   * Each request node names the full triple - node, user group and document type - and carries the
   * stamp of *that* triple's entries as read from `getDocTypePermissionsForEditor`. The stamp on
   * the tree node is that one and never the content stamp the content tree endpoint first
   * supplied (see {@link UapDocTypePermissionsEditorRootElement.#toTreeNodeState}).
   * @param force Whether to write through a stale stamp. Only true after the user has confirmed
   * via the conflict dialog, and the dialog's overwrite handler is the only caller that passes it.
   */
  async #saveChanges(force = false): Promise<void> {
    if (!this._pendingChanges.size || !this._selectedRole || !this._selectedDocType || this._saving) return;
    this._saving = true;
    const roleAlias = this._selectedRole.alias;
    const contentTypeKey = this._selectedDocType.key;

    try {
      const nodes: BatchSaveDocTypeNode[] = [];

      for (const [nodeKey, pending] of this._pendingChanges) {
        const node = this.#findNode(nodeKey);
        if (!node) continue;

        // The server replaces the whole triple, every verb, so the request is built from what is
        // stored plus this editor's one change. Entries for other verbs are carried through
        // untouched: leaving them out would delete them from the server.
        const others = node.entries
          .filter((e) => e.verb !== VERB)
          .map((e) => ({ verb: e.verb, state: e.state, scope: e.scope, isPriorityOverride: e.isPriorityOverride }));
        const mine = pending.map((p) => ({
          verb: VERB,
          state: p.state,
          scope: p.scope,
          isPriorityOverride: p.isPriorityOverride,
        }));

        // Two different things look like "no stamp", and only one of them may skip the check.
        // A node with nothing stored has a real stamp (the empty-set one, see `EMPTY_BUCKET`), and
        // is checked like any other. An empty string is a genuinely absent stamp - the server sent
        // none - and sending it as `''` would compare as a value that never matches, so it is
        // omitted and the server skips the check for that node.
        nodes.push({
          nodeKey: nodeKey === VIRTUAL_ROOT_LOCAL_KEY ? VIRTUAL_ROOT_NODE_KEY : nodeKey,
          roleAlias,
          contentTypeKey,
          entries: [...others, ...mine],
          expectedStamp: node.stamp === '' ? undefined : node.stamp,
        });
      }

      const result = await saveDocTypePermissionsBatch(nodes, force);

      if (!result.ok) {
        // Not an error path. A conflict is the answer this feature exists to produce, and what
        // happens next is the user's decision, not this method's.
        this.#openConflictDialog(result.conflicts);
        return;
      }

      // Adopt the stamps the server just handed back before reloading, so the nodes this save
      // touched are immediately correct even though the reload below re-derives the same thing.
      // Keyed by the full triple: a stamp for another user group or document type is not this
      // tree's, and is skipped.
      for (const [key, stamp] of result.stamps) {
        const [savedNodeKey, savedRoleAlias, savedContentTypeKey] = key.split('|');
        if (savedRoleAlias !== roleAlias || savedContentTypeKey?.toLowerCase() !== contentTypeKey.toLowerCase()) continue;
        const localKey = savedNodeKey === VIRTUAL_ROOT_NODE_KEY ? VIRTUAL_ROOT_LOCAL_KEY : savedNodeKey!;
        this.#updateNode(localKey, { stamp });
      }

      await this.#reloadEntries();
      this._pendingChanges = new Map();
      this.#resetLiveState();
      this.#notificationContext?.peek('positive', { data: { message: this.#localize.term('uap_permissionsSaved') } });
    } catch (err) {
      this.#notificationContext?.peek('danger', { data: { message: this.#localize.term('uap_saveFailed', String(err)) } });
    } finally {
      this._saving = false;
    }
  }

  /**
   * Turns the server's triple-level conflict list into per-node lines for the confirmation
   * dialog, flags the same cells on the grid, and opens the dialog.
   *
   * The server names each conflict by node, user group and document type. A conflict for a pair
   * other than the one on screen (the selection changed while the save was in flight) describes a
   * tree that is no longer here, and is ignored rather than written into this one.
   *
   * The affected nodes' entries are advanced to what the server just reported, but their stamp is
   * left alone, exactly as for a live-detected conflict - `#loadStoredForConflicts` resolves both
   * the same way, by re-reading the server rather than trusting either half of what is held here.
   * The advanced entries are what keeps a confirmed overwrite from reverting the node's other
   * verbs; the flags set here are sticky, so the advanced baseline cannot make a later pass read
   * the cell as clean.
   * @param conflicts The conflicting triples the server refused to overwrite.
   */
  #openConflictDialog(conflicts: BatchSaveDocTypeConflict[]): void {
    const lines: UapConflictLine[] = [];
    const flagged = new Set(this._conflicts);
    const verbName = this.#localize.term('uap_docTypePermissions_verbInsert');
    const roleAlias = this._selectedRole?.alias;
    const currentContentTypeKey = this._selectedDocType?.key.toLowerCase();

    for (const conflict of conflicts) {
      if (conflict.roleAlias !== roleAlias || conflict.contentTypeKey.toLowerCase() !== currentContentTypeKey) continue;

      const localKey = conflict.nodeKey === VIRTUAL_ROOT_NODE_KEY ? VIRTUAL_ROOT_LOCAL_KEY : conflict.nodeKey;
      const isVirtualRoot = localKey === VIRTUAL_ROOT_LOCAL_KEY;
      const nodeName = isVirtualRoot
        ? this.#localize.term('uap_contentRoot')
        : (this.#findNode(localKey)?.name ?? localKey);

      this.#updateNode(localKey, { entries: this.#toPermissionEntries(conflict.currentEntries) });

      const pending = this._pendingChanges.get(localKey);
      if (!pending) continue;

      flagged.add(conflictKey(localKey, conflict.contentTypeKey, VERB));
      lines.push({
        nodeName,
        verbName,
        stored: this.#describeCell(conflict.currentEntries.filter((e) => e.verb === VERB), isVirtualRoot),
        mine: this.#describeCell(pending, isVirtualRoot),
      });
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
   * Renders a set of stored/pending entries as the short human-readable phrase the conflict
   * dialog shows for "stored" and "yours" - the same phrasing `uap-permission-scope-dialog` uses
   * for its live preview, reused here so every surface describes a cell the same way.
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

    // Read the same way `#openPicker` reads it: the virtual root is one entry, whatever its scope.
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
  #findNode(key: string): TreeNodeState | null {
    return findNode(this._treeNodes, key);
  }

  /**
   * The entries a cell should currently display: its pending value if it has one, otherwise the
   * stored entries for this editor's verb. Filtered to `VERB` because the stored list carries
   * every verb stored for the triple, and only this one is shown.
   * @param node The node the cell belongs to.
   * @returns The pending entries, or the stored entries for `VERB`.
   */
  #getCellEntries(node: TreeNodeState): PendingVerbEntries | PermissionEntry[] {
    const pending = this._pendingChanges.get(node.key);
    if (pending !== undefined) return pending;
    return node.entries.filter((e) => e.verb === VERB);
  }

  /**
   * Computes the cell's display info. For the virtual-root row, an empty entry list defaults
   * to a visible Allow (rather than the usual Inherit dash) to reflect default-Allow semantics.
   * @param node The node the cell belongs to.
   * @returns What the permission block should draw.
   */
  #getDisplayInfo(node: TreeNodeState): CellInfo {
    const entries = this.#getCellEntries(node);
    if (entries.length === 0 && node.key === VIRTUAL_ROOT_LOCAL_KEY) {
      return { split: false, nodeClass: 'allow', descClass: 'allow' };
    }
    return getCellInfo(entries);
  }

  // ── Selection panel ──────────────────────────────────────────────────────

  /** The selector options for the selection panel: a user-group picker and a document-type picker. */
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

  /**
   * Routes a click on one of the selection panel's controls to its picker.
   * @param id The id of the selector option that was clicked.
   */
  #onSelectorClick(id: string): void {
    if (id === 'group') void this.#openRolePicker();
    else if (id === 'docType') void this.#openDocTypePicker();
  }

  // ── Rendering ────────────────────────────────────────────────────────────

  /**
   * Renders a level of the tree and, for expanded nodes, everything beneath it.
   *
   * Recursive and private, so the return type is annotated explicitly (CLAUDE.md #4).
   * @param nodes The sibling nodes to render.
   * @param depth The nesting depth, used for indentation.
   * @returns One row per visible node, parents before children.
   */
  #renderRows(nodes: TreeNodeState[], depth: number): TemplateResult[] {
    return nodes.flatMap((node) => [
      this.#renderRow(node, depth),
      ...(node.expanded && node.children ? this.#renderRows(node.children, depth + 1) : []),
    ]);
  }

  /**
   * Renders one node's row: its expander, icon and name, then the single permission cell,
   * outlined when the server has changed it under an unsaved edit.
   * @param node The node to render.
   * @param depth The nesting depth, used for indentation.
   * @returns The table row.
   */
  #renderRow(node: TreeNodeState, depth: number): TemplateResult {
    const hasPending = this._pendingChanges.has(node.key);
    const info = this.#getDisplayInfo(node);
    const conflicted = this._conflicts.has(conflictKey(node.key, this._selectedDocType?.key ?? '', VERB));
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
        ctaIcon="icon-diploma"
        orLabel=${this.#localize.term('uap_subjectOr')}
        ?clearable=${true}
        clearLabel=${this.#localize.term('uap_clearSelection')}
        @uap-selector-click=${(e: CustomEvent<{ id: string }>) => this.#onSelectorClick(e.detail.id)}
        @uap-selection-clear=${() => void this.#onClearSelection()}>

        ${hasPending
          ? html`
              <div slot="actions">
                <uui-button label=${this.#localize.term('uap_saveChanges')} look="primary" color="positive" ?loading=${this._saving} @click=${() => void this.#saveChanges()}>
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

        ${!this._loading && this._treeNodes.length > 0
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
                    ${this.#renderRow(this._treeNodes[0], 0)}
                    ${this.#renderRows(this._treeNodes.slice(1), 1)}
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

      <!-- Save-time conflict confirmation — rendered outside umb-body-layout so it always layers on top -->
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
