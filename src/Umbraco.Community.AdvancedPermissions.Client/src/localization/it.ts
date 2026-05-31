import type { UmbLocalizationDictionary } from '@umbraco-cms/backoffice/localization-api';

export default {
  uap: {
    // ── Navigazione ───────────────────────────────────────────────────────
    sectionLabel: 'Permessi avanzati',
    editorsSectionLabel: 'Editor',
    viewersSectionLabel: 'Visualizzazioni',
    permissionsEditor: 'Editor dei permessi sui contenuti',
    accessViewer: 'Visualizzatore degli accessi',

    // ── Generale ──────────────────────────────────────────────────────────
    roleLabel: 'Gruppo di utenti',
    rolePlaceholder: '— Seleziona un gruppo di utenti —',
    userLabel: 'Utente',
    saveChanges: 'Salva modifiche',
    discard: 'Scarta',
    cancel: 'Annulla',
    apply: 'Applica',
    close: 'Chiudi',
    inherit: 'Eredita',
    allow: 'Consenti',
    deny: 'Nega',
    umbracoUsers: 'Tutti gli utenti',

    // ── Editor dei permessi ───────────────────────────────────────────────
    editorHeadline: 'Editor dei permessi sui contenuti',
    selectRolePrompt: 'Seleziona un gruppo di utenti qui sopra per gestirne i permessi.',
    permissionsSaved: 'Permessi salvati.',
    saveFailed: (error: string) => `Salvataggio non riuscito: ${error}`,
    contentNodeHeader: 'Nodo di contenuto',
    contentRoot: 'Permessi predefiniti',
    expand: 'Espandi',
    collapse: 'Comprimi',

    // ── Finestra dei permessi ─────────────────────────────────────────────
    dialogHeadline: (verb: string, nodeName: string) => `Imposta il permesso ${verb} per ‘${nodeName}’`,
    descendantsSection: 'Discendenti (se diversi)',
    dialogInstructions: 'Imposta il permesso per questo nodo. Per impostazione predefinita, questo si applica anche a tutti i discendenti. Usa “Discendenti (se diversi)” per impostare un permesso diverso per i nodi discendenti.',
    virtualRootInherit: 'Non impostato (rimuovi voce)',
    virtualRootAllow: 'Consenti (tutti i contenuti)',
    virtualRootDeny: 'Nega (tutti i contenuti)',
    dialogResult: 'Risultato',
    previewBothInherit: 'Nessun permesso impostato. Eredita dal nodo padre.',
    previewUniform: (action: string) => `${action} su questo nodo e su tutti i discendenti.`,
    previewNodeOnly: (action: string) => `${action} solo su questo nodo. I discendenti non sono interessati da questa regola.`,
    previewDescOnly: (action: string) => `Nessun permesso esplicito su questo nodo. ${action} su tutti i discendenti.`,
    previewSplit: (nodeAction: string, descAction: string) => `${nodeAction} su questo nodo. ${descAction} su tutti i discendenti.`,
    previewVirtualInherit: 'Nessun permesso predefinito impostato.',
    previewVirtualSet: (action: string) => `${action} per impostazione predefinita per tutti i contenuti.`,
    previewPriorityNode: 'La sostituzione prioritaria è impostata su questo nodo.',
    previewPriorityDesc: 'La sostituzione prioritaria è impostata sui discendenti.',
    previewPriorityBoth: 'La sostituzione prioritaria è impostata.',

    // ── Sostituzione prioritaria ──────────────────────────────────────────
    priorityOverride: 'Sostituzione prioritaria',
    priorityOverrideTooltip: (permission: string, nodeName: string) =>
      `Un utente può appartenere a più gruppi di utenti. Normalmente, il permesso effettivo segue un ordine di priorità fisso. Selezionando questa casella si sostituisce tale ordine, così l’impostazione “${permission}” scelta qui diventa quasi sempre il risultato per “${nodeName}”, indipendentemente dagli altri gruppi dell’utente. Usare con parsimonia.`,
    priorityOverrideBadgeTitle: 'La sostituzione prioritaria è impostata su questa voce',
    priorityOverrideWonTitle: 'Risolto tramite sostituzione prioritaria',
    priorityOverrideSuppressedHeader: 'La sostituzione prioritaria ha cambiato il risultato',
    priorityOverrideSuppressedHint: 'Senza di essa, il risultato sarebbe stato:',

    // ── Visualizzatore degli accessi ──────────────────────────────────────
    viewerHeadline: 'Visualizzatore degli accessi',
    byRole: 'Per gruppo di utenti',
    byUser: 'Per utente',
    chooseRole: 'Scegli gruppo di utenti',
    chooseUser: 'Scegli utente',
    selectSubjectPrompt: 'Seleziona un gruppo di utenti o un utente per visualizzare i permessi effettivi.',
    legendAllow: 'Consenti',
    legendDeny: 'Nega',
    clickForReasoning: (label: string) => `${label} — clicca per la motivazione`,
    subjectOr: 'o',

    // ── Finestra di selezione gruppo di utenti ────────────────────────────
    rolePickerHeadline: 'Seleziona un gruppo di utenti',
    rolePickerFilter: 'Digita per filtrare…',
    rolePickerNoResults: 'Nessun gruppo di utenti corrisponde al filtro.',
    rolePickerNameHeader: 'Gruppo di utenti',

    // ── Finestra di selezione utente ──────────────────────────────────────
    userPickerHeadline: 'Seleziona un utente',
    userPickerFilter: 'Digita per filtrare…',
    userPickerNoResults: 'Nessun utente corrisponde al filtro.',
    userPickerNameHeader: 'Utente',

    // ── Finestra della motivazione ────────────────────────────────────────
    reasoningHeadline: (verb: string, nodeName: string) => `Permesso ${verb} per “${nodeName}”`,
    effectiveAllowed: (subject: string, verb: string, nodeName: string) =>
      `A ${subject} è stato consentito il permesso ${verb} per “${nodeName}”.`,
    effectiveDenied: (subject: string, verb: string, nodeName: string) =>
      `A ${subject} è stato negato il permesso ${verb} per “${nodeName}”.`,
    dialogSecurityHeader: 'Sicurezza',
    defaultPermissions: 'Permessi predefiniti',
    determiningEntry: 'Questa voce ha la precedenza',
    noReasoningData: 'Nessun dato sui permessi disponibile per questo verbo.',
    defaultAllowNote: 'Nessun permesso impostato, questo è consentito per impostazione predefinita.',
    defaultDenyNote: 'Nessun permesso impostato, questo è negato per impostazione predefinita.',

    // ── Messaggio di reindirizzamento permessi granulari ──────────────────
    redirectMessage:
      'I permessi sui documenti per questo gruppo di utenti sono gestiti dal pacchetto Advanced Permissions. Apri l’Editor dei permessi nella sezione Utenti per configurare i permessi.',

    // ── Permessi per tipo di documento ────────────────────────────────────
    role: 'Gruppo di utenti',
    pickRole: 'Scegli gruppo di utenti',
    user: 'Utente',
    pickUser: 'Scegli utente',
    node: 'Nodo',
    pickNode: 'Scegli nodo',
    state: 'Stato',
    scope: 'Ambito',
    scope_thisNodeOnly: 'Solo questo nodo',
    scope_thisNodeAndDescendants: 'Questo nodo e i discendenti',
    scope_descendantsOnly: 'Solo i discendenti',

    docTypePermissions_menuLabel: 'Editor dei permessi per tipo di documento',
    docTypePermissions_insertOptionsMenuLabel: 'Visualizzatore delle opzioni di inserimento',
    docTypePermissions_workspaceTitle: 'Editor dei permessi per tipo di documento',
    docTypePermissions_auditTitle: 'Visualizzatore delle opzioni di inserimento',
    docTypePermissions_allDocTypes: 'Tutti i tipi di documento',
    docTypePermissions_verbInsert: 'Inserisci',
    docTypePermissions_documentType: 'Tipo di documento',
    chooseDocType: 'Scegli tipo di documento',
    notAnInsertOption: 'Questo tipo di documento attualmente non è un’opzione di inserimento su questo nodo.',
    notAnInsertOptionAllowedNote: 'Questo tipo di documento non è un’opzione di inserimento su questo nodo, ma altrimenti sarebbe consentito.',
    notAnInsertOptionDeniedNote: 'Questo tipo di documento non è un’opzione di inserimento su questo nodo, ma altrimenti sarebbe negato.',
    docTypePermissions_pickDocType: '— Seleziona un tipo di documento —',
    docTypePermissions_pickToStart: 'Scegli un gruppo di utenti e un tipo di documento per iniziare.',
    docTypePermissions_defaultRowLabel: 'Predefinito (si applica ovunque)',
    docTypePermissions_pendingNodeLabel: '(nodo non salvato)',
    docTypePermissions_addScopeNode: 'Aggiungi sostituzione dell’ambito',
    docTypePermissions_notSet: 'Non impostato',
    docTypePermissions_noResults: 'Nessun tipo di documento trovato.',
    docTypePermissions_useRoot: 'Usa root',
    docTypePermissions_pickedNode: 'Nodo:',
    docTypePermissions_rootLevel: 'Livello root',
    docTypePermissions_reasoning: 'Motivazione',
    docTypePermissions_defaultAllow: 'Consentito per impostazione predefinita',
    docTypePermissions_viaDefault: 'dalla riga predefinita',
  },
} satisfies UmbLocalizationDictionary;
