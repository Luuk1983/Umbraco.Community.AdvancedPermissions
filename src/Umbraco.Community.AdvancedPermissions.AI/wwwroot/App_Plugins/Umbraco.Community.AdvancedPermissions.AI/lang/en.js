// English localization for the Advanced Permissions AI copilot tools.
//
// Umbraco AI's "Select Tools" dialog localizes each tool through keys derived
// from the tool id: `uaiTool_{camelCaseId}Label` / `uaiTool_{camelCaseId}Description`
// (see Umbraco.AI.Web.StaticAssets app bundle). Umbraco's localization system
// splits a term on the FIRST underscore into a dictionary section and key, so
// `uaiTool_uapExplainUserAccessLabel` resolves to `uaiTool.uapExplainUserAccessLabel`.
//
// Tool id -> camelCase mapping (split on [-_.\s]+, first segment lowercased):
//   uap_explain_user_access -> uapExplainUserAccess
//   uap_explain_role_access -> uapExplainRoleAccess
//   uap_who_can             -> uapWhoCan
//   uap_audit_permissions   -> uapAuditPermissions
//
// Scope id `advanced-permissions:read` -> `advancedPermissions:read` (the colon
// is NOT a separator and survives camelCasing), giving the quoted key below.
export default {
  uaiTool: {
    uapExplainUserAccessLabel: 'Explain user access',
    uapExplainUserAccessDescription:
      'Explain whether a user can perform actions at a content node, with the reasoning behind each decision.',
    uapExplainRoleAccessLabel: 'Explain role access',
    uapExplainRoleAccessDescription:
      'Explain what a user group (role) can do at a content node, with the reasoning behind each decision.',
    uapWhoCanLabel: 'Who can…',
    uapWhoCanDescription:
      "List which roles can or can't perform a specific action at a content node.",
    uapAuditPermissionsLabel: 'Audit permissions',
    uapAuditPermissionsDescription:
      "Scan a role's permission entries for risks and conflicts.",
  },
  uaiToolScope: {
    'advancedPermissions:readLabel': 'Advanced Permissions (read)',
    'advancedPermissions:readDescription':
      'Read-only access to query and audit Advanced Permissions.',
  },
};
