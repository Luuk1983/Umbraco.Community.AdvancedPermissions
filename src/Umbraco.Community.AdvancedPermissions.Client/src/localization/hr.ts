import type { UmbLocalizationDictionary } from '@umbraco-cms/backoffice/localization-api';

export default {
  uap: {
    // ── Navigacija ────────────────────────────────────────────────────────
    sectionLabel: 'Napredne dozvole',
    editorsSectionLabel: 'Uređivači',
    viewersSectionLabel: 'Prikazi',
    permissionsEditor: 'Uređivač dozvola sadržaja',
    accessViewer: 'Prikaz pristupa',

    // ── Općenito ──────────────────────────────────────────────────────────
    roleLabel: 'Grupa korisnika',
    rolePlaceholder: '— Odaberite grupu korisnika —',
    userLabel: 'Korisnik',
    saveChanges: 'Spremi promjene',
    discard: 'Otkaži',
    cancel: 'Odustani',
    apply: 'Primijeni',
    close: 'Zatvori',
    inherit: 'Naslijedi',
    allow: 'Dozvoli',
    deny: 'Zabrani',
    umbracoUsers: 'Svi korisnici',

    // ── Uređivač dozvola ─────────────────────────────────────────────────
    editorHeadline: 'Uređivač dozvola sadržaja',
    selectRolePrompt: 'Odaberite grupu korisnika iznad kako biste upravljali njezinim dozvolama.',
    permissionsSaved: 'Dozvole su spremljene.',
    saveFailed: (error: string) => `Spremanje nije uspjelo: ${error}`,
    contentNodeHeader: 'Čvor sadržaja',
    contentRoot: 'Zadane dozvole',
    expand: 'Proširi',
    collapse: 'Sažmi',

    // ── Dijalog dozvola ───────────────────────────────────────────────────
    dialogHeadline: (verb: string, nodeName: string) => `Postavi dozvolu „${verb}” za ‘${nodeName}’`,
    descendantsSection: 'Podređeni čvorovi (ako se razlikuju)',
    dialogInstructions: 'Postavite dozvolu za ovaj čvor. Prema zadanim postavkama, ovo vrijedi i za sve podređene čvorove. Upotrijebite „Podređeni čvorovi (ako se razlikuju)” kako biste postavili drukčiju dozvolu za podređene čvorove.',
    virtualRootInherit: 'Nije postavljeno (ukloni unos)',
    virtualRootAllow: 'Dozvoli (sav sadržaj)',
    virtualRootDeny: 'Zabrani (sav sadržaj)',
    dialogResult: 'Rezultat',
    previewBothInherit: 'Dozvola nije postavljena. Nasljeđuje se od nadređenog čvora.',
    previewUniform: (action: string) => `${action} za ovaj čvor i sve podređene čvorove.`,
    previewNodeOnly: (action: string) => `${action} samo za ovaj čvor. Podređeni čvorovi nisu obuhvaćeni ovim pravilom.`,
    previewDescOnly: (action: string) => `Nema izričite dozvole za ovaj čvor. ${action} za sve podređene čvorove.`,
    previewSplit: (nodeAction: string, descAction: string) => `${nodeAction} za ovaj čvor. ${descAction} za sve podređene čvorove.`,
    previewVirtualInherit: 'Zadana dozvola nije postavljena.',
    previewVirtualSet: (action: string) => `${action} prema zadanim postavkama za sav sadržaj.`,
    previewPriorityNode: 'Prioritetno nadjačavanje postavljeno je za ovaj čvor.',
    previewPriorityDesc: 'Prioritetno nadjačavanje postavljeno je za podređene čvorove.',
    previewPriorityBoth: 'Prioritetno nadjačavanje je postavljeno.',

    // ── Prioritetno nadjačavanje ──────────────────────────────────────
    priorityOverride: 'Prioritetno nadjačavanje',
    priorityOverrideTooltip: (permission: string, nodeName: string) =>
      `Korisnik može pripadati nekolicini grupa korisnika. Uobičajeno, efektivna dozvola slijedi utvrđeni redoslijed prioriteta. Označavanjem ovog okvira taj se redoslijed nadjačava, tako da ovdje odabrana postavka „${permission}” gotovo uvijek postaje rezultat za „${nodeName}”, neovisno o ostalim grupama korisnika. Koristite umjereno.`,
    priorityOverrideBadgeTitle: 'Prioritetno nadjačavanje postavljeno je za ovaj unos',
    priorityOverrideWonTitle: 'Razriješeno putem prioritetnog nadjačavanja',
    priorityOverrideSuppressedHeader: 'Prioritetno nadjačavanje promijenilo je rezultat',
    priorityOverrideSuppressedHint: 'Bez njega bi rezultat bio:',

    // ── Prikaz pristupa ───────────────────────────────────────────────────
    viewerHeadline: 'Prikaz pristupa',
    byRole: 'Po grupi korisnika',
    byUser: 'Po korisniku',
    chooseRole: 'Odaberi grupu korisnika',
    chooseUser: 'Odaberi korisnika',
    selectSubjectPrompt: 'Odaberite grupu korisnika ili korisnika za pregled efektivnih dozvola.',
    legendAllow: 'Dozvoli',
    legendDeny: 'Zabrani',
    clickForReasoning: (label: string) => `${label} — kliknite za obrazloženje`,
    subjectOr: 'ili',

    // ── Dijalog za odabir grupe korisnika ──────────────────────────────────
    rolePickerHeadline: 'Odaberite grupu korisnika',
    rolePickerFilter: 'Upišite za filtriranje…',
    rolePickerNoResults: 'Nijedna grupa korisnika ne odgovara filtru.',
    rolePickerNameHeader: 'Grupa korisnika',

    // ── Dijalog za odabir korisnika ─────────────────────────────────────────
    userPickerHeadline: 'Odaberite korisnika',
    userPickerFilter: 'Upišite za filtriranje…',
    userPickerNoResults: 'Nijedan korisnik ne odgovara filtru.',
    userPickerNameHeader: 'Korisnik',

    // ── Dijalog obrazloženja ──────────────────────────────────────────────
    reasoningHeadline: (verb: string, nodeName: string) => `Dozvola „${verb}” za “${nodeName}”`,
    effectiveAllowed: (subject: string, verb: string, nodeName: string) =>
      `${subject} ima dozvoljenu dozvolu „${verb}” za “${nodeName}”.`,
    effectiveDenied: (subject: string, verb: string, nodeName: string) =>
      `${subject} ima zabranjenu dozvolu „${verb}” za “${nodeName}”.`,
    dialogSecurityHeader: 'Sigurnost',
    defaultPermissions: 'Zadane dozvole',
    determiningEntry: 'Ovaj unos ima prednost',
    noReasoningData: 'Nema dostupnih podataka o dozvolama za ovu radnju.',
    defaultAllowNote: 'Nisu postavljene dozvole, ovo je dozvoljeno prema zadanim postavkama.',
    defaultDenyNote: 'Nisu postavljene dozvole, ovo je zabranjeno prema zadanim postavkama.',

    // ── Poruka preusmjeravanja granuliranih dozvola ───────────────────────
    redirectMessage:
      'Dozvolama dokumenata za ovu grupu korisnika upravlja paket Advanced Permissions. Otvorite Uređivač dozvola u sekciji Korisnici kako biste konfigurirali dozvole.',

    // ── Dozvole vrste dokumenta ───────────────────────────────────────────
    role: 'Grupa korisnika',
    pickRole: 'Odaberi grupu korisnika',
    user: 'Korisnik',
    pickUser: 'Odaberi korisnika',
    node: 'Čvor',
    pickNode: 'Odaberi čvor',
    state: 'Status',
    scope: 'Opseg',
    scope_thisNodeOnly: 'Samo ovaj čvor',
    scope_thisNodeAndDescendants: 'Ovaj čvor i podređeni čvorovi',
    scope_descendantsOnly: 'Samo podređeni čvorovi',

    docTypePermissions_menuLabel: 'Uređivač dozvola vrste dokumenta',
    docTypePermissions_insertOptionsMenuLabel: 'Prikaz opcija umetanja',
    docTypePermissions_workspaceTitle: 'Uređivač dozvola vrste dokumenta',
    docTypePermissions_auditTitle: 'Prikaz opcija umetanja',
    docTypePermissions_allDocTypes: 'Sve vrste dokumenata',
    docTypePermissions_verbInsert: 'Umetni',
    docTypePermissions_documentType: 'Vrsta dokumenta',
    chooseDocType: 'Odaberi vrstu dokumenta',
    notAnInsertOption: 'Ova vrsta dokumenta trenutno nije opcija umetanja za ovaj čvor.',
    notAnInsertOptionAllowedNote: 'Ova vrsta dokumenta nije opcija umetanja za ovaj čvor, ali bi inače bila dozvoljena.',
    notAnInsertOptionDeniedNote: 'Ova vrsta dokumenta nije opcija umetanja za ovaj čvor, ali bi inače bila zabranjena.',
    docTypePermissions_pickDocType: '— Odaberite vrstu dokumenta —',
    docTypePermissions_pickToStart: 'Odaberite grupu korisnika i vrstu dokumenta za početak.',
    docTypePermissions_defaultRowLabel: 'Zadano (vrijedi svuda)',
    docTypePermissions_pendingNodeLabel: '(nespremljeni čvor)',
    docTypePermissions_addScopeNode: 'Dodaj nadjačavanje opsega',
    docTypePermissions_notSet: 'Nije postavljeno',
    docTypePermissions_noResults: 'Nije pronađena nijedna vrsta dokumenta.',
    docTypePermissions_useRoot: 'Koristi korijen',
    docTypePermissions_pickedNode: 'Čvor:',
    docTypePermissions_rootLevel: 'Korijenska razina',
    docTypePermissions_reasoning: 'Obrazloženje',
    docTypePermissions_defaultAllow: 'Dozvoljeno prema zadanim postavkama',
    docTypePermissions_viaDefault: 'iz zadanog retka',
  },
} satisfies UmbLocalizationDictionary;
