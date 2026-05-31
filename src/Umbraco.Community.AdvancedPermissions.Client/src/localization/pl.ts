import type { UmbLocalizationDictionary } from '@umbraco-cms/backoffice/localization-api';

export default {
  uap: {
    // ── Nawigacja ─────────────────────────────────────────────────────────
    sectionLabel: 'Zaawansowane uprawnienia',
    editorsSectionLabel: 'Edytorzy',
    viewersSectionLabel: 'Podglądy',
    permissionsEditor: 'Edytor uprawnień do treści',
    accessViewer: 'Podgląd dostępu',

    // ── Wspólne ───────────────────────────────────────────────────────────
    roleLabel: 'Grupa użytkowników',
    rolePlaceholder: '— Wybierz grupę użytkowników —',
    userLabel: 'Użytkownik',
    saveChanges: 'Zapisz zmiany',
    discard: 'Odrzuć',
    cancel: 'Anuluj',
    apply: 'Zastosuj',
    close: 'Zamknij',
    inherit: 'Dziedzicz',
    allow: 'Zezwól',
    deny: 'Odmów',
    umbracoUsers: 'Wszyscy użytkownicy',

    // ── Edytor uprawnień ─────────────────────────────────────────────────
    editorHeadline: 'Edytor uprawnień do treści',
    selectRolePrompt: 'Wybierz powyżej grupę użytkowników, aby zarządzać jej uprawnieniami.',
    permissionsSaved: 'Uprawnienia zapisane.',
    saveFailed: (error: string) => `Zapisywanie nie powiodło się: ${error}`,
    contentNodeHeader: 'Węzeł treści',
    contentRoot: 'Uprawnienia domyślne',
    expand: 'Rozwiń',
    collapse: 'Zwiń',

    // ── Okno dialogowe uprawnień ──────────────────────────────────────────
    dialogHeadline: (verb: string, nodeName: string) => `Ustaw uprawnienie „${verb}” dla ‘${nodeName}’`,
    descendantsSection: 'Elementy podrzędne (jeśli inne)',
    dialogInstructions: 'Ustaw uprawnienie dla tego węzła. Domyślnie dotyczy ono również wszystkich elementów podrzędnych. Użyj opcji „Elementy podrzędne (jeśli inne)”, aby ustawić inne uprawnienie dla węzłów podrzędnych.',
    virtualRootInherit: 'Nie ustawiono (usuń wpis)',
    virtualRootAllow: 'Zezwól (cała treść)',
    virtualRootDeny: 'Odmów (cała treść)',
    dialogResult: 'Wynik',
    previewBothInherit: 'Nie ustawiono uprawnienia. Dziedziczy z elementu nadrzędnego.',
    previewUniform: (action: string) => `${action} dla tego węzła i wszystkich elementów podrzędnych.`,
    previewNodeOnly: (action: string) => `${action} tylko dla tego węzła. Elementy podrzędne nie są objęte tą regułą.`,
    previewDescOnly: (action: string) => `Brak jawnego uprawnienia dla tego węzła. ${action} dla wszystkich elementów podrzędnych.`,
    previewSplit: (nodeAction: string, descAction: string) => `${nodeAction} dla tego węzła. ${descAction} dla wszystkich elementów podrzędnych.`,
    previewVirtualInherit: 'Nie ustawiono uprawnienia domyślnego.',
    previewVirtualSet: (action: string) => `${action} domyślnie dla całej treści.`,
    previewPriorityNode: 'Nadpisanie priorytetu jest ustawione dla tego węzła.',
    previewPriorityDesc: 'Nadpisanie priorytetu jest ustawione dla elementów podrzędnych.',
    previewPriorityBoth: 'Nadpisanie priorytetu jest ustawione.',

    // ── Nadpisanie priorytetu ─────────────────────────────────────────
    priorityOverride: 'Nadpisanie priorytetu',
    priorityOverrideTooltip: (permission: string, nodeName: string) =>
      `Użytkownik może należeć do kilku grup użytkowników. Zwykle efektywne uprawnienie wynika ze stałej kolejności priorytetów. Zaznaczenie tego pola nadpisuje tę kolejność, dzięki czemu wybrane tutaj ustawienie „${permission}” niemal zawsze staje się wynikiem dla „${nodeName}”, niezależnie od pozostałych grup użytkownika. Używaj oszczędnie.`,
    priorityOverrideBadgeTitle: 'Nadpisanie priorytetu jest ustawione dla tego wpisu',
    priorityOverrideWonTitle: 'Rozstrzygnięto przez nadpisanie priorytetu',
    priorityOverrideSuppressedHeader: 'Nadpisanie priorytetu zmieniło wynik',
    priorityOverrideSuppressedHint: 'Bez niego wynik byłby następujący:',

    // ── Podgląd dostępu ──────────────────────────────────────────────────
    viewerHeadline: 'Podgląd dostępu',
    byRole: 'Według grupy użytkowników',
    byUser: 'Według użytkownika',
    chooseRole: 'Wybierz grupę użytkowników',
    chooseUser: 'Wybierz użytkownika',
    selectSubjectPrompt: 'Wybierz grupę użytkowników lub użytkownika, aby zobaczyć efektywne uprawnienia.',
    legendAllow: 'Zezwól',
    legendDeny: 'Odmów',
    clickForReasoning: (label: string) => `${label} — kliknij, aby zobaczyć uzasadnienie`,
    subjectOr: 'lub',

    // ── Okno wyboru grupy użytkowników ──────────────────────────────────
    rolePickerHeadline: 'Wybierz grupę użytkowników',
    rolePickerFilter: 'Wpisz, aby filtrować…',
    rolePickerNoResults: 'Brak grup użytkowników pasujących do filtra.',
    rolePickerNameHeader: 'Grupa użytkowników',

    // ── Okno wyboru użytkownika ──────────────────────────────────────────
    userPickerHeadline: 'Wybierz użytkownika',
    userPickerFilter: 'Wpisz, aby filtrować…',
    userPickerNoResults: 'Brak użytkowników pasujących do filtra.',
    userPickerNameHeader: 'Użytkownik',

    // ── Okno uzasadnienia ─────────────────────────────────────────────────
    reasoningHeadline: (verb: string, nodeName: string) => `Uprawnienie „${verb}” dla „${nodeName}”`,
    effectiveAllowed: (subject: string, verb: string, nodeName: string) =>
      `${subject} otrzymał(a) uprawnienie „${verb}” dla „${nodeName}”.`,
    effectiveDenied: (subject: string, verb: string, nodeName: string) =>
      `${subject} odmówiono uprawnienia „${verb}” dla „${nodeName}”.`,
    dialogSecurityHeader: 'Bezpieczeństwo',
    defaultPermissions: 'Uprawnienia domyślne',
    determiningEntry: 'Ten wpis ma pierwszeństwo',
    noReasoningData: 'Brak danych o uprawnieniach dla tego uprawnienia.',
    defaultAllowNote: 'Nie ustawiono żadnych uprawnień, jest to domyślnie dozwolone.',
    defaultDenyNote: 'Nie ustawiono żadnych uprawnień, jest to domyślnie odmawiane.',

    // ── Komunikat przekierowania uprawnień szczegółowych ─────────────────
    redirectMessage:
      'Uprawnienia do dokumentów dla tej grupy użytkowników są zarządzane przez pakiet Advanced Permissions. Otwórz Edytor uprawnień w sekcji Użytkownicy, aby skonfigurować uprawnienia.',

    // ── Uprawnienia typów dokumentów ──────────────────────────────────────
    role: 'Grupa użytkowników',
    pickRole: 'Wybierz grupę użytkowników',
    user: 'Użytkownik',
    pickUser: 'Wybierz użytkownika',
    node: 'Węzeł',
    pickNode: 'Wybierz węzeł',
    state: 'Status',
    scope: 'Zakres',
    scope_thisNodeOnly: 'Tylko ten węzeł',
    scope_thisNodeAndDescendants: 'Ten węzeł i elementy podrzędne',
    scope_descendantsOnly: 'Tylko elementy podrzędne',

    docTypePermissions_menuLabel: 'Edytor uprawnień typów dokumentów',
    docTypePermissions_insertOptionsMenuLabel: 'Podgląd opcji wstawiania',
    docTypePermissions_workspaceTitle: 'Edytor uprawnień typów dokumentów',
    docTypePermissions_auditTitle: 'Podgląd opcji wstawiania',
    docTypePermissions_allDocTypes: 'Wszystkie typy dokumentów',
    docTypePermissions_verbInsert: 'Wstaw',
    docTypePermissions_documentType: 'Typ dokumentu',
    chooseDocType: 'Wybierz typ dokumentu',
    notAnInsertOption: 'Ten typ dokumentu nie jest obecnie opcją wstawiania dla tego węzła.',
    notAnInsertOptionAllowedNote: 'Ten typ dokumentu nie jest opcją wstawiania dla tego węzła, ale w innym przypadku byłby dozwolony.',
    notAnInsertOptionDeniedNote: 'Ten typ dokumentu nie jest opcją wstawiania dla tego węzła, ale w innym przypadku byłby odmawiany.',
    docTypePermissions_pickDocType: '— Wybierz typ dokumentu —',
    docTypePermissions_pickToStart: 'Wybierz grupę użytkowników i typ dokumentu, aby rozpocząć.',
    docTypePermissions_defaultRowLabel: 'Domyślne (dotyczy wszędzie)',
    docTypePermissions_pendingNodeLabel: '(niezapisany węzeł)',
    docTypePermissions_addScopeNode: 'Dodaj nadpisanie zakresu',
    docTypePermissions_notSet: 'Nie ustawiono',
    docTypePermissions_noResults: 'Nie znaleziono typów dokumentów.',
    docTypePermissions_useRoot: 'Użyj korzenia',
    docTypePermissions_pickedNode: 'Węzeł:',
    docTypePermissions_rootLevel: 'Poziom korzenia',
    docTypePermissions_reasoning: 'Uzasadnienie',
    docTypePermissions_defaultAllow: 'Domyślnie dozwolone',
    docTypePermissions_viaDefault: 'z wiersza domyślnego',
  },
} satisfies UmbLocalizationDictionary;
