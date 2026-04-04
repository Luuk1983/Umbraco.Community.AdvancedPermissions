/**
 * Extension manifests for the Umbraco Advanced Security package.
 * Registers the dedicated Advanced Security section with its sidebar and dashboards.
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

  // ─── Section ─────────────────────────────────────────────────────────────
  {
    type: "section",
    alias: "UAS.Section.AdvancedSecurity",
    name: "Advanced Security Section",
    meta: {
      label: "Advanced Security",
      pathname: "advanced-security",
    },
  },

  // ─── Section Sidebar App ──────────────────────────────────────────────────
  {
    type: "sectionSidebarApp",
    kind: "menu",
    alias: "UAS.SidebarApp.AdvancedSecurity",
    name: "Advanced Security Sidebar",
    weight: 100,
    conditions: [
      {
        alias: "Umb.Condition.SectionAlias",
        match: "UAS.Section.AdvancedSecurity",
      },
    ],
    meta: {
      label: "Advanced Security",
      menu: "UAS.Menu.AdvancedSecurity",
    },
  },

  // ─── Menu ─────────────────────────────────────────────────────────────────
  {
    type: "menu",
    alias: "UAS.Menu.AdvancedSecurity",
    name: "Advanced Security Menu",
  },

  // ─── Menu Items ───────────────────────────────────────────────────────────
  {
    type: "menuItem",
    alias: "UAS.MenuItem.SecurityEditor",
    name: "Security Editor Menu Item",
    weight: 100,
    meta: {
      label: "Security Editor",
      icon: "icon-lock",
      entityType: "uas-security-editor",
      menus: ["UAS.Menu.AdvancedSecurity"],
    },
  },
  {
    type: "menuItem",
    alias: "UAS.MenuItem.AccessViewer",
    name: "Access Viewer Menu Item",
    weight: 200,
    meta: {
      label: "Access Viewer",
      icon: "icon-eye",
      entityType: "uas-access-viewer",
      menus: ["UAS.Menu.AdvancedSecurity"],
    },
  },

  // ─── Workspace (Security Editor) ─────────────────────────────────────────
  {
    type: "workspace",
    alias: "UAS.Workspace.SecurityEditor",
    name: "Security Editor Workspace",
    meta: {
      entityType: "uas-security-editor",
    },
    element: () => import("./security-editor/uas-security-editor-root.element.js"),
  },

  // ─── Granular Permission Override ─────────────────────────────────────────
  // Replaces the built-in document granular permissions UI with a redirect
  // message pointing users to the Advanced Security section.
  {
    type: 'userGranularPermission',
    alias: 'UAS.UserGranularPermission.Document.Redirect',
    name: 'Advanced Security Document Permission Redirect',
    weight: 2000,
    overwrites: ['Umb.UserGranularPermission.Document'],
    forEntityTypes: ['document'],
    element: () => import('./components/uas-granular-permission-redirect.element.js'),
    meta: {
      schemaType: 'DocumentPermissionPresentationModel',
      label: '#user_permissionsGranular',
      description: 'Document permissions are managed by the Advanced Security package.',
    },
  } as UmbExtensionManifest,

  // ─── Workspace (Access Viewer) ────────────────────────────────────────────
  {
    type: "workspace",
    alias: "UAS.Workspace.AccessViewer",
    name: "Access Viewer Workspace",
    meta: {
      entityType: "uas-access-viewer",
    },
    element: () => import("./access-viewer/uas-access-viewer-root.element.js"),
  },
];

export { manifests };
