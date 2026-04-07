import { html, css, customElement, state, property } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import type { UmbModalContext } from '@umbraco-cms/backoffice/modal';
import type { UserPickerModalData, UserPickerModalValue } from './user-picker-modal.token.js';
import type { UserItem } from '../models/permission.models.js';
import { getUsers } from '../api/advanced-permissions.api.js';

/**
 * User picker modal — fetches and shows a filterable list of users in a sidebar.
 * Selecting a user immediately confirms and closes.
 */
@customElement('uap-user-picker-modal')
export class UapUserPickerModalElement extends UmbLitElement {
  #localize = new UmbLocalizationController(this);

  @property({ attribute: false })
  modalContext?: UmbModalContext<UserPickerModalData, UserPickerModalValue>;

  @state() private _users: UserItem[] = [];
  @state() private _loading = true;
  @state() private _filter = '';

  get #currentUser(): string | undefined {
    return this.modalContext?.data?.currentUser;
  }

  get #filteredUsers(): UserItem[] {
    const filter = this._filter.trim().toLowerCase();
    if (!filter) return this._users;
    return this._users.filter((u) => u.name.toLowerCase().includes(filter));
  }

  override async connectedCallback(): Promise<void> {
    super.connectedCallback();
    try {
      this._users = await getUsers();
    } finally {
      this._loading = false;
    }
  }

  #selectUser(user: UserItem): void {
    this.modalContext?.updateValue({ user });
    this.modalContext?.submit();
  }

  #cancel(): void {
    this.modalContext?.reject();
  }

  override render() {
    const filtered = this.#filteredUsers;

    return html`
      <umb-body-layout .headline=${this.#localize.term('uap_userPickerHeadline')}>

        <div id="filter-bar">
          <uui-input
            type="search"
            label=${this.#localize.term('uap_userPickerFilter')}
            placeholder=${this.#localize.term('uap_userPickerFilter')}
            .value=${this._filter}
            @input=${(e: InputEvent) => { this._filter = (e.target as HTMLInputElement).value; }}>
          </uui-input>
        </div>

        ${this._loading
          ? html`<div class="loading"><uui-loader></uui-loader></div>`
          : filtered.length === 0
            ? html`<p class="empty">${this.#localize.term('uap_userPickerNoResults')}</p>`
            : html`
                <uui-table>
                  <uui-table-head>
                    <uui-table-head-cell>${this.#localize.term('uap_userPickerNameHeader')}</uui-table-head-cell>
                  </uui-table-head>
                  ${filtered.map((user) => this.#renderRow(user))}
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

  #renderRow(user: UserItem) {
    const isCurrent = user.unique === this.#currentUser;
    const initial = user.name[0]?.toUpperCase() ?? '?';
    const avatarUrl = user.avatarUrls[0];
    return html`
      <uui-table-row
        ?selected=${isCurrent}
        @click=${() => this.#selectUser(user)}>
        <uui-table-cell>
          <div class="user-row">
            ${avatarUrl
              ? html`<img class="user-avatar-img" src=${avatarUrl} alt="" />`
              : html`<div class="user-avatar">${initial}</div>`}
            <span class="user-name">${user.name}</span>
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

    .user-row {
      display: flex;
      align-items: center;
      gap: var(--uui-size-3, 9px);
    }

    .user-avatar {
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

    .user-avatar-img {
      width: 36px;
      height: 36px;
      border-radius: 50%;
      object-fit: cover;
      flex-shrink: 0;
    }

    .user-name {
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

export default UapUserPickerModalElement;

declare global {
  interface HTMLElementTagNameMap {
    'uap-user-picker-modal': UapUserPickerModalElement;
  }
}
