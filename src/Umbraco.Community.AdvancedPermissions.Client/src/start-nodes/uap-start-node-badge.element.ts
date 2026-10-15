import { html, css, nothing, customElement, property } from '@umbraco-cms/backoffice/external/lit';
import type { TemplateResult } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import type { StartNodeBadge } from './start-node-display.js';

/**
 * The start-node marker next to a node name in the viewers: a flag on a start node, or a badge on a
 * node the subject can only browse through or cannot see at all.
 */
@customElement('uap-start-node-badge')
export class UapStartNodeBadgeElement extends UmbLitElement {
  /** Which marker to show; null shows nothing. */
  @property({ attribute: false }) badge: StartNodeBadge = null;

  /** The localization controller for this element. */
  #localize = new UmbLocalizationController(this);

  override render(): TemplateResult | typeof nothing {
    switch (this.badge) {
      case 'startNode':
        return html`<umb-icon class="flag" name="icon-flag" title=${this.#localize.term('uap_startNodes_badgeStartNode')}></umb-icon>`;
      case 'pathOnly':
        return html`<span class="badge" title=${this.#localize.term('uap_startNodes_badgePathOnlyTitle')}>
          <umb-icon name="icon-navigation"></umb-icon>${this.#localize.term('uap_startNodes_badgePathOnly')}</span>`;
      case 'notVisible':
        return html`<span class="badge" title=${this.#localize.term('uap_startNodes_badgeNotVisibleTitle')}>
          <umb-icon name="icon-eye"></umb-icon>${this.#localize.term('uap_startNodes_badgeNotVisible')}</span>`;
      default:
        return nothing;
    }
  }

  static override styles = css`
    :host { display: inline-flex; align-items: center; flex-shrink: 0; }
    .flag { color: var(--uui-color-interactive, #3544b1); }
    .badge {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      margin-left: 6px;
      padding: 0 6px;
      border-radius: 4px;
      border: 1px solid var(--uui-color-border, #ddd);
      background: var(--uui-color-surface-alt, #f3f3f5);
      color: var(--uui-color-text-alt, #666);
      font-size: 11px;
      line-height: 18px;
      white-space: nowrap;
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'uap-start-node-badge': UapStartNodeBadgeElement;
  }
}
