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

  // ─── Section Sidebar Apps (inside Users section) ──────────────────────────
  // Two distinct sidebar apps so the two feature families are clearly separated in nav:
  //  - Advanced content permissions: node-level Permissions Editor + Access Viewer
  //  - Advanced document type permissions: doc-type editor + Insert Options viewer
  {
    type: 'sectionSidebarApp',
    kind: 'menu',
    alias: 'UAP.SidebarApp.ContentPermissions',
    name: 'Advanced Content Permissions Sidebar',
    weight: 60,
    conditions: [
      {
        alias: 'Umb.Condition.SectionAlias',
        match: 'Umb.Section.Users',
      },
    ],
    meta: {
      label: '#uap_contentPermissionsSectionLabel',
      menu: 'UAP.Menu.ContentPermissions',
    },
  },
  {
    type: 'sectionSidebarApp',
    kind: 'menu',
    alias: 'UAP.SidebarApp.DocTypePermissions',
    name: 'Advanced Document Type Permissions Sidebar',
    weight: 50,
    conditions: [
      {
        alias: 'Umb.Condition.SectionAlias',
        match: 'Umb.Section.Users',
      },
    ],
    meta: {
      label: '#uap_docTypePermissionsSectionLabel',
      menu: 'UAP.Menu.DocTypePermissions',
    },
  },

  // ─── Menus ────────────────────────────────────────────────────────────────
  {
    type: 'menu',
    alias: 'UAP.Menu.ContentPermissions',
    name: 'Advanced Content Permissions Menu',
  },
  {
    type: 'menu',
    alias: 'UAP.Menu.DocTypePermissions',
    name: 'Advanced Document Type Permissions Menu',
  },

  // ─── Menu Items ───────────────────────────────────────────────────────────
  // Each sidebar app gets its own pair of menu items.
  {
    type: 'menuItem',
    alias: 'UAP.MenuItem.PermissionsEditor',
    name: 'Permissions Editor Menu Item',
    weight: 100,
    meta: {
      label: '#uap_permissionsEditor',
      icon: 'icon-lock',
      entityType: 'uap-permissions-editor',
      menus: ['UAP.Menu.ContentPermissions'],
    },
  },
  {
    type: 'menuItem',
    alias: 'UAP.MenuItem.AccessViewer',
    name: 'Access Viewer Menu Item',
    weight: 90,
    meta: {
      label: '#uap_accessViewer',
      icon: 'icon-eye',
      entityType: 'uap-access-viewer',
      menus: ['UAP.Menu.ContentPermissions'],
    },
  },
  {
    type: 'menuItem',
    alias: 'UAP.MenuItem.DocTypePermissions',
    name: 'Document Type Permissions Menu Item',
    weight: 100,
    meta: {
      label: '#uap_docTypePermissions_menuLabel',
      icon: 'icon-document',
      entityType: 'uap-doc-type-permissions',
      menus: ['UAP.Menu.DocTypePermissions'],
    },
  },
  {
    type: 'menuItem',
    alias: 'UAP.MenuItem.InsertOptions',
    name: 'Insert Options Menu Item',
    weight: 90,
    meta: {
      label: '#uap_docTypePermissions_insertOptionsMenuLabel',
      icon: 'icon-eye',
      entityType: 'uap-doc-type-create-audit',
      menus: ['UAP.Menu.DocTypePermissions'],
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

  // ─── Workspace (Document Type Permissions Editor) ────────────────────────
  {
    type: 'workspace',
    alias: 'UAP.Workspace.DocTypePermissions',
    name: 'Document Type Permissions Workspace',
    meta: {
      entityType: 'uap-doc-type-permissions',
    },
    element: () =>
      import('./doc-type-permissions/uap-doc-type-permissions-editor-root.element.js'),
  },

  // ─── Workspace (Document Type Create Audit) ──────────────────────────────
  {
    type: 'workspace',
    alias: 'UAP.Workspace.DocTypeCreateAudit',
    name: 'Document Type Create Audit Workspace',
    meta: {
      entityType: 'uap-doc-type-create-audit',
    },
    element: () =>
      import('./doc-type-permissions/uap-doc-type-create-audit-root.element.js'),
  },

  // ─── Role Picker Modal ────────────────────────────────────────────────────
  {
    type: 'modal',
    alias: 'UAP.Modal.RolePicker',
    name: 'Advanced Permissions Role Picker Modal',
    element: () => import('./access-viewer/role-picker-modal.element.js'),
  },

  // ─── User Picker Modal ────────────────────────────────────────────────────
  {
    type: 'modal',
    alias: 'UAP.Modal.UserPicker',
    name: 'Advanced Permissions User Picker Modal',
    element: () => import('./access-viewer/user-picker-modal.element.js'),
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
