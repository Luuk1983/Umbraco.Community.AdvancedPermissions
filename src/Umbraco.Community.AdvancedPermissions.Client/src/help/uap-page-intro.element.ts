import { html, css, nothing, customElement, property } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import { UMB_MODAL_MANAGER_CONTEXT } from '@umbraco-cms/backoffice/modal';
import { SURFACE_HELP } from './help-content.js';
import { UAP_HELP_MODAL } from './uap-help-modal.token.js';

/**
 * One-sentence page description rendered as native dashboard-style intro copy at the top of
 * an editor/viewer. When the surface has a how-to doc (pilot surfaces), a right-aligned
 * "Learn more" button opens the help modal.
 */
@customElement('uap-page-intro')
export class UapPageIntroElement extends UmbLitElement {
  #localize = new UmbLocalizationController(this);

  /** Workspace entityType identifying the surface (keys into SURFACE_HELP). */
  @property() surface = '';

  /** Headline passed to the help modal (the surface name). */
  @property() headline = '';

  #modalManager: typeof UMB_MODAL_MANAGER_CONTEXT.TYPE | undefined = undefined;

  /** Wires the modal manager so "Learn more" can open the help modal. */
  constructor() {
    super();
    this.consumeContext(UMB_MODAL_MANAGER_CONTEXT, (ctx) => {
      this.#modalManager = ctx ?? undefined;
    });
  }

  /** Opens the help modal for this surface. No-op when the surface has no how-to doc. */
  #openModal(): void {
    const cfg = SURFACE_HELP[this.surface];
    if (!cfg?.howToDoc || !this.#modalManager) return;
    this.#modalManager.open(this, UAP_HELP_MODAL, {
      data: { headline: this.headline, howToDoc: cfg.howToDoc },
    });
  }

  override render() {
    const cfg = SURFACE_HELP[this.surface];
    if (!cfg) return nothing;
    return html`
      <div class="intro">
        <span class="desc">${this.#localize.term(cfg.descriptionKey)}</span>
        ${cfg.howToDoc
          ? html`<uui-button
              class="learn"
              look="secondary"
              compact
              label=${this.#localize.term('uap_help_learnMore')}
              @click=${this.#openModal}>
              <umb-icon name="icon-help"></umb-icon>
              ${this.#localize.term('uap_help_learnMore')}
            </uui-button>`
          : nothing}
      </div>
    `;
  }

  static override styles = css`
    :host { display: block; }
    .intro {
      display: flex;
      align-items: center;
      gap: var(--uui-size-space-4, 12px);
      margin: 0 0 var(--uui-size-space-4, 12px);
    }
    .desc {
      flex: 1;
      color: var(--uui-color-text-alt);
      line-height: 1.5;
    }
    .learn { flex-shrink: 0; }
    .learn umb-icon { margin-right: var(--uui-size-space-1, 3px); }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'uap-page-intro': UapPageIntroElement;
  }
}

export default UapPageIntroElement;
