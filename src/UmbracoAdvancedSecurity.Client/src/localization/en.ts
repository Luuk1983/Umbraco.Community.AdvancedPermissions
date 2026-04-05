import type { UmbLocalizationDictionary } from '@umbraco-cms/backoffice/localization-api';

export default {
  uas: {
    // ── Navigation ────────────────────────────────────────────────────────
    sectionLabel: 'Advanced Security',
    securityEditor: 'Security Editor',
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
    everyoneSuffix: '(Everyone)',

    // ── Security Editor ───────────────────────────────────────────────────
    editorHeadline: 'Security Editor',
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
    selectSubjectPrompt: 'Select a role or user to view effective permissions.',
    legendAllow: 'Allow',
    legendDeny: 'Deny',
    clickForReasoning: (label: string) => `${label} \u2014 click for reasoning`,

    // ── Reasoning dialog ──────────────────────────────────────────────────
    reasoningHeadline: (verb: string) => `Permission Reasoning: ${verb}`,
    reasoningNodeLabel: 'Node',
    resultAllowed: 'Allowed',
    resultDenied: 'Denied',
    resultExplicit: 'explicit (set directly on this node)',
    resultImplicit: 'implicit (inherited or from group defaults)',
    contributingFactors: 'Contributing factors:',
    groupDefault: 'group default',
    fromNode: 'from node',
    inherited: '(inherited)',
    noReasoningData: 'No effective permission data available for this verb.',
    noReasoningEntries: 'No explicit entries found \u2014 effective permission comes from system defaults.',

    // ── Granular permission redirect ──────────────────────────────────────
    redirectMessage:
      'Document permissions for this user group are managed by the Advanced Security package. Open the Security Editor in the Users section to configure permissions.',
  },
} satisfies UmbLocalizationDictionary;
