/**
 * Extension manifests for the Umbraco Advanced Security package.
 * Registers menu items within the Users section, localization, and workspaces.
 * UmbExtensionManifest is a global ambient type - no import needed.
 */
const manifests: Array<UmbExtensionManifest> = [
  // ─── Entry Point ──────────────────────────────────────────────────────────
  {
    type: 'backofficeEntryPoint',
    alias: 'UAS.EntryPoint',
    name: 'Umbraco Advanced Security Entry Point',
    js: () => import('./entrypoint.js'),
  },

  // ─── Section Sidebar App (inside Users section) ───────────────────────────
  {
    type: 'sectionSidebarApp',
    kind: 'menu',
    alias: 'UAS.SidebarApp.AdvancedSecurity',
    name: 'Advanced Security Sidebar',
    weight: 50,
    conditions: [
      {
        alias: 'Umb.Condition.SectionAlias',
        match: 'Umb.Section.Users',
      },
    ],
    meta: {
      label: '#uas_sectionLabel',
      menu: 'UAS.Menu.AdvancedSecurity',
    },
  },

  // ─── Menu ─────────────────────────────────────────────────────────────────
  {
    type: 'menu',
    alias: 'UAS.Menu.AdvancedSecurity',
    name: 'Advanced Security Menu',
  },

  // ─── Menu Items ───────────────────────────────────────────────────────────
  {
    type: 'menuItem',
    alias: 'UAS.MenuItem.SecurityEditor',
    name: 'Security Editor Menu Item',
    weight: 100,
    meta: {
      label: '#uas_securityEditor',
      icon: 'icon-lock',
      entityType: 'uas-security-editor',
      menus: ['UAS.Menu.AdvancedSecurity'],
    },
  },
  {
    type: 'menuItem',
    alias: 'UAS.MenuItem.AccessViewer',
    name: 'Access Viewer Menu Item',
    weight: 50,
    meta: {
      label: '#uas_accessViewer',
      icon: 'icon-eye',
      entityType: 'uas-access-viewer',
      menus: ['UAS.Menu.AdvancedSecurity'],
    },
  },

  // ─── Workspace (Security Editor) ─────────────────────────────────────────
  {
    type: 'workspace',
    alias: 'UAS.Workspace.SecurityEditor',
    name: 'Security Editor Workspace',
    meta: {
      entityType: 'uas-security-editor',
    },
    element: () => import('./security-editor/uas-security-editor-root.element.js'),
  },

  // ─── Granular Permission Redirect (ungrouped) ────────────────────────────
  // Appears in the "General rights" box in the user group editor (rendered
  // by umb-user-group-entity-type-permission-groups when any userGranularPermission
  // has no forEntityTypes). Shows a message directing users to the Security Editor.
  // The native document entityUserPermission manifests are excluded in the entry
  // point, so the "Documents" box disappears entirely — this ungrouped entry is
  // the sole visible hint about document permission management.
  {
    type: 'userGranularPermission',
    alias: 'UAS.UserGranularPermission.Document.Redirect',
    name: 'Advanced Security Document Permission Redirect',
    weight: 2000,
    element: () => import('./components/uas-granular-permission-redirect.element.js'),
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
    alias: 'UAS.Workspace.AccessViewer',
    name: 'Access Viewer Workspace',
    meta: {
      entityType: 'uas-access-viewer',
    },
    element: () => import('./access-viewer/uas-access-viewer-root.element.js'),
  },

  // ─── Localization ─────────────────────────────────────────────────────────
  {
    type: 'localization',
    alias: 'UAS.Localization.En',
    name: 'Advanced Security English Localization',
    meta: {
      culture: 'en',
    },
    js: () => import('./localization/en.js'),
  } as UmbExtensionManifest,
  {
    type: 'localization',
    alias: 'UAS.Localization.Nl',
    name: 'Advanced Security Dutch Localization',
    meta: {
      culture: 'nl',
    },
    js: () => import('./localization/nl.js'),
  } as UmbExtensionManifest,
  {
    type: 'localization',
    alias: 'UAS.Localization.De',
    name: 'Advanced Security German Localization',
    meta: {
      culture: 'de',
    },
    js: () => import('./localization/de.js'),
  } as UmbExtensionManifest,
];

export { manifests };
