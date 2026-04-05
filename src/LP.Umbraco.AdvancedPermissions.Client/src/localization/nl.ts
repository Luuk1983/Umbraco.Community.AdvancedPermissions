import type { UmbLocalizationDictionary } from '@umbraco-cms/backoffice/localization-api';

export default {
  uap: {
    // ── Navigatie ─────────────────────────────────────────────────────────
    sectionLabel: 'Geavanceerde machtigingen',
    permissionsEditor: 'Machtigingeneditor',
    accessViewer: 'Toegangsweergave',

    // ── Algemeen ──────────────────────────────────────────────────────────
    roleLabel: 'Rol',
    rolePlaceholder: '\u2014 Selecteer een rol \u2014',
    userLabel: 'Gebruiker',
    saveChanges: 'Wijzigingen opslaan',
    discard: 'Verwerpen',
    cancel: 'Annuleren',
    apply: 'Toepassen',
    close: 'Sluiten',
    inherit: 'Overnemen',
    allow: 'Toestaan',
    deny: 'Weigeren',
    everyoneSuffix: '(Iedereen)',

    // ── Machtigingeneditor ───────────────────────────────────────────────
    editorHeadline: 'Machtigingeneditor',
    selectRolePrompt: 'Selecteer hierboven een rol om de machtigingen te beheren.',
    permissionsSaved: 'Machtigingen opgeslagen.',
    saveFailed: (error: string) => `Opslaan mislukt: ${error}`,
    contentNodeHeader: 'Contentpagina',
    contentRoot: 'Standaardmachtigingen',
    expand: 'Uitvouwen',
    collapse: 'Invouwen',

    // ── Machtigingsdialoog ────────────────────────────────────────────────
    dialogHeadline: (verb: string) => `Machtiging instellen: ${verb}`,
    dialogNodeLabel: 'Pagina',
    thisNodeSection: 'Deze pagina',
    descendantsSection: 'Onderliggende pagina\u2019s',
    sameAsNode: 'Zelfde als pagina',
    virtualRootInherit: 'Niet ingesteld (verwijder vermelding)',
    virtualRootAllow: 'Toestaan (alle content)',
    virtualRootDeny: 'Weigeren (alle content)',

    // ── Toegangsweergave ──────────────────────────────────────────────────
    viewerHeadline: 'Toegangsweergave',
    byRole: 'Op rol',
    byUser: 'Op gebruiker',
    selectSubjectPrompt: 'Selecteer een rol of gebruiker om de effectieve machtigingen te bekijken.',
    legendAllow: 'Toestaan',
    legendDeny: 'Weigeren',
    clickForReasoning: (label: string) => `${label} \u2014 klik voor onderbouwing`,

    // ── Onderbouwingsdialoog ──────────────────────────────────────────────
    reasoningHeadline: (verb: string) => `Machtigingsonderbouwing: ${verb}`,
    reasoningNodeLabel: 'Pagina',
    resultAllowed: 'Toegestaan',
    resultDenied: 'Geweigerd',
    resultExplicit: 'expliciet (direct ingesteld op deze pagina)',
    resultImplicit: 'impliciet (overgenomen of vanuit groepsstandaarden)',
    contributingFactors: 'Bijdragende factoren:',
    groupDefault: 'groepsstandaard',
    fromNode: 'van pagina',
    inherited: '(overgenomen)',
    noReasoningData: 'Geen effectieve machtigingsgegevens beschikbaar voor dit recht.',
    noReasoningEntries: 'Geen expliciete vermeldingen gevonden \u2014 effectieve machtiging komt van systeemstandaarden.',

    // ── Omleidingsbericht granulaire machtigingen ─────────────────────────
    redirectMessage:
      'Documentmachtigingen voor deze gebruikersgroep worden beheerd door het Advanced Permissions-pakket. Open de Machtigingeneditor in de sectie Gebruikers om machtigingen te configureren.',
  },
} satisfies UmbLocalizationDictionary;
