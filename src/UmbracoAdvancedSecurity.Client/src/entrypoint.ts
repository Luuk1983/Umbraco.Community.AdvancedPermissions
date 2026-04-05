import type { UmbEntryPointOnInit, UmbEntryPointOnUnload } from '@umbraco-cms/backoffice/extension-api';
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';
import { setAuthContext } from './api/advanced-security.api.js';

const DOCUMENT_PERMISSION_CONDITION_ALIAS = 'Umb.Condition.UserPermission.Document';

// Native Document entityUserPermission manifests drive the "Default permissions" checkboxes
// in the user group editor. We remove them so the section disappears entirely — document
// permissions are managed exclusively via our Security Editor.
const DOCUMENT_ENTITY_PERMISSION_ALIASES = [
  'Umb.Document.Read',
  'Umb.Document.Create',
  'Umb.Document.Update',
  'Umb.Document.Delete',
  'Umb.Document.Publish',
  'Umb.Document.Unpublish',
  'Umb.Document.Duplicate',
  'Umb.Document.Move',
  'Umb.Document.Sort',
  'Umb.Document.CreateBlueprint',
  'Umb.Document.Notifications',
  'Umb.Document.CultureAndHostnames',
  'Umb.Document.PublicAccess',
  'Umb.Document.Rollback',
  'Umb.Document.Permissions',
];

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

  // Exclude native Document entityUserPermission manifests.
  // The user group editor's "Default permissions" section reads forEntityTypes metadata
  // directly via byType() — overwrites only affect element rendering, not raw metadata reads.
  // exclude() is the correct API: it removes any already-registered extension AND permanently
  // blocks future registration, so it works regardless of whether the native manifests
  // have already loaded or not when onInit runs.
  for (const alias of DOCUMENT_ENTITY_PERMISSION_ALIASES) {
    _extensionRegistry.exclude(alias);
  }
};

export const onUnload: UmbEntryPointOnUnload = (_host, _extensionRegistry) => {
  setAuthContext(undefined);
};
