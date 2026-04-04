import type { UmbEntryPointOnInit, UmbEntryPointOnUnload } from '@umbraco-cms/backoffice/extension-api';
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';
import { setAuthContext } from './api/advanced-security.api.js';

const DOCUMENT_PERMISSION_CONDITION_ALIAS = 'Umb.Condition.UserPermission.Document';

export const onInit: UmbEntryPointOnInit = (_host, _extensionRegistry) => {
  _host.consumeContext(UMB_AUTH_CONTEXT, (authContext) => {
    setAuthContext(authContext ?? undefined);
  });

  // Replace the native document permission condition with the Advanced Security version.
  // The native condition reads from cached group permissions and doesn't support our
  // scope-based Allow/Deny model. The replacement calls the /effective API per document.
  _extensionRegistry.unregister(DOCUMENT_PERMISSION_CONDITION_ALIAS);
  _extensionRegistry.register({
    type: 'condition',
    name: 'Advanced Security Document User Permission Condition',
    alias: DOCUMENT_PERMISSION_CONDITION_ALIAS,
    api: () => import('./conditions/document-user-permission.condition.js'),
  } as UmbExtensionManifest);
};

export const onUnload: UmbEntryPointOnUnload = (_host, _extensionRegistry) => {
  setAuthContext(undefined);
};
