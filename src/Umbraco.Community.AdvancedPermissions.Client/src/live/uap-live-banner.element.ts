import { html, css, nothing, customElement, property } from '@umbraco-cms/backoffice/external/lit';
import type { TemplateResult } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';

/**
 * The notice an editor shows when somebody else changed the permissions it is holding unsaved work
 * against.
 *
 * Dismissible and non-blocking on purpose. A modal here would interrupt whatever the person was in
 * the middle of typing to tell them about a collision they may not care about yet, and the
 * save-time dialog already stops anything irreversible. This only has to be noticeable.
 */
@customElement('uap-live-banner')
export class UapLiveBannerElement extends UmbLitElement {
  /** How many cells collide with the stored values. */
  @property({ type: Number }) conflictCount = 0;

  /** The localization controller for this element. */
  #localize = new UmbLocalizationController(this);

  override render(): TemplateResult | typeof nothing {
    if (this.conflictCount < 1) return nothing;

    const body = this.conflictCount === 1
      ? this.#localize.term('uap_liveConflictBodyOne')
      : this.#localize.term('uap_liveConflictBody', this.conflictCount);

    return html`
      <div class="banner" role="status">
        <uui-icon name="icon-alert"></uui-icon>
        <div class="text">
          <strong>${this.#localize.term('uap_liveConflictTitle')}</strong>
          <p>${body}</p>
          <div class="actions">
            <uui-button
              look="outline"
              label=${this.#localize.term('uap_liveLoadStored')}
              @click=${() => this.dispatchEvent(new CustomEvent('uap-live-load-stored'))}>
              ${this.#localize.term('uap_liveLoadStored')}
            </uui-button>
            <uui-button
              look="outline"
              label=${this.#localize.term('uap_liveKeepMine')}
              @click=${() => this.dispatchEvent(new CustomEvent('uap-live-keep-mine'))}>
              ${this.#localize.term('uap_liveKeepMine')}
            </uui-button>
          </div>
        </div>
      </div>
    `;
  }

  static override styles = css`
    .banner {
      display: flex;
      gap: 10px;
      padding: 11px 13px;
      background: var(--uui-color-warning);
      color: var(--uui-color-warning-contrast);
      border-bottom: 1px solid var(--uui-color-warning-standalone);
    }

    .text p {
      margin: 2px 0 0;
      font-size: 13px;
    }

    .actions {
      display: flex;
      gap: 8px;
      margin-top: 9px;
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    /** The live-change banner. */
    'uap-live-banner': UapLiveBannerElement;
  }
}

export default UapLiveBannerElement;
