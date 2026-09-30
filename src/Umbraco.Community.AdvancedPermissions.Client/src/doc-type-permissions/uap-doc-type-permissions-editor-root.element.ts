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

/** The single verb v1 ships for doc-type permissions. */
const VERB = 'Umb.Document.CreateOfType';

/** Sentinel local key for the virtual-root row in the tree. Mapped back to `VIRTUAL_ROOT_NODE_KEY` on save. */
const VIRTUAL_ROOT_LOCAL_KEY = 'virtual-root';

/** Stable identifier used to key this surface's remembered selection (issue #46). */
const SURFACE_ID = 'doc-type-permissions-editor';

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

  /** Set after a silent live refresh, to show the "updated" pill. */
  @state() private _liveRefreshedAt: number | null = null;

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

  /** Clears `_liveRefreshedAt` after the pill has had a moment to be seen. */
  #liveRefreshedTimer: ReturnType<typeof setTimeout> | undefined = undefined;

  /**
   * Stamps withheld from a node while it has an unresolved conflict, keyed by node key: the stamp
   * the server reported as current. A node with an outstanding conflict must not adopt that stamp
   * onto itself — doing so would let the next save for that node sail through the concurrency
   * check on a value nobody has agreed to overwrite. Applied once the conflict is resolved (see
   * `#loadStoredForConflicts`), and dropped without being applied if the node classifies clean on
   * a later pass (its live stamp is adopted normally at that point instead).
   *
   * Keyed by node alone, not the full triple: this editor holds exactly one role and one document
   * type at a time, so the node key alone already disambiguates every withheld stamp it can hold.
   */
  #withheldStamps: Map<string, string> = new Map();

  constructor() {
    super();
    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (ctx) => {
      this.#notificationContext = ctx ?? undefined;
    });
    this.consumeContext(UMB_MODAL_MANAGER_CONTEXT, (ctx) => {
      this.#modalManager = ctx ?? undefined;
    });

    // A clean editor takes the server's version in place; a dirty one is classified cell by cell
    // and nothing of the user's is touched. Mirrors the node Permissions Editor exactly — see its
    // constructor for the full reasoning.
    new UapSecurityEventsController(this, async () => {
      if (!this._selectedRole || !this._selectedDocType) return;
      if (this._pendingChanges.size === 0) {
        await this.#reloadEntries();
        this.#markLiveRefreshed();
        return;
      }
      await this.#reconcileWithServer();
    });
  }

  /**
   * Records a silent live refresh and shows the "updated" pill for a few seconds.
   *
   * Timed out rather than left standing: the pill says "Updated just now", and a claim like that
   * left on screen indefinitely turns into a lie the moment a few minutes pass with nothing
   * happening. A fresh event before the timer fires restarts the clock instead of stacking a
   * second one.
   */
  #markLiveRefreshed(): void {
    this._liveRefreshedAt = Date.now();
    clearTimeout(this.#liveRefreshedTimer);
    this.#liveRefreshedTimer = setTimeout(() => {
      this._liveRefreshedAt = null;
    }, 5000);
  }

  /**
   * Clears every trace of an unresolved live conflict: the flagged cells, the dismissed-banner
   * flag, and the stamps withheld while they were unresolved.
   *
   * Called wherever the tree itself is being replaced or abandoned — switching user group or
   * document type, or clearing the selection — so a conflict against the previous pair's data
   * cannot outlive the data it was about.
   */
  #resetLiveState(): void {
    this._conflicts = new Set();
    this._bannerDismissed = false;
    this.#withheldStamps = new Map();
  }

  override connectedCallback(): void {
    super.connectedCallback();
    void this.#loadMeta();
    this.#restoreSelection();
  }

  override disconnectedCallback(): void {
    super.disconnectedCallback();
    this.#loadAbortController?.abort();
    clearTimeout(this.#liveRefreshedTimer);
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

  /** Clears the current selection, resets the view, and forgets the stored selection. */
  #onClearSelection(): void {
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
        stamp: virtualRootFresh?.stamp ?? '',
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
            // doc-type concurrency stamp always comes from `getDocTypePermissionsForEditor`.
            stamp: fresh?.stamp ?? '',
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
   * Reloads only the entries (and stamps) for the current tree on (role, doc-type) change, or
   * after a clean live refresh. Preserves expanded state and existing children.
   */
  async #reloadEntries(): Promise<void> {
    if (!this._selectedRole || !this._selectedDocType || this._treeNodes.length === 0) return;

    this.#loadAbortController?.abort();
    const controller = new AbortController();
    this.#loadAbortController = controller;

    this._loading = true;
    this._error = null;

    try {
      const editorNodes = await getDocTypePermissionsForEditor(
        this._selectedRole.alias,
        this._selectedDocType.key,
        controller.signal,
      );
      if (controller.signal.aborted) return;

      const byNode = this.#groupByNode(editorNodes);
      this.#applyEntriesRecursive(this._treeNodes, byNode);
      this._treeNodes = [...this._treeNodes];
    } catch (err) {
      if (controller.signal.aborted) return;
      this._error = String(err);
    } finally {
      if (!controller.signal.aborted) this._loading = false;
    }
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

  /**
   * Walks the loaded tree and sets each node's `entries` and `stamp` from the supplied lookup. A
   * node absent from the lookup has nothing stored (or, mid-save, nothing to report yet); either
   * way it takes an empty entry list and the empty-string "no stamp available" sentinel, the same
   * convention `PermissionEntriesWithStamp` uses on the node-permission side. Virtual-root row is
   * keyed locally; the lookup uses `VIRTUAL_ROOT_NODE_KEY` for it.
   */
  #applyEntriesRecursive(
    nodes: TreeNodeState[],
    byNode: Map<string, { entries: PermissionEntry[]; stamp: string }>,
  ): void {
    for (const node of nodes) {
      const lookupKey = node.key === VIRTUAL_ROOT_LOCAL_KEY ? VIRTUAL_ROOT_NODE_KEY : node.key;
      const fresh = byNode.get(lookupKey);
      node.entries = fresh?.entries ?? [];
      node.stamp = fresh?.stamp ?? '';
      if (node.children) this.#applyEntriesRecursive(node.children, byNode);
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
            stamp: fresh?.stamp ?? '',
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
   * Receives the composed entries from the shared scope dialog and writes them into the
   * pending-changes map for the cell that opened the dialog.
   */
  #handleScopeApply(e: CustomEvent<{ entries: PendingVerbEntries }>): void {
    if (!this._pickerNode) return;
    this._pendingChanges = new Map(this._pendingChanges).set(this._pickerNode.key, e.detail.entries);
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
   */
  async #reconcileWithServer(): Promise<void> {
    if (!this._selectedRole || !this._selectedDocType) return;
    const roleAlias = this._selectedRole.alias;
    const contentTypeKey = this._selectedDocType.key;

    this.#loadAbortController?.abort();
    const controller = new AbortController();
    this.#loadAbortController = controller;

    try {
      const editorNodes = await getDocTypePermissionsForEditor(roleAlias, contentTypeKey, controller.signal);
      if (
        controller.signal.aborted ||
        this._selectedRole?.alias !== roleAlias ||
        this._selectedDocType?.key !== contentTypeKey
      ) {
        return;
      }

      const fresh = this.#groupByNode(editorNodes);
      const conflicts = new Set<string>();

      for (const node of this.#flattenNodes(this._treeNodes)) {
        const apiKey = node.key === VIRTUAL_ROOT_LOCAL_KEY ? VIRTUAL_ROOT_NODE_KEY : node.key;
        const theirsForNode = fresh.get(apiKey) ?? { entries: [] as PermissionEntry[], stamp: '' };
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

        const result = reconcileNode({ base, pending, theirs });
        for (const verb of result.conflictedVerbs) {
          conflicts.add(`${node.key}|${contentTypeKey}|${verb}`);
        }

        // Rebuilt from the per-verb merge, exactly as the node Permissions Editor does: a
        // conflicted verb keeps the entries it had before this event, a clean verb takes the
        // fresh ones. Never the raw server response — see reconcileNode's doc comment for why
        // that distinction is the entire point.
        const mergedEntries = this.#entriesFromCells(apiKey, roleAlias, result.nextBase.get(VERB) ?? []);

        if (result.adoptStamp) {
          this.#withheldStamps.delete(node.key);
          this.#updateNode(node.key, { entries: mergedEntries, stamp: theirsForNode.stamp });
        } else {
          this.#withheldStamps.set(node.key, theirsForNode.stamp);
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

      if (conflicts.size > 0) {
        this._bannerDismissed = false;
      }
      this._conflicts = conflicts;
    } catch (err) {
      if (controller.signal.aborted) return;
      this._error = String(err);
    }
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
   * untouched.
   *
   * Scoped to the flagged nodes on purpose — discarding everything would throw away edits
   * elsewhere in the tree that nobody is contesting. Because there is only one verb in this
   * editor, a flagged cell and its node coincide, so resolving the cell is exactly deleting that
   * node's whole pending entry. Any node that was withholding its stamp can adopt it now: nothing
   * at that node is still in dispute once this returns.
   */
  #loadStoredForConflicts(): void {
    const next = new Map(this._pendingChanges);
    const affectedNodes = new Set<string>();

    for (const cell of this._conflicts) {
      const [nodeKey] = cell.split('|');
      affectedNodes.add(nodeKey!);
      next.delete(nodeKey!);
    }

    for (const nodeKey of affectedNodes) {
      const stamp = this.#withheldStamps.get(nodeKey);
      if (stamp !== undefined) {
        this.#updateNode(nodeKey, { stamp });
        this.#withheldStamps.delete(nodeKey);
      }
    }

    this._pendingChanges = next;
    this._conflicts = new Set();
    this._bannerDismissed = false;
  }

  // ── Save ─────────────────────────────────────────────────────────────────

  /**
   * Writes every pending change in one all-or-nothing request, and stops to ask if the stored
   * values moved since this editor read them.
   * @param force Whether to write through a stale stamp. Only true after the user has confirmed
   * via the conflict dialog.
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

        // Only one verb exists per node here, so the pending list already is the complete new
        // entry list — unlike the node Permissions Editor there is nothing else to merge it with.
        // An empty stamp means "no stamp available" (see `#applyEntriesRecursive`'s doc comment);
        // sending it as `''` would compare as a value that never matches, so it is omitted instead.
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
      for (const [key, stamp] of result.stamps) {
        const [savedNodeKey, savedRoleAlias, savedContentTypeKey] = key.split('|');
        if (savedRoleAlias !== roleAlias || savedContentTypeKey !== contentTypeKey) continue;
        const localKey = savedNodeKey === VIRTUAL_ROOT_NODE_KEY ? VIRTUAL_ROOT_LOCAL_KEY : savedNodeKey!;
        this.#updateNode(localKey, { stamp });
        this.#withheldStamps.delete(localKey);
      }

      await this.#reloadEntries();
      this._pendingChanges = new Map();
      this.#resetLiveState();
      this.#notificationContext?.peek('positive', {
        data: { message: this.#localize.term('uap_permissionsSaved') },
      });
    } catch (err) {
      this.#notificationContext?.peek('danger', {
        data: { message: this.#localize.term('uap_saveFailed', String(err)) },
      });
    } finally {
      this._saving = false;
    }
  }

  /**
   * Turns the server's triple-level conflict list into per-node lines for the confirmation
   * dialog, flags the same cells on the grid, and opens the dialog.
   *
   * The affected nodes' entries are advanced to what the server just reported and their stamp is
   * withheld, exactly as a live-detected conflict would be — `#loadStoredForConflicts` resolves
   * both the same way.
   */
  #openConflictDialog(conflicts: BatchSaveDocTypeConflict[]): void {
    const lines: UapConflictLine[] = [];
    const flagged = new Set(this._conflicts);
    const verbName = this.#localize.term('uap_docTypePermissions_verbInsert');

    for (const conflict of conflicts) {
      const localKey = conflict.nodeKey === VIRTUAL_ROOT_NODE_KEY ? VIRTUAL_ROOT_LOCAL_KEY : conflict.nodeKey;
      const isVirtualRoot = localKey === VIRTUAL_ROOT_LOCAL_KEY;
      const nodeName = isVirtualRoot
        ? this.#localize.term('uap_contentRoot')
        : (this.#findNode(localKey)?.name ?? localKey);

      this.#withheldStamps.set(localKey, conflict.currentStamp);
      this.#updateNode(localKey, { entries: conflict.currentEntries });

      const pending = this._pendingChanges.get(localKey);
      if (!pending) continue;

      flagged.add(`${localKey}|${conflict.contentTypeKey}|${VERB}`);
      lines.push({
        nodeName,
        verbName,
        stored: this.#describeCell(conflict.currentEntries, isVirtualRoot),
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
        @uap-selection-clear=${() => this.#onClearSelection()}>

        ${hasPending
          ? html`
              <div slot="actions">
                <uui-button label=${this.#localize.term('uap_saveChanges')} look="primary" color="positive" ?loading=${this._saving} @click=${() => void this.#saveChanges()}>
                  ${this.#localize.term('uap_saveChanges')}
                </uui-button>
                <uui-button label=${this.#localize.term('uap_discard')} look="outline" @click=${() => { this._pendingChanges = new Map(); }}>
                  ${this.#localize.term('uap_discard')}
                </uui-button>
              </div>
            `
          : nothing}
        ${this._liveRefreshedAt
          ? html`<span slot="actions" class="live-pill"><uui-icon name="icon-sync"></uui-icon>${this.#localize.term('uap_liveUpdated')}</span>`
          : nothing}

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
        @uap-conflict-overwrite=${() => { this.#closeConflictDialog(); void this.#saveChanges(true); }}>
      </uap-conflict-dialog>
    `;
  }

  static override styles = css`
    :host { display: block; height: 100%; }

    .loading { display: flex; justify-content: center; padding: 32px; }
    .error-msg { padding: 12px 18px; color: var(--uui-color-danger, #b91c1c); }

    .live-pill {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 3px 9px;
      border-radius: 4px;
      font-size: 12px;
      background: var(--uui-color-surface-alt);
      color: var(--uui-color-text-alt);
    }

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
