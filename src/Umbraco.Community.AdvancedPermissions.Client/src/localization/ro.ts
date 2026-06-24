import type { UmbLocalizationDictionary } from '@umbraco-cms/backoffice/localization-api';

export default {
  uap: {
    // ── Navigare ──────────────────────────────────────────────────────────
    sectionLabel: 'Permisiuni avansate',
    editorsSectionLabel: 'Editori',
    viewersSectionLabel: 'Vizualizatoare',
    permissionsEditor: 'Editor de permisiuni pentru conținut',
    accessViewer: 'Vizualizator de acces',

    // ── Comune ────────────────────────────────────────────────────────────
    roleLabel: 'Grup de utilizatori',
    rolePlaceholder: '— Selectați un grup de utilizatori —',
    userLabel: 'Utilizator',
    saveChanges: 'Salvează modificările',
    discard: 'Renunță',
    cancel: 'Anulează',
    apply: 'Aplică',
    close: 'Închide',
    inherit: 'Moștenește',
    allow: 'Permite',
    deny: 'Refuză',
    umbracoUsers: 'Toți utilizatorii',

    // ── Help ──────────────────────────────────────────────────────────────
    help_contentPermissions_description: 'Gestionați permisiunile de tip Permite/Refuză per grup de utilizatori în arborele de conținut.',
    help_accessViewer_description: 'Vedeți permisiunile efective pe care le are un utilizator sau un grup la orice nod, cu justificare completă.',
    help_docTypePermissions_description: 'Decideți ce tipuri de documente poate crea fiecare grup de utilizatori și unde.',
    help_insertOptions_description: 'Auditați ce tipuri de documente poate crea un utilizator sau un grup de utilizatori la fiecare nod.',
    help_learnMore: 'Aflați mai multe',
    help_modalTitle: 'Ajutor',
    help_tabAbout: 'Despre această pagină',
    help_tabConcepts: 'Concepte',
    help_concept_scope_tip: 'Domeniul controlează cât de departe se aplică o regulă: acest nod, acest nod și descendenții săi sau doar descendenții.',
    help_concept_priorityOverride_tip: 'Suprascrierea de prioritate forțează această înregistrare să prevaleze asupra ordinii normale de rezolvare.',
    help_concept_allowDeny_tip: 'Permite acordă permisiunea, Refuză o revocă, iar lăsarea nesetată o moștenește de la cel mai apropiat strămoș.',
    help_concept_reasoning_tip: 'Faceți clic pe orice celulă pentru a vedea exact cum a fost rezolvată permisiunea.',

    // ── Editor de permisiuni ─────────────────────────────────────────────
    editorHeadline: 'Editor de permisiuni pentru conținut',
    selectRolePrompt: 'Selectați mai sus un grup de utilizatori pentru a-i gestiona permisiunile.',
    permissionsSaved: 'Permisiunile au fost salvate.',
    saveFailed: (error: string) => `Salvarea a eșuat: ${error}`,
    contentNodeHeader: 'Nod de conținut',
    contentRoot: 'Permisiuni implicite',
    expand: 'Extinde',
    collapse: 'Restrânge',

    // ── Dialog de permisiune ──────────────────────────────────────────────
    dialogHeadline: (verb: string, nodeName: string) => `Setați permisiunea ${verb} pentru ‘${nodeName}’`,
    descendantsSection: 'Descendenți (dacă diferă)',
    dialogInstructions: 'Setați permisiunea pentru acest nod. În mod implicit, aceasta se aplică și tuturor descendenților. Folosiți „Descendenți (dacă diferă)” pentru a seta o permisiune diferită pentru nodurile descendente.',
    virtualRootInherit: 'Nesetat (eliminați înregistrarea)',
    virtualRootAllow: 'Permite (tot conținutul)',
    virtualRootDeny: 'Refuză (tot conținutul)',
    dialogResult: 'Rezultat',
    previewBothInherit: 'Nicio permisiune setată. Se moștenește de la nodul părinte.',
    previewUniform: (action: string) => `${action} pe acest nod și pe toți descendenții.`,
    previewNodeOnly: (action: string) => `${action} doar pe acest nod. Descendenții nu sunt afectați de această regulă.`,
    previewDescOnly: (action: string) => `Nicio permisiune explicită pe acest nod. ${action} pe toți descendenții.`,
    previewSplit: (nodeAction: string, descAction: string) => `${nodeAction} pe acest nod. ${descAction} pe toți descendenții.`,
    previewVirtualInherit: 'Nicio permisiune implicită setată.',
    previewVirtualSet: (action: string) => `${action} în mod implicit pentru tot conținutul.`,
    previewPriorityNode: 'Suprascrierea de prioritate este activată pe acest nod.',
    previewPriorityDesc: 'Suprascrierea de prioritate este activată pe descendenți.',
    previewPriorityBoth: 'Suprascrierea de prioritate este activată.',

    // ── Suprascriere de prioritate ────────────────────────────────────
    priorityOverride: 'Suprascriere de prioritate',
    priorityOverrideTooltip: (permission: string, nodeName: string) =>
      `Un utilizator poate aparține mai multor grupuri de utilizatori. În mod normal, permisiunea efectivă urmează o ordine fixă de prioritate. Bifarea acestei căsuțe suprascrie acea ordine, astfel încât setarea „${permission}” aleasă aici devine aproape întotdeauna rezultatul pentru „${nodeName}”, indiferent de celelalte grupuri ale utilizatorului. Folosiți cu moderație.`,
    priorityOverrideBadgeTitle: 'Suprascrierea de prioritate este activată pe această înregistrare',
    priorityOverrideWonTitle: 'Rezolvat prin suprascrierea de prioritate',
    priorityOverrideSuppressedHeader: 'Suprascrierea de prioritate a modificat rezultatul',
    priorityOverrideSuppressedHint: 'Fără ea, rezultatul ar fi fost:',

    // ── Vizualizator de acces ─────────────────────────────────────────────
    viewerHeadline: 'Vizualizator de acces',
    byRole: 'După grup de utilizatori',
    byUser: 'După utilizator',
    chooseRole: 'Alegeți un grup de utilizatori',
    chooseUser: 'Alegeți un utilizator',
    selectSubjectPrompt: 'Selectați un grup de utilizatori sau un utilizator pentru a vedea permisiunile efective.',
    legendAllow: 'Permite',
    legendDeny: 'Refuză',
    clickForReasoning: (label: string) => `${label} — faceți clic pentru justificare`,
    subjectOr: 'sau',

    // ── Dialog de selectare a grupului de utilizatori ─────────────────────
    rolePickerHeadline: 'Selectați un grup de utilizatori',
    rolePickerFilter: 'Tastați pentru a filtra…',
    rolePickerNoResults: 'Niciun grup de utilizatori nu corespunde filtrului.',
    rolePickerNameHeader: 'Grup de utilizatori',

    // ── Dialog de selectare a utilizatorului ──────────────────────────────
    userPickerHeadline: 'Selectați un utilizator',
    userPickerFilter: 'Tastați pentru a filtra…',
    userPickerNoResults: 'Niciun utilizator nu corespunde filtrului.',
    userPickerNameHeader: 'Utilizator',

    // ── Dialog de justificare ─────────────────────────────────────────────
    reasoningHeadline: (verb: string, nodeName: string) => `Permisiunea ${verb} pentru “${nodeName}”`,
    effectiveAllowed: (subject: string, verb: string, nodeName: string) =>
      `${subject} a primit permisiunea ${verb} pentru “${nodeName}”.`,
    effectiveDenied: (subject: string, verb: string, nodeName: string) =>
      `${subject} i s-a refuzat permisiunea ${verb} pentru “${nodeName}”.`,
    dialogSecurityHeader: 'Securitate',
    defaultPermissions: 'Permisiuni implicite',
    determiningEntry: 'Această înregistrare are prioritate',
    noReasoningData: 'Nu există date de permisiune disponibile pentru această acțiune.',
    defaultAllowNote: 'Nu sunt setate permisiuni, aceasta este permisă în mod implicit.',
    defaultDenyNote: 'Nu sunt setate permisiuni, aceasta este refuzată în mod implicit.',

    // ── Mesaj de redirecționare pentru permisiuni granulare ───────────────
    redirectMessage:
      'Permisiunile pentru documente ale acestui grup de utilizatori sunt gestionate de pachetul Advanced Permissions. Deschideți Editorul de permisiuni din secțiunea Utilizatori pentru a configura permisiunile.',

    // ── Permisiuni pe tip de document ─────────────────────────────────────
    role: 'Grup de utilizatori',
    pickRole: 'Alegeți un grup de utilizatori',
    user: 'Utilizator',
    pickUser: 'Alegeți un utilizator',
    node: 'Nod',
    pickNode: 'Alegeți un nod',
    state: 'Stare',
    scope: 'Domeniu',
    scope_thisNodeOnly: 'Doar acest nod',
    scope_thisNodeAndDescendants: 'Acest nod și descendenții',
    scope_descendantsOnly: 'Doar descendenții',

    docTypePermissions_menuLabel: 'Editor de permisiuni pe tip de document',
    docTypePermissions_insertOptionsMenuLabel: 'Vizualizator opțiuni de inserare',
    docTypePermissions_workspaceTitle: 'Editor de permisiuni pe tip de document',
    docTypePermissions_auditTitle: 'Vizualizator opțiuni de inserare',
    docTypePermissions_allDocTypes: 'Toate tipurile de documente',
    docTypePermissions_verbInsert: 'Inserare',
    docTypePermissions_documentType: 'Tip de document',
    chooseDocType: 'Alegeți un tip de document',
    notAnInsertOption: 'Acest tip de document nu este momentan o opțiune de inserare pe acest nod.',
    notAnInsertOptionAllowedNote: 'Acest tip de document nu este o opțiune de inserare pe acest nod, dar altfel ar fi permis.',
    notAnInsertOptionDeniedNote: 'Acest tip de document nu este o opțiune de inserare pe acest nod, dar altfel ar fi refuzat.',
    docTypePermissions_pickDocType: '— Selectați un tip de document —',
    docTypePermissions_pickToStart: 'Alegeți un grup de utilizatori și un tip de document pentru a începe.',
    docTypePermissions_defaultRowLabel: 'Implicit (se aplică peste tot)',
    docTypePermissions_pendingNodeLabel: '(nod nesalvat)',
    docTypePermissions_addScopeNode: 'Adăugați o suprascriere de domeniu',
    docTypePermissions_notSet: 'Nesetat',
    docTypePermissions_noResults: 'Nu s-au găsit tipuri de documente.',
    docTypePermissions_useRoot: 'Folosește rădăcina',
    docTypePermissions_pickedNode: 'Nod:',
    docTypePermissions_rootLevel: 'Nivel rădăcină',
    docTypePermissions_reasoning: 'Justificare',
    docTypePermissions_defaultAllow: 'Permis în mod implicit',
    docTypePermissions_viaDefault: 'din rândul implicit',
  },
} satisfies UmbLocalizationDictionary;
