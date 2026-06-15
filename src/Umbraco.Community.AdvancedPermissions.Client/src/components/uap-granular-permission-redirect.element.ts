import { html, css, customElement, property } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';

/**
 * Replaces a built-in granular permissions UI with a message directing users to the relevant editor
 * in the Users section. The localization key is overridable so each permission target (documents,
 * library elements) can show a target-specific message.
 */
@customElement('uap-granular-permission-redirect')
export class UapGranularPermissionRedirectElement extends UmbLitElement {
  #localize = new UmbLocalizationController(this);

  /** Set by the parent granular-permissions component — accepted but unused. */
  @property({ attribute: false })
  permissions: Array<unknown> = [];

  /** Set by the parent granular-permissions component — accepted but unused. */
  @property({ attribute: false })
  fallbackPermissions: Array<string> = [];

  /**
   * The localization key for the redirect message. Defaults to the document message; subclasses
   * override it to show a target-specific message (e.g. library elements).
   */
  protected messageKey = 'uap_redirectMessage';

  override render() {
    return html`
      <div id="message">
        <uui-icon name="icon-lock"></uui-icon>
        <p>${this.#localize.term(this.messageKey)}</p>
      </div>
    `;
  }

  static override styles = css`
    #message {
      display: flex;
      align-items: flex-start;
      gap: var(--uui-size-space-3);
      padding: var(--uui-size-space-4);
      border: 1px solid var(--uui-color-border);
      border-radius: var(--uui-border-radius);
      background: var(--uui-color-surface-alt);
    }

    uui-icon {
      flex-shrink: 0;
      font-size: 1.2em;
      margin-top: 2px;
      color: var(--uui-color-interactive);
    }

    p {
      margin: 0;
      line-height: 1.4;
    }
  `;
}

export default UapGranularPermissionRedirectElement;

declare global {
  interface HTMLElementTagNameMap {
    'uap-granular-permission-redirect': UapGranularPermissionRedirectElement;
  }
}
