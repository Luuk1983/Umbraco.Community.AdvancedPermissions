import { html, css, nothing, customElement, state, query } from '@umbraco-cms/backoffice/external/lit';
import type { TemplateResult } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import { UMB_MODAL_MANAGER_CONTEXT } from '@umbraco-cms/backoffice/modal';
import type { RoleInfo } from '../models/permission.models.js';
import { VIRTUAL_ROOT_NODE_KEY } from '../models/permission.models.js';
import type { DocTypeListItem, SaveDocTypePermissionItem } from '../models/doc-type-permission.models.js';
import { getElementTypes, getDocTypePermissions, saveDocTypePermissions } from '../api/doc-type-permissions.api.js';
import { UAP_ROLE_PICKER_MODAL } from '../access-viewer/role-picker-modal.token.js';
import type { CellInfo } from '../utils/cell-info.js';
import type { PendingVerbEntries } from '../utils/compose-entries.js';
import '../components/uap-picker-button.element.js';
import '../shared/components/uap-perm-block.element.js';
import '../shared/components/uap-permission-scope-dialog.element.js';
import type { UapPermissionScopeDialogElement } from '../shared/components/uap-permission-scope-dialog.element.js';
import '../help/uap-page-intro.element.js';
import '../help/uap-selection-panel.element.js';
import type { UapSelectorGroup } from '../help/uap-selection-panel.element.js';

/** The element-type create-of-type verb stored in the doc-type table. */
const ELEMENT_CREATE_OF_TYPE = 'Umb.Element.CreateOfType';

/** Tri-state choice + priority-override for a single element type. */
type TypeState = { state: 'inherit' | 'allow' | 'deny'; isPriorityOverride: boolean };

/** The neutral starting state for a type with no stored entry (allowed by default). */
const DEFAULT_STATE: TypeState = { state: 'inherit', isPriorityOverride: false };

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
 */
@customElement('uap-element-type-permissions-editor-root')
export class UapElementTypePermissionsEditorRootElement extends UmbLitElement {
  #localize = new UmbLocalizationController(this);

  @state() private _selectedRole: RoleInfo | null = null;
  @state() private _types: DocTypeListItem[] = [];
  /** typeKey → tri-state + override. Absent means the default (inherit/allowed). */
  @state() private _states: Map<string, TypeState> = new Map();
  @state() private _pending: Set<string> = new Set();
  @state() private _loading = false;
  @state() private _saving = false;
  @state() private _error: string | null = null;

  // ── Scope dialog state ───────────────────────────────────────────────────
  @state() private _pickerType: DocTypeListItem | null = null;
  @state() private _pickerState: 'inherit' | 'allow' | 'deny' = 'inherit';
  @state() private _pickerOverride = false;

  @query('uap-permission-scope-dialog') private _scopeDialog!: UapPermissionScopeDialogElement;

  #notificationContext: typeof UMB_NOTIFICATION_CONTEXT.TYPE | undefined = undefined;
  #modalManager: typeof UMB_MODAL_MANAGER_CONTEXT.TYPE | undefined = undefined;
  #abort: AbortController | null = null;

  constructor() {
    super();
    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (ctx) => { this.#notificationContext = ctx ?? undefined; });
    this.consumeContext(UMB_MODAL_MANAGER_CONTEXT, (ctx) => { this.#modalManager = ctx ?? undefined; });
  }

  override disconnectedCallback(): void {
    super.disconnectedCallback();
    this.#abort?.abort();
  }

  async #openRolePicker(): Promise<void> {
    if (!this.#modalManager) return;
    const modal = this.#modalManager.open(this, UAP_ROLE_PICKER_MODAL, {
      data: { ...(this._selectedRole ? { currentRole: this._selectedRole.alias } : {}) },
    });
    const result = await modal.onSubmit().catch(() => undefined);
    if (!result) return;
    this._selectedRole = result.role;
    this._pending = new Set();
    void this.#load();
  }

  async #load(): Promise<void> {
    if (!this._selectedRole) return;
    this.#abort?.abort();
    const controller = new AbortController();
    this.#abort = controller;

    this._loading = true;
    this._error = null;
    try {
      const types = await getElementTypes(controller.signal);
      if (controller.signal.aborted) return;

      // Resolve the current stored state per type (default = inherit/allowed).
      const states = new Map<string, TypeState>();
      await Promise.all(types.map(async (t) => {
        const entries = await getDocTypePermissions(this._selectedRole!.alias, t.key, controller.signal);
        const createEntry = entries.find((e) => e.verb === ELEMENT_CREATE_OF_TYPE);
        states.set(t.key, createEntry
          ? { state: createEntry.state === 'Deny' ? 'deny' : 'allow', isPriorityOverride: createEntry.isPriorityOverride }
          : { ...DEFAULT_STATE });
      }));
      if (controller.signal.aborted) return;

      this._types = types;
      this._states = states;
    } catch (err) {
      if (controller.signal.aborted) return;
      this._error = String(err);
    } finally {
      if (!controller.signal.aborted) this._loading = false;
    }
  }

  #openTypePicker(t: DocTypeListItem): void {
    const current = this._states.get(t.key) ?? DEFAULT_STATE;
    this._pickerType = t;
    this._pickerState = current.state;
    this._pickerOverride = current.isPriorityOverride;
    void this.updateComplete.then(() => this._scopeDialog.open());
  }

  #handleScopeApply(e: CustomEvent<{ entries: PendingVerbEntries }>): void {
    if (!this._pickerType) return;
    const entry = e.detail.entries[0];
    const next: TypeState = entry
      ? { state: entry.state === 'Allow' ? 'allow' : 'deny', isPriorityOverride: entry.isPriorityOverride }
      : { ...DEFAULT_STATE };

    this._states = new Map(this._states).set(this._pickerType.key, next);
    this._pending = new Set(this._pending).add(this._pickerType.key);
  }

  async #save(): Promise<void> {
    if (!this._pending.size || !this._selectedRole || this._saving) return;
    this._saving = true;
    try {
      for (const typeKey of this._pending) {
        const ts = this._states.get(typeKey) ?? DEFAULT_STATE;
        // Inherit → clear (revert to default allow). Allow/Deny → one virtual-root entry; scope is
        // ThisNodeAndDescendants because element-type filtering is section-global (no node context).
        const entries: SaveDocTypePermissionItem[] = ts.state === 'inherit'
          ? []
          : [{
              verb: ELEMENT_CREATE_OF_TYPE,
              state: ts.state === 'allow' ? 'Allow' : 'Deny',
              scope: 'ThisNodeAndDescendants',
              isPriorityOverride: ts.isPriorityOverride,
            }];
        await saveDocTypePermissions(VIRTUAL_ROOT_NODE_KEY, this._selectedRole.alias, typeKey, entries);
      }
      this._pending = new Set();
      this.#notificationContext?.peek('positive', { data: { message: this.#localize.term('uap_permissionsSaved') } });
    } catch (err) {
      this.#notificationContext?.peek('danger', { data: { message: this.#localize.term('uap_saveFailed', String(err)) } });
    } finally {
      this._saving = false;
    }
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

  override render(): TemplateResult {
    return html`
      <umb-body-layout headline=${this.#localize.term('uap_elementTypePermissions_headline')}>
        <uap-page-intro surface="uap-element-type-permissions" headline=${this.#localize.term('uap_elementTypePermissions_headline')}></uap-page-intro>
        <uap-selection-panel
          .groups=${this.#selectionGroups}
          promptText=${this.#localize.term('uap_library_selectRolePrompt')}
          ctaIcon="icon-thumbnail-list"
          orLabel=${this.#localize.term('uap_subjectOr')}
          @uap-selector-click=${(e: CustomEvent<{ id: string }>) => this.#onSelectorClick(e.detail.id)}>
          ${this._pending.size > 0
            ? html`<div slot="actions">
                <uui-button label=${this.#localize.term('uap_saveChanges')} look="primary" color="positive" ?loading=${this._saving} @click=${() => void this.#save()}>
                  ${this.#localize.term('uap_saveChanges')}
                </uui-button>
                <uui-button label=${this.#localize.term('uap_discard')} look="outline" @click=${() => { this._pending = new Set(); void this.#load(); }}>
                  ${this.#localize.term('uap_discard')}
                </uui-button>
              </div>`
            : nothing}
          ${this._error ? html`<p class="error-msg">⚠ ${this._error}</p>` : nothing}
          ${this._loading ? html`<div class="loading"><uui-loader></uui-loader></div>` : nothing}
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
                : html`<p class="empty-msg">${this.#localize.term('uap_elementTypePermissions_noTypes')}</p>`)
            : nothing}
        </uap-selection-panel>
      </umb-body-layout>

      ${this.#renderDialog()}
    `;
  }

  #renderType(t: DocTypeListItem): TemplateResult {
    const ts = this._states.get(t.key) ?? DEFAULT_STATE;
    const pending = this._pending.has(t.key);
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
        <div class="type-cell" @click=${() => this.#openTypePicker(t)}>
          <uap-perm-block
            .info=${info}
            ?pending=${pending}
            priority-override-title=${this.#localize.term('uap_priorityOverrideBadgeTitle')}></uap-perm-block>
        </div>
      </div>
    `;
  }

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
  `;
}

export default UapElementTypePermissionsEditorRootElement;

declare global {
  interface HTMLElementTagNameMap {
    'uap-element-type-permissions-editor-root': UapElementTypePermissionsEditorRootElement;
  }
}
