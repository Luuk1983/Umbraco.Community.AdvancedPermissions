/**
 * Extension manifests for the Umbraco Advanced Permissions package.
 * Registers menu items within the Users section, localization, and workspaces.
 * UmbExtensionManifest is a global ambient type - no import needed.
 */
const manifests: Array<UmbExtensionManifest> = [
  // ─── Entry Point ──────────────────────────────────────────────────────────
  {
    type: 'backofficeEntryPoint',
    alias: 'UAP.EntryPoint',
    name: 'Umbraco Advanced Permissions Entry Point',
    js: () => import('./entrypoint.js'),
  },

  // ─── Section Sidebar App (inside Users section) ───────────────────────────
  {
    type: 'sectionSidebarApp',
    kind: 'menu',
    alias: 'UAP.SidebarApp.AdvancedPermissions',
    name: 'Advanced Permissions Sidebar',
    weight: 50,
    conditions: [
      {
        alias: 'Umb.Condition.SectionAlias',
        match: 'Umb.Section.Users',
      },
    ],
    meta: {
      label: '#uap_sectionLabel',
      menu: 'UAP.Menu.AdvancedPermissions',
    },
  },

  // ─── Menu ─────────────────────────────────────────────────────────────────
  {
    type: 'menu',
    alias: 'UAP.Menu.AdvancedPermissions',
    name: 'Advanced Permissions Menu',
  },

  // ─── Menu Items ───────────────────────────────────────────────────────────
  {
    type: 'menuItem',
    alias: 'UAP.MenuItem.PermissionsEditor',
    name: 'Permissions Editor Menu Item',
    weight: 100,
    meta: {
      label: '#uap_permissionsEditor',
      icon: 'icon-lock',
      entityType: 'uap-permissions-editor',
      menus: ['UAP.Menu.AdvancedPermissions'],
    },
  },
  {
    type: 'menuItem',
    alias: 'UAP.MenuItem.AccessViewer',
    name: 'Access Viewer Menu Item',
    weight: 50,
    meta: {
      label: '#uap_accessViewer',
      icon: 'icon-eye',
      entityType: 'uap-access-viewer',
      menus: ['UAP.Menu.AdvancedPermissions'],
    },
  },

  // ─── Workspace (Permissions Editor) ──────────────────────────────────────
  {
    type: 'workspace',
    alias: 'UAP.Workspace.PermissionsEditor',
    name: 'Permissions Editor Workspace',
    meta: {
      entityType: 'uap-permissions-editor',
    },
    element: () => import('./permissions-editor/uap-permissions-editor-root.element.js'),
  },

  // ─── Granular Permission Redirect (ungrouped) ────────────────────────────
  // Appears in the "General rights" box in the user group editor (rendered
  // by umb-user-group-entity-type-permission-groups when any userGranularPermission
  // has no forEntityTypes). Shows a message directing users to the Permissions Editor.
  // The native document entityUserPermission manifests are excluded in the entry
  // point, so the "Documents" box disappears entirely — this ungrouped entry is
  // the sole visible hint about document permission management.
  {
    type: 'userGranularPermission',
    alias: 'UAP.UserGranularPermission.Document.Redirect',
    name: 'Advanced Permissions Document Permission Redirect',
    weight: 2000,
    element: () => import('./components/uap-granular-permission-redirect.element.js'),
    meta: {
      schemaType: 'DocumentPermissionPresentationModel',
      // labelKey resolves via localize.term() — 'user_permissionsEntityGroup_document'
      // gives "Documents" in Umbraco's built-in localization files.
      labelKey: 'user_permissionsEntityGroup_document',
    },
  } as UmbExtensionManifest,

  // ─── Workspace (Access Viewer) ────────────────────────────────────────────
  {
    type: 'workspace',
    alias: 'UAP.Workspace.AccessViewer',
    name: 'Access Viewer Workspace',
    meta: {
      entityType: 'uap-access-viewer',
    },
    element: () => import('./access-viewer/uap-access-viewer-root.element.js'),
  },

  // ─── Localization ─────────────────────────────────────────────────────────
  {
    type: 'localization',
    alias: 'UAP.Localization.En',
    name: 'Advanced Permissions English Localization',
    meta: {
      culture: 'en',
    },
    js: () => import('./localization/en.js'),
  } as UmbExtensionManifest,
  {
    type: 'localization',
    alias: 'UAP.Localization.Nl',
    name: 'Advanced Permissions Dutch Localization',
    meta: {
      culture: 'nl',
    },
    js: () => import('./localization/nl.js'),
  } as UmbExtensionManifest,
  {
    type: 'localization',
    alias: 'UAP.Localization.De',
    name: 'Advanced Permissions German Localization',
    meta: {
      culture: 'de',
    },
    js: () => import('./localization/de.js'),
  } as UmbExtensionManifest,
];

export { manifests };
