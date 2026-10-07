import type { ManifestBase, UmbEntryPointOnInit, UmbEntryPointOnUnload } from '@umbraco-cms/backoffice/extension-api';
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';
import { UMB_CURRENT_USER_CONTEXT } from '@umbraco-cms/backoffice/current-user';
import { client } from './api/generated/client.gen.js';
import { setCurrentUserKey } from './utils/selection-store.js';
import { replaceExtensionApi } from './workspace-actions/replace-extension-api.js';

const DOCUMENT_PERMISSION_CONDITION_ALIAS = 'Umb.Condition.UserPermission.Document';
const SAVE_AND_PUBLISH_WORKSPACE_ACTION_ALIAS = 'Umb.WorkspaceAction.Document.SaveAndPublish';

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

export const onInit: UmbEntryPointOnInit = (_host, _extensionRegistry) => {
  // Wire our hey-api generated client through Umbraco's auth pipeline.
  // configureClient() installs:
  //   - an auth callback that calls #ensureTokenReady() (refreshes the session
  //     cookie if expired) before each request;
  //   - the default response interceptors (401 retry queue for GETs, 403, error
  //     → ProblemDetails, umb-notifications forwarding).
  // This is what the official Umbraco backoffice uses for its own API calls;
  // without it, expired sessions surface as intermittent 401s in the UI.
  _host.consumeContext(UMB_AUTH_CONTEXT, (authContext) => {
    if (!authContext) return;
    authContext.configureClient(client);
  });

  // Track the current backoffice user so per-user selection memory (issue #46) can key its
  // sessionStorage entries. Resolves well before any editor/viewer opens; until it does,
  // persistence is a safe no-op.
  _host.consumeContext(UMB_CURRENT_USER_CONTEXT, (currentUserContext) => {
    if (!currentUserContext) return;
    _host.observe(currentUserContext.unique, (unique) => {
      setCurrentUserKey(unique ?? undefined);
    });
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

  // Umbraco's "Save and publish" action constructs the native document permission condition
  // directly instead of resolving it by alias, so the replacement above never decides it and the
  // button follows the user group's native default permissions (hidden from the user group editor)
  // instead of the per-node Advanced Permissions verdict. Swap in a copy of the action that
  // resolves the condition by alias, keeping the native alias, placement and conditions.
  replaceExtensionApi<ManifestBase>(_extensionRegistry, SAVE_AND_PUBLISH_WORKSPACE_ACTION_ALIAS, {
    name: 'Advanced Permissions Save And Publish Document Workspace Action',
    api: () => import('./workspace-actions/save-and-publish.action.js'),
  });

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

export const onUnload: UmbEntryPointOnUnload = () => {
  // configureClient() is idempotent (guarded by #configuredClients); no teardown needed.
};
