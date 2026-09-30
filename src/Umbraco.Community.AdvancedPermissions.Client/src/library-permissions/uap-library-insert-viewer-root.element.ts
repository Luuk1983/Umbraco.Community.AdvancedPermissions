import { html, css, nothing, customElement, state, query } from '@umbraco-cms/backoffice/external/lit';
import type { TemplateResult } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import { UMB_MODAL_MANAGER_CONTEXT } from '@umbraco-cms/backoffice/modal';
import type { RoleInfo, UserItem, PathNode, PermissionEntry } from '../models/permission.models.js';
import { VIRTUAL_ROOT_NODE_KEY } from '../models/permission.models.js';
import { getRoles } from '../api/advanced-permissions.api.js';
import { getElementTypeAudit, getDocTypePathEntries } from '../api/doc-type-permissions.api.js';
import type { DocTypeAuditForNodeRow } from '../models/doc-type-permission.models.js';
import type { CellInfo } from '../utils/cell-info.js';
import { UAP_ROLE_PICKER_MODAL } from '../access-viewer/role-picker-modal.token.js';
import { UAP_USER_PICKER_MODAL } from '../access-viewer/user-picker-modal.token.js';
import { loadSelection, saveSelection, clearSelection } from '../utils/selection-store.js';
import { UapSecurityEventsController, UAP_EVENT_SOURCES } from '../live/security-events.controller.js';
import type { UapRefreshPhase } from '../live/refresh-phase.js';
import '../live/uap-live-refresh.element.js';
import '../shared/components/uap-perm-block.element.js';
import '../shared/components/uap-reasoning-dialog.element.js';
import '../help/uap-page-intro.element.js';
import '../help/uap-selection-panel.element.js';
import type { UapSelectorGroup } from '../help/uap-selection-panel.element.js';
import type {
  UapReasoningDialogElement,
  ReasoningRoleEntries,
} from '../shared/components/uap-reasoning-dialog.element.js';

/** The canonical element-type create verb the audit resolves. */
const ELEMENT_CREATE_OF_TYPE = 'Umb.Element.CreateOfType';

/** Stable identifier used to key this surface's remembered selection (issue #46). */
const SURFACE_ID = 'library-insert-viewer';

/**
 * Library Insert Viewer. The element-type analogue of the document-type Insert Options Viewer: shows,
 * for a user or role, which library element types resolve to allow/deny for creation, with reasoning.
 *
 * Library element-type create-filtering is section-global (Umbraco supplies no parent context for
 * Library creates), so this is a flat list rather than a tree — every decision is resolved at the
 * virtual root.
 *
 * Reads the element-type audit, which belongs to the document-type table's element family: it
 * answers from `Umb.Element.CreateOfType` entries, not from the element (library node) permissions
 * the Library Access Viewer reads. It holds nothing of the user's, so a live or manual refresh
 * just re-asks and swaps the rows in place; there is no client-side cache to drop.
 */
@customElement('uap-library-insert-viewer-root')
export class UapLibraryInsertViewerRootElement extends UmbLitElement {
  #localize = new UmbLocalizationController(this);

  /** Maps role alias → display name (for the reasoning dialog). */
  #roleNames = new Map<string, string>();

  /** The user group being inspected, when the subject is a group. */
  @state() private _selectedRole: RoleInfo | null = null;
  /** The user being inspected, when the subject is a user. */
  @state() private _selectedUser: UserItem | null = null;
  /** Which subject was most recently picked; determines which audit query is made. */
  @state() private _activeSubject: 'role' | 'user' | null = null;

  /** One row per library element type with whether the subject may create it; swapped in place on a refresh. */
  @state() private _rows: DocTypeAuditForNodeRow[] = [];
  /** True only for a first load or a selection change, which hides the list behind the loader. A live refresh never sets it. */
  @state() private _loading = false;
  /** The last load failure, shown above the list. */
  @state() private _error: string | null = null;

  /** Where the refresh control is: idle, refreshing, or briefly confirming an update. */
  @state() private _refreshPhase: UapRefreshPhase = 'idle';

  // ── Reasoning dialog state ─────────────────────────────────────────────────
  /** The row whose cell opened the reasoning dialog. */
  @state() private _reasoningRow: DocTypeAuditForNodeRow | null = null;
  /** The ancestor chain the dialog walks through; only the virtual root here. */
  @state() private _dialogPath: PathNode[] = [];
  /** nodeKey → the user groups' entries that contributed to the result. */
  @state() private _dialogEntriesByNode: Map<string, ReasoningRoleEntries[]> = new Map();
  /** True while the dialog's inheritance path is being fetched. */
  @state() private _dialogLoading = false;
  /** Whether the dialog should show stars where groups at one node disagree. */
  @state() private _dialogShowStars = false;

  @query('uap-reasoning-dialog') private _reasoningDialog!: UapReasoningDialogElement;

  #modalManager: typeof UMB_MODAL_MANAGER_CONTEXT.TYPE | undefined = undefined;
  #abort: AbortController | null = null;

  /** Live-event subscription; also the entry point for the manual refresh control. */
  #liveEvents: UapSecurityEventsController;

  constructor() {
    super();
    this.consumeContext(UMB_MODAL_MANAGER_CONTEXT, (ctx) => { this.#modalManager = ctx ?? undefined; });

    // A viewer holds nothing of the user's, so it never asks and never flags — it just becomes
    // correct again. The keys are ignored deliberately: a rule written on any node, or a change of
    // group membership, can move any row, so there is no subset worth refetching.
    // Sources: Shows which element types a subject may create, resolved from the element-type rules, so it
    // listens for `elementTypePermissions` plus `access` (group membership and group deletion change
    // who is covered by which rule). It must NOT listen to `docTypePermissions`: that is the
    // Document Type editor's half of the same table. Element and folder permission writes belong to
    // the library permission surfaces, not to this one.
    this.#liveEvents = new UapSecurityEventsController(
      this,
      async () => {
        if (!this.#subject) return;
        // Background: the list stays on screen and the rows are swapped when the answer arrives.
        await this.#load(true);
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
    void this.#loadMeta();
    this.#restoreSelection();
  }

  /** Cancels any in-flight load, and detaches any navigation guard, so a dismounted surface does nothing on behalf of a page that is gone. */
  override disconnectedCallback(): void {
    super.disconnectedCallback();
    this.#abort?.abort();
  }

  /** Restores the last-used subject (user group or user) from per-user storage and loads its rows. */
  #restoreSelection(): void {
    const stored = loadSelection(SURFACE_ID);
    if (stored?.subjectKind === 'role' && stored.role) {
      this._selectedRole = stored.role;
      this._activeSubject = 'role';
      void this.#load();
    } else if (stored?.subjectKind === 'user' && stored.user) {
      this._selectedUser = stored.user;
      this._activeSubject = 'user';
      void this.#load();
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
    this.#abort?.abort();
    this._selectedRole = null;
    this._selectedUser = null;
    this._activeSubject = null;
    this._rows = [];
    this._error = null;
    // A load aborted here never reaches its own `finally`, so the loader is cleared here.
    this._loading = false;
    clearSelection(SURFACE_ID);
  }

  /** Loads the user-group display names used by the reasoning dialog. */
  async #loadMeta(): Promise<void> {
    try {
      const roles = await getRoles();
      for (const r of roles) this.#roleNames.set(r.alias, r.name);
    } catch (err) {
      this._error = String(err);
    }
  }

  /** Returns the audited subject as a discriminated object for the API helper. */
  get #subject(): { userKey: string } | { roleAlias: string } | null {
    if (this._activeSubject === 'role' && this._selectedRole) return { roleAlias: this._selectedRole.alias };
    if (this._activeSubject === 'user' && this._selectedUser) return { userKey: this._selectedUser.unique };
    return null;
  }

  /** Returns the role display name (used by the reasoning dialog). */
  #roleName = (alias: string): string => this.#roleNames.get(alias) ?? alias;

  /**
   * Asks the server which library element types the current subject may create.
   *
   * Two modes, because they answer different questions. A first load or selection change
   * (`background` false) shows the loader, since the rows on screen describe somebody else. A live
   * or manual refresh (`background` true) asks the same question again, so the current rows stay on
   * screen and are replaced only when the answer has arrived: no loader, no flash.
   * @param background True to refresh in place without any loading state.
   * @returns A promise that resolves when done. In background mode it rejects on failure, so the
   * refresh control does not claim the data was updated.
   */
  async #load(background = false): Promise<void> {
    const subject = this.#subject;
    if (!subject) return;

    this.#abort?.abort();
    const controller = new AbortController();
    this.#abort = controller;

    if (!background) {
      this._loading = true;
      this._error = null;
    }
    try {
      const result = await getElementTypeAudit(subject, controller.signal);
      if (controller.signal.aborted) return;
      this._rows = result.results;
    } catch (err) {
      if (controller.signal.aborted) return;
      this._error = String(err);
      if (background) throw err;
    } finally {
      // Cleared by whichever load finishes un-superseded, background included. A background
      // refresh aborts a selection load that was still showing the loader, and that load's own
      // `finally` then skips clearing it, so leaving this to foreground loads would strand the
      // list behind the loader for good.
      if (!controller.signal.aborted) this._loading = false;
    }
  }

  // ── Subject pickers ────────────────────────────────────────────────────────

  /** Lets the user pick a user group as the subject, then loads the rows for it. */
  async #openRolePicker(): Promise<void> {
    if (!this.#modalManager) return;
    const modal = this.#modalManager.open(this, UAP_ROLE_PICKER_MODAL, {
      data: { ...(this._selectedRole ? { currentRole: this._selectedRole.alias } : {}) },
    });
    const result = await modal.onSubmit().catch(() => undefined);
    if (!result) return;
    this._selectedRole = result.role;
    this._selectedUser = null;
    this._activeSubject = 'role';
    this.#persistSelection();
    void this.#load();
  }

  /** Lets the user pick a user as the subject, then loads the rows for them. */
  async #openUserPicker(): Promise<void> {
    if (!this.#modalManager) return;
    const modal = this.#modalManager.open(this, UAP_USER_PICKER_MODAL, {
      data: { ...(this._selectedUser ? { currentUser: this._selectedUser.unique } : {}) },
    });
    const result = await modal.onSubmit().catch(() => undefined);
    if (!result) return;
    this._selectedUser = result.user;
    this._selectedRole = null;
    this._activeSubject = 'user';
    this.#persistSelection();
    void this.#load();
  }

  // ── Reasoning dialog ─────────────────────────────────────────────────────

  /**
   * Opens the reasoning dialog for one element type and fills it with the entries that contributed
   * to the result.
   * @param row The audit row whose cell was clicked.
   */
  async #openReasoning(row: DocTypeAuditForNodeRow): Promise<void> {
    this._reasoningRow = row;
    this._dialogPath = [];
    this._dialogEntriesByNode = new Map();
    this._dialogLoading = true;
    this._dialogShowStars = false;

    void this.updateComplete.then(() => this._reasoningDialog.open());

    try {
      // The decision is section-global, so the path is just the virtual root.
      const result = await getDocTypePathEntries(VIRTUAL_ROOT_NODE_KEY, row.contentTypeKey);
      this._dialogPath = result.path;
      const targetKey = VIRTUAL_ROOT_NODE_KEY;

      let relevantRoles: Set<string>;
      if (this._activeSubject === 'role') {
        relevantRoles = new Set([this._selectedRole!.alias]);
      } else {
        relevantRoles = new Set(['$everyone']);
        for (const step of row.reasoning) relevantRoles.add(step.contributingRole);
        for (const step of row.suppressedReasoning ?? []) relevantRoles.add(step.contributingRole);
      }

      const byNode = new Map<string, Map<string, PermissionEntry[]>>();
      for (const entry of result.entries) {
        // Only the element create verb is relevant to this audit.
        if (entry.verb !== ELEMENT_CREATE_OF_TYPE) continue;
        if (!relevantRoles.has(entry.roleAlias)) continue;
        const isTarget = entry.nodeKey === targetKey;
        if (isTarget && entry.scope === 'DescendantsOnly') continue;
        if (!isTarget && entry.scope === 'ThisNodeOnly') continue;

        const roleMap = byNode.get(entry.nodeKey) ?? new Map<string, PermissionEntry[]>();
        const list = roleMap.get(entry.roleAlias) ?? [];
        list.push({
          id: entry.id,
          nodeKey: entry.nodeKey,
          roleAlias: entry.roleAlias,
          verb: entry.verb,
          state: entry.state,
          scope: entry.scope,
          isPriorityOverride: entry.isPriorityOverride,
        });
        roleMap.set(entry.roleAlias, list);
        byNode.set(entry.nodeKey, roleMap);
      }

      const display = new Map<string, ReasoningRoleEntries[]>();
      for (const [nodeKey, roleMap] of byNode) {
        const rows: ReasoningRoleEntries[] = [];
        for (const [role, entries] of roleMap) rows.push({ role, entries });
        display.set(nodeKey, rows);
      }
      this._dialogEntriesByNode = display;

      let showStars = false;
      for (const [, roleMap] of byNode) {
        if (roleMap.size < 2) continue;
        let allow = false;
        let deny = false;
        for (const [, entries] of roleMap) {
          if (entries.some((e) => e.state === 'Allow')) allow = true;
          if (entries.some((e) => e.state === 'Deny')) deny = true;
        }
        if (allow && deny) { showStars = true; break; }
      }
      this._dialogShowStars = showStars;
    } catch {
      // Non-fatal: dialog shows the loading-cleared empty state.
    } finally {
      this._dialogLoading = false;
    }
  }

  /** Resets the dialog state when the user closes the reasoning dialog. */
  #onReasoningClose = (): void => {
    this._reasoningRow = null;
    this._dialogPath = [];
    this._dialogEntriesByNode = new Map();
    this._dialogShowStars = false;
  };

  /**
   * The effective permission shown in the reasoning dialog banner.
   * @returns The open row's result shaped as an effective permission, or `null` when no row is open.
   */
  #currentEffective() {
    const row = this._reasoningRow;
    if (!row) return null;
    return {
      verb: ELEMENT_CREATE_OF_TYPE,
      isAllowed: row.isAllowed,
      isExplicit: row.isExplicit,
      reasoning: row.reasoning,
      wasPriorityOverrideActive: row.wasPriorityOverrideActive,
      suppressedReasoning: row.suppressedReasoning,
    };
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

  // ── Rendering ────────────────────────────────────────────────────────────

  /** Renders the surface: the selection panel with its actions and grid, plus the dialogs that must layer above it. */
  override render(): TemplateResult {
    return html`
      <umb-body-layout headline=${this.#localize.term('uap_libraryInsertViewer_headline')}>
        <uap-page-intro surface="uap-library-insert-viewer" headline=${this.#localize.term('uap_libraryInsertViewer_headline')}></uap-page-intro>
        <uap-selection-panel
          .groups=${this.#selectionGroups}
          promptText=${this.#localize.term('uap_selectSubjectPrompt')}
          ctaIcon="icon-thumbnail-list"
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
          ${!this._loading
            ? (this._rows.length > 0
                ? html`
                    <div class="type-list">
                      <div class="type-header">
                        <span class="type-name">${this.#localize.term('uap_elementTypePermissions_typeHeader')}</span>
                        <span class="type-cell">${this.#localize.term('uap_elementTypePermissions_verbCreate')}</span>
                      </div>
                      ${this._rows.map((row) => this.#renderRow(row))}
                    </div>`
                : html`<p class="empty-msg">${this.#localize.term('uap_elementTypePermissions_noTypes')}</p>`)
            : nothing}
        </uap-selection-panel>
      </umb-body-layout>

      <uap-reasoning-dialog
        .path=${this._dialogPath}
        .entriesByNode=${this._dialogEntriesByNode}
        .showStars=${this._dialogShowStars}
        .effectivePerm=${this.#currentEffective()}
        .subjectName=${this.#currentSubjectName()}
        .verbLabel=${this.#localize.term('uap_elementTypePermissions_verbCreate')}
        .nodeName=${this._reasoningRow?.contentTypeName ?? ''}
        .loading=${this._dialogLoading}
        .defaultState=${'allow'}
        .roleNameLookup=${this.#roleName}
        @uap-reasoning-close=${this.#onReasoningClose}>
      </uap-reasoning-dialog>
    `;
  }

  /**
   * Renders one element type's row with its resolved allow or deny.
   * @param row The audit row to render.
   * @returns The row.
   */
  #renderRow(row: DocTypeAuditForNodeRow): TemplateResult {
    const cls: 'allow' | 'deny' = row.isAllowed ? 'allow' : 'deny';
    const wasOverride = row.wasPriorityOverrideActive === true;
    const info: CellInfo = { split: false, nodeClass: cls, descClass: cls, nodeOverride: wasOverride, descOverride: wasOverride };
    const title = this.#localize.term('uap_clickForReasoning', row.isAllowed ? this.#localize.term('uap_allow') : this.#localize.term('uap_deny'));
    return html`
      <div class="type-row">
        <umb-icon name=${row.contentTypeIcon ?? 'icon-document'}></umb-icon>
        <span class="type-name">${row.contentTypeName}</span>
        <div class="type-cell" title=${title} @click=${() => void this.#openReasoning(row)}>
          <uap-perm-block
            .info=${info}
            priority-override-title=${this.#localize.term('uap_priorityOverrideWonTitle')}></uap-perm-block>
        </div>
      </div>
    `;
  }

  static override styles = css`
    :host { display: block; height: 100%; }
    .loading { display: flex; justify-content: center; padding: 32px; }
    .error-msg { padding: 12px 18px; color: var(--uui-color-danger, #b91c1c); }
    .empty-msg { padding: 32px 18px; color: var(--uui-color-text-alt, #888); }

    .type-list { display: flex; flex-direction: column; gap: 4px; padding: 12px 18px; }
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
    .type-name { flex: 1; }
    .type-cell { width: 72px; flex-shrink: 0; text-align: center; cursor: pointer; }
  `;
}

export default UapLibraryInsertViewerRootElement;

declare global {
  interface HTMLElementTagNameMap {
    'uap-library-insert-viewer-root': UapLibraryInsertViewerRootElement;
  }
}
