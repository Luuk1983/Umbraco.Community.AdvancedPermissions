import { customElement } from '@umbraco-cms/backoffice/external/lit';
import { UapGranularPermissionRedirectElement } from './uap-granular-permission-redirect.element.js';

/**
 * Library element variant of the granular-permission redirect. Shows an element-specific message
 * directing users to the Library permissions editor, rather than the document message.
 */
@customElement('uap-element-granular-permission-redirect')
export class UapElementGranularPermissionRedirectElement extends UapGranularPermissionRedirectElement {
  protected override messageKey = 'uap_redirectMessageElement';
}

export default UapElementGranularPermissionRedirectElement;

declare global {
  interface HTMLElementTagNameMap {
    'uap-element-granular-permission-redirect': UapElementGranularPermissionRedirectElement;
  }
}
