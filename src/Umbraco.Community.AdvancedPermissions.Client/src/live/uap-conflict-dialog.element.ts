import { html, css, customElement, property, query } from '@umbraco-cms/backoffice/external/lit';
import type { TemplateResult } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';

/** One line of the dialog's "stored versus yours" list. */
export interface UapConflictLine {
  /** The node's display name. */
  nodeName: string;
  /** The verb's display name. */
  verbName: string;
  /** What is stored now, already localized. */
  stored: string;
  /** What this editor holds, already localized. */
  mine: string;
}

/**
 * The confirmation shown when a save is refused because the stored values moved.
 *
 * It exists because the banner can be missed — a tab left in the background, a hub connection that
 * dropped, a save that beat its own event — and this is the last point at which somebody else's
 * work can still be kept. So it is reached from the server's refusal rather than from the client's
 * own knowledge, and it names what would be lost rather than warning in the abstract.
 *
 * Opens and closes the same way as `uap-permission-scope-dialog`: a native `<dialog>` driven via
 * `showModal()` / `close()`, rather than a second modal mechanism.
 */
@customElement('uap-conflict-dialog')
export class UapConflictDialogElement extends UmbLitElement {
  /** The conflicting cells, as the server reported them. */
  @property({ attribute: false }) lines: UapConflictLine[] = [];

  /** The localization controller for this element. */
  #localize = new UmbLocalizationController(this);

  @query('dialog') private _dialog!: HTMLDialogElement;

  /** Shows the dialog. */
  open(): void {
    void this.updateComplete.then(() => this._dialog.showModal());
  }

  /** Hides the dialog. */
  close(): void {
    this._dialog.close();
  }

  override render(): TemplateResult {
    const body = this.lines.length === 1
      ? this.#localize.term('uap_conflictDialogBodyOne')
      : this.#localize.term('uap_conflictDialogBody', this.lines.length);

    return html`
      <dialog class="conflict-dialog">
        <uui-dialog-layout headline=${this.#localize.term('uap_conflictDialogTitle')}>
          <p>${body}</p>
          <ul class="lines">
            ${this.lines.map((l) => html`
              <li>
                <strong>${l.nodeName} · ${l.verbName}</strong>
                ${this.#localize.term('uap_conflictStoredLabel')}: ${l.stored} ·
                ${this.#localize.term('uap_conflictYoursLabel')}: ${l.mine}
              </li>
            `)}
          </ul>
          <div slot="actions">
            <uui-button
              label=${this.#localize.term('uap_conflictCancel')}
              @click=${() => this.dispatchEvent(new CustomEvent('uap-conflict-cancel'))}>
              ${this.#localize.term('uap_conflictCancel')}
            </uui-button>
            <uui-button
              look="outline"
              label=${this.#localize.term('uap_liveLoadStored')}
              @click=${() => this.dispatchEvent(new CustomEvent('uap-conflict-load-stored'))}>
              ${this.#localize.term('uap_liveLoadStored')}
            </uui-button>
            <uui-button
              look="primary"
              color="danger"
              label=${this.#localize.term('uap_conflictOverwrite')}
              @click=${() => this.dispatchEvent(new CustomEvent('uap-conflict-overwrite'))}>
              ${this.#localize.term('uap_conflictOverwrite')}
            </uui-button>
          </div>
        </uui-dialog-layout>
      </dialog>
    `;
  }

  static override styles = css`
    :host { display: contents; }

    .conflict-dialog {
      border: none;
      border-radius: 8px;
      box-shadow: 0 8px 32px rgba(0, 0, 0, 0.25);
      padding: 0;
      min-width: 420px;
      max-width: 540px;
    }
    .conflict-dialog::backdrop {
      background: rgba(0, 0, 0, 0.4);
    }

    .lines {
      list-style: none;
      margin: 12px 0 0;
      padding: 9px 11px;
      background: var(--uui-color-surface-alt);
      border-radius: 4px;
      font-size: 13px;
    }

    .lines li { padding: 3px 0; }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    /** The save-time conflict confirmation. */
    'uap-conflict-dialog': UapConflictDialogElement;
  }
}

export default UapConflictDialogElement;
