// English localization for the Advanced Permissions AI copilot tools.
//
// Umbraco AI's "Select Tools" dialog localizes each tool through keys derived
// from the tool id: `uaiTool_{camelCaseId}Label` / `uaiTool_{camelCaseId}Description`
// (see Umbraco.AI.Web.StaticAssets app bundle). Umbraco's localization system
// splits a term on the FIRST underscore into a dictionary section and key, so
// `uaiTool_uapExplainAccessLabel` resolves to `uaiTool.uapExplainAccessLabel`.
//
// Tool id -> camelCase mapping (split on [-_.\s]+, first segment lowercased):
//   uap_explain_access     -> uapExplainAccess
//   uap_audit_permissions  -> uapAuditPermissions
//
// Scope id `advanced-permissions:read` -> `advancedPermissions:read` (the colon
// is NOT a separator and survives camelCasing), giving the quoted scope key below.
//
// Scope domain `advanced-permissions` -> `advancedPermissions`, surfaced as the
// heading key `uaiToolScopeDomain_advancedPermissions`.
export default {
  uaiTool: {
    uapExplainAccessLabel: 'Explain access',
    uapExplainAccessDescription:
      "Explain who can do what on a content node — and why — including read-only / can't-delete diagnoses and which document types can be created here.",
    uapAuditPermissionsLabel: 'Audit permissions',
    uapAuditPermissionsDescription:
      "Scan a role's, a subtree's, or the whole site's permission setup for risks and conflicts.",
  },
  uaiToolScope: {
    'advancedPermissions:readLabel': 'Advanced Permissions (read)',
    'advancedPermissions:readDescription':
      'Read-only access to query and audit Advanced Permissions.',
  },
  uaiToolScopeDomain: {
    advancedPermissions: 'Advanced Permissions',
  },
};
