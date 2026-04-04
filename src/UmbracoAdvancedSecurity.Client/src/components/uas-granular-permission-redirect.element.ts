import { html, css, customElement, property } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';

/**
 * Replaces the built-in document granular permissions UI with a message
 * directing users to the Advanced Security section.
 */
@customElement('uas-granular-permission-redirect')
export class UasGranularPermissionRedirectElement extends UmbLitElement {
  /** Set by the parent granular-permissions component — accepted but unused. */
  @property({ attribute: false })
  permissions: Array<unknown> = [];

  /** Set by the parent granular-permissions component — accepted but unused. */
  @property({ attribute: false })
  fallbackPermissions: Array<string> = [];

  override render() {
    return html`
      <div id="message">
        <uui-icon name="icon-lock"></uui-icon>
        <p>
          Document permissions for this user group are managed by the
          <strong>Advanced Security</strong> package. Open the
          <em>Advanced Security &gt; Security Editor</em> section to configure
          permissions.
        </p>
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

export default UasGranularPermissionRedirectElement;

declare global {
  interface HTMLElementTagNameMap {
    'uas-granular-permission-redirect': UasGranularPermissionRedirectElement;
  }
}
