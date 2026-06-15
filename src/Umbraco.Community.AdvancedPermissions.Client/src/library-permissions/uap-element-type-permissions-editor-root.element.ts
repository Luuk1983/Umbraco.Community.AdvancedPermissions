import { html, css, nothing, customElement, state } from '@umbraco-cms/backoffice/external/lit';
import type { TemplateResult } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import { UMB_MODAL_MANAGER_CONTEXT } from '@umbraco-cms/backoffice/modal';
import type { RoleInfo } from '../models/permission.models.js';
import { VIRTUAL_ROOT_NODE_KEY } from '../models/permission.models.js';
import type { DocTypeListItem } from '../models/doc-type-permission.models.js';
import { getElementTypes, getDocTypePermissions, saveDocTypePermissions } from '../api/doc-type-permissions.api.js';
import { UAP_ROLE_PICKER_MODAL } from '../access-viewer/role-picker-modal.token.js';
import '../components/uap-picker-button.element.js';

/** The element-type create-of-type verb stored in the doc-type table. */
const ELEMENT_CREATE_OF_TYPE = 'Umb.Element.CreateOfType';

/**
 * Library element-type permissions editor. Controls which element types each user group may create in
 * the Library. Library element-type filtering is section-global (no node context), so each type is a
 * single Allow/Deny choice stored on the virtual root. Allowed by default — a Deny removes the type from
 * the Library create menu.
 */
@customElement('uap-element-type-permissions-editor-root')
export class UapElementTypePermissionsEditorRootElement extends UmbLitElement {
  #localize = new UmbLocalizationController(this);

  @state() private _selectedRole: RoleInfo | null = null;
  @state() private _types: DocTypeListItem[] = [];
  /** typeKey → effective state. Absent means "allow" (the default). */
  @state() private _states: Map<string, 'allow' | 'deny'> = new Map();
  @state() private _pending: Set<string> = new Set();
  @state() private _loading = false;
  @state() private _saving = false;
  @state() private _error: string | null = null;

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

      // Resolve the current explicit state per type (default = allow).
      const states = new Map<string, 'allow' | 'deny'>();
      await Promise.all(types.map(async (t) => {
        const entries = await getDocTypePermissions(this._selectedRole!.alias, t.key, controller.signal);
        const createEntry = entries.find((e) => e.verb === ELEMENT_CREATE_OF_TYPE);
        states.set(t.key, createEntry?.state === 'Deny' ? 'deny' : 'allow');
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

  #toggle(typeKey: string, allowed: boolean): void {
    const next = new Map(this._states);
    next.set(typeKey, allowed ? 'allow' : 'deny');
    this._states = next;
    const pending = new Set(this._pending);
    pending.add(typeKey);
    this._pending = pending;
  }

  async #save(): Promise<void> {
    if (!this._pending.size || !this._selectedRole || this._saving) return;
    this._saving = true;
    try {
      for (const typeKey of this._pending) {
        const state = this._states.get(typeKey) ?? 'allow';
        // Deny → store one virtual-root entry; Allow → clear (revert to default allow).
        const entries = state === 'deny'
          ? [{ verb: ELEMENT_CREATE_OF_TYPE, state: 'Deny' as const, scope: 'ThisNodeAndDescendants' as const, isPriorityOverride: false }]
          : [];
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

  override render(): TemplateResult {
    const hasPending = this._pending.size > 0;
    return html`
      <umb-body-layout headline=${this.#localize.term('uap_elementTypePermissions_headline')}>
        <div class="toolbar">
          <uap-picker-button
            label=${this.#localize.term('uap_chooseRole')}
            .selectedName=${this._selectedRole?.name ?? ''}
            icon="icon-users"
            @click=${() => void this.#openRolePicker()}>
          </uap-picker-button>
          ${hasPending
            ? html`
                <uui-button label=${this.#localize.term('uap_saveChanges')} look="primary" color="positive" ?loading=${this._saving} @click=${() => void this.#save()}>
                  ${this.#localize.term('uap_saveChanges')}
                </uui-button>
                <uui-button label=${this.#localize.term('uap_discard')} look="outline" @click=${() => { this._pending = new Set(); void this.#load(); }}>
                  ${this.#localize.term('uap_discard')}
                </uui-button>`
            : nothing}
        </div>

        <p class="intro">${this.#localize.term('uap_elementTypePermissions_intro')}</p>

        ${this._error ? html`<p class="error-msg">⚠ ${this._error}</p>` : nothing}
        ${this._loading ? html`<div class="loading"><uui-loader></uui-loader></div>` : nothing}
        ${!this._selectedRole ? html`<p class="empty-msg">${this.#localize.term('uap_library_selectRolePrompt')}</p>` : nothing}

        ${this._selectedRole && !this._loading
          ? (this._types.length > 0
              ? html`<div class="type-list">${this._types.map((t) => this.#renderType(t))}</div>`
              : html`<p class="empty-msg">${this.#localize.term('uap_elementTypePermissions_noTypes')}</p>`)
          : nothing}
      </umb-body-layout>
    `;
  }

  #renderType(t: DocTypeListItem): TemplateResult {
    const allowed = (this._states.get(t.key) ?? 'allow') === 'allow';
    const pending = this._pending.has(t.key);
    return html`
      <div class="type-row ${pending ? 'pending' : ''}">
        <umb-icon name=${t.icon ?? 'icon-document'}></umb-icon>
        <span class="type-name">${t.name}</span>
        <uui-toggle
          label=${this.#localize.term('uap_elementTypePermissions_verbCreate')}
          ?checked=${allowed}
          @change=${(e: Event) => this.#toggle(t.key, (e.target as HTMLInputElement).checked)}>
          ${allowed ? this.#localize.term('uap_allow') : this.#localize.term('uap_deny')}
        </uui-toggle>
      </div>
    `;
  }

  static override styles = css`
    :host { display: block; height: 100%; }
    .toolbar {
      display: flex;
      align-items: center;
      gap: var(--uui-size-4, 12px);
      padding: var(--uui-size-3, 9px) var(--uui-size-6, 18px);
      background: var(--uui-color-surface, #fff);
      border-bottom: 1px solid var(--uui-color-border, #e0e0e0);
      flex-wrap: wrap;
    }
    .intro { padding: 12px 18px 0; color: var(--uui-color-text-alt, #666); margin: 0; line-height: 1.4; }
    .loading { display: flex; justify-content: center; padding: 32px; }
    .error-msg { padding: 12px 18px; color: var(--uui-color-danger, #b91c1c); }
    .empty-msg { padding: 32px 18px; color: var(--uui-color-text-alt, #888); }

    .type-list {
      display: flex;
      flex-direction: column;
      gap: 4px;
      padding: 12px 18px;
    }
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
  `;
}

export default UapElementTypePermissionsEditorRootElement;

declare global {
  interface HTMLElementTagNameMap {
    'uap-element-type-permissions-editor-root': UapElementTypePermissionsEditorRootElement;
  }
}
