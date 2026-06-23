import { html, css, customElement, property } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UMB_MODAL_MANAGER_CONTEXT } from '@umbraco-cms/backoffice/modal';
import { UAP_HELP_MODAL } from './uap-help-modal.token.js';

/**
 * Small inline help affordance. Always shows `text` as a tooltip. When `concept`
 * is set, clicking opens the help modal scrolled to that concept's anchor.
 */
@customElement('uap-info-icon')
export class UapInfoIconElement extends UmbLitElement {
  /** Short tooltip text shown on hover/focus. */
  @property() text = '';

  /** Optional concept anchor id to deep-link into the concepts reference. Empty = tooltip only. */
  @property() concept = '';

  #modalManager: typeof UMB_MODAL_MANAGER_CONTEXT.TYPE | undefined = undefined;

  /** Wires the modal manager so the icon can open the help modal on click. */
  constructor() {
    super();
    this.consumeContext(UMB_MODAL_MANAGER_CONTEXT, (ctx) => {
      this.#modalManager = ctx ?? undefined;
    });
  }

  /**
   * Opens the help modal at this icon's concept. Stops propagation so the click
   * doesn't also trigger the surrounding cell/row handler.
   *
   * @param e The click event.
   */
  #onClick(e: Event): void {
    if (!this.concept || !this.#modalManager) return;
    e.stopPropagation();
    e.preventDefault();
    this.#modalManager.open(this, UAP_HELP_MODAL, {
      data: { scrollToConcept: this.concept },
    });
  }

  override render() {
    return html`
      <button
        type="button"
        class="info ${this.concept ? 'clickable' : ''}"
        title=${this.text}
        aria-label=${this.text}
        @click=${this.#onClick}>
        i
      </button>
    `;
  }

  static override styles = css`
    :host { display: inline-flex; vertical-align: middle; }
    .info {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 16px;
      height: 16px;
      border-radius: 50%;
      border: none;
      background: var(--uui-color-surface-emphasis, #f0f0f0);
      color: var(--uui-color-text-alt, #666);
      font-size: 11px;
      font-style: italic;
      font-family: Georgia, 'Times New Roman', serif;
      line-height: 1;
      padding: 0;
      cursor: help;
    }
    .info.clickable { cursor: pointer; }
    .info:hover {
      background: var(--uui-color-default, #3544b1);
      color: var(--uui-color-default-contrast, #fff);
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'uap-info-icon': UapInfoIconElement;
  }
}

export default UapInfoIconElement;
