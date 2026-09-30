import { html, css, nothing, customElement, state, query } from '@umbraco-cms/backoffice/external/lit';
import type { TemplateResult } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import { UMB_MODAL_MANAGER_CONTEXT } from '@umbraco-cms/backoffice/modal';
import type { RoleInfo } from '../models/permission.models.js';
import { VIRTUAL_ROOT_NODE_KEY } from '../models/permission.models.js';
import type { DocTypeListItem, BatchSaveDocTypeNode, BatchSaveDocTypeConflict } from '../models/doc-type-permission.models.js';
import {
  getElementTypes,
  getDocTypePermissionsForEditor,
  saveDocTypePermissionsBatch,
} from '../api/doc-type-permissions.api.js';
import { UAP_ROLE_PICKER_MODAL } from '../access-viewer/role-picker-modal.token.js';
import { loadSelection, saveSelection, clearSelection } from '../utils/selection-store.js';
import type { CellInfo } from '../utils/cell-info.js';
import type { PendingVerbEntries } from '../utils/compose-entries.js';
import { UapSecurityEventsController, UAP_EVENT_SOURCES } from '../live/security-events.controller.js';
import { UapUnsavedChangesGuard, confirmDiscardUnsavedChanges } from '../live/unsaved-changes-guard.js';
import type { UapRefreshPhase } from '../live/refresh-phase.js';
import '../live/uap-live-refresh.element.js';
import type { CellEntry } from '../live/conflict.js';
import { reconcileNode } from '../live/reconcile.js';
import { sameCell, staleRefreshVerbs } from './pending-cells.js';
import {
  ELEMENT_CREATE_OF_TYPE,
  EMPTY_SET_STAMP,
  cellOfEntries,
  cellOfState,
  entriesForSave,
  entriesFromNextBase,
  stateOfStored,
  storedFromEditorNodes,
} from './element-type-state.js';
import type { StoredElementType, TypeState } from './element-type-state.js';
import '../shared/components/uap-perm-block.element.js';
import '../shared/components/uap-permission-scope-dialog.element.js';
import '../live/uap-live-banner.element.js';
import '../live/uap-conflict-dialog.element.js';
import '../help/uap-page-intro.element.js';
import '../help/uap-selection-panel.element.js';
import type { UapSelectorGroup } from '../help/uap-selection-panel.element.js';
import type { UapPermissionScopeDialogElement } from '../shared/components/uap-permission-scope-dialog.element.js';
import type { UapConflictDialogElement, UapConflictLine } from '../live/uap-conflict-dialog.element.js';

/** Stable identifier used to key this surface's remembered selection (issue #46). */
const SURFACE_ID = 'element-type-permissions-editor';

/**
 * Library element-type permissions editor. Controls which element types each user group may create in
 * the Library.
 *
 * Library element-type filtering is section-global — Umbraco supplies no parent context for Library
 * creates — so each decision is stored once on the virtual root with the canonical
 * <c>Umb.Element.CreateOfType</c> verb. Rather than a plain on/off toggle, each type uses the shared
 * Allow / Deny / Inherit + priority-override modal so the choice flows through the resolver: when a user
 * belongs to several groups, an Allow-with-override can re-grant a type another group denied. "Inherit"
 * means no explicit rule — element types are creatable by default.
 *
 * Despite sitting beside the library permissions editor, this is not the element permission family.
 * It reads and writes through the document-type API, and its concurrency unit is the three-part key
 * node (always the virtual root) plus user group plus element type — one stamp per type, not per
 * node. Its stamps are the ones `GetForEditor` reports; a tree endpoint's node stamps describe a
 * different table and would never match.
 *
 * In reconciliation terms each element type is one "node" carrying one "verb", so the shared
 * `reconcileNode` and the sticky `typeKey|verb` flags work here unchanged.
 */
@customElement('uap-element-type-permissions-editor-root')
export class UapElementTypePermissionsEditorRootElement extends UmbLitElement {
  #localize = new UmbLocalizationController(this);

  // ── Selection & data ────────────────────────────────────────────────────
  /** The user group being edited; `null` until one is picked or restored. */
  @state() private _selectedRole: RoleInfo | null = null;

  /** The library element types, one row each; empty until the first load lands. */
  @state() private _types: DocTypeListItem[] = [];

  /**
   * What the server held for each type when it was last read or reconciled, keyed by type key: its
   * entries and the stamp they were read under. This is the baseline pending edits are compared
   * against, and never carries the user's edits.
   */
  @state() private _stored: Map<string, StoredElementType> = new Map();

  /**
   * The user's unsaved choices, keyed by type key. A type absent here shows what is stored. A type
   * present with `inherit` means "remove the rule on save".
   */
  @state() private _pending: Map<string, TypeState> = new Map();

  /** True while a first load or a user-group switch is running, which hides the list behind the loader. */
  @state() private _loading = false;

  /**
   * True while a save is in flight. Doubles as the guard that stops the editor reacting to the
   * live-event echo of its own write, so it must stay set until the post-save reload has finished.
   */
  @state() private _saving = false;

  /** The last load or refresh failure, shown above the list. */
  @state() private _error: string | null = null;

  // ── Live updates & conflicts ────────────────────────────────────────────
  /**
   * Types the server changed under an unsaved edit, keyed `typeKey|verb`.
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

  // ── Scope dialog state ───────────────────────────────────────────────────
  /** The element type whose cell opened the scope dialog. */
  @state() private _pickerType: DocTypeListItem | null = null;

  /** The dialog's initial choice. */
  @state() private _pickerState: 'inherit' | 'allow' | 'deny' = 'inherit';

  /** Whether the dialog starts as a priority override. */
  @state() private _pickerOverride = false;

  @query('uap-permission-scope-dialog') private _scopeDialog!: UapPermissionScopeDialogElement;
  @query('uap-conflict-dialog') private _conflictDialog!: UapConflictDialogElement;

  #notificationContext: typeof UMB_NOTIFICATION_CONTEXT.TYPE | undefined = undefined;
  #modalManager: typeof UMB_MODAL_MANAGER_CONTEXT.TYPE | undefined = undefined;
  #abort: AbortController | null = null;

  /** Live-event subscription; also the entry point for the manual refresh control. */
  #liveEvents: UapSecurityEventsController;

  /**
   * Vetoes a router navigation, browser close or reload while there are unsaved edits, and asks
   * first. In-page changes (group switch, clearing the selection) are guarded by their own
   * handlers through the same `#confirmDiscard`, because they are not router navigations.
   */
  #unsavedGuard = new UapUnsavedChangesGuard({
    hasChanges: () => this._pending.size > 0,
    confirm: () => this.#confirmDiscard(),
    onDiscard: () => this.#discardChanges(),
  });

  constructor() {
    super();
    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (ctx) => { this.#notificationContext = ctx ?? undefined; });
    this.consumeContext(UMB_MODAL_MANAGER_CONTEXT, (ctx) => { this.#modalManager = ctx ?? undefined; });

    // Every event, live or manual, is answered by one reconcile pass. A clean editor is just the
    // degenerate case (nothing pending, so every type takes the server's value); a dirty one is
    // classified type by type and nothing of the user's is touched. Going through the same path
    // for both closes a window a plain reload would leave: an edit started while a reload is in
    // flight would otherwise have its baseline replaced underneath it by whatever the reload
    // brought. The keys are ignored deliberately: an event names one anchor node, but every row
    // here is resolved at the virtual root, so there is no subset worth refetching.
    // Sources: Listens for element-type create-permission writes, and for `access` because deleting a user
    // group removes that group's stored rows from the shared document-type table directly, with no
    // permission notification of its own. It must NOT listen to `docTypePermissions`: the Document
    // Type editor owns that half of the same table, and the two are split so the editors do not wake
    // for each other's saves. Content-node and library element writes are irrelevant here.
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
        UAP_EVENT_SOURCES.elementTypePermissions,
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
    this.#abort?.abort();
  }

  /** Restores the last-used user group (if any) from per-user storage and loads its element types. */
  #restoreSelection(): void {
    const stored = loadSelection(SURFACE_ID);
    if (stored?.role) {
      this._selectedRole = stored.role;
      void this.#load();
    }
  }

  /**
   * Clears every trace of an unresolved live conflict: the flagged types and the dismissed-banner
   * flag.
   *
   * Called wherever the data is being replaced or abandoned — switching user group or clearing the
   * selection — and after a completed save or a resolved conflict, so a conflict against data that
   * has since been replaced cannot outlive it.
   */
  #resetLiveState(): void {
    this._conflicts = new Set();
    this._bannerDismissed = false;
  }

  /**
   * Throws away every pending change and returns the list to whatever the server currently holds.
   *
   * Discarding resolves any outstanding conflict by definition: with nothing of the user's left,
   * there is nothing left to contest, so the flags and the banner have to go with the edits.
   *
   * The re-read is not cosmetic. A conflicted type's entries are frozen at what they were before
   * the other person's write and its stamp was never advanced, so leaving it alone would keep both
   * out of date and the next save would be refused against a value nobody is editing any more — a
   * conflict dialog with no conflict behind it. Re-reading adopts the server's current stamps along
   * with its entries. It runs in the background so the list stays on screen.
   *
   * Does nothing while a save is in flight. The save's own reload would be aborted by this one and
   * leave the list blank, and the save is about to clear the pending state itself.
   */
  #discardChanges(): void {
    if (this._saving) return;
    this._pending = new Map();
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
    if (this._pending.size === 0) return true;
    return confirmDiscardUnsavedChanges(this, this.#localize);
  }

  /**
   * Clears the current selection, resets the view, and forgets the stored selection.
   *
   * Asks first when there are unsaved edits, since clearing drops them along with the list they
   * belong to. Cancelling leaves the selection, the list and the edits exactly as they were.
   */
  async #onClearSelection(): Promise<void> {
    if (!(await this.#confirmDiscard())) return;
    this.#abort?.abort();
    this._selectedRole = null;
    this._types = [];
    this._stored = new Map();
    this._pending = new Map();
    this.#resetLiveState();
    this._error = null;
    // A load aborted here never reaches its own `finally`, so the loader is cleared here.
    this._loading = false;
    clearSelection(SURFACE_ID);
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
      data: { ...(this._selectedRole ? { currentRole: this._selectedRole.alias } : {}) },
    });
    const result = await modal.onSubmit().catch(() => undefined);
    if (!result) return;

    // Picking the group that is already selected changes nothing, so it must not cost the user
    // their edits.
    if (this._pending.size > 0 && result.role.alias === this._selectedRole?.alias) return;
    // Asked after the pick, not before opening the picker: cancelling the picker then costs nothing.
    if (!(await this.#confirmDiscard())) return;

    this._selectedRole = result.role;
    this._pending = new Map();
    this.#resetLiveState();
    saveSelection(SURFACE_ID, { subjectKind: 'role', role: result.role });
    void this.#load();
  }

  // ── Data loading ────────────────────────────────────────────────────────

  /**
   * Reads what the server holds for every element type and the selected user group.
   *
   * One `GetForEditor` call per type, in parallel: the endpoint answers for one (user group,
   * content type) pair at a time. The stamp comes from that answer, per type, and from nowhere
   * else — see {@link storedFromEditorNodes} for how a type with nothing stored is stamped.
   * @param roleAlias The user group to read for.
   * @param types The element types to read.
   * @param signal Aborts the reads when the load they belong to is superseded.
   * @returns The stored entries and stamp per type, keyed by type key.
   */
  async #fetchStored(
    roleAlias: string,
    types: readonly DocTypeListItem[],
    signal: AbortSignal,
  ): Promise<Map<string, StoredElementType>> {
    const stored = new Map<string, StoredElementType>();
    await Promise.all(types.map(async (t) => {
      const nodes = await getDocTypePermissionsForEditor(roleAlias, t.key, signal);
      stored.set(t.key, storedFromEditorNodes(nodes));
    }));
    return stored;
  }

  /**
   * Loads the element types and what is stored for each, with the loader up.
   *
   * Used for the first load, a user-group switch and the refresh after a successful save: cases
   * where what is on screen belongs to something else or is about to be superseded wholesale, and
   * nothing of the user's is pending, so nothing needs classifying. The previous rows are blanked
   * first so that a failed read cannot leave the previous group's values showing under this
   * group's name.
   */
  async #load(): Promise<void> {
    if (!this._selectedRole) return;
    const roleAlias = this._selectedRole.alias;
    this.#abort?.abort();
    const controller = new AbortController();
    this.#abort = controller;

    this._loading = true;
    this._error = null;
    this._types = [];
    this._stored = new Map();
    try {
      const types = await getElementTypes(controller.signal);
      if (controller.signal.aborted) return;
      const stored = await this.#fetchStored(roleAlias, types, controller.signal);
      if (controller.signal.aborted) return;

      this._types = types;
      this._stored = stored;
    } catch (err) {
      if (controller.signal.aborted) return;
      this._error = String(err);
    } finally {
      // Cleared by whichever load finishes un-superseded. A live refresh aborts a load that was
      // still showing the loader, and that load's own `finally` then skips clearing it, so
      // leaving this to the foreground path alone would strand the list behind the loader.
      if (!controller.signal.aborted) this._loading = false;
    }
  }

  // ── Live reconciliation ─────────────────────────────────────────────────

  /**
   * Re-reads the server in the background and works out, type by type, what the change means for
   * whatever the editor is holding — which may be nothing at all.
   *
   * The decision itself is delegated to the pure, unit-tested `reconcileNode`; this method fetches,
   * translates the shapes it needs, and applies what comes back. Types the user has not touched
   * take the server's value silently. Types where both sides moved are flagged, and nothing else
   * happens to them: the pending map is never written over here except to drop an entry that
   * reconciliation proves was never an edit.
   *
   * What is written back is always `reconcileNode`'s `nextBase`, never the raw server response: a
   * conflicted type must keep the entries it had, or the next pass would read it as unchanged and
   * the flag would silently stop meaning anything. Entries for verbs this editor has no column for
   * survive the rebuild (see {@link entriesFromNextBase}).
   *
   * Cancellable and role-guarded: the fetch is assigned to `#abort` (so a role switch or a cleared
   * selection cancels it, and it cancels them), and the selected user group's alias is captured up
   * front and checked again after the one await this method makes.
   * @returns A promise that resolves when the pass is done and rejects if the fetch failed, so
   * the refresh control does not claim the data was updated.
   */
  async #refreshFromServer(): Promise<void> {
    if (!this._selectedRole || this._types.length === 0) return;
    const roleAlias = this._selectedRole.alias;

    this.#abort?.abort();
    const controller = new AbortController();
    this.#abort = controller;

    try {
      const fresh = await this.#fetchStored(roleAlias, this._types, controller.signal);
      // `_saving` too: a read that finished after the save began may predate its write, and would
      // be reconciled against as if it were somebody else's change.
      if (controller.signal.aborted || this._selectedRole?.alias !== roleAlias || this._saving) return;

      // Starts from what is already flagged and only ever grows: a flag is removed by the user
      // acting on it, never by a later pass that happens to read the type as unchanged.
      const conflicts = new Set(this._conflicts);
      const nextStored = new Map(this._stored);
      const stalePending = new Set<string>();

      for (const type of this._types) {
        const theirsForType = fresh.get(type.key);
        if (!theirsForType) continue;

        // A type this editor has no baseline for is checked against the empty set, the same
        // stamp the server gives a pair with nothing stored.
        const current = this._stored.get(type.key) ?? { entries: [], stamp: EMPTY_SET_STAMP };
        const cellKey = `${type.key}|${ELEMENT_CREATE_OF_TYPE}`;

        const base = new Map<string, ReadonlyArray<CellEntry>>([[ELEMENT_CREATE_OF_TYPE, cellOfEntries(current.entries)]]);
        const theirs = new Map<string, ReadonlyArray<CellEntry>>([[ELEMENT_CREATE_OF_TYPE, cellOfEntries(theirsForType.entries)]]);
        const pendingState = this._pending.get(type.key);
        const pending = pendingState
          ? new Map<string, ReadonlyArray<CellEntry>>([[ELEMENT_CREATE_OF_TYPE, cellOfState(pendingState)]])
          : undefined;
        const alreadyFlagged = new Set<string>(this._conflicts.has(cellKey) ? [ELEMENT_CREATE_OF_TYPE] : []);

        const result = reconcileNode({ base, pending, theirs, conflicted: alreadyFlagged });
        for (const verb of result.conflictedVerbs) {
          conflicts.add(`${type.key}|${verb}`);
        }

        // A `refresh` verdict means the user holds nothing of their own on that cell, so a
        // pending entry there is stale bookkeeping — and a dangerous one, because it is what the
        // list would go on rendering and the save would go on sending, over the value adopted here.
        if (pending && staleRefreshVerbs({ base, pending, theirs, flagged: new Set(result.conflictedVerbs) }).length > 0) {
          stalePending.add(type.key);
        }

        // A type with a flagged verb keeps the stamp it has. Adopting the server's would let the
        // next save for it sail through the concurrency check on a value nobody has agreed to
        // overwrite. Resolving the conflict re-reads the server, which supplies a current one.
        nextStored.set(type.key, {
          entries: entriesFromNextBase(type.key, roleAlias, result.nextBase, theirsForType.entries),
          stamp: result.adoptStamp ? theirsForType.stamp : current.stamp,
        });
      }

      this._stored = nextStored;
      if (stalePending.size > 0) {
        const nextPending = new Map(this._pending);
        for (const key of stalePending) nextPending.delete(key);
        this._pending = nextPending;
      }
      this.#adoptConflicts(conflicts);
    } catch (err) {
      if (controller.signal.aborted) return;
      this._error = String(err);
      // Rethrown so the refresh control does not claim the data was updated.
      throw err;
    } finally {
      // Cleared by whichever load finishes un-superseded, background included. A live refresh
      // aborts a group-switch load that was still showing the loader, and that load's own
      // `finally` then skips clearing it, so leaving this to foreground loads would strand the
      // list behind the loader for good.
      if (!controller.signal.aborted) this._loading = false;
    }
  }

  /**
   * Adopts the flagged set a reconcile pass just computed, without disturbing what is already on
   * screen. The set is a superset of the previous one by construction (flags are sticky), so this
   * only ever adds.
   *
   * A pass that finds exactly the types already flagged leaves the state alone: swapping in an
   * equal set would re-render the list and, worse, bring back a banner the person had dismissed
   * for those very types. The banner returns only when a pass flags something that was not flagged
   * before, which is genuinely new news.
   * @param next The previous flags plus any this pass found, keyed `typeKey|verb`.
   */
  #adoptConflicts(next: Set<string>): void {
    const previous = this._conflicts;
    const unchanged = next.size === previous.size && [...next].every((c) => previous.has(c));
    if (unchanged) return;
    if ([...next].some((c) => !previous.has(c))) this._bannerDismissed = false;
    this._conflicts = next;
  }

  /**
   * Resolves every currently-flagged type to its stored value, keeping every other pending
   * change untouched, then re-reads the server so the list actually shows that stored value.
   *
   * Scoped to the flagged types on purpose. Discarding everything would be simpler and would throw
   * away edits elsewhere that nobody is contesting.
   *
   * THE RE-READ IS NOT OPTIONAL — do not remove it as a redundant round trip. A conflicted type's
   * baseline is deliberately frozen (see `reconcileNode`): its stored entries still hold the value
   * from *before* the other person's write, and its stamp was held back to match. Dropping the
   * pending change only removes the user's edit; it does not make the entries current. Without the
   * re-read the row keeps showing the pre-conflict value, and the next save could send that stale
   * value under a stamp the server accepts, silently reverting the other person's change.
   *
   * The re-read goes through the live-event controller, exactly as the manual refresh control
   * does, so it queues behind any run already in flight and compares before it adopts. Until it
   * lands the types still hold their old stamp, so a save made in the meantime is refused by the
   * server rather than written stale.
   *
   * Used for both the banner's "Load stored values" and the save-time conflict dialog's, so the
   * two cannot end in different states.
   */
  #loadStoredForConflicts(): void {
    const next = new Map(this._pending);
    for (const cell of this._conflicts) {
      const [typeKey] = cell.split('|');
      next.delete(typeKey!);
    }
    this._pending = next;
    this.#resetLiveState();
    void this.#liveEvents.refresh();
  }

  // ── Editing ─────────────────────────────────────────────────────────────

  /**
   * Opens the scope dialog for one type, seeded from its pending choice if it has one and its
   * stored value otherwise.
   * @param t The element type whose cell was clicked.
   */
  #openTypePicker(t: DocTypeListItem): void {
    const current = this.#displayedState(t.key);
    this._pickerType = t;
    this._pickerState = current.state;
    this._pickerOverride = current.isPriorityOverride;
    void this.updateComplete.then(() => this._scopeDialog.open());
  }

  /**
   * Handles `uap-scope-apply` from the shared scope dialog: records the choice as a pending change
   * for the type that opened it — unless it equals what is stored.
   *
   * Applying what is already stored is not an edit, so it is not recorded. The dialog's Apply is
   * unconditional; recording the no-op would leave a pending entry that a later reconcile treats
   * as "nothing of the user's here" while the save still sends it, reverting a colleague's change.
   * It also removes an earlier edit the user has just changed back.
   * @param e The apply event carrying the composed entries.
   */
  #handleScopeApply(e: CustomEvent<{ entries: PendingVerbEntries }>): void {
    if (!this._pickerType) return;
    const typeKey = this._pickerType.key;
    const entry = e.detail.entries[0];
    const next: TypeState = entry
      ? { state: entry.state === 'Allow' ? 'allow' : 'deny', isPriorityOverride: entry.isPriorityOverride }
      : { state: 'inherit', isPriorityOverride: false };

    const storedCell = cellOfEntries(this._stored.get(typeKey)?.entries ?? []);
    const nextPending = new Map(this._pending);
    if (sameCell(cellOfState(next), storedCell)) nextPending.delete(typeKey);
    else nextPending.set(typeKey, next);
    this._pending = nextPending;
  }

  /**
   * The choice a type's row should currently display: its pending choice if it has one, otherwise
   * what is stored.
   * @param typeKey The element type's key.
   * @returns The tri-state and override flag.
   */
  #displayedState(typeKey: string): TypeState {
    return this._pending.get(typeKey) ?? stateOfStored(this._stored.get(typeKey)?.entries ?? []);
  }

  // ── Save ─────────────────────────────────────────────────────────────────

  /**
   * Writes every pending choice in one all-or-nothing request, and stops to ask if the stored
   * values moved since this editor read them.
   *
   * One request rather than a loop of one save per type: a rejection partway through a loop would
   * leave some types written and some not, with nothing afterwards able to tell which. Each triple
   * carries its own type's stamp, from `GetForEditor`. A type with nothing stored carries the
   * empty-set stamp, so its first save is checked like any other; an empty string, which would skip
   * the check, is reserved for a read that returned no ETag at all.
   * @param force Whether to write through a stale stamp. Only true after the user has confirmed
   * via the conflict dialog, and the dialog's overwrite handler is the only caller that passes it.
   */
  async #save(force = false): Promise<void> {
    if (!this._pending.size || !this._selectedRole || this._saving) return;
    this._saving = true;

    try {
      const roleAlias = this._selectedRole.alias;
      const nodes: BatchSaveDocTypeNode[] = [];

      for (const [typeKey, choice] of this._pending) {
        const stored = this._stored.get(typeKey) ?? { entries: [], stamp: EMPTY_SET_STAMP };
        nodes.push({
          nodeKey: VIRTUAL_ROOT_NODE_KEY,
          roleAlias,
          contentTypeKey: typeKey,
          // Inherit clears the create rule (back to the default allow); Allow and Deny write one
          // virtual-root entry. Entries for any other verb stored under the pair are carried
          // through, because a save replaces the pair's whole list.
          entries: entriesForSave(stored.entries, choice),
          // '' means the read came back with no ETag: "no stamp available", not "nothing stored".
          expectedStamp: stored.stamp === '' ? undefined : stored.stamp,
        });
      }

      const result = await saveDocTypePermissionsBatch(nodes, force);

      if (!result.ok) {
        // Not an error path. A conflict is the answer this feature exists to produce, and what
        // happens next is the user's decision, not this method's.
        this.#openConflictDialog(result.conflicts);
        return;
      }

      // Adopt the stamps the server just handed back before reloading, so the types this save
      // touched are immediately correct even though the reload below re-derives the same thing.
      const stamped = new Map(this._stored);
      for (const [key, stamp] of result.stamps) {
        const [, savedRoleAlias, savedTypeKey] = key.split('|');
        if (savedRoleAlias !== roleAlias || !savedTypeKey) continue;
        const current = stamped.get(savedTypeKey);
        if (current) stamped.set(savedTypeKey, { ...current, stamp });
      }
      this._stored = stamped;

      await this.#load();
      this._pending = new Map();
      this.#resetLiveState();
      this.#notificationContext?.peek('positive', { data: { message: this.#localize.term('uap_permissionsSaved') } });
    } catch (err) {
      this.#notificationContext?.peek('danger', { data: { message: this.#localize.term('uap_saveFailed', String(err)) } });
    } finally {
      this._saving = false;
    }
  }

  /**
   * Turns the server's conflict list into lines for the confirmation dialog, flags the same types
   * in the list, and opens the dialog.
   *
   * The batch endpoint reports a conflict per (node, user group, element type) triple — its unit of
   * write — which here is exactly one line per type, since the only node is the virtual root. The
   * affected types' entries are also advanced to what the server just reported, but their stamp is
   * left alone, exactly as for a live-detected conflict — `#loadStoredForConflicts` resolves both
   * the same way, by re-reading the server rather than trusting either half of what is held here.
   * The advanced entries are what keeps a confirmed overwrite from reverting the pair's other
   * verbs; the flags set here are sticky, so the advanced baseline cannot make a later pass read
   * the type as clean.
   * @param conflicts The conflicting triples the server refused to overwrite.
   */
  #openConflictDialog(conflicts: BatchSaveDocTypeConflict[]): void {
    const lines: UapConflictLine[] = [];
    const flagged = new Set(this._conflicts);
    const nextStored = new Map(this._stored);

    for (const conflict of conflicts) {
      const typeKey = conflict.contentTypeKey;
      const current = nextStored.get(typeKey);
      nextStored.set(typeKey, { entries: conflict.currentEntries, stamp: current?.stamp ?? EMPTY_SET_STAMP });

      const choice = this._pending.get(typeKey);
      if (!choice) continue;

      flagged.add(`${typeKey}|${ELEMENT_CREATE_OF_TYPE}`);
      lines.push({
        nodeName: this._types.find((t) => t.key === typeKey)?.name ?? typeKey,
        verbName: this.#localize.term('uap_elementTypePermissions_verbCreate'),
        stored: this.#describeChoice(stateOfStored(conflict.currentEntries)),
        mine: this.#describeChoice(choice),
      });
    }

    this._stored = nextStored;
    this._conflicts = flagged;
    this._conflictDialog.lines = lines;
    this._conflictDialog.open();
  }

  /** Closes the save-time conflict dialog without changing anything. */
  #closeConflictDialog(): void {
    this._conflictDialog.close();
  }

  /**
   * Describes a choice as the short phrase the conflict dialog shows for "stored" and "yours" —
   * the same wording the scope dialog's preview uses for this surface.
   * @param choice The tri-state to describe.
   * @returns The localized description.
   */
  #describeChoice(choice: TypeState): string {
    if (choice.state === 'inherit') return this.#localize.term('uap_elementType_previewInherit');
    const action = choice.state === 'allow' ? this.#localize.term('uap_allow') : this.#localize.term('uap_deny');
    return this.#localize.term('uap_elementType_previewSet', action);
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

  // ── Rendering ────────────────────────────────────────────────────────────

  /** Renders the surface: the selection panel with its actions and grid, plus the dialogs that must layer above it. */
  override render(): TemplateResult {
    return html`
      <umb-body-layout headline=${this.#localize.term('uap_elementTypePermissions_headline')}>
        <uap-page-intro surface="uap-element-type-permissions" headline=${this.#localize.term('uap_elementTypePermissions_headline')}></uap-page-intro>
        <uap-selection-panel
          .groups=${this.#selectionGroups}
          promptText=${this.#localize.term('uap_library_selectRolePrompt')}
          ctaIcon="icon-thumbnail-list"
          orLabel=${this.#localize.term('uap_subjectOr')}
          ?clearable=${true}
          clearLabel=${this.#localize.term('uap_clearSelection')}
          @uap-selector-click=${(e: CustomEvent<{ id: string }>) => this.#onSelectorClick(e.detail.id)}
          @uap-selection-clear=${() => void this.#onClearSelection()}>
          ${this._pending.size > 0
            ? html`<div slot="actions">
                <uui-button label=${this.#localize.term('uap_saveChanges')} look="primary" color="positive" ?loading=${this._saving} @click=${() => void this.#save()}>
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
          ${!this._loading
            ? (this._types.length > 0
                ? html`
                    <div class="type-list">
                      <div class="type-header">
                        <span class="type-name">${this.#localize.term('uap_elementTypePermissions_typeHeader')}</span>
                        <span class="type-cell">${this.#localize.term('uap_elementTypePermissions_verbCreate')}</span>
                      </div>
                      ${this._types.map((t) => this.#renderType(t))}
                    </div>`
                : (this._error ? nothing : html`<p class="empty-msg">${this.#localize.term('uap_elementTypePermissions_noTypes')}</p>`))
            : nothing}
        </uap-selection-panel>
      </umb-body-layout>

      <!-- Permission dialog — rendered outside umb-body-layout so it always layers on top -->
      ${this.#renderDialog()}

      <!-- Save-time conflict confirmation — same reasoning as the permission dialog above -->
      <uap-conflict-dialog
        @uap-conflict-cancel=${() => this.#closeConflictDialog()}
        @uap-conflict-load-stored=${() => { this.#closeConflictDialog(); this.#loadStoredForConflicts(); }}
        @uap-conflict-overwrite=${() => { this.#closeConflictDialog(); void this.#save(true); }}>
      </uap-conflict-dialog>
    `;
  }

  /**
   * Renders one element type's row: its icon and name, then its create cell, outlined when the
   * server has changed it under an unsaved edit.
   * @param t The element type to render.
   * @returns The row.
   */
  #renderType(t: DocTypeListItem): TemplateResult {
    const ts = this.#displayedState(t.key);
    const pending = this._pending.has(t.key);
    const conflicted = this._conflicts.has(`${t.key}|${ELEMENT_CREATE_OF_TYPE}`);
    const info: CellInfo = {
      split: false,
      nodeClass: ts.state,
      descClass: ts.state,
      nodeOverride: ts.isPriorityOverride && ts.state !== 'inherit',
    };
    return html`
      <div class="type-row ${pending ? 'pending' : ''}">
        <umb-icon name=${t.icon ?? 'icon-document'}></umb-icon>
        <span class="type-name">${t.name}</span>
        <div class="type-cell ${conflicted ? 'conflicted' : ''}" @click=${() => this.#openTypePicker(t)}>
          <uap-perm-block
            .info=${info}
            ?pending=${pending}
            priority-override-title=${this.#localize.term('uap_priorityOverrideBadgeTitle')}></uap-perm-block>
        </div>
      </div>
    `;
  }

  /**
   * Renders the shared scope dialog instance in its single-choice form. The dialog opens via
   * `#openTypePicker`; `uap-scope-apply` is handled by `#handleScopeApply`.
   * @returns The dialog element.
   */
  #renderDialog(): TemplateResult {
    const typeName = this._pickerType?.name ?? '';
    return html`
      <uap-permission-scope-dialog
        .isVirtualRoot=${true}
        .verb=${this.#localize.term('uap_elementTypePermissions_verbCreate')}
        .nodeName=${typeName}
        .headlineOverride=${this.#localize.term('uap_elementType_dialogHeadline', typeName)}
        .singleInheritLabel=${this.#localize.term('uap_elementType_inheritLabel')}
        .singleAllowLabel=${this.#localize.term('uap_elementType_allowLabel')}
        .singleDenyLabel=${this.#localize.term('uap_elementType_denyLabel')}
        .singlePreviewInherit=${this.#localize.term('uap_elementType_previewInherit')}
        .singlePreviewSet=${(action: string) => this.#localize.term('uap_elementType_previewSet', action)}
        .initialNodeState=${this._pickerState}
        .initialNodeIsPriorityOverride=${this._pickerOverride}
        @uap-scope-apply=${(e: CustomEvent<{ entries: PendingVerbEntries }>) => this.#handleScopeApply(e)}>
      </uap-permission-scope-dialog>
    `;
  }

  static override styles = css`
    :host { display: block; height: 100%; }
    .loading { display: flex; justify-content: center; padding: 32px; }
    .error-msg { padding: 12px 18px; color: var(--uui-color-danger, #b91c1c); }
    .empty-msg { padding: 32px 18px; color: var(--uui-color-text-alt, #888); }

    .type-list {
      display: flex;
      flex-direction: column;
      gap: 4px;
      padding: 12px 18px;
    }
    .type-header {
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 0 12px 4px;
      font-weight: 600;
      color: var(--uui-color-text-alt, #666);
    }
    .type-header .type-name { flex: 1; }
    .type-row {
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 8px 12px;
      border: 1px solid var(--uui-color-border, #e8e8e8);
      border-radius: var(--uui-border-radius, 4px);
      background: var(--uui-color-surface, #fff);
    }
    .type-row.pending {
      border-color: var(--uui-color-warning-standalone, #f59e0b);
      border-style: dashed;
    }
    .type-name { flex: 1; }
    .type-cell { width: 72px; flex-shrink: 0; text-align: center; cursor: pointer; }

    .type-cell.conflicted uap-perm-block {
      outline: 2px solid var(--uui-color-danger);
      outline-offset: 2px;
    }
  `;
}

export default UapElementTypePermissionsEditorRootElement;

declare global {
  interface HTMLElementTagNameMap {
    'uap-element-type-permissions-editor-root': UapElementTypePermissionsEditorRootElement;
  }
}
