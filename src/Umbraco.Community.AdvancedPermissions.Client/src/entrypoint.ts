import type { UmbEntryPointOnInit, UmbEntryPointOnUnload } from '@umbraco-cms/backoffice/extension-api';
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';
import { client } from './api/generated/client.gen.js';

const DOCUMENT_PERMISSION_CONDITION_ALIAS = 'Umb.Condition.UserPermission.Document';
const ELEMENT_PERMISSION_CONDITION_ALIAS = 'Umb.Condition.UserPermission.Element';
const ELEMENT_FOLDER_PERMISSION_CONDITION_ALIAS = 'Umb.Condition.UserPermission.ElementFolder';

// Native Document entityUserPermission manifests drive the "Default permissions" checkboxes
// in the user group editor. We remove them so the section disappears entirely — document
// permissions are managed exclusively via our Permissions Editor.
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

// Native Element + Element Folder entityUserPermission manifests (the "Element permissions" /
// "Element Folder permissions" toggles) and the native element granular permission picker. Excluding
// them removes the native library permission UI from the user group editor so library permissions are
// managed exclusively via our Library permissions editor.
const ELEMENT_ENTITY_PERMISSION_ALIASES = [
  'Umb.EntityUserPermission.Element.Read',
  'Umb.EntityUserPermission.Element.Create',
  'Umb.EntityUserPermission.Element.Update',
  'Umb.EntityUserPermission.Element.Delete',
  'Umb.EntityUserPermission.Element.Publish',
  'Umb.EntityUserPermission.Element.Unpublish',
  'Umb.EntityUserPermission.Element.Duplicate',
  'Umb.EntityUserPermission.Element.Move',
  'Umb.EntityUserPermission.Element.Rollback',
  'Umb.EntityUserPermission.ElementFolder.Read',
  'Umb.EntityUserPermission.ElementFolder.Create',
  'Umb.EntityUserPermission.ElementFolder.Update',
  'Umb.EntityUserPermission.ElementFolder.Delete',
  'Umb.EntityUserPermission.ElementFolder.Move',
  // Native per-element granular permission picker — replaced by our redirect (manifests.ts).
  'Umb.UserGranularPermission.Element',
];

export const onInit: UmbEntryPointOnInit = (_host, _extensionRegistry) => {
  // Wire our hey-api generated client through Umbraco's auth pipeline (token refresh, 401 retry).
  _host.consumeContext(UMB_AUTH_CONTEXT, (authContext) => {
    if (!authContext) return;
    authContext.configureClient(client);
  });

  // Replace the native document permission condition with the Advanced Permissions version.
  // The native condition reads from cached group permissions and doesn't support our
  // scope-based Allow/Deny model. The replacement calls the /effective API per document.
  _extensionRegistry.unregister(DOCUMENT_PERMISSION_CONDITION_ALIAS);
  _extensionRegistry.register({
    type: 'condition',
    name: 'Advanced Permissions Document User Permission Condition',
    alias: DOCUMENT_PERMISSION_CONDITION_ALIAS,
    api: () => import('./conditions/document-user-permission.condition.js'),
  } as UmbExtensionManifest);

  // Replace the native element + element-folder permission conditions. Unlike documents, Umbraco 18 does
  // not route the element current-user permission path through IElementPermissionService, so these
  // replacements resolve UI action visibility via the package's own element effective endpoint.
  _extensionRegistry.unregister(ELEMENT_PERMISSION_CONDITION_ALIAS);
  _extensionRegistry.register({
    type: 'condition',
    name: 'Advanced Permissions Element User Permission Condition',
    alias: ELEMENT_PERMISSION_CONDITION_ALIAS,
    api: () => import('./conditions/element-user-permission.condition.js'),
  } as UmbExtensionManifest);

  _extensionRegistry.unregister(ELEMENT_FOLDER_PERMISSION_CONDITION_ALIAS);
  _extensionRegistry.register({
    type: 'condition',
    name: 'Advanced Permissions Element Folder User Permission Condition',
    alias: ELEMENT_FOLDER_PERMISSION_CONDITION_ALIAS,
    api: () => import('./conditions/element-folder-user-permission.condition.js'),
  } as UmbExtensionManifest);

  // Exclude native Document, Element and Element Folder entityUserPermission manifests (and the native
  // element granular picker). exclude() removes any already-registered extension AND permanently blocks
  // future registration, so it works regardless of load order.
  for (const alias of [...DOCUMENT_ENTITY_PERMISSION_ALIASES, ...ELEMENT_ENTITY_PERMISSION_ALIASES]) {
    _extensionRegistry.exclude(alias);
  }
};

export const onUnload: UmbEntryPointOnUnload = () => {
  // configureClient() is idempotent (guarded by #configuredClients); no teardown needed.
};
