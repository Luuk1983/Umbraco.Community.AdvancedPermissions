import type { UmbLocalizationDictionary } from '@umbraco-cms/backoffice/localization-api';

export default {
  uap: {
    // ── Navigation ────────────────────────────────────────────────────────
    sectionLabel: 'Advanced Permissions',
    permissionsEditor: 'Permissions Editor',
    accessViewer: 'Access Viewer',

    // ── Common ────────────────────────────────────────────────────────────
    roleLabel: 'Role',
    rolePlaceholder: '\u2014 Select a role \u2014',
    userLabel: 'User',
    saveChanges: 'Save Changes',
    discard: 'Discard',
    cancel: 'Cancel',
    apply: 'Apply',
    close: 'Close',
    inherit: 'Inherit',
    allow: 'Allow',
    deny: 'Deny',
    umbracoUsers: 'Umbraco Users',

    // ── Permissions Editor ───────────────────────────────────────────────
    editorHeadline: 'Permissions Editor',
    selectRolePrompt: 'Select a role above to manage its permissions.',
    permissionsSaved: 'Permissions saved.',
    saveFailed: (error: string) => `Save failed: ${error}`,
    contentNodeHeader: 'Content Node',
    contentRoot: 'Default permissions',
    expand: 'Expand',
    collapse: 'Collapse',

    // ── Permission dialog ─────────────────────────────────────────────────
    dialogHeadline: (verb: string) => `Set Permission: ${verb}`,
    dialogNodeLabel: 'Node',
    thisNodeSection: 'This node',
    descendantsSection: 'Descendants',
    sameAsNode: 'Same as node',
    virtualRootInherit: 'Not set (remove entry)',
    virtualRootAllow: 'Allow (all content)',
    virtualRootDeny: 'Deny (all content)',

    // ── Access Viewer ─────────────────────────────────────────────────────
    viewerHeadline: 'Access Viewer',
    byRole: 'By Role',
    byUser: 'By User',
    chooseRole: 'Choose role',
    chooseUser: 'Choose user',
    selectSubjectPrompt: 'Select a role or user to view effective permissions.',
    legendAllow: 'Allow',
    legendDeny: 'Deny',
    clickForReasoning: (label: string) => `${label} \u2014 click for reasoning`,

    // ── Role picker modal ─────────────────────────────────────────────────
    rolePickerHeadline: 'Select a role',
    rolePickerFilter: 'Type to filter\u2026',
    rolePickerNoResults: 'No roles match the filter.',
    rolePickerNameHeader: 'Role',

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
