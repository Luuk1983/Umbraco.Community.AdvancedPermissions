import { html, css, customElement, state, property } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import type { UmbModalContext } from '@umbraco-cms/backoffice/modal';
import type { RolePickerModalData, RolePickerModalValue } from './role-picker-modal.token.js';
import type { RoleInfo } from '../models/permission.models.js';
import { getRoles } from '../api/advanced-permissions.api.js';

/**
 * Role picker modal — fetches and shows a filterable list of roles in a sidebar.
 * Selecting a role immediately confirms and closes.
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

  #cancel(): void {
    this.modalContext?.reject();
  }

  override render() {
    const filtered = this.#filteredRoles;

    return html`
      <umb-body-layout .headline=${this.#localize.term('uap_rolePickerHeadline')}>

        <div id="filter-bar">
          <uui-input
            type="search"
            label=${this.#localize.term('uap_rolePickerFilter')}
            placeholder=${this.#localize.term('uap_rolePickerFilter')}
            .value=${this._filter}
            @input=${(e: InputEvent) => { this._filter = (e.target as HTMLInputElement).value; }}>
          </uui-input>
        </div>

        ${this._loading
          ? html`<div class="loading"><uui-loader></uui-loader></div>`
          : filtered.length === 0
            ? html`<p class="empty">${this.#localize.term('uap_rolePickerNoResults')}</p>`
            : html`
                <uui-table>
                  <uui-table-head>
                    <uui-table-head-cell>${this.#localize.term('uap_rolePickerNameHeader')}</uui-table-head-cell>
                  </uui-table-head>
                  ${filtered.map((role) => this.#renderRow(role))}
                </uui-table>
              `}

        <div slot="actions">
          <uui-button
            label=${this.#localize.term('uap_cancel')}
            @click=${this.#cancel}>
            ${this.#localize.term('uap_cancel')}
          </uui-button>
        </div>

      </umb-body-layout>
    `;
  }

  #renderRow(role: RoleInfo) {
    const isCurrent = role.alias === this.#currentRole;
    const initial = role.name[0]?.toUpperCase() ?? '?';
    return html`
      <uui-table-row
        ?selected=${isCurrent}
        @click=${() => this.#selectRole(role)}>
        <uui-table-cell>
          <div class="role-row">
            <div class="role-avatar">${initial}</div>
            <span class="role-name">${role.name}</span>
          </div>
        </uui-table-cell>
      </uui-table-row>
    `;
  }

  static override styles = css`
    :host {
      display: contents;
    }

    #filter-bar {
      position: sticky;
      top: 0;
      z-index: 2;
      padding: var(--uui-size-3, 9px) var(--uui-size-6, 18px);
      border-bottom: 1px solid var(--uui-color-border);
      background: var(--uui-color-surface);
    }

    #filter-bar uui-input {
      width: 100%;
    }

    uui-table {
      width: 100%;
    }

    uui-table-head {
      position: sticky;
      top: 53px;
      z-index: 1;
      background: var(--uui-color-surface);
    }

    uui-table-row {
      cursor: pointer;
    }

    uui-table-row[selected] {
      background: color-mix(in srgb, var(--uui-color-selected) 10%, transparent);
      outline: 2px solid var(--uui-color-selected, #3544b1);
      outline-offset: -2px;
    }

    .role-row {
      display: flex;
      align-items: center;
      gap: var(--uui-size-3, 9px);
    }

    .role-avatar {
      width: 36px;
      height: 36px;
      border-radius: 50%;
      background: var(--uui-color-default, #1b264f);
      color: var(--uui-color-default-contrast, #fff);
      display: flex;
      align-items: center;
      justify-content: center;
      font-weight: 600;
      font-size: 14px;
      flex-shrink: 0;
    }

    .role-name {
      flex: 1;
    }

    .loading {
      display: flex;
      justify-content: center;
      padding: var(--uui-size-6, 18px);
    }

    .empty {
      padding: var(--uui-size-6, 18px);
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
