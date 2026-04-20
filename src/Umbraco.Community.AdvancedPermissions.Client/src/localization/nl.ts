import type { UmbLocalizationDictionary } from '@umbraco-cms/backoffice/localization-api';

export default {
  uap: {
    // ── Navigatie ─────────────────────────────────────────────────────────
    sectionLabel: 'Geavanceerde machtigingen',
    permissionsEditor: 'Machtigingeneditor',
    accessViewer: 'Toegangsweergave',

    // ── Algemeen ──────────────────────────────────────────────────────────
    roleLabel: 'Gebruikersgroep',
    rolePlaceholder: '\u2014 Selecteer een gebruikersgroep \u2014',
    userLabel: 'Gebruiker',
    saveChanges: 'Wijzigingen opslaan',
    discard: 'Verwerpen',
    cancel: 'Annuleren',
    apply: 'Toepassen',
    close: 'Sluiten',
    inherit: 'Overnemen',
    allow: 'Toestaan',
    deny: 'Weigeren',
    umbracoUsers: 'Alle gebruikers',

    // ── Machtigingeneditor ───────────────────────────────────────────────
    editorHeadline: 'Machtigingeneditor',
    selectRolePrompt: 'Selecteer hierboven een gebruikersgroep om de machtigingen te beheren.',
    permissionsSaved: 'Machtigingen opgeslagen.',
    saveFailed: (error: string) => `Opslaan mislukt: ${error}`,
    contentNodeHeader: 'Contentpagina',
    contentRoot: 'Standaardmachtigingen',
    expand: 'Uitvouwen',
    collapse: 'Invouwen',

    // ── Machtigingsdialoog ────────────────────────────────────────────────
    dialogHeadline: (verb: string, nodeName: string) => `${verb}-machtiging instellen voor \u2018${nodeName}\u2019`,
    descendantsSection: 'Overschrijving onderliggende pagina\u2019s',
    dialogInstructions: 'Stel de machtiging in voor deze pagina. Standaard geldt dit ook voor alle onderliggende pagina\u2019s. Gebruik de overschrijving om een andere machtiging in te stellen voor onderliggende pagina\u2019s.',
    virtualRootInherit: 'Niet ingesteld (verwijder vermelding)',
    virtualRootAllow: 'Toestaan (alle content)',
    virtualRootDeny: 'Weigeren (alle content)',
    dialogResult: 'Resultaat',
    previewBothInherit: 'Geen machtiging ingesteld. Neemt over van bovenliggende pagina.',
    previewUniform: (action: string) => `${action} voor deze pagina en alle onderliggende pagina\u2019s.`,
    previewNodeOnly: (action: string) => `${action} alleen voor deze pagina. Onderliggende pagina\u2019s worden niet be\u00efnvloed door deze regel.`,
    previewDescOnly: (action: string) => `Geen expliciete machtiging voor deze pagina. ${action} voor alle onderliggende pagina\u2019s.`,
    previewSplit: (nodeAction: string, descAction: string) => `${nodeAction} voor deze pagina. ${descAction} voor alle onderliggende pagina\u2019s.`,
    previewVirtualInherit: 'Geen standaardmachtiging ingesteld.',
    previewVirtualSet: (action: string) => `${action} standaard voor alle content.`,

    // ── Toegangsweergave ──────────────────────────────────────────────────
    viewerHeadline: 'Toegangsweergave',
    byRole: 'Op gebruikersgroep',
    byUser: 'Op gebruiker',
    chooseRole: 'Kies gebruikersgroep',
    chooseUser: 'Kies gebruiker',
    selectSubjectPrompt: 'Selecteer een gebruikersgroep of gebruiker om de effectieve machtigingen te bekijken.',
    legendAllow: 'Toestaan',
    legendDeny: 'Weigeren',
    clickForReasoning: (label: string) => `${label} \u2014 klik voor onderbouwing`,

    // ── Rolkiezerdialoog ──────────────────────────────────────────────────
    rolePickerHeadline: 'Selecteer een gebruikersgroep',
    rolePickerFilter: 'Gebruikersgroepen filteren\u2026',
    rolePickerNoResults: 'Geen gebruikersgroepen gevonden.',
    rolePickerNameHeader: 'Gebruikersgroep',

    // ── Gebruikerskiezerdialoog ───────────────────────────────────────────
    userPickerHeadline: 'Selecteer een gebruiker',
    userPickerFilter: 'Gebruikers filteren\u2026',
    userPickerNoResults: 'Geen gebruikers gevonden.',
    userPickerNameHeader: 'Gebruiker',

    // ── Onderbouwingsdialoog ──────────────────────────────────────────────
    reasoningHeadline: (verb: string, nodeName: string) => `${verb}-machtiging voor \u201c${nodeName}\u201d`,
    effectiveAllowed: (subject: string, verb: string, nodeName: string) =>
      `${subject} heeft ${verb}-machtiging gekregen voor \u201c${nodeName}\u201d.`,
    effectiveDenied: (subject: string, verb: string, nodeName: string) =>
      `${subject} is ${verb}-machtiging geweigerd voor \u201c${nodeName}\u201d.`,
    dialogSecurityHeader: 'Beveiliging',
    defaultPermissions: 'Standaardmachtigingen',
    determiningEntry: 'Deze vermelding heeft voorrang',
    noReasoningData: 'Geen machtigingsgegevens beschikbaar voor dit recht.',

    // ── Omleidingsbericht granulaire machtigingen ─────────────────────────
    redirectMessage:
      'Documentmachtigingen voor deze gebruikersgroep worden beheerd door het Advanced Permissions-pakket. Open de Machtigingeneditor in de sectie Gebruikers om machtigingen te configureren.',
  },
} satisfies UmbLocalizationDictionary;
