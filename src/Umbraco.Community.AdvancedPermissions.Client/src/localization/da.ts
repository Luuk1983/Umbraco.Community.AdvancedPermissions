import type { UmbLocalizationDictionary } from '@umbraco-cms/backoffice/localization-api';

export default {
  uap: {
    // ── Navigation ────────────────────────────────────────────────────────
    sectionLabel: 'Avancerede tilladelser',
    editorsSectionLabel: 'Redaktører',
    viewersSectionLabel: 'Visninger',
    permissionsEditor: 'Editor til indholdstilladelser',
    accessViewer: 'Adgangsvisning',

    // ── Almindeligt ───────────────────────────────────────────────────────
    roleLabel: 'Brugergruppe',
    rolePlaceholder: '— Vælg en brugergruppe —',
    userLabel: 'Bruger',
    saveChanges: 'Gem ændringer',
    discard: 'Kassér',
    cancel: 'Fortryd',
    apply: 'Anvend',
    close: 'Luk',
    inherit: 'Nedarv',
    allow: 'Tillad',
    deny: 'Nægt',
    umbracoUsers: 'Alle brugere',

    // ── Tilladelseseditor ─────────────────────────────────────────────────
    editorHeadline: 'Editor til indholdstilladelser',
    selectRolePrompt: 'Vælg en brugergruppe ovenfor for at administrere dens tilladelser.',
    permissionsSaved: 'Tilladelser gemt.',
    saveFailed: (error: string) => `Gem mislykkedes: ${error}`,
    contentNodeHeader: 'Indholdsnode',
    contentRoot: 'Standardtilladelser',
    expand: 'Fold ud',
    collapse: 'Fold sammen',

    // ── Tilladelsesdialog ─────────────────────────────────────────────────
    dialogHeadline: (verb: string, nodeName: string) => `Indstil ${verb}-tilladelse for ‘${nodeName}’`,
    descendantsSection: 'Undernoder (hvis afvigende)',
    dialogInstructions: 'Indstil tilladelsen for denne node. Som standard gælder dette også for alle undernoder. Brug “Undernoder (hvis afvigende)” til at indstille en anden tilladelse for undernoder.',
    virtualRootInherit: 'Ikke indstillet (fjern post)',
    virtualRootAllow: 'Tillad (alt indhold)',
    virtualRootDeny: 'Nægt (alt indhold)',
    dialogResult: 'Resultat',
    previewBothInherit: 'Ingen tilladelse indstillet. Nedarves fra overordnet node.',
    previewUniform: (action: string) => `${action} på denne node og alle undernoder.`,
    previewNodeOnly: (action: string) => `${action} kun på denne node. Undernoder påvirkes ikke af denne regel.`,
    previewDescOnly: (action: string) => `Ingen eksplicit tilladelse på denne node. ${action} på alle undernoder.`,
    previewSplit: (nodeAction: string, descAction: string) => `${nodeAction} på denne node. ${descAction} på alle undernoder.`,
    previewVirtualInherit: 'Ingen standardtilladelse indstillet.',
    previewVirtualSet: (action: string) => `${action} som standard for alt indhold.`,
    previewPriorityNode: 'Prioritetstilsidesættelse er angivet på denne node.',
    previewPriorityDesc: 'Prioritetstilsidesættelse er angivet på undernoder.',
    previewPriorityBoth: 'Prioritetstilsidesættelse er angivet.',

    // ── Prioritetstilsidesættelse ─────────────────────────────────────
    priorityOverride: 'Prioritetstilsidesættelse',
    priorityOverrideTooltip: (permission: string, nodeName: string) =>
      `En bruger kan tilhøre flere brugergrupper. Normalt følger den effektive tilladelse en fast prioritetsrækkefølge. Ved at markere dette felt tilsidesættes den rækkefølge, så den “${permission}”-indstilling, du vælger her, næsten altid bliver resultatet for “${nodeName}”, uanset brugerens øvrige grupper. Bruges med omtanke.`,
    priorityOverrideBadgeTitle: 'Prioritetstilsidesættelse er angivet på denne post',
    priorityOverrideWonTitle: 'Løst via prioritetstilsidesættelse',
    priorityOverrideSuppressedHeader: 'Prioritetstilsidesættelse ændrede resultatet',
    priorityOverrideSuppressedHint: 'Uden den ville resultatet have været:',

    // ── Adgangsvisning ────────────────────────────────────────────────────
    viewerHeadline: 'Adgangsvisning',
    byRole: 'Efter brugergruppe',
    byUser: 'Efter bruger',
    chooseRole: 'Vælg brugergruppe',
    chooseUser: 'Vælg bruger',
    selectSubjectPrompt: 'Vælg en brugergruppe eller bruger for at se de effektive tilladelser.',
    legendAllow: 'Tillad',
    legendDeny: 'Nægt',
    clickForReasoning: (label: string) => `${label} — klik for begrundelse`,
    subjectOr: 'eller',

    // ── Brugergruppevælger-dialog ─────────────────────────────────────────
    rolePickerHeadline: 'Vælg en brugergruppe',
    rolePickerFilter: 'Skriv for at filtrere…',
    rolePickerNoResults: 'Ingen brugergrupper matcher filteret.',
    rolePickerNameHeader: 'Brugergruppe',

    // ── Brugervælger-dialog ───────────────────────────────────────────────
    userPickerHeadline: 'Vælg en bruger',
    userPickerFilter: 'Skriv for at filtrere…',
    userPickerNoResults: 'Ingen brugere matcher filteret.',
    userPickerNameHeader: 'Bruger',

    // ── Begrundelsesdialog ────────────────────────────────────────────────
    reasoningHeadline: (verb: string, nodeName: string) => `${verb}-tilladelse for “${nodeName}”`,
    effectiveAllowed: (subject: string, verb: string, nodeName: string) =>
      `${subject} er blevet tilladt ${verb}-tilladelse for “${nodeName}”.`,
    effectiveDenied: (subject: string, verb: string, nodeName: string) =>
      `${subject} er blevet nægtet ${verb}-tilladelse for “${nodeName}”.`,
    dialogSecurityHeader: 'Sikkerhed',
    defaultPermissions: 'Standardtilladelser',
    determiningEntry: 'Denne post har forrang',
    noReasoningData: 'Ingen tilladelsesdata tilgængelige for dette verbum.',
    defaultAllowNote: 'Der er ikke indstillet nogen tilladelser, dette er tilladt som standard.',
    defaultDenyNote: 'Der er ikke indstillet nogen tilladelser, dette nægtes som standard.',

    // ── Omdirigeringsbesked for granulære tilladelser ─────────────────────
    redirectMessage:
      'Dokumenttilladelser for denne brugergruppe administreres af Advanced Permissions-pakken. Åbn Tilladelseseditoren i sektionen Brugere for at konfigurere tilladelser.',

    // ── Dokumenttype-tilladelser ──────────────────────────────────────────
    role: 'Brugergruppe',
    pickRole: 'Vælg brugergruppe',
    user: 'Bruger',
    pickUser: 'Vælg bruger',
    node: 'Node',
    pickNode: 'Vælg node',
    state: 'Status',
    scope: 'Omfang',
    scope_thisNodeOnly: 'Kun denne node',
    scope_thisNodeAndDescendants: 'Denne node og undernoder',
    scope_descendantsOnly: 'Kun undernoder',

    docTypePermissions_menuLabel: 'Editor til dokumenttype-tilladelser',
    docTypePermissions_insertOptionsMenuLabel: 'Visning af indsætningsmuligheder',
    docTypePermissions_workspaceTitle: 'Editor til dokumenttype-tilladelser',
    docTypePermissions_auditTitle: 'Visning af indsætningsmuligheder',
    docTypePermissions_allDocTypes: 'Alle dokumenttyper',
    docTypePermissions_verbInsert: 'Indsæt',
    docTypePermissions_documentType: 'Dokumenttype',
    chooseDocType: 'Vælg dokumenttype',
    notAnInsertOption: 'Denne dokumenttype er i øjeblikket ikke en indsætningsmulighed på denne node.',
    notAnInsertOptionAllowedNote: 'Denne dokumenttype er ikke en indsætningsmulighed på denne node, men ville ellers være tilladt.',
    notAnInsertOptionDeniedNote: 'Denne dokumenttype er ikke en indsætningsmulighed på denne node, men ville ellers blive nægtet.',
    docTypePermissions_pickDocType: '— Vælg en dokumenttype —',
    docTypePermissions_pickToStart: 'Vælg en brugergruppe og dokumenttype for at begynde.',
    docTypePermissions_defaultRowLabel: 'Standard (gælder overalt)',
    docTypePermissions_pendingNodeLabel: '(ikke-gemt node)',
    docTypePermissions_addScopeNode: 'Tilføj omfangstilsidesættelse',
    docTypePermissions_notSet: 'Ikke indstillet',
    docTypePermissions_noResults: 'Ingen dokumenttyper fundet.',
    docTypePermissions_useRoot: 'Brug rod',
    docTypePermissions_pickedNode: 'Node:',
    docTypePermissions_rootLevel: 'Rodniveau',
    docTypePermissions_reasoning: 'Begrundelse',
    docTypePermissions_defaultAllow: 'Tilladt som standard',
    docTypePermissions_viaDefault: 'fra standardrække',
  },
} satisfies UmbLocalizationDictionary;
