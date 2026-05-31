import { html, css, customElement, state, property, repeat } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement, umbFocus } from '@umbraco-cms/backoffice/lit-element';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import type { UmbModalContext } from '@umbraco-cms/backoffice/modal';
import type { UUIInputEvent } from '@umbraco-cms/backoffice/external/uui';
import type { UserPickerModalData, UserPickerModalValue } from './user-picker-modal.token.js';
import type { UserItem } from '../models/permission.models.js';
import { getUsers } from '../api/advanced-permissions.api.js';

/**
 * User picker modal — fetches and shows a filterable list of users. Selecting a user
 * immediately confirms and closes.
 *
 * Layout mirrors Umbraco v18's native picker modals (umb-body-layout → uui-box → search
 * field + uui-ref-node rows) so it inherits the native rounded styling and design tokens
 * and avoids the horizontal-overflow that a custom uui-table layout caused under v18.
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

  #onFilterInput(e: UUIInputEvent): void {
    this._filter = (e.target.value as string) ?? '';
  }

  #cancel(): void {
    this.modalContext?.reject();
  }

  override render() {
    const filtered = this.#filteredUsers;

    return html`
      <umb-body-layout headline=${this.#localize.term('uap_userPickerHeadline')}>
        <uui-box>
          <uui-input
            type="search"
            id="filter"
            label=${this.#localize.term('uap_userPickerFilter')}
            placeholder=${this.#localize.term('uap_userPickerFilter')}
            .value=${this._filter}
            @input=${this.#onFilterInput}
            ${umbFocus()}>
            <uui-icon name="search" slot="prepend" id="filter-icon"></uui-icon>
          </uui-input>

          ${this._loading
            ? html`<div class="center"><uui-loader></uui-loader></div>`
            : filtered.length === 0
              ? html`<p class="empty">${this.#localize.term('uap_userPickerNoResults')}</p>`
              : repeat(filtered, (user) => user.unique, (user) => this.#renderRow(user))}
        </uui-box>

        <div slot="actions">
          <uui-button label=${this.#localize.term('uap_cancel')} @click=${this.#cancel}></uui-button>
        </div>
      </umb-body-layout>
    `;
  }

  #renderRow(user: UserItem) {
    const isCurrent = user.unique === this.#currentUser;
    const avatarUrl = user.avatarUrls[0];
    return html`
      <uui-ref-node
        name=${user.name}
        selectable
        select-only
        ?selected=${isCurrent}
        @selected=${() => this.#selectUser(user)}
        @deselected=${() => this.#selectUser(user)}>
        <uui-avatar slot="icon" name=${user.name} img-src=${avatarUrl ?? ''}></uui-avatar>
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

    /* Keep the avatar compact so it matches the row height of Umbraco's native pickers. */
    uui-avatar {
      font-size: var(--uui-size-4);
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

export default UapUserPickerModalElement;

declare global {
  interface HTMLElementTagNameMap {
    'uap-user-picker-modal': UapUserPickerModalElement;
  }
}
