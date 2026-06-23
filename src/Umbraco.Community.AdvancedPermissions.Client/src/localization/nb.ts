import type { UmbLocalizationDictionary } from '@umbraco-cms/backoffice/localization-api';

export default {
  uap: {
    // ── Navigasjon ────────────────────────────────────────────────────────
    sectionLabel: 'Avanserte tillatelser',
    editorsSectionLabel: 'Redigering',
    viewersSectionLabel: 'Visning',
    permissionsEditor: 'Editor for innholdstillatelser',
    accessViewer: 'Tilgangsvisning',

    // ── Generelt ──────────────────────────────────────────────────────────
    roleLabel: 'Brukergruppe',
    rolePlaceholder: '— Velg en brukergruppe —',
    userLabel: 'Bruker',
    saveChanges: 'Lagre endringer',
    discard: 'Forkast',
    cancel: 'Avbryt',
    apply: 'Bruk',
    close: 'Lukk',
    inherit: 'Arv',
    allow: 'Tillat',
    deny: 'Nekt',
    umbracoUsers: 'Alle brukere',

    // ── Help ──────────────────────────────────────────────────────────────
    help_contentPermissions_description: 'Administrer Tillat/Nekt-tillatelser per brukergruppe på tvers av innholdstreet.',
    help_accessViewer_description: 'Se de effektive tillatelsene en bruker eller gruppe har på en hvilken som helst node, med full begrunnelse.',
    help_docTypePermissions_description: 'Bestem hvilke dokumenttyper hver brukergruppe kan opprette, og hvor.',
    help_insertOptions_description: 'Revider hvilke dokumenttyper en bruker eller brukergruppe kan opprette på hver node.',
    help_learnMore: 'Lær mer',
    help_modalTitle: 'Hjelp',
    help_tabAbout: 'Om denne siden',
    help_tabConcepts: 'Begreper',
    help_concept_scope_tip: 'Omfang styrer hvor langt en regel rekker: denne noden, denne noden og dens underordnede, eller kun underordnede.',
    help_concept_priorityOverride_tip: 'Prioritetsoverstyring tvinger denne oppføringen til å vinne over den normale rekkefølgen for løsning.',
    help_concept_allowDeny_tip: 'Tillat gir tillatelsen, Nekt opphever den, og hvis den ikke angis arves den fra nærmeste overordnede node.',
    help_concept_reasoning_tip: 'Klikk på en hvilken som helst celle for å se nøyaktig hvordan tillatelsen ble løst.',

    // ── Editor for tillatelser ───────────────────────────────────────────
    editorHeadline: 'Editor for innholdstillatelser',
    selectRolePrompt: 'Velg en brukergruppe ovenfor for å administrere tillatelsene.',
    permissionsSaved: 'Tillatelser lagret.',
    saveFailed: (error: string) => `Lagring mislyktes: ${error}`,
    contentNodeHeader: 'Innholdsnode',
    contentRoot: 'Standardtillatelser',
    expand: 'Utvid',
    collapse: 'Skjul',

    // ── Tillatelsesdialog ─────────────────────────────────────────────────
    dialogHeadline: (verb: string, nodeName: string) => `Angi ${verb}-tillatelse for ‘${nodeName}’`,
    descendantsSection: 'Underordnede noder (hvis avvikende)',
    dialogInstructions: 'Angi tillatelsen for denne noden. Som standard gjelder dette også for alle underordnede noder. Bruk “Underordnede noder (hvis avvikende)” for å angi en annen tillatelse for underordnede noder.',
    virtualRootInherit: 'Ikke angitt (fjern oppføring)',
    virtualRootAllow: 'Tillat (alt innhold)',
    virtualRootDeny: 'Nekt (alt innhold)',
    dialogResult: 'Resultat',
    previewBothInherit: 'Ingen tillatelse angitt. Arver fra overordnet node.',
    previewUniform: (action: string) => `${action} for denne noden og alle underordnede noder.`,
    previewNodeOnly: (action: string) => `${action} kun for denne noden. Underordnede noder påvirkes ikke av denne regelen.`,
    previewDescOnly: (action: string) => `Ingen eksplisitt tillatelse for denne noden. ${action} for alle underordnede noder.`,
    previewSplit: (nodeAction: string, descAction: string) => `${nodeAction} for denne noden. ${descAction} for alle underordnede noder.`,
    previewVirtualInherit: 'Ingen standardtillatelse angitt.',
    previewVirtualSet: (action: string) => `${action} som standard for alt innhold.`,
    previewPriorityNode: 'Prioritetsoverstyring er angitt for denne noden.',
    previewPriorityDesc: 'Prioritetsoverstyring er angitt for underordnede noder.',
    previewPriorityBoth: 'Prioritetsoverstyring er angitt.',

    // ── Prioritetsoverstyring ─────────────────────────────────────────
    priorityOverride: 'Prioritetsoverstyring',
    priorityOverrideTooltip: (permission: string, nodeName: string) =>
      `En bruker kan tilhøre flere brukergrupper. Normalt følger den effektive tillatelsen en fast prioritetsrekkefølge. Å krysse av i denne boksen overstyrer den rekkefølgen, slik at “${permission}”-innstillingen du velger her nesten alltid blir resultatet for “${nodeName}”, uavhengig av brukerens andre grupper. Bruk sparsomt.`,
    priorityOverrideBadgeTitle: 'Prioritetsoverstyring er angitt for denne oppføringen',
    priorityOverrideWonTitle: 'Løst via prioritetsoverstyring',
    priorityOverrideSuppressedHeader: 'Prioritetsoverstyring endret resultatet',
    priorityOverrideSuppressedHint: 'Uten den ville resultatet vært:',

    // ── Tilgangsvisning ───────────────────────────────────────────────────
    viewerHeadline: 'Tilgangsvisning',
    byRole: 'Etter brukergruppe',
    byUser: 'Etter bruker',
    chooseRole: 'Velg brukergruppe',
    chooseUser: 'Velg bruker',
    selectSubjectPrompt: 'Velg en brukergruppe eller bruker for å se effektive tillatelser.',
    legendAllow: 'Tillat',
    legendDeny: 'Nekt',
    clickForReasoning: (label: string) => `${label} — klikk for begrunnelse`,
    subjectOr: 'eller',

    // ── Brukergruppevelger ──────────────────────────────────────────────────
    rolePickerHeadline: 'Velg en brukergruppe',
    rolePickerFilter: 'Skriv for å filtrere…',
    rolePickerNoResults: 'Ingen brukergrupper samsvarer med filteret.',
    rolePickerNameHeader: 'Brukergruppe',

    // ── Brukervelger ───────────────────────────────────────────
    userPickerHeadline: 'Velg en bruker',
    userPickerFilter: 'Skriv for å filtrere…',
    userPickerNoResults: 'Ingen brukere samsvarer med filteret.',
    userPickerNameHeader: 'Bruker',

    // ── Begrunnelsesdialog ──────────────────────────────────────────────────
    reasoningHeadline: (verb: string, nodeName: string) => `${verb}-tillatelse for “${nodeName}”`,
    effectiveAllowed: (subject: string, verb: string, nodeName: string) =>
      `${subject} har fått ${verb}-tillatelse for “${nodeName}”.`,
    effectiveDenied: (subject: string, verb: string, nodeName: string) =>
      `${subject} er nektet ${verb}-tillatelse for “${nodeName}”.`,
    dialogSecurityHeader: 'Sikkerhet',
    defaultPermissions: 'Standardtillatelser',
    determiningEntry: 'Denne oppføringen har forrang',
    noReasoningData: 'Ingen tillatelsesdata tilgjengelig for dette verbet.',
    defaultAllowNote: 'Ingen tillatelser er angitt, dette er tillatt som standard.',
    defaultDenyNote: 'Ingen tillatelser er angitt, dette nektes som standard.',

    // ── Omdirigeringsmelding for granulære tillatelser ────────────────────
    redirectMessage:
      'Dokumenttillatelser for denne brukergruppen administreres av Advanced Permissions-pakken. Åpne editoren for tillatelser i Brukere-seksjonen for å konfigurere tillatelser.',

    // ── Dokumenttype-tillatelser ──────────────────────────────────────────
    role: 'Brukergruppe',
    pickRole: 'Velg brukergruppe',
    user: 'Bruker',
    pickUser: 'Velg bruker',
    node: 'Node',
    pickNode: 'Velg node',
    state: 'Status',
    scope: 'Omfang',
    scope_thisNodeOnly: 'Kun denne noden',
    scope_thisNodeAndDescendants: 'Denne noden og underordnede',
    scope_descendantsOnly: 'Kun underordnede',

    docTypePermissions_menuLabel: 'Editor for dokumenttype-tillatelser',
    docTypePermissions_insertOptionsMenuLabel: 'Visning av innsettingsalternativer',
    docTypePermissions_workspaceTitle: 'Editor for dokumenttype-tillatelser',
    docTypePermissions_auditTitle: 'Visning av innsettingsalternativer',
    docTypePermissions_allDocTypes: 'Alle dokumenttyper',
    docTypePermissions_verbInsert: 'Sett inn',
    docTypePermissions_documentType: 'Dokumenttype',
    chooseDocType: 'Velg dokumenttype',
    notAnInsertOption: 'Denne dokumenttypen er for øyeblikket ikke et innsettingsalternativ på denne noden.',
    notAnInsertOptionAllowedNote: 'Denne dokumenttypen er ikke et innsettingsalternativ på denne noden, men ville ellers vært tillatt.',
    notAnInsertOptionDeniedNote: 'Denne dokumenttypen er ikke et innsettingsalternativ på denne noden, men ville ellers blitt nektet.',
    docTypePermissions_pickDocType: '— Velg en dokumenttype —',
    docTypePermissions_pickToStart: 'Velg en brukergruppe og dokumenttype for å starte.',
    docTypePermissions_defaultRowLabel: 'Standard (gjelder overalt)',
    docTypePermissions_pendingNodeLabel: '(ikke-lagret node)',
    docTypePermissions_addScopeNode: 'Legg til omfangsoverstyring',
    docTypePermissions_notSet: 'Ikke angitt',
    docTypePermissions_noResults: 'Ingen dokumenttyper funnet.',
    docTypePermissions_useRoot: 'Bruk rot',
    docTypePermissions_pickedNode: 'Node:',
    docTypePermissions_rootLevel: 'Rotnivå',
    docTypePermissions_reasoning: 'Begrunnelse',
    docTypePermissions_defaultAllow: 'Tillatt som standard',
    docTypePermissions_viaDefault: 'fra standardrad',
  },
} satisfies UmbLocalizationDictionary;
