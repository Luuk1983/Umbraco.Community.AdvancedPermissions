import type { UmbLocalizationDictionary } from '@umbraco-cms/backoffice/localization-api';

export default {
  uap: {
    // ── Navigace ──────────────────────────────────────────────────────────
    sectionLabel: 'Pokročilá oprávnění',
    editorsSectionLabel: 'Editory',
    viewersSectionLabel: 'Zobrazení',
    permissionsEditor: 'Editor oprávnění obsahu',
    accessViewer: 'Zobrazení přístupu',

    // ── Obecné ──────────────────────────────────────────────────────────
    roleLabel: 'Uživatelská skupina',
    rolePlaceholder: '— Vyberte uživatelskou skupinu —',
    userLabel: 'Uživatel',
    saveChanges: 'Uložit změny',
    discard: 'Zahodit',
    cancel: 'Zrušit',
    apply: 'Použít',
    close: 'Zavřít',
    inherit: 'Dědit',
    allow: 'Povolit',
    deny: 'Zakázat',
    umbracoUsers: 'Všichni uživatelé',

    // ── Help ──────────────────────────────────────────────────────────────
    help_contentPermissions_description: 'Spravujte oprávnění Povolit/Zakázat pro jednotlivé uživatelské skupiny napříč stromem obsahu.',
    help_accessViewer_description: 'Zobrazte výsledná oprávnění, která má uživatel nebo skupina na libovolném uzlu, včetně úplného zdůvodnění.',
    help_docTypePermissions_description: 'Určete, které typy dokumentů může každá uživatelská skupina vytvářet a kde.',
    help_insertOptions_description: 'Zkontrolujte, které typy dokumentů může uživatel nebo uživatelská skupina vytvořit na každém uzlu.',
    help_learnMore: 'Zjistit více',
    help_modalTitle: 'Nápověda',
    help_tabAbout: 'O této stránce',
    help_tabConcepts: 'Pojmy',
    help_concept_scope_tip: 'Rozsah určuje, jak daleko pravidlo dosahuje: tento uzel, tento uzel a jeho potomci, nebo pouze potomci.',
    help_concept_priorityOverride_tip: 'Přepsání priority vynutí, aby tento záznam zvítězil nad běžným pořadím vyhodnocení.',
    help_concept_allowDeny_tip: 'Povolit oprávnění udělí, Zakázat jej odebere, a ponechání nenastaveného se dědí z nejbližšího nadřazeného uzlu.',
    help_concept_reasoning_tip: 'Klikněte na libovolnou buňku a uvidíte přesně, jak bylo oprávnění vyhodnoceno.',

    // ── Editor oprávnění ──────────────────────────────────────────────
    editorHeadline: 'Editor oprávnění obsahu',
    selectRolePrompt: 'Vyberte výše uživatelskou skupinu pro správu jejích oprávnění.',
    permissionsSaved: 'Oprávnění uložena.',
    saveFailed: (error: string) => `Uložení se nezdařilo: ${error}`,
    contentNodeHeader: 'Uzel obsahu',
    contentRoot: 'Výchozí oprávnění',
    expand: 'Rozbalit',
    collapse: 'Sbalit',

    // ── Dialog oprávnění ───────────────────────────────────────────────
    dialogHeadline: (verb: string, nodeName: string) => `Nastavit oprávnění „${verb}“ pro ‘${nodeName}’`,
    descendantsSection: 'Potomci (pokud se liší)',
    dialogInstructions: 'Nastavte oprávnění pro tento uzel. Ve výchozím nastavení platí i pro všechny potomky. Pomocí „Potomci (pokud se liší)“ nastavíte odlišné oprávnění pro potomky.',
    virtualRootInherit: 'Nenastaveno (odebrat záznam)',
    virtualRootAllow: 'Povolit (veškerý obsah)',
    virtualRootDeny: 'Zakázat (veškerý obsah)',
    dialogResult: 'Výsledek',
    previewBothInherit: 'Není nastaveno žádné oprávnění. Dědí se z nadřazeného uzlu.',
    previewUniform: (action: string) => `${action} na tomto uzlu a všech potomcích.`,
    previewNodeOnly: (action: string) => `${action} pouze na tomto uzlu. Toto pravidlo se netýká potomků.`,
    previewDescOnly: (action: string) => `Žádné explicitní oprávnění na tomto uzlu. ${action} na všech potomcích.`,
    previewSplit: (nodeAction: string, descAction: string) => `${nodeAction} na tomto uzlu. ${descAction} na všech potomcích.`,
    previewVirtualInherit: 'Není nastaveno žádné výchozí oprávnění.',
    previewVirtualSet: (action: string) => `${action} ve výchozím nastavení pro veškerý obsah.`,
    previewPriorityNode: 'Na tomto uzlu je nastaveno přepsání priority.',
    previewPriorityDesc: 'Na potomcích je nastaveno přepsání priority.',
    previewPriorityBoth: 'Přepsání priority je nastaveno.',

    // ── Přepsání priority ───────────────────────────────────────
    priorityOverride: 'Přepsání priority',
    priorityOverrideTooltip: (permission: string, nodeName: string) =>
      `Uživatel může patřit do několika uživatelských skupin. Běžně se výsledné oprávnění řídí pevným pořadím priorit. Zaškrtnutím tohoto políčka se toto pořadí přepíše, takže zde zvolené nastavení „${permission}“ se téměř vždy stane výsledkem pro „${nodeName}“ bez ohledu na ostatní skupiny uživatele. Používejte střídmě.`,
    priorityOverrideBadgeTitle: 'Přepsání priority je nastaveno pro tento záznam',
    priorityOverrideWonTitle: 'Vyřešeno pomocí přepsání priority',
    priorityOverrideSuppressedHeader: 'Přepsání priority změnilo výsledek',
    priorityOverrideSuppressedHint: 'Bez něj by výsledek byl:',

    // ── Zobrazení přístupu ──────────────────────────────────────────────
    viewerHeadline: 'Zobrazení přístupu',
    byRole: 'Podle uživatelské skupiny',
    byUser: 'Podle uživatele',
    chooseRole: 'Vyberte uživatelskou skupinu',
    chooseUser: 'Vyberte uživatele',
    selectSubjectPrompt: 'Vyberte uživatelskou skupinu nebo uživatele pro zobrazení výsledných oprávnění.',
    legendAllow: 'Povolit',
    legendDeny: 'Zakázat',
    clickForReasoning: (label: string) => `${label} — klikněte pro zdůvodnění`,
    subjectOr: 'nebo',

    // ── Modal výběru uživatelské skupiny ──────────────────────────────
    rolePickerHeadline: 'Vyberte uživatelskou skupinu',
    rolePickerFilter: 'Filtrovat psaním…',
    rolePickerNoResults: 'Žádné uživatelské skupiny neodpovídají filtru.',
    rolePickerNameHeader: 'Uživatelská skupina',

    // ── Modal výběru uživatele ───────────────────────────────────────
    userPickerHeadline: 'Vyberte uživatele',
    userPickerFilter: 'Filtrovat psaním…',
    userPickerNoResults: 'Žádní uživatelé neodpovídají filtru.',
    userPickerNameHeader: 'Uživatel',

    // ── Dialog zdůvodnění ──────────────────────────────────────────────
    reasoningHeadline: (verb: string, nodeName: string) => `Oprávnění „${verb}“ pro “${nodeName}”`,
    effectiveAllowed: (subject: string, verb: string, nodeName: string) =>
      `${subject} má povoleno oprávnění „${verb}“ pro “${nodeName}”.`,
    effectiveDenied: (subject: string, verb: string, nodeName: string) =>
      `${subject} má zakázáno oprávnění „${verb}“ pro “${nodeName}”.`,
    dialogSecurityHeader: 'Zabezpečení',
    defaultPermissions: 'Výchozí oprávnění',
    determiningEntry: 'Tento záznam má přednost',
    noReasoningData: 'Pro toto oprávnění nejsou k dispozici žádná data.',
    defaultAllowNote: 'Nejsou nastavena žádná oprávnění, toto je ve výchozím nastavení povoleno.',
    defaultDenyNote: 'Nejsou nastavena žádná oprávnění, toto je ve výchozím nastavení zakázáno.',

    // ── Zpráva o přesměrování granulárních oprávnění ───────────────
    redirectMessage:
      'Oprávnění k dokumentům pro tuto uživatelskou skupinu spravuje balíček Advanced Permissions. Otevřete Editor oprávnění v sekci Uživatelé pro konfiguraci oprávnění.',

    // ── Oprávnění typu dokumentu ──────────────────────────────────────
    role: 'Uživatelská skupina',
    pickRole: 'Vyberte uživatelskou skupinu',
    user: 'Uživatel',
    pickUser: 'Vyberte uživatele',
    node: 'Uzel',
    pickNode: 'Vyberte uzel',
    state: 'Stav',
    scope: 'Rozsah',
    scope_thisNodeOnly: 'Pouze tento uzel',
    scope_thisNodeAndDescendants: 'Tento uzel a potomci',
    scope_descendantsOnly: 'Pouze potomci',

    docTypePermissions_menuLabel: 'Editor oprávnění typu dokumentu',
    docTypePermissions_insertOptionsMenuLabel: 'Zobrazení možností vkládání',
    docTypePermissions_workspaceTitle: 'Editor oprávnění typu dokumentu',
    docTypePermissions_auditTitle: 'Zobrazení možností vkládání',
    docTypePermissions_allDocTypes: 'Všechny typy dokumentů',
    docTypePermissions_verbInsert: 'Vložit',
    docTypePermissions_documentType: 'Typ dokumentu',
    chooseDocType: 'Vyberte typ dokumentu',
    notAnInsertOption: 'Tento typ dokumentu není aktuálně možností vkládání na tomto uzlu.',
    notAnInsertOptionAllowedNote: 'Tento typ dokumentu není možností vkládání na tomto uzlu, jinak by však byl povolen.',
    notAnInsertOptionDeniedNote: 'Tento typ dokumentu není možností vkládání na tomto uzlu, jinak by však byl zakázán.',
    docTypePermissions_pickDocType: '— Vyberte typ dokumentu —',
    docTypePermissions_pickToStart: 'Vyberte uživatelskou skupinu a typ dokumentu pro začátek.',
    docTypePermissions_defaultRowLabel: 'Výchozí (platí všude)',
    docTypePermissions_pendingNodeLabel: '(neuložený uzel)',
    docTypePermissions_addScopeNode: 'Přidat přepis rozsahu',
    docTypePermissions_notSet: 'Nenastaveno',
    docTypePermissions_noResults: 'Nebyly nalezeny žádné typy dokumentů.',
    docTypePermissions_useRoot: 'Použít kořen',
    docTypePermissions_pickedNode: 'Uzel:',
    docTypePermissions_rootLevel: 'Kořenová úroveň',
    docTypePermissions_reasoning: 'Zdůvodnění',
    docTypePermissions_defaultAllow: 'Ve výchozím nastavení povoleno',
    docTypePermissions_viaDefault: 'z výchozího řádku',
  },
} satisfies UmbLocalizationDictionary;
