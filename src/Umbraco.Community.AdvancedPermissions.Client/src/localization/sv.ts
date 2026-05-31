import type { UmbLocalizationDictionary } from '@umbraco-cms/backoffice/localization-api';

export default {
  uap: {
    // ── Navigering ────────────────────────────────────────────────────────
    sectionLabel: 'Avancerade behörigheter',
    editorsSectionLabel: 'Redigerare',
    viewersSectionLabel: 'Visningar',
    permissionsEditor: 'Redigerare för innehållsbehörigheter',
    accessViewer: 'Åtkomstvisning',

    // ── Allmänt ──────────────────────────────────────────────────────────
    roleLabel: 'Användargrupp',
    rolePlaceholder: '— Välj en användargrupp —',
    userLabel: 'Användare',
    saveChanges: 'Spara ändringar',
    discard: 'Ignorera',
    cancel: 'Avbryt',
    apply: 'Verkställ',
    close: 'Stäng',
    inherit: 'Ärv',
    allow: 'Tillåt',
    deny: 'Neka',
    umbracoUsers: 'Alla användare',

    // ── Behörighetsredigerare ─────────────────────────────────────────────
    editorHeadline: 'Redigerare för innehållsbehörigheter',
    selectRolePrompt: 'Välj en användargrupp ovan för att hantera dess behörigheter.',
    permissionsSaved: 'Behörigheter sparade.',
    saveFailed: (error: string) => `Sparandet misslyckades: ${error}`,
    contentNodeHeader: 'Innehållsnod',
    contentRoot: 'Standardbehörigheter',
    expand: 'Visa',
    collapse: 'Dölj',

    // ── Behörighetsdialog ───────────────────────────────────────────────
    dialogHeadline: (verb: string, nodeName: string) => `Ställ in ${verb}-behörighet för ‘${nodeName}’`,
    descendantsSection: 'Underordnade noder (om avvikande)',
    dialogInstructions: 'Ställ in behörigheten för denna nod. Som standard gäller detta även alla underordnade noder. Använd “Underordnade noder (om avvikande)” för att ställa in en annan behörighet för underordnade noder.',
    virtualRootInherit: 'Inte angiven (ta bort post)',
    virtualRootAllow: 'Tillåt (allt innehåll)',
    virtualRootDeny: 'Neka (allt innehåll)',
    dialogResult: 'Resultat',
    previewBothInherit: 'Ingen behörighet angiven. Ärvs från överordnad nod.',
    previewUniform: (action: string) => `${action} på denna nod och alla underordnade noder.`,
    previewNodeOnly: (action: string) => `${action} endast på denna nod. Underordnade noder påverkas inte av denna regel.`,
    previewDescOnly: (action: string) => `Ingen explicit behörighet på denna nod. ${action} på alla underordnade noder.`,
    previewSplit: (nodeAction: string, descAction: string) => `${nodeAction} på denna nod. ${descAction} på alla underordnade noder.`,
    previewVirtualInherit: 'Ingen standardbehörighet angiven.',
    previewVirtualSet: (action: string) => `${action} som standard för allt innehåll.`,
    previewPriorityNode: 'Prioritetsåsidosättning är angiven på denna nod.',
    previewPriorityDesc: 'Prioritetsåsidosättning är angiven på underordnade noder.',
    previewPriorityBoth: 'Prioritetsåsidosättning är angiven.',

    // ── Prioritetsåsidosättning ─────────────────────────────────────
    priorityOverride: 'Prioritetsåsidosättning',
    priorityOverrideTooltip: (permission: string, nodeName: string) =>
      `En användare kan tillhöra flera användargrupper. Normalt följer den effektiva behörigheten en fast prioritetsordning. Genom att kryssa i denna ruta åsidosätts den ordningen, så att inställningen “${permission}” som du väljer här nästan alltid blir resultatet för “${nodeName}”, oavsett användarens övriga grupper. Använd sparsamt.`,
    priorityOverrideBadgeTitle: 'Prioritetsåsidosättning är angiven på denna post',
    priorityOverrideWonTitle: 'Löst via prioritetsåsidosättning',
    priorityOverrideSuppressedHeader: 'Prioritetsåsidosättning ändrade resultatet',
    priorityOverrideSuppressedHint: 'Utan den skulle resultatet ha blivit:',

    // ── Åtkomstvisning ──────────────────────────────────────────────────
    viewerHeadline: 'Åtkomstvisning',
    byRole: 'Per användargrupp',
    byUser: 'Per användare',
    chooseRole: 'Välj användargrupp',
    chooseUser: 'Välj användare',
    selectSubjectPrompt: 'Välj en användargrupp eller användare för att visa effektiva behörigheter.',
    legendAllow: 'Tillåt',
    legendDeny: 'Neka',
    clickForReasoning: (label: string) => `${label} — klicka för motivering`,
    subjectOr: 'eller',

    // ── Modal för användargruppsval ───────────────────────────────────────
    rolePickerHeadline: 'Välj en användargrupp',
    rolePickerFilter: 'Skriv för att filtrera…',
    rolePickerNoResults: 'Inga användargrupper matchar filtret.',
    rolePickerNameHeader: 'Användargrupp',

    // ── Modal för användarval ───────────────────────────────────────────
    userPickerHeadline: 'Välj en användare',
    userPickerFilter: 'Skriv för att filtrera…',
    userPickerNoResults: 'Inga användare matchar filtret.',
    userPickerNameHeader: 'Användare',

    // ── Motiveringsdialog ──────────────────────────────────────────────────
    reasoningHeadline: (verb: string, nodeName: string) => `${verb}-behörighet för “${nodeName}”`,
    effectiveAllowed: (subject: string, verb: string, nodeName: string) =>
      `${subject} har beviljats ${verb}-behörighet för “${nodeName}”.`,
    effectiveDenied: (subject: string, verb: string, nodeName: string) =>
      `${subject} har nekats ${verb}-behörighet för “${nodeName}”.`,
    dialogSecurityHeader: 'Säkerhet',
    defaultPermissions: 'Standardbehörigheter',
    determiningEntry: 'Denna post har företräde',
    noReasoningData: 'Inga behörighetsdata tillgängliga för detta verb.',
    defaultAllowNote: 'Inga behörigheter är angivna, detta tillåts som standard.',
    defaultDenyNote: 'Inga behörigheter är angivna, detta nekas som standard.',

    // ── Omdirigeringsmeddelande för granulära behörigheter ───────────────
    redirectMessage:
      'Dokumentbehörigheter för denna användargrupp hanteras av paketet Advanced Permissions. Öppna behörighetsredigeraren i sektionen Användare för att konfigurera behörigheter.',

    // ── Dokumenttypsbehörigheter ──────────────────────────────────────────
    role: 'Användargrupp',
    pickRole: 'Välj användargrupp',
    user: 'Användare',
    pickUser: 'Välj användare',
    node: 'Nod',
    pickNode: 'Välj nod',
    state: 'Status',
    scope: 'Omfattning',
    scope_thisNodeOnly: 'Endast denna nod',
    scope_thisNodeAndDescendants: 'Denna nod och underordnade',
    scope_descendantsOnly: 'Endast underordnade',

    docTypePermissions_menuLabel: 'Redigerare för dokumenttypsbehörigheter',
    docTypePermissions_insertOptionsMenuLabel: 'Visning av infogningsalternativ',
    docTypePermissions_workspaceTitle: 'Redigerare för dokumenttypsbehörigheter',
    docTypePermissions_auditTitle: 'Visning av infogningsalternativ',
    docTypePermissions_allDocTypes: 'Alla dokumenttyper',
    docTypePermissions_verbInsert: 'Infoga',
    docTypePermissions_documentType: 'Dokumenttyp',
    chooseDocType: 'Välj dokumenttyp',
    notAnInsertOption: 'Denna dokumenttyp är för närvarande inte ett infogningsalternativ på denna nod.',
    notAnInsertOptionAllowedNote: 'Denna dokumenttyp är inte ett infogningsalternativ på denna nod, men skulle annars tillåtas.',
    notAnInsertOptionDeniedNote: 'Denna dokumenttyp är inte ett infogningsalternativ på denna nod, men skulle annars nekas.',
    docTypePermissions_pickDocType: '— Välj en dokumenttyp —',
    docTypePermissions_pickToStart: 'Välj en användargrupp och dokumenttyp för att börja.',
    docTypePermissions_defaultRowLabel: 'Standard (gäller överallt)',
    docTypePermissions_pendingNodeLabel: '(osparad nod)',
    docTypePermissions_addScopeNode: 'Lägg till omfattningsåsidosättning',
    docTypePermissions_notSet: 'Inte angiven',
    docTypePermissions_noResults: 'Inga dokumenttyper hittades.',
    docTypePermissions_useRoot: 'Använd rot',
    docTypePermissions_pickedNode: 'Nod:',
    docTypePermissions_rootLevel: 'Rotnivå',
    docTypePermissions_reasoning: 'Motivering',
    docTypePermissions_defaultAllow: 'Tillåts som standard',
    docTypePermissions_viaDefault: 'från standardrad',
  },
} satisfies UmbLocalizationDictionary;
