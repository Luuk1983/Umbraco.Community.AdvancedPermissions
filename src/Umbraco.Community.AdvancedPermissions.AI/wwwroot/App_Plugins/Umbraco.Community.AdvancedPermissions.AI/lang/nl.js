// Dutch (nl) localization for the Advanced Permissions AI copilot tools.
//
// See en.js for an explanation of how Umbraco AI derives these keys from the
// tool ids (`uaiTool_{camelCaseId}Label` / `...Description`) and the read scope id
// (`uaiToolScope_advancedPermissions:read{Label,Description}`).
export default {
  uaiTool: {
    uapExplainUserAccessLabel: 'Toegang van gebruiker uitleggen',
    uapExplainUserAccessDescription:
      'Leg uit of een gebruiker acties mag uitvoeren op een inhoudsitem, met de onderbouwing per beslissing.',
    uapExplainRoleAccessLabel: 'Toegang van rol uitleggen',
    uapExplainRoleAccessDescription:
      'Leg uit wat een gebruikersgroep (rol) mag doen op een inhoudsitem, met de onderbouwing per beslissing.',
    uapWhoCanLabel: 'Wie kan…',
    uapWhoCanDescription:
      'Toon welke rollen een specifieke actie wel of niet mogen uitvoeren op een inhoudsitem.',
    uapAuditPermissionsLabel: 'Permissies controleren',
    uapAuditPermissionsDescription:
      "Controleer de permissie-instellingen van een rol op risico's en conflicten.",
  },
  uaiToolScope: {
    'advancedPermissions:readLabel': 'Geavanceerde permissies (lezen)',
    'advancedPermissions:readDescription':
      'Alleen-lezen toegang om geavanceerde permissies op te vragen en te controleren.',
  },
};
