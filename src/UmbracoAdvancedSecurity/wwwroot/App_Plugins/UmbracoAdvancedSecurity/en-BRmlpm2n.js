const o = {
  uas: {
    // ── Navigation ────────────────────────────────────────────────────────
    sectionLabel: "Advanced Security",
    securityEditor: "Security Editor",
    accessViewer: "Access Viewer",
    // ── Common ────────────────────────────────────────────────────────────
    roleLabel: "Role",
    rolePlaceholder: "— Select a role —",
    userLabel: "User",
    saveChanges: "Save Changes",
    discard: "Discard",
    cancel: "Cancel",
    apply: "Apply",
    close: "Close",
    inherit: "Inherit",
    allow: "Allow",
    deny: "Deny",
    everyoneSuffix: "(Everyone)",
    // ── Security Editor ───────────────────────────────────────────────────
    editorHeadline: "Security Editor",
    selectRolePrompt: "Select a role above to manage its permissions.",
    permissionsSaved: "Permissions saved.",
    saveFailed: (e) => `Save failed: ${e}`,
    contentNodeHeader: "Content Node",
    contentRoot: "Default permissions",
    expand: "Expand",
    collapse: "Collapse",
    // ── Permission dialog ─────────────────────────────────────────────────
    dialogHeadline: (e) => `Set Permission: ${e}`,
    dialogNodeLabel: "Node",
    thisNodeSection: "This node",
    descendantsSection: "Descendants",
    sameAsNode: "Same as node",
    virtualRootInherit: "Not set (remove entry)",
    virtualRootAllow: "Allow (all content)",
    virtualRootDeny: "Deny (all content)",
    // ── Access Viewer ─────────────────────────────────────────────────────
    viewerHeadline: "Access Viewer",
    byRole: "By Role",
    byUser: "By User",
    selectSubjectPrompt: "Select a role or user to view effective permissions.",
    legendAllow: "Allow",
    legendDeny: "Deny",
    clickForReasoning: (e) => `${e} — click for reasoning`,
    // ── Reasoning dialog ──────────────────────────────────────────────────
    reasoningHeadline: (e) => `Permission Reasoning: ${e}`,
    reasoningNodeLabel: "Node",
    resultAllowed: "Allowed",
    resultDenied: "Denied",
    resultExplicit: "explicit (set directly on this node)",
    resultImplicit: "implicit (inherited or from group defaults)",
    contributingFactors: "Contributing factors:",
    groupDefault: "group default",
    fromNode: "from node",
    inherited: "(inherited)",
    noReasoningData: "No effective permission data available for this verb.",
    noReasoningEntries: "No explicit entries found — effective permission comes from system defaults.",
    // ── Granular permission redirect ──────────────────────────────────────
    redirectMessage: "Document permissions for this user group are managed by the Advanced Security package. Open the Security Editor in the Users section to configure permissions."
  }
};
export {
  o as default
};
//# sourceMappingURL=en-BRmlpm2n.js.map
