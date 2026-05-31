import { html, css, customElement, state, property, repeat } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement, umbFocus } from '@umbraco-cms/backoffice/lit-element';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import type { UmbModalContext } from '@umbraco-cms/backoffice/modal';
import type { UUIInputEvent } from '@umbraco-cms/backoffice/external/uui';
import type { RolePickerModalData, RolePickerModalValue } from './role-picker-modal.token.js';
import type { RoleInfo } from '../models/permission.models.js';
import { getRoles } from '../api/advanced-permissions.api.js';

/**
 * Role (user group) picker modal — fetches and shows a filterable list of user groups,
 * including the virtual "All Users Group" ($everyone). Selecting an entry immediately
 * confirms and closes.
 *
 * Layout mirrors Umbraco v18's native picker modals (umb-body-layout → uui-box → search
 * field + uui-ref-node rows) so it inherits the native rounded styling and design tokens
 * and avoids the horizontal-overflow that a custom uui-table layout caused under v18.
 */
@customElement('uap-role-picker-modal')
export class UapRolePickerModalElement extends UmbLitElement {
  #localize = new UmbLocalizationController(this);

  @property({ attribute: false })
  modalContext?: UmbModalContext<RolePickerModalData, RolePickerModalValue>;

  @state() private _roles: RoleInfo[] = [];
  @state() private _loading = true;
  @state() private _filter = '';

  get #currentRole(): string | undefined {
    return this.modalContext?.data?.currentRole;
  }

  get #filteredRoles(): RoleInfo[] {
    const filter = this._filter.trim().toLowerCase();
    if (!filter) return this._roles;
    return this._roles.filter((r) => r.name.toLowerCase().includes(filter));
  }

  override async connectedCallback(): Promise<void> {
    super.connectedCallback();
    try {
      this._roles = await getRoles();
    } finally {
      this._loading = false;
    }
  }

  #selectRole(role: RoleInfo): void {
    this.modalContext?.updateValue({ role });
    this.modalContext?.submit();
  }

  #onFilterInput(e: UUIInputEvent): void {
    this._filter = (e.target.value as string) ?? '';
  }

  #cancel(): void {
    this.modalContext?.reject();
  }

  override render() {
    const filtered = this.#filteredRoles;

    return html`
      <umb-body-layout headline=${this.#localize.term('uap_rolePickerHeadline')}>
        <uui-box>
          <uui-input
            type="search"
            id="filter"
            label=${this.#localize.term('uap_rolePickerFilter')}
            placeholder=${this.#localize.term('uap_rolePickerFilter')}
            .value=${this._filter}
            @input=${this.#onFilterInput}
            ${umbFocus()}>
            <uui-icon name="search" slot="prepend" id="filter-icon"></uui-icon>
          </uui-input>

          ${this._loading
            ? html`<div class="center"><uui-loader></uui-loader></div>`
            : filtered.length === 0
              ? html`<p class="empty">${this.#localize.term('uap_rolePickerNoResults')}</p>`
              : repeat(filtered, (role) => role.alias, (role) => this.#renderRow(role))}
        </uui-box>

        <div slot="actions">
          <uui-button label=${this.#localize.term('uap_cancel')} @click=${this.#cancel}></uui-button>
        </div>
      </umb-body-layout>
    `;
  }

  #renderRow(role: RoleInfo) {
    const isCurrent = role.alias === this.#currentRole;
    const icon = role.isEveryone ? 'icon-globe' : 'icon-users';
    return html`
      <uui-ref-node
        name=${role.name}
        selectable
        select-only
        ?selected=${isCurrent}
        @selected=${() => this.#selectRole(role)}
        @deselected=${() => this.#selectRole(role)}>
        <umb-icon slot="icon" name=${icon}></umb-icon>
      </uui-ref-node>
    `;
  }

  static override styles = css`
    :host {
      display: contents;
    }

    #filter {
      width: 100%;
      margin-bottom: var(--uui-size-space-4);
    }

    #filter-icon {
      display: flex;
      color: var(--uui-color-border);
      height: 100%;
      padding-left: var(--uui-size-space-2);
    }

    /* uui-ref-node ships a 250px min-width; relax it so rows never force a horizontal
       scrollbar inside the narrow sidebar modal. */
    uui-ref-node {
      min-width: 0;
    }

    uui-ref-node:not(:last-of-type) {
      margin-bottom: var(--uui-size-space-1);
    }

    .center {
      display: flex;
      justify-content: center;
      padding: var(--uui-size-6);
    }

    .empty {
      margin: 0;
      padding: var(--uui-size-6);
      color: var(--uui-color-text-alt);
    }
  `;
}

export default UapRolePickerModalElement;

declare global {
  interface HTMLElementTagNameMap {
    'uap-role-picker-modal': UapRolePickerModalElement;
  }
}
