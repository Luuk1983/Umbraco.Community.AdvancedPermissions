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
  // Grouped by workflow (editing vs inspecting), not by feature family. The Access Viewer is
  // intentionally NOT scoped to "content permissions" because future of-type verbs
  // (DeleteOfType, MoveOfType…) will surface in its reasoning chain too — it's downstream of
  // wherever entries originate. Insert Options Viewer stays separate because it answers a
  // structurally different question ("which types may I insert here?").
  {
    type: 'sectionSidebarApp',
    kind: 'menu',
    alias: 'UAP.SidebarApp.Editors',
    name: 'Advanced Permissions Editors Sidebar',
    weight: 60,
    conditions: [
      {
        alias: 'Umb.Condition.SectionAlias',
        match: 'Umb.Section.Users',
      },
    ],
    meta: {
      label: '#uap_editorsSectionLabel',
      menu: 'UAP.Menu.Editors',
    },
  },
  {
    type: 'sectionSidebarApp',
    kind: 'menu',
    alias: 'UAP.SidebarApp.Viewers',
    name: 'Advanced Permissions Viewers Sidebar',
    weight: 50,
    conditions: [
      {
        alias: 'Umb.Condition.SectionAlias',
        match: 'Umb.Section.Users',
      },
    ],
    meta: {
      label: '#uap_viewersSectionLabel',
      menu: 'UAP.Menu.Viewers',
    },
  },

  // ─── Menus ────────────────────────────────────────────────────────────────
  {
    type: 'menu',
    alias: 'UAP.Menu.Editors',
    name: 'Advanced Permissions Editors Menu',
  },
  {
    type: 'menu',
    alias: 'UAP.Menu.Viewers',
    name: 'Advanced Permissions Viewers Menu',
  },

  // ─── Menu Items ───────────────────────────────────────────────────────────
  {
    type: 'menuItem',
    alias: 'UAP.MenuItem.PermissionsEditor',
    name: 'Content Permissions Editor Menu Item',
    weight: 100,
    meta: {
      label: '#uap_permissionsEditor',
      icon: 'icon-lock',
      entityType: 'uap-permissions-editor',
      menus: ['UAP.Menu.Editors'],
    },
  },
  {
    type: 'menuItem',
    alias: 'UAP.MenuItem.DocTypePermissions',
    name: 'Document Type Permissions Editor Menu Item',
    weight: 90,
    meta: {
      label: '#uap_docTypePermissions_menuLabel',
      icon: 'icon-document',
      entityType: 'uap-doc-type-permissions',
      menus: ['UAP.Menu.Editors'],
    },
  },
  {
    type: 'menuItem',
    alias: 'UAP.MenuItem.AccessViewer',
    name: 'Access Viewer Menu Item',
    weight: 100,
    meta: {
      label: '#uap_accessViewer',
      icon: 'icon-eye',
      entityType: 'uap-access-viewer',
      menus: ['UAP.Menu.Viewers'],
    },
  },
  {
    type: 'menuItem',
    alias: 'UAP.MenuItem.InsertOptions',
    name: 'Insert Options Viewer Menu Item',
    weight: 90,
    meta: {
      label: '#uap_docTypePermissions_insertOptionsMenuLabel',
      icon: 'icon-eye',
      entityType: 'uap-doc-type-create-audit',
      menus: ['UAP.Menu.Viewers'],
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

  // ─── Help Modal ───────────────────────────────────────────────────────────
  {
    type: 'modal',
    alias: 'UAP.Modal.Help',
    name: 'Advanced Permissions Help Modal',
    element: () => import('./help/uap-help-modal.element.js'),
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
  {
    type: 'localization',
    alias: 'UAP.Localization.EnUs',
    name: 'Advanced Permissions English (US) Localization',
    meta: {
      culture: 'en-US',
    },
    js: () => import('./localization/en-us.js'),
  } as UmbExtensionManifest,
  {
    type: 'localization',
    alias: 'UAP.Localization.Ar',
    name: 'Advanced Permissions Arabic Localization',
    meta: {
      culture: 'ar',
    },
    js: () => import('./localization/ar.js'),
  } as UmbExtensionManifest,
  {
    type: 'localization',
    alias: 'UAP.Localization.Bs',
    name: 'Advanced Permissions Bosnian Localization',
    meta: {
      culture: 'bs',
    },
    js: () => import('./localization/bs.js'),
  } as UmbExtensionManifest,
  {
    type: 'localization',
    alias: 'UAP.Localization.Cs',
    name: 'Advanced Permissions Czech Localization',
    meta: {
      culture: 'cs',
    },
    js: () => import('./localization/cs.js'),
  } as UmbExtensionManifest,
  {
    type: 'localization',
    alias: 'UAP.Localization.Cy',
    name: 'Advanced Permissions Welsh Localization',
    meta: {
      culture: 'cy',
    },
    js: () => import('./localization/cy.js'),
  } as UmbExtensionManifest,
  {
    type: 'localization',
    alias: 'UAP.Localization.Da',
    name: 'Advanced Permissions Danish Localization',
    meta: {
      culture: 'da',
    },
    js: () => import('./localization/da.js'),
  } as UmbExtensionManifest,
  {
    type: 'localization',
    alias: 'UAP.Localization.Es',
    name: 'Advanced Permissions Spanish Localization',
    meta: {
      culture: 'es',
    },
    js: () => import('./localization/es.js'),
  } as UmbExtensionManifest,
  {
    type: 'localization',
    alias: 'UAP.Localization.Fr',
    name: 'Advanced Permissions French Localization',
    meta: {
      culture: 'fr',
    },
    js: () => import('./localization/fr.js'),
  } as UmbExtensionManifest,
  {
    type: 'localization',
    alias: 'UAP.Localization.He',
    name: 'Advanced Permissions Hebrew Localization',
    meta: {
      culture: 'he',
    },
    js: () => import('./localization/he.js'),
  } as UmbExtensionManifest,
  {
    type: 'localization',
    alias: 'UAP.Localization.Hr',
    name: 'Advanced Permissions Croatian Localization',
    meta: {
      culture: 'hr',
    },
    js: () => import('./localization/hr.js'),
  } as UmbExtensionManifest,
  {
    type: 'localization',
    alias: 'UAP.Localization.It',
    name: 'Advanced Permissions Italian Localization',
    meta: {
      culture: 'it',
    },
    js: () => import('./localization/it.js'),
  } as UmbExtensionManifest,
  {
    type: 'localization',
    alias: 'UAP.Localization.Ja',
    name: 'Advanced Permissions Japanese Localization',
    meta: {
      culture: 'ja',
    },
    js: () => import('./localization/ja.js'),
  } as UmbExtensionManifest,
  {
    type: 'localization',
    alias: 'UAP.Localization.Ko',
    name: 'Advanced Permissions Korean Localization',
    meta: {
      culture: 'ko',
    },
    js: () => import('./localization/ko.js'),
  } as UmbExtensionManifest,
  {
    type: 'localization',
    alias: 'UAP.Localization.Nb',
    name: 'Advanced Permissions Norwegian Localization',
    meta: {
      culture: 'nb',
    },
    js: () => import('./localization/nb.js'),
  } as UmbExtensionManifest,
  {
    type: 'localization',
    alias: 'UAP.Localization.Pl',
    name: 'Advanced Permissions Polish Localization',
    meta: {
      culture: 'pl',
    },
    js: () => import('./localization/pl.js'),
  } as UmbExtensionManifest,
  {
    type: 'localization',
    alias: 'UAP.Localization.Pt',
    name: 'Advanced Permissions Portuguese Localization',
    meta: {
      culture: 'pt',
    },
    js: () => import('./localization/pt.js'),
  } as UmbExtensionManifest,
  {
    type: 'localization',
    alias: 'UAP.Localization.PtBr',
    name: 'Advanced Permissions Portuguese (Brazil) Localization',
    meta: {
      culture: 'pt-BR',
    },
    js: () => import('./localization/pt-br.js'),
  } as UmbExtensionManifest,
  {
    type: 'localization',
    alias: 'UAP.Localization.Ro',
    name: 'Advanced Permissions Romanian Localization',
    meta: {
      culture: 'ro',
    },
    js: () => import('./localization/ro.js'),
  } as UmbExtensionManifest,
  {
    type: 'localization',
    alias: 'UAP.Localization.Ru',
    name: 'Advanced Permissions Russian Localization',
    meta: {
      culture: 'ru',
    },
    js: () => import('./localization/ru.js'),
  } as UmbExtensionManifest,
  {
    type: 'localization',
    alias: 'UAP.Localization.Sv',
    name: 'Advanced Permissions Swedish Localization',
    meta: {
      culture: 'sv',
    },
    js: () => import('./localization/sv.js'),
  } as UmbExtensionManifest,
  {
    type: 'localization',
    alias: 'UAP.Localization.Tr',
    name: 'Advanced Permissions Turkish Localization',
    meta: {
      culture: 'tr',
    },
    js: () => import('./localization/tr.js'),
  } as UmbExtensionManifest,
  {
    type: 'localization',
    alias: 'UAP.Localization.Uk',
    name: 'Advanced Permissions Ukrainian Localization',
    meta: {
      culture: 'uk',
    },
    js: () => import('./localization/uk.js'),
  } as UmbExtensionManifest,
  {
    type: 'localization',
    alias: 'UAP.Localization.Vi',
    name: 'Advanced Permissions Vietnamese Localization',
    meta: {
      culture: 'vi',
    },
    js: () => import('./localization/vi.js'),
  } as UmbExtensionManifest,
  {
    type: 'localization',
    alias: 'UAP.Localization.Zh',
    name: 'Advanced Permissions Chinese (Simplified) Localization',
    meta: {
      culture: 'zh',
    },
    js: () => import('./localization/zh.js'),
  } as UmbExtensionManifest,
  {
    type: 'localization',
    alias: 'UAP.Localization.ZhTw',
    name: 'Advanced Permissions Chinese (Traditional) Localization',
    meta: {
      culture: 'zh-TW',
    },
    js: () => import('./localization/zh-tw.js'),
  } as UmbExtensionManifest,
];

export { manifests };
