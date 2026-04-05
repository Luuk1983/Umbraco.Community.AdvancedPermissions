const e = [
  // ─── Entry Point ──────────────────────────────────────────────────────────
  {
    type: "backofficeEntryPoint",
    alias: "UAS.EntryPoint",
    name: "Umbraco Advanced Security Entry Point",
    js: () => import("./entrypoint-CBdy9Z8U.js")
  },
  // ─── Section Sidebar App (inside Users section) ───────────────────────────
  {
    type: "sectionSidebarApp",
    kind: "menu",
    alias: "UAS.SidebarApp.AdvancedSecurity",
    name: "Advanced Security Sidebar",
    weight: 50,
    conditions: [
      {
        alias: "Umb.Condition.SectionAlias",
        match: "Umb.Section.Users"
      }
    ],
    meta: {
      label: "#uas_sectionLabel",
      menu: "UAS.Menu.AdvancedSecurity"
    }
  },
  // ─── Menu ─────────────────────────────────────────────────────────────────
  {
    type: "menu",
    alias: "UAS.Menu.AdvancedSecurity",
    name: "Advanced Security Menu"
  },
  // ─── Menu Items ───────────────────────────────────────────────────────────
  {
    type: "menuItem",
    alias: "UAS.MenuItem.SecurityEditor",
    name: "Security Editor Menu Item",
    weight: 100,
    meta: {
      label: "#uas_securityEditor",
      icon: "icon-lock",
      entityType: "uas-security-editor",
      menus: ["UAS.Menu.AdvancedSecurity"]
    }
  },
  {
    type: "menuItem",
    alias: "UAS.MenuItem.AccessViewer",
    name: "Access Viewer Menu Item",
    weight: 50,
    meta: {
      label: "#uas_accessViewer",
      icon: "icon-eye",
      entityType: "uas-access-viewer",
      menus: ["UAS.Menu.AdvancedSecurity"]
    }
  },
  // ─── Workspace (Security Editor) ─────────────────────────────────────────
  {
    type: "workspace",
    alias: "UAS.Workspace.SecurityEditor",
    name: "Security Editor Workspace",
    meta: {
      entityType: "uas-security-editor"
    },
    element: () => import("./uas-security-editor-root.element-BTb6bx31.js")
  },
  // ─── Granular Permission Redirect (ungrouped) ────────────────────────────
  // Appears in the "General rights" box in the user group editor (rendered
  // by umb-user-group-entity-type-permission-groups when any userGranularPermission
  // has no forEntityTypes). Shows a message directing users to the Security Editor.
  // The native document entityUserPermission manifests are excluded in the entry
  // point, so the "Documents" box disappears entirely — this ungrouped entry is
  // the sole visible hint about document permission management.
  {
    type: "userGranularPermission",
    alias: "UAS.UserGranularPermission.Document.Redirect",
    name: "Advanced Security Document Permission Redirect",
    weight: 2e3,
    element: () => import("./uas-granular-permission-redirect.element-x1yxkse-.js"),
    meta: {
      schemaType: "DocumentPermissionPresentationModel",
      // labelKey resolves via localize.term() — 'user_permissionsEntityGroup_document'
      // gives "Documents" in Umbraco's built-in localization files.
      labelKey: "user_permissionsEntityGroup_document"
    }
  },
  // ─── Workspace (Access Viewer) ────────────────────────────────────────────
  {
    type: "workspace",
    alias: "UAS.Workspace.AccessViewer",
    name: "Access Viewer Workspace",
    meta: {
      entityType: "uas-access-viewer"
    },
    element: () => import("./uas-access-viewer-root.element-BpqlsKzY.js")
  },
  // ─── Localization ─────────────────────────────────────────────────────────
  {
    type: "localization",
    alias: "UAS.Localization.En",
    name: "Advanced Security English Localization",
    meta: {
      culture: "en"
    },
    js: () => import("./en-BRmlpm2n.js")
  },
  {
    type: "localization",
    alias: "UAS.Localization.Nl",
    name: "Advanced Security Dutch Localization",
    meta: {
      culture: "nl"
    },
    js: () => import("./nl-nASR5uCX.js")
  },
  {
    type: "localization",
    alias: "UAS.Localization.De",
    name: "Advanced Security German Localization",
    meta: {
      culture: "de"
    },
    js: () => import("./de-CYWjgqlm.js")
  }
];
export {
  e as manifests
};
//# sourceMappingURL=uas.js.map
