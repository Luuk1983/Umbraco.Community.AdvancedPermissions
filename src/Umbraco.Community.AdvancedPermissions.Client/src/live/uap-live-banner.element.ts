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

    // The icon is decoration: the title already says everything the icon would, and announcing
    // "alert" on top of a polite status update would make a non-blocking notice sound urgent.
    return html`
      <div class="banner" role="status">
        <uui-icon class="icon" name="icon-alert" aria-hidden="true"></uui-icon>
        <div class="content">
          <p class="title">${this.#localize.term('uap_liveConflictTitle')}</p>
          <p class="body">${body}</p>
          <div class="actions">
            <uui-button
              look="primary"
              compact
              label=${this.#localize.term('uap_liveLoadStored')}
              @click=${() => this.dispatchEvent(new CustomEvent('uap-live-load-stored'))}>
              ${this.#localize.term('uap_liveLoadStored')}
            </uui-button>
            <uui-button
              look="outline"
              compact
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
    :host {
      display: block;
      /* Separates the notice from the grid below it; the grid has no top margin of its own. */
      margin-bottom: var(--uui-size-layout-1);
    }

    /*
     * A tint of the warning colour over the page surface rather than the solid warning fill: this
     * is a dismissible heads-up, not an alarm. Mixing against the surface token, not white, keeps
     * it a tint in dark mode too. The plain surface below is the fallback where color-mix is not
     * supported, so the text always sits on a token-defined background.
     */
    .banner {
      display: flex;
      align-items: flex-start;
      gap: var(--uui-size-space-4);
      padding: var(--uui-size-space-4) var(--uui-size-space-5);
      background: var(--uui-color-surface-alt);
      background: color-mix(in srgb, var(--uui-color-warning) 16%, var(--uui-color-surface));
      color: var(--uui-color-text);
      border: 1px solid var(--uui-color-border);
      border-left: 4px solid var(--uui-color-warning-standalone);
      border-radius: var(--uui-border-radius);
    }

    .icon {
      flex-shrink: 0;
      /* Nudged to sit on the title's first line rather than the middle of the block. */
      margin-top: 1px;
      font-size: var(--uui-type-h5-size);
      color: var(--uui-color-warning-standalone);
    }

    .content {
      min-width: 0;
    }

    .title {
      margin: 0;
      font-size: var(--uui-type-default-size);
      font-weight: 700;
      color: var(--uui-color-text);
    }

    .body {
      margin: var(--uui-size-space-1) 0 0;
      font-size: var(--uui-type-small-size);
      line-height: 1.5;
      color: var(--uui-color-text-alt);
    }

    .actions {
      display: flex;
      flex-wrap: wrap;
      gap: var(--uui-size-space-2);
      margin-top: var(--uui-size-space-4);
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
