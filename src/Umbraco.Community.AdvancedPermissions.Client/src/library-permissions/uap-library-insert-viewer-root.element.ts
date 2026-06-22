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
import '../components/uap-picker-button.element.js';
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

/**
 * Library Insert Viewer. The element-type analogue of the document-type Insert Options Viewer: shows,
 * for a user or role, which library element types resolve to allow/deny for creation, with reasoning.
 *
 * Library element-type create-filtering is section-global (Umbraco supplies no parent context for
 * Library creates), so this is a flat list rather than a tree — every decision is resolved at the
 * virtual root.
 */
@customElement('uap-library-insert-viewer-root')
export class UapLibraryInsertViewerRootElement extends UmbLitElement {
  #localize = new UmbLocalizationController(this);

  /** Maps role alias → display name (for the reasoning dialog). */
  #roleNames = new Map<string, string>();

  @state() private _selectedRole: RoleInfo | null = null;
  @state() private _selectedUser: UserItem | null = null;
  @state() private _activeSubject: 'role' | 'user' | null = null;

  @state() private _rows: DocTypeAuditForNodeRow[] = [];
  @state() private _loading = false;
  @state() private _error: string | null = null;

  // ── Reasoning dialog state ─────────────────────────────────────────────────
  @state() private _reasoningRow: DocTypeAuditForNodeRow | null = null;
  @state() private _dialogPath: PathNode[] = [];
  @state() private _dialogEntriesByNode: Map<string, ReasoningRoleEntries[]> = new Map();
  @state() private _dialogLoading = false;
  @state() private _dialogShowStars = false;

  @query('uap-reasoning-dialog') private _reasoningDialog!: UapReasoningDialogElement;

  #modalManager: typeof UMB_MODAL_MANAGER_CONTEXT.TYPE | undefined = undefined;
  #abort: AbortController | null = null;

  constructor() {
    super();
    this.consumeContext(UMB_MODAL_MANAGER_CONTEXT, (ctx) => { this.#modalManager = ctx ?? undefined; });
  }

  override connectedCallback(): void {
    super.connectedCallback();
    void this.#loadMeta();
  }

  override disconnectedCallback(): void {
    super.disconnectedCallback();
    this.#abort?.abort();
  }

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

  async #load(): Promise<void> {
    const subject = this.#subject;
    if (!subject) return;

    this.#abort?.abort();
    const controller = new AbortController();
    this.#abort = controller;

    this._loading = true;
    this._error = null;
    try {
      const result = await getElementTypeAudit(subject, controller.signal);
      if (controller.signal.aborted) return;
      this._rows = result.results;
    } catch (err) {
      if (controller.signal.aborted) return;
      this._error = String(err);
    } finally {
      if (!controller.signal.aborted) this._loading = false;
    }
  }

  // ── Subject pickers ────────────────────────────────────────────────────────

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
    void this.#load();
  }

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
    void this.#load();
  }

  // ── Reasoning dialog ─────────────────────────────────────────────────────

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

  #onReasoningClose = (): void => {
    this._reasoningRow = null;
    this._dialogPath = [];
    this._dialogEntriesByNode = new Map();
    this._dialogShowStars = false;
  };

  /** The effective permission shown in the reasoning dialog banner. */
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

  #currentSubjectName(): string {
    if (this._activeSubject === 'role') return this._selectedRole?.name ?? '';
    if (this._activeSubject === 'user') return this._selectedUser?.name ?? '';
    return '';
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

  #onSelectorClick(id: string): void {
    if (id === 'group') void this.#openRolePicker();
    else if (id === 'user') void this.#openUserPicker();
  }

  // ── Rendering ────────────────────────────────────────────────────────────

  override render(): TemplateResult {
    return html`
      <umb-body-layout headline=${this.#localize.term('uap_libraryInsertViewer_headline')}>
        <uap-page-intro surface="uap-library-insert-viewer" headline=${this.#localize.term('uap_libraryInsertViewer_headline')}></uap-page-intro>
        <uap-selection-panel
          .groups=${this.#selectionGroups}
          promptText=${this.#localize.term('uap_selectSubjectPrompt')}
          ctaIcon="icon-eye"
          orLabel=${this.#localize.term('uap_subjectOr')}
          @uap-selector-click=${(e: CustomEvent<{ id: string }>) => this.#onSelectorClick(e.detail.id)}>
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
