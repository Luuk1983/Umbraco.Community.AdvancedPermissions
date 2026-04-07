import type { UmbLocalizationDictionary } from '@umbraco-cms/backoffice/localization-api';

export default {
  uap: {
    // ── Navigation ────────────────────────────────────────────────────────
    sectionLabel: 'Advanced Permissions',
    permissionsEditor: 'Permissions Editor',
    accessViewer: 'Access Viewer',

    // ── Common ────────────────────────────────────────────────────────────
    roleLabel: 'User Group',
    rolePlaceholder: '\u2014 Select a user group \u2014',
    userLabel: 'User',
    saveChanges: 'Save Changes',
    discard: 'Discard',
    cancel: 'Cancel',
    apply: 'Apply',
    close: 'Close',
    inherit: 'Inherit',
    allow: 'Allow',
    deny: 'Deny',
    umbracoUsers: 'All Users',

    // ── Permissions Editor ───────────────────────────────────────────────
    editorHeadline: 'Permissions Editor',
    selectRolePrompt: 'Select a user group above to manage its permissions.',
    permissionsSaved: 'Permissions saved.',
    saveFailed: (error: string) => `Save failed: ${error}`,
    contentNodeHeader: 'Content Node',
    contentRoot: 'Default permissions',
    expand: 'Expand',
    collapse: 'Collapse',

    // ── Permission dialog ─────────────────────────────────────────────────
    dialogHeadline: (verb: string, nodeName: string) => `Set ${verb} permission for \u2018${nodeName}\u2019`,
    descendantsSection: 'Descendant override',
    dialogInstructions: 'Set the permission for this node. By default, this also applies to all descendants. Use the descendant override to set a different permission for descendant nodes.',
    virtualRootInherit: 'Not set (remove entry)',
    virtualRootAllow: 'Allow (all content)',
    virtualRootDeny: 'Deny (all content)',
    dialogResult: 'Result',
    previewBothInherit: 'No permission set. Inherits from parent.',
    previewUniform: (action: string) => `${action} on this node and all descendants.`,
    previewNodeOnly: (action: string) => `${action} on this node only. Descendants are not affected by this rule.`,
    previewDescOnly: (action: string) => `No explicit permission on this node. ${action} on all descendants.`,
    previewSplit: (nodeAction: string, descAction: string) => `${nodeAction} on this node. ${descAction} on all descendants.`,
    previewVirtualInherit: 'No default permission set.',
    previewVirtualSet: (action: string) => `${action} by default for all content.`,

    // ── Access Viewer ─────────────────────────────────────────────────────
    viewerHeadline: 'Access Viewer',
    byRole: 'By User Group',
    byUser: 'By User',
    chooseRole: 'Choose user group',
    chooseUser: 'Choose user',
    selectSubjectPrompt: 'Select a user group or user to view effective permissions.',
    legendAllow: 'Allow',
    legendDeny: 'Deny',
    clickForReasoning: (label: string) => `${label} \u2014 click for reasoning`,

    // ── Role picker modal ─────────────────────────────────────────────────
    rolePickerHeadline: 'Select a user group',
    rolePickerFilter: 'Type to filter\u2026',
    rolePickerNoResults: 'No user groups match the filter.',
    rolePickerNameHeader: 'User Group',

    // ── User picker modal ─────────────────────────────────────────────────
    userPickerHeadline: 'Select a user',
    userPickerFilter: 'Type to filter\u2026',
    userPickerNoResults: 'No users match the filter.',
    userPickerNameHeader: 'User',

    // ── Reasoning dialog ──────────────────────────────────────────────────
    reasoningHeadline: (verb: string, nodeName: string) => `${verb} permission for \u201c${nodeName}\u201d`,
    effectiveAllowed: (subject: string, verb: string, nodeName: string) =>
      `${subject} has been allowed ${verb} permission for \u201c${nodeName}\u201d.`,
    effectiveDenied: (subject: string, verb: string, nodeName: string) =>
      `${subject} has been denied ${verb} permission for \u201c${nodeName}\u201d.`,
    dialogSecurityHeader: 'Security',
    defaultPermissions: 'Default permissions',
    determiningEntry: 'This entry takes precedence',
    noReasoningData: 'No permission data available for this verb.',

    // ── Granular permission redirect ──────────────────────────────────────
    redirectMessage:
      'Document permissions for this user group are managed by the Advanced Permissions package. Open the Permissions Editor in the Users section to configure permissions.',
  },
} satisfies UmbLocalizationDictionary;
