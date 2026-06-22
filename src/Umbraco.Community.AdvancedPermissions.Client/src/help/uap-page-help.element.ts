import {
  html,
  css,
  nothing,
  customElement,
  property,
} from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import { UMB_MODAL_MANAGER_CONTEXT } from '@umbraco-cms/backoffice/modal';
import { SURFACE_HELP } from './help-content.js';
import { UAP_HELP_MODAL } from './uap-help-modal.token.js';

/**
 * Always-visible help bar mounted at the top of each editor/viewer. Shows a one-line
 * description and a "Learn more" button that opens the help modal for the surface.
 */
@customElement('uap-page-help')
export class UapPageHelpElement extends UmbLitElement {
  #localize = new UmbLocalizationController(this);

  /** The workspace entityType identifying which surface this bar belongs to. */
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

  /** Opens the help modal for this surface (About + Concepts). */
  #openModal(): void {
    const cfg = SURFACE_HELP[this.surface];
    if (!cfg || !this.#modalManager) return;
    this.#modalManager.open(this, UAP_HELP_MODAL, {
      data: { headline: this.headline, howToDoc: cfg.howToDoc },
    });
  }

  override render() {
    const cfg = SURFACE_HELP[this.surface];
    if (!cfg) return nothing;
    return html`
      <div class="bar">
        <umb-icon name="icon-info" class="lead-icon"></umb-icon>
        <span class="desc">${this.#localize.term(cfg.descriptionKey)}</span>
        <uui-button
          compact
          look="secondary"
          label=${this.#localize.term('uap_help_learnMore')}
          @click=${this.#openModal}>
          ${this.#localize.term('uap_help_learnMore')}
        </uui-button>
      </div>
    `;
  }

  static override styles = css`
    :host { display: block; }
    .bar {
      display: flex;
      align-items: center;
      gap: var(--uui-size-3, 9px);
      padding: var(--uui-size-2, 6px) var(--uui-size-6, 18px);
      background: var(--uui-color-surface-alt, #f7f7f9);
      border-bottom: 1px solid var(--uui-color-border, #e0e0e0);
      font-size: 13px;
      color: var(--uui-color-text-alt, #555);
    }
    .lead-icon { flex-shrink: 0; }
    .desc { flex: 1; }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'uap-page-help': UapPageHelpElement;
  }
}

export default UapPageHelpElement;
