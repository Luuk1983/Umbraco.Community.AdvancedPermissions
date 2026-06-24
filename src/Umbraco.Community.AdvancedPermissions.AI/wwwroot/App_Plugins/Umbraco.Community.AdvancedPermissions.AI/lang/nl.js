// Dutch (nl) localization for the Advanced Permissions AI copilot tools.
//
// See en.js for an explanation of how Umbraco AI derives these keys from the
// tool ids (`uaiTool_{camelCaseId}Label` / `...Description`), the read scope id
// (`uaiToolScope_advancedPermissions:read{Label,Description}`) and the scope
// domain heading (`uaiToolScopeDomain_advancedPermissions`).
export default {
  uaiTool: {
    uapExplainAccessLabel: 'Toegang uitleggen',
    uapExplainAccessDescription:
      'Leg uit wie wat mag doen op een inhoudsitem — en waarom — inclusief alleen-lezen / niet-kunnen-verwijderen diagnoses en welke documenttypes hier aangemaakt mogen worden.',
    uapAuditPermissionsLabel: 'Permissies controleren',
    uapAuditPermissionsDescription:
      "Controleer de permissie-instellingen van een rol, een tak of de hele site op risico's en conflicten.",
  },
  uaiToolScope: {
    'advancedPermissions:readLabel': 'Geavanceerde permissies (lezen)',
    'advancedPermissions:readDescription':
      'Alleen-lezen toegang om geavanceerde permissies op te vragen en te controleren.',
  },
  uaiToolScopeDomain: {
    advancedPermissions: 'Geavanceerde permissies',
  },
};
