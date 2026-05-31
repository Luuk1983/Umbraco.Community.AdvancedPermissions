import type { UmbLocalizationDictionary } from '@umbraco-cms/backoffice/localization-api';

export default {
  uap: {
    // ── Navigacija ────────────────────────────────────────────────────────
    sectionLabel: 'Napredne dozvole',
    editorsSectionLabel: 'Urednici',
    viewersSectionLabel: 'Pregledi',
    permissionsEditor: 'Uređivač dozvola za sadržaj',
    accessViewer: 'Pregled pristupa',

    // ── Općenito ──────────────────────────────────────────────────────────
    roleLabel: 'Grupa korisnika',
    rolePlaceholder: '— Odaberite grupu korisnika —',
    userLabel: 'Korisnik',
    saveChanges: 'Spremi promjene',
    discard: 'Odbaci',
    cancel: 'Otkaži',
    apply: 'Primijeni',
    close: 'Zatvori',
    inherit: 'Naslijedi',
    allow: 'Dozvoli',
    deny: 'Zabrani',
    umbracoUsers: 'Svi korisnici',

    // ── Uređivač dozvola ─────────────────────────────────────────────────
    editorHeadline: 'Uređivač dozvola za sadržaj',
    selectRolePrompt: 'Odaberite grupu korisnika iznad za upravljanje njenim dozvolama.',
    permissionsSaved: 'Dozvole spremljene.',
    saveFailed: (error: string) => `Spremanje nije uspjelo: ${error}`,
    contentNodeHeader: 'Čvor sadržaja',
    contentRoot: 'Zadane dozvole',
    expand: 'Proširi',
    collapse: 'Skupi',

    // ── Dijalog dozvola ───────────────────────────────────────────────────
    dialogHeadline: (verb: string, nodeName: string) => `Postavi dozvolu ${verb} za ‘${nodeName}’`,
    descendantsSection: 'Potomci (ako se razlikuju)',
    dialogInstructions: 'Postavite dozvolu za ovaj čvor. Po zadanim postavkama ovo se primjenjuje i na sve potomke. Koristite “Potomci (ako se razlikuju)” da postavite drugačiju dozvolu za podređene čvorove.',
    virtualRootInherit: 'Nije postavljeno (ukloni stavku)',
    virtualRootAllow: 'Dozvoli (sav sadržaj)',
    virtualRootDeny: 'Zabrani (sav sadržaj)',
    dialogResult: 'Rezultat',
    previewBothInherit: 'Nijedna dozvola nije postavljena. Nasljeđuje od nadređenog čvora.',
    previewUniform: (action: string) => `${action} na ovom čvoru i svim potomcima.`,
    previewNodeOnly: (action: string) => `${action} samo na ovom čvoru. Potomci nisu pogođeni ovim pravilom.`,
    previewDescOnly: (action: string) => `Nema eksplicitne dozvole na ovom čvoru. ${action} na svim potomcima.`,
    previewSplit: (nodeAction: string, descAction: string) => `${nodeAction} na ovom čvoru. ${descAction} na svim potomcima.`,
    previewVirtualInherit: 'Nije postavljena zadana dozvola.',
    previewVirtualSet: (action: string) => `${action} po zadanim postavkama za sav sadržaj.`,
    previewPriorityNode: 'Prioritetno nadjačavanje je postavljeno na ovom čvoru.',
    previewPriorityDesc: 'Prioritetno nadjačavanje je postavljeno na potomcima.',
    previewPriorityBoth: 'Prioritetno nadjačavanje je postavljeno.',

    // ── Prioritetno nadjačavanje ──────────────────────────────────────
    priorityOverride: 'Prioritetno nadjačavanje',
    priorityOverrideTooltip: (permission: string, nodeName: string) =>
      `Korisnik može pripadati većem broju grupa korisnika. Uobičajeno, efektivna dozvola slijedi fiksni redoslijed prioriteta. Označavanjem ovog polja taj se redoslijed nadjačava, tako da ovdje odabrana postavka “${permission}” gotovo uvijek postaje rezultat za “${nodeName}”, bez obzira na ostale grupe korisnika. Koristite štedljivo.`,
    priorityOverrideBadgeTitle: 'Prioritetno nadjačavanje je postavljeno na ovoj stavci',
    priorityOverrideWonTitle: 'Razriješeno putem prioritetnog nadjačavanja',
    priorityOverrideSuppressedHeader: 'Prioritetno nadjačavanje je promijenilo rezultat',
    priorityOverrideSuppressedHint: 'Bez njega bi rezultat bio:',

    // ── Pregled pristupa ──────────────────────────────────────────────────
    viewerHeadline: 'Pregled pristupa',
    byRole: 'Po grupi korisnika',
    byUser: 'Po korisniku',
    chooseRole: 'Odaberite grupu korisnika',
    chooseUser: 'Odaberite korisnika',
    selectSubjectPrompt: 'Odaberite grupu korisnika ili korisnika za pregled efektivnih dozvola.',
    legendAllow: 'Dozvoli',
    legendDeny: 'Zabrani',
    clickForReasoning: (label: string) => `${label} — kliknite za obrazloženje`,
    subjectOr: 'ili',

    // ── Dijalog za odabir grupe korisnika ─────────────────────────────────
    rolePickerHeadline: 'Odaberite grupu korisnika',
    rolePickerFilter: 'Unesite za filtriranje…',
    rolePickerNoResults: 'Nijedna grupa korisnika ne odgovara filteru.',
    rolePickerNameHeader: 'Grupa korisnika',

    // ── Dijalog za odabir korisnika ───────────────────────────────────────
    userPickerHeadline: 'Odaberite korisnika',
    userPickerFilter: 'Unesite za filtriranje…',
    userPickerNoResults: 'Nijedan korisnik ne odgovara filteru.',
    userPickerNameHeader: 'Korisnik',

    // ── Dijalog obrazloženja ──────────────────────────────────────────────
    reasoningHeadline: (verb: string, nodeName: string) => `Dozvola ${verb} za “${nodeName}”`,
    effectiveAllowed: (subject: string, verb: string, nodeName: string) =>
      `${subject} ima dozvolu ${verb} za “${nodeName}”.`,
    effectiveDenied: (subject: string, verb: string, nodeName: string) =>
      `${subject} nema dozvolu ${verb} za “${nodeName}”.`,
    dialogSecurityHeader: 'Sigurnost',
    defaultPermissions: 'Zadane dozvole',
    determiningEntry: 'Ova stavka ima prednost',
    noReasoningData: 'Nema dostupnih podataka o dozvoli za ovaj glagol.',
    defaultAllowNote: 'Nisu postavljene dozvole, ovo je dozvoljeno po zadanim postavkama.',
    defaultDenyNote: 'Nisu postavljene dozvole, ovo je zabranjeno po zadanim postavkama.',

    // ── Poruka preusmjeravanja granularnih dozvola ────────────────────────
    redirectMessage:
      'Dozvolama za dokumente za ovu grupu korisnika upravlja paket Advanced Permissions. Otvorite Uređivač dozvola u sekciji Korisnici za konfiguraciju dozvola.',

    // ── Dozvole za tipove dokumenata ──────────────────────────────────────
    role: 'Grupa korisnika',
    pickRole: 'Odaberite grupu korisnika',
    user: 'Korisnik',
    pickUser: 'Odaberite korisnika',
    node: 'Čvor',
    pickNode: 'Odaberite čvor',
    state: 'Status',
    scope: 'Opseg',
    scope_thisNodeOnly: 'Samo ovaj čvor',
    scope_thisNodeAndDescendants: 'Ovaj čvor i potomci',
    scope_descendantsOnly: 'Samo potomci',

    docTypePermissions_menuLabel: 'Uređivač dozvola za tip dokumenta',
    docTypePermissions_insertOptionsMenuLabel: 'Pregled opcija umetanja',
    docTypePermissions_workspaceTitle: 'Uređivač dozvola za tip dokumenta',
    docTypePermissions_auditTitle: 'Pregled opcija umetanja',
    docTypePermissions_allDocTypes: 'Svi tipovi dokumenata',
    docTypePermissions_verbInsert: 'Umetni',
    docTypePermissions_documentType: 'Tip dokumenta',
    chooseDocType: 'Odaberite tip dokumenta',
    notAnInsertOption: 'Ovaj tip dokumenta trenutno nije opcija umetanja na ovom čvoru.',
    notAnInsertOptionAllowedNote: 'Ovaj tip dokumenta nije opcija umetanja na ovom čvoru, ali bi inače bio dozvoljen.',
    notAnInsertOptionDeniedNote: 'Ovaj tip dokumenta nije opcija umetanja na ovom čvoru, ali bi inače bio zabranjen.',
    docTypePermissions_pickDocType: '— Odaberite tip dokumenta —',
    docTypePermissions_pickToStart: 'Odaberite grupu korisnika i tip dokumenta za početak.',
    docTypePermissions_defaultRowLabel: 'Zadano (primjenjuje se svuda)',
    docTypePermissions_pendingNodeLabel: '(nespremljeni čvor)',
    docTypePermissions_addScopeNode: 'Dodaj nadjačavanje opsega',
    docTypePermissions_notSet: 'Nije postavljeno',
    docTypePermissions_noResults: 'Nisu pronađeni tipovi dokumenata.',
    docTypePermissions_useRoot: 'Koristi korijen',
    docTypePermissions_pickedNode: 'Čvor:',
    docTypePermissions_rootLevel: 'Korijenski nivo',
    docTypePermissions_reasoning: 'Obrazloženje',
    docTypePermissions_defaultAllow: 'Dozvoljeno po zadanim postavkama',
    docTypePermissions_viaDefault: 'iz zadanog reda',
  },
} satisfies UmbLocalizationDictionary;
