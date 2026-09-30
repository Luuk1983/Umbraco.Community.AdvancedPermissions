import { html, css, customElement, property } from '@umbraco-cms/backoffice/external/lit';
import type { TemplateResult } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import { buttonStateFor } from './refresh-phase.js';
import type { UapRefreshPhase } from './refresh-phase.js';

/**
 * The single control every surface shows for "is this data current, and can I make it so".
 *
 * One `uui-button` whose label never changes. The phase is shown through the button's own `state`
 * (see {@link buttonStateFor}): `uui-button` draws a spinner, check or cross in an absolutely
 * positioned layer and hides the label with opacity rather than removing it, so the button keeps
 * exactly the width it has when idle and the neighbouring Save and Discard buttons never move.
 *
 * Four phases, driven entirely by the host:
 * - `idle` — the plain button. Clicking raises `uap-live-refresh`, which the host forwards to the
 *   same coalesced refresh a live event uses.
 * - `refreshing` — the spinner, and disabled so a second click cannot be made. The host holds this
 *   for a minimum time so a fast request does not flicker.
 * - `updated` — a check, confirming fresh data has arrived. `uui-button` clears it by itself, so the
 *   host does not time it. Still clickable: having just refreshed does not make another refresh wrong.
 * - `failed` — a cross, so a refresh that did not land is not silent. Cleared by `uui-button` too.
 *
 * Because the visible label is constant, a state change would be announced as nothing at all. A
 * visually hidden live region beside the button says what happened instead.
 *
 * Deliberately stateless. The phase comes from the controller that sees both live events and
 * clicks, so the spinner is honest about either.
 * @fires uap-live-refresh When the person asks for a refresh by hand.
 */
@customElement('uap-live-refresh')
export class UapLiveRefreshElement extends UmbLitElement {
  /** The current phase of the control; the host owns it, the element only displays it. */
  @property({ type: String }) phase: UapRefreshPhase = 'idle';

  /** The localization controller for this element. */
  #localize = new UmbLocalizationController(this);

  /** Raises the refresh request, unless a refresh is already showing. */
  #onClick = (): void => {
    if (this.phase === 'refreshing') return;
    this.dispatchEvent(new CustomEvent('uap-live-refresh', { bubbles: true, composed: true }));
  };

  /**
   * The words a screen reader hears for the current phase. Empty while idle, so the live region
   * has nothing to announce until something happens.
   * @returns The localized announcement, or an empty string.
   */
  #announcement(): string {
    switch (this.phase) {
      case 'refreshing':
        return this.#localize.term('uap_liveRefreshing');
      case 'updated':
        return this.#localize.term('uap_liveUpdated');
      case 'failed':
        return this.#localize.term('uap_liveRefreshFailed');
      default:
        return '';
    }
  }

  override render(): TemplateResult {
    const label = this.#localize.term('uap_liveRefresh');

    // `.state` is a property binding, so `undefined` for idle is passed straight through to
    // uui-button (whose type permits it) and is never assigned to one of our own optional fields.
    return html`
      <uui-button
        look="secondary"
        compact
        label=${label}
        .state=${buttonStateFor(this.phase)}
        ?disabled=${this.phase === 'refreshing'}
        @click=${this.#onClick}>
        <uui-icon name="icon-sync"></uui-icon>
        ${label}
      </uui-button>
      <span class="visually-hidden" role="status">${this.#announcement()}</span>
    `;
  }

  static override styles = css`
    :host {
      display: inline-flex;
    }

    uui-button {
      --uui-button-font-size: var(--uui-type-small-size);
    }

    uui-icon {
      margin-right: var(--uui-size-space-2);
    }

    /* Off screen but still in the accessibility tree, so the status is read out and never seen. */
    .visually-hidden {
      position: absolute;
      width: 1px;
      height: 1px;
      margin: -1px;
      padding: 0;
      overflow: hidden;
      clip: rect(0, 0, 0, 0);
      white-space: nowrap;
      border: 0;
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    /** The manual refresh control. */
    'uap-live-refresh': UapLiveRefreshElement;
  }
}

export default UapLiveRefreshElement;
