const e = [
  // ─── Entry Point ──────────────────────────────────────────────────────────
  {
    type: "backofficeEntryPoint",
    alias: "UAS.EntryPoint",
    name: "Umbraco Advanced Security Entry Point",
    js: () => import("./entrypoint-Jm72DZvJ.js")
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
    element: () => import("./uas-security-editor-root.element-DGShFjwq.js")
  },
  // ─── Granular Permission Override ─────────────────────────────────────────
  // Replaces the built-in document granular permissions UI with a redirect
  // message pointing users to the Security Editor in the Users section.
  {
    type: "userGranularPermission",
    alias: "UAS.UserGranularPermission.Document.Redirect",
    name: "Advanced Security Document Permission Redirect",
    weight: 2e3,
    overwrites: ["Umb.UserGranularPermission.Document"],
    forEntityTypes: ["document"],
    element: () => import("./uas-granular-permission-redirect.element-x1yxkse-.js"),
    meta: {
      schemaType: "DocumentPermissionPresentationModel",
      label: "#user_permissionsGranular",
      description: "#uas_redirectMessage"
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
