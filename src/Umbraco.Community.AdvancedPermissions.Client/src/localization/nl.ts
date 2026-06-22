import type { UmbLocalizationDictionary } from '@umbraco-cms/backoffice/localization-api';

export default {
  uap: {
    // ── Navigatie ─────────────────────────────────────────────────────────
    sectionLabel: 'Geavanceerde machtigingen',
    editorsSectionLabel: 'Editors',
    viewersSectionLabel: 'Weergaven',
    permissionsEditor: 'Inhoudsmachtigingen-editor',
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

    // ── Help ──────────────────────────────────────────────────────────────
    help_contentPermissions_description: 'Beheer Toestaan/Weigeren-machtigingen per gebruikersgroep over je contentboom.',
    help_accessViewer_description: 'Bekijk de effectieve machtigingen van een gebruiker of groep op elk knooppunt, met volledige redenering.',
    help_docTypePermissions_description: 'Bepaal welke documenttypen elke gebruikersgroep mag aanmaken, en waar.',
    help_insertOptions_description: 'Controleer welke documenttypen een gebruiker of gebruikersgroep op elk knooppunt mag aanmaken.',
    help_libraryPermissions_description: 'Beheer Toestaan/Weigeren-machtigingen per gebruikersgroep over de Library-boom.',
    help_elementTypePermissions_description: 'Bepaal welke elementtypen elke gebruikersgroep in de Library mag gebruiken.',
    help_libraryAccessViewer_description: 'Bekijk de effectieve Library-machtigingen van een gebruiker of gebruikersgroep op elk knooppunt.',
    help_libraryInsertViewer_description: 'Controleer welke elementtypen een gebruiker of gebruikersgroep in de Library mag invoegen.',
    help_learnMore: 'Meer informatie',
    help_modalTitle: 'Help',
    help_tabAbout: 'Over deze pagina',
    help_tabConcepts: 'Concepten',
    help_concept_scope_tip: 'Bereik bepaalt hoe ver een regel reikt: dit knooppunt, dit knooppunt en onderliggende, of alleen onderliggende.',
    help_concept_priorityOverride_tip: 'Prioriteitsoverschrijving laat deze invoer winnen boven de normale oplosvolgorde.',
    help_concept_allowDeny_tip: 'Toestaan verleent de machtiging, Weigeren trekt die in, en niet instellen erft van de dichtstbijzijnde bovenliggende.',
    help_concept_reasoning_tip: 'Klik op een cel om te zien hoe de machtiging precies is opgelost.',

    // ── Machtigingeneditor ───────────────────────────────────────────────
    editorHeadline: 'Inhoudsmachtigingen-editor',
    selectRolePrompt: 'Selecteer hierboven een gebruikersgroep om de machtigingen te beheren.',
    permissionsSaved: 'Machtigingen opgeslagen.',
    saveFailed: (error: string) => `Opslaan mislukt: ${error}`,
    contentNodeHeader: 'Contentpagina',
    contentRoot: 'Standaardmachtigingen',
    expand: 'Uitvouwen',
    collapse: 'Invouwen',

    // ── Machtigingsdialoog ────────────────────────────────────────────────
    dialogHeadline: (verb: string, nodeName: string) => `${verb}-machtiging instellen voor \u2018${nodeName}\u2019`,
    descendantsSection: 'Onderliggende pagina\u2019s (indien afwijkend)',
    dialogInstructions: 'Stel de machtiging in voor deze pagina. Standaard geldt dit ook voor alle onderliggende pagina\u2019s. Gebruik \u201cOnderliggende pagina\u2019s (indien afwijkend)\u201d om een andere machtiging in te stellen voor onderliggende pagina\u2019s.',
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
    previewPriorityNode: 'Prioriteitsoverschrijving staat aan voor deze pagina.',
    previewPriorityDesc: 'Prioriteitsoverschrijving staat aan voor onderliggende pagina’s.',
    previewPriorityBoth: 'Prioriteitsoverschrijving staat aan.',

    // ── Prioriteitsoverschrijving ─────────────────────────────────────
    priorityOverride: 'Prioriteitsoverschrijving',
    priorityOverrideTooltip: (permission: string, nodeName: string) =>
      `Een gebruiker kan tot meerdere gebruikersgroepen behoren. Normaal volgt de effectieve machtiging een vaste prioriteitsvolgorde. Door dit vakje aan te vinken wordt die volgorde overschreven, zodat de hier gekozen “${permission}”-instelling vrijwel altijd het resultaat voor “${nodeName}” wordt, ongeacht de andere groepen van de gebruiker. Spaarzaam gebruiken.`,
    priorityOverrideBadgeTitle: 'Prioriteitsoverschrijving staat aan voor deze invoer',
    priorityOverrideWonTitle: 'Opgelost via prioriteitsoverschrijving',
    priorityOverrideSuppressedHeader: 'Prioriteitsoverschrijving heeft het resultaat gewijzigd',
    priorityOverrideSuppressedHint: 'Zonder deze zou het resultaat zijn geweest:',

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
    subjectOr: 'of',

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
    defaultAllowNote: 'Er zijn geen machtigingen ingesteld, dit is standaard toegestaan.',
    defaultDenyNote: 'Er zijn geen machtigingen ingesteld, dit wordt standaard geweigerd.',

    // ── Omleidingsbericht granulaire machtigingen ─────────────────────────
    redirectMessage:
      'Documentmachtigingen voor deze gebruikersgroep worden beheerd door het Advanced Permissions-pakket. Open de Machtigingeneditor in de sectie Gebruikers om machtigingen te configureren.',

    // ── Documenttype-machtigingen ─────────────────────────────────────────
    role: 'Gebruikersgroep',
    pickRole: 'Kies gebruikersgroep',
    user: 'Gebruiker',
    pickUser: 'Kies gebruiker',
    node: 'Pagina',
    pickNode: 'Kies pagina',
    state: 'Status',
    scope: 'Bereik',
    scope_thisNodeOnly: 'Alleen deze pagina',
    scope_thisNodeAndDescendants: 'Deze pagina en onderliggende',
    scope_descendantsOnly: 'Alleen onderliggende',

    docTypePermissions_menuLabel: 'Documenttype-machtigingeneditor',
    docTypePermissions_insertOptionsMenuLabel: 'Invoegopties-weergave',
    docTypePermissions_workspaceTitle: 'Documenttype-machtigingeneditor',
    docTypePermissions_auditTitle: 'Invoegopties-weergave',
    docTypePermissions_allDocTypes: 'Alle documenttypes',
    docTypePermissions_verbInsert: 'Invoegen',
    docTypePermissions_documentType: 'Documenttype',
    chooseDocType: 'Kies documenttype',
    notAnInsertOption: 'Dit documenttype is momenteel geen invoegoptie op deze pagina.',
    notAnInsertOptionAllowedNote: 'Dit documenttype is geen invoegoptie op deze pagina, maar zou anders worden toegestaan.',
    notAnInsertOptionDeniedNote: 'Dit documenttype is geen invoegoptie op deze pagina, maar zou anders worden geweigerd.',
    docTypePermissions_pickDocType: '— Selecteer een documenttype —',
    docTypePermissions_pickToStart: 'Kies een gebruikersgroep en documenttype om te beginnen.',
    docTypePermissions_defaultRowLabel: 'Standaard (geldt overal)',
    docTypePermissions_pendingNodeLabel: '(niet-opgeslagen pagina)',
    docTypePermissions_addScopeNode: 'Bereik per pagina toevoegen',
    docTypePermissions_notSet: 'Niet ingesteld',
    docTypePermissions_noResults: 'Geen documenttypes gevonden.',
    docTypePermissions_useRoot: 'Gebruik root',
    docTypePermissions_pickedNode: 'Pagina:',
    docTypePermissions_rootLevel: 'Rootniveau',
    docTypePermissions_reasoning: 'Onderbouwing',
    docTypePermissions_defaultAllow: 'Standaard toegestaan',
    docTypePermissions_viaDefault: 'via standaardregel',
  },
} satisfies UmbLocalizationDictionary;
