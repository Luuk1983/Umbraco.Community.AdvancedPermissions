import { html, css, nothing, customElement, state } from '@umbraco-cms/backoffice/external/lit';
import type { TemplateResult } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import { UMB_MODAL_MANAGER_CONTEXT } from '@umbraco-cms/backoffice/modal';
import { UMB_DOCUMENT_PICKER_MODAL } from '@umbraco-cms/backoffice/document';
import type { UserItem } from '../models/permission.models.js';
import { VIRTUAL_ROOT_NODE_KEY } from '../models/permission.models.js';
import type { DocTypeCreateAuditItem } from '../models/doc-type-permission.models.js';
import { getDocTypeCreateAudit } from '../api/doc-type-permissions.api.js';
import { UAP_USER_PICKER_MODAL } from '../access-viewer/user-picker-modal.token.js';
import '../components/uap-picker-button.element.js';

/**
 * Create Audit workspace. Picks a user and a parent node; for each non-element doc-type,
 * shows whether the user may create instances under that parent, plus the reasoning.
 */
@customElement('uap-doc-type-create-audit-root')
export class UapDocTypeCreateAuditRootElement extends UmbLitElement {
  #localize = new UmbLocalizationController(this);

  @state() private _selectedUser: UserItem | null = null;
  @state() private _selectedParentKey: string | null = null;
  @state() private _selectedParentLabel: string | null = null;

  @state() private _rows: DocTypeCreateAuditItem[] = [];
  @state() private _loading = false;
  @state() private _error: string | null = null;

  #modalManager: typeof UMB_MODAL_MANAGER_CONTEXT.TYPE | undefined = undefined;
  #abort: AbortController | null = null;

  constructor() {
    super();
    this.consumeContext(UMB_MODAL_MANAGER_CONTEXT, (ctx) => {
      this.#modalManager = ctx ?? undefined;
    });
  }

  override disconnectedCallback(): void {
    super.disconnectedCallback();
    this.#abort?.abort();
  }

  async #pickUser(): Promise<void> {
    if (!this.#modalManager) return;
    const modal = this.#modalManager.open(this, UAP_USER_PICKER_MODAL, {
      data: {
        ...(this._selectedUser ? { currentUser: this._selectedUser.unique } : {}),
      },
    });
    const value = await modal.onSubmit().catch(() => undefined);
    if (!value) return;
    this._selectedUser = value.user;
    void this.#runAudit();
  }

  async #pickParent(): Promise<void> {
    if (!this.#modalManager) return;
    const modal = this.#modalManager.open(this, UMB_DOCUMENT_PICKER_MODAL, {
      data: { multiple: false } as any,
    });
    const value = await modal.onSubmit().catch(() => null);
    if (!value) return;
    const selection = (value as { selection: string[] }).selection;
    if (!selection || selection.length === 0) return;
    this._selectedParentKey = selection[0]!;
    this._selectedParentLabel = this.#localize.term('uap_docTypePermissions_pickedNode') + ' ' + this._selectedParentKey;
    void this.#runAudit();
  }

  #pickRoot(): void {
    this._selectedParentKey = VIRTUAL_ROOT_NODE_KEY;
    this._selectedParentLabel = this.#localize.term('uap_docTypePermissions_rootLevel');
    void this.#runAudit();
  }

  async #runAudit(): Promise<void> {
    if (!this._selectedUser || !this._selectedParentKey) return;
    this.#abort?.abort();
    this.#abort = new AbortController();
    this._loading = true;
    this._error = null;
    try {
      this._rows = await getDocTypeCreateAudit(
        this._selectedUser.unique,
        this._selectedParentKey,
        this.#abort.signal,
      );
    } catch (err) {
      if (err instanceof Error && err.name === 'AbortError') return;
      this._error = err instanceof Error ? err.message : String(err);
    } finally {
      this._loading = false;
    }
  }

  override render(): TemplateResult {
    return html`
      <umb-body-layout headline=${this.#localize.term('uap_docTypePermissions_auditTitle')}>
        <div class="content">
          ${this.#renderPickers()}
          ${this._error ? html`<div class="error">${this._error}</div>` : nothing}
          ${this._loading
            ? html`<uui-loader></uui-loader>`
            : this._rows.length > 0
              ? this.#renderRows()
              : this._selectedUser && this._selectedParentKey
                ? html`<div class="hint">${this.#localize.term('uap_docTypePermissions_noResults')}</div>`
                : html`<div class="hint">${this.#localize.term('uap_docTypePermissions_pickToStart')}</div>`}
        </div>
      </umb-body-layout>
    `;
  }

  #renderPickers(): TemplateResult {
    return html`
      <div class="pickers">
        <div class="picker">
          <strong>${this.#localize.term('uap_user')}:</strong>
          <uap-picker-button
            .label=${this._selectedUser?.name ?? this.#localize.term('uap_pickUser')}
            @click=${() => this.#pickUser()}>
          </uap-picker-button>
        </div>
        <div class="picker">
          <strong>${this.#localize.term('uap_node')}:</strong>
          <uap-picker-button
            .label=${this._selectedParentLabel ?? this.#localize.term('uap_pickNode')}
            @click=${() => this.#pickParent()}>
          </uap-picker-button>
          <uui-button look="secondary" @click=${() => this.#pickRoot()}>
            ${this.#localize.term('uap_docTypePermissions_useRoot')}
          </uui-button>
        </div>
      </div>
    `;
  }

  #renderRows(): TemplateResult {
    return html`
      <table class="rows">
        <thead>
          <tr>
            <th>${this.#localize.term('uap_docTypePermissions_documentType')}</th>
            <th>${this.#localize.term('uap_state')}</th>
            <th>${this.#localize.term('uap_docTypePermissions_reasoning')}</th>
          </tr>
        </thead>
        <tbody>
          ${this._rows.map((r) => this.#renderRow(r))}
        </tbody>
      </table>
    `;
  }

  #renderRow(row: DocTypeCreateAuditItem): TemplateResult {
    return html`
      <tr>
        <td>${row.contentTypeName}</td>
        <td class=${row.isAllowed ? 'allowed' : 'denied'}>
          ${row.isAllowed
            ? this.#localize.term('uap_allow')
            : this.#localize.term('uap_deny')}
        </td>
        <td>
          ${row.reasoning.length === 0
            ? html`<em>${this.#localize.term('uap_docTypePermissions_defaultAllow')}</em>`
            : html`<ul class="reasoning">
                ${row.reasoning.map(
                  (r) => html`<li>
                    <strong>${r.contributingRole}</strong>: ${r.state}
                    ${r.isFromGroupDefault
                      ? html` <em>(${this.#localize.term('uap_docTypePermissions_viaDefault')})</em>`
                      : nothing}
                  </li>`,
                )}
              </ul>`}
        </td>
      </tr>
    `;
  }

  static override styles = css`
    :host { display: block; }
    .content { padding: var(--uui-size-layout-1); }
    .pickers { display: flex; gap: var(--uui-size-layout-2); margin-bottom: var(--uui-size-layout-1); flex-wrap: wrap; }
    .picker { display: flex; align-items: center; gap: var(--uui-size-2); }
    .error { color: var(--uui-color-danger); }
    .hint { color: var(--uui-color-text-alt); padding: var(--uui-size-layout-1) 0; }
    table.rows { width: 100%; border-collapse: collapse; }
    table.rows th, table.rows td { padding: var(--uui-size-1); border-bottom: 1px solid var(--uui-color-divider); text-align: left; vertical-align: top; }
    .allowed { color: var(--uui-color-positive); font-weight: 600; }
    .denied { color: var(--uui-color-danger); font-weight: 600; }
    .reasoning { margin: 0; padding-left: var(--uui-size-3); }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'uap-doc-type-create-audit-root': UapDocTypeCreateAuditRootElement;
  }
}

export default UapDocTypeCreateAuditRootElement;
