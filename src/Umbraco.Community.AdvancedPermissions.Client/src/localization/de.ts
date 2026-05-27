import type { UmbLocalizationDictionary } from '@umbraco-cms/backoffice/localization-api';

export default {
  uap: {
    // ── Navigation ────────────────────────────────────────────────────────
    sectionLabel: 'Erweiterte Berechtigungen',
    editorsSectionLabel: 'Editoren',
    viewersSectionLabel: 'Anzeigen',
    permissionsEditor: 'Inhaltsberechtigungs-Editor',
    accessViewer: 'Zugriffsanzeige',

    // ── Allgemein ─────────────────────────────────────────────────────────
    roleLabel: 'Benutzergruppe',
    rolePlaceholder: '\u2014 Benutzergruppe ausw\u00e4hlen \u2014',
    userLabel: 'Benutzer',
    saveChanges: '\u00c4nderungen speichern',
    discard: 'Verwerfen',
    cancel: 'Abbrechen',
    apply: '\u00dcbernehmen',
    close: 'Schlie\u00dfen',
    inherit: 'Vererben',
    allow: 'Erlauben',
    deny: 'Verweigern',
    umbracoUsers: 'Alle Benutzer',

    // ── Berechtigungseditor ──────────────────────────────────────────────
    editorHeadline: 'Inhaltsberechtigungs-Editor',
    selectRolePrompt: 'W\u00e4hlen Sie oben eine Benutzergruppe aus, um deren Berechtigungen zu verwalten.',
    permissionsSaved: 'Berechtigungen gespeichert.',
    saveFailed: (error: string) => `Speichern fehlgeschlagen: ${error}`,
    contentNodeHeader: 'Inhaltsknoten',
    contentRoot: 'Standardberechtigungen',
    expand: 'Aufklappen',
    collapse: 'Zuklappen',

    // ── Berechtigungsdialog ───────────────────────────────────────────────
    dialogHeadline: (verb: string, nodeName: string) => `${verb}-Berechtigung f\u00fcr \u201e${nodeName}\u201c festlegen`,
    descendantsSection: 'Untergeordnete Knoten (falls abweichend)',
    dialogInstructions: 'Legen Sie die Berechtigung f\u00fcr diesen Knoten fest. Standardm\u00e4\u00dfig gilt dies auch f\u00fcr alle untergeordneten Knoten. Verwenden Sie \u201eUntergeordnete Knoten (falls abweichend)\u201c, um eine andere Berechtigung f\u00fcr untergeordnete Knoten festzulegen.',
    virtualRootInherit: 'Nicht festgelegt (Eintrag entfernen)',
    virtualRootAllow: 'Erlauben (gesamter Inhalt)',
    virtualRootDeny: 'Verweigern (gesamter Inhalt)',
    dialogResult: 'Ergebnis',
    previewBothInherit: 'Keine Berechtigung festgelegt. Erbt vom \u00fcbergeordneten Knoten.',
    previewUniform: (action: string) => `${action} f\u00fcr diesen Knoten und alle untergeordneten Knoten.`,
    previewNodeOnly: (action: string) => `${action} nur f\u00fcr diesen Knoten. Untergeordnete Knoten werden von dieser Regel nicht beeinflusst.`,
    previewDescOnly: (action: string) => `Keine explizite Berechtigung f\u00fcr diesen Knoten. ${action} f\u00fcr alle untergeordneten Knoten.`,
    previewSplit: (nodeAction: string, descAction: string) => `${nodeAction} f\u00fcr diesen Knoten. ${descAction} f\u00fcr alle untergeordneten Knoten.`,
    previewVirtualInherit: 'Keine Standardberechtigung festgelegt.',
    previewVirtualSet: (action: string) => `${action} standardm\u00e4\u00dfig f\u00fcr gesamten Inhalt.`,
    previewPriorityNode: 'Priorit\u00e4ts-\u00dcberschreibung ist f\u00fcr diesen Knoten gesetzt.',
    previewPriorityDesc: 'Priorit\u00e4ts-\u00dcberschreibung ist f\u00fcr untergeordnete Knoten gesetzt.',
    previewPriorityBoth: 'Priorit\u00e4ts-\u00dcberschreibung ist gesetzt.',

    // \u2500\u2500 Priorit\u00e4ts-\u00dcberschreibung \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500
    priorityOverride: 'Priorit\u00e4ts-\u00dcberschreibung',
    priorityOverrideTooltip: (permission: string, nodeName: string) =>
      `Ein Benutzer kann mehreren Benutzergruppen angeh\u00f6ren. Normalerweise folgt die effektive Berechtigung einer festen Priorit\u00e4tsreihenfolge. Wenn Sie dieses K\u00e4stchen aktivieren, wird diese Reihenfolge \u00fcberschrieben, sodass die hier gew\u00e4hlte Einstellung f\u00fcr \u201e${permission}\u201c fast immer zum Ergebnis f\u00fcr \u201e${nodeName}\u201c wird \u2013 unabh\u00e4ngig von den anderen Gruppen des Benutzers. Sparsam verwenden.`,
    priorityOverrideBadgeTitle: 'Priorit\u00e4ts-\u00dcberschreibung ist f\u00fcr diesen Eintrag gesetzt',
    priorityOverrideWonTitle: 'Aufgel\u00f6st \u00fcber Priorit\u00e4ts-\u00dcberschreibung',
    priorityOverrideSuppressedHeader: 'Priorit\u00e4ts-\u00dcberschreibung hat das Ergebnis ge\u00e4ndert',
    priorityOverrideSuppressedHint: 'Andernfalls w\u00e4re das Ergebnis gewesen:',

    // ── Zugriffsanzeige ───────────────────────────────────────────────────
    viewerHeadline: 'Zugriffsanzeige',
    byRole: 'Nach Benutzergruppe',
    byUser: 'Nach Benutzer',
    chooseRole: 'Benutzergruppe ausw\u00e4hlen',
    chooseUser: 'Benutzer ausw\u00e4hlen',
    selectSubjectPrompt: 'W\u00e4hlen Sie eine Benutzergruppe oder einen Benutzer aus, um die effektiven Berechtigungen anzuzeigen.',
    legendAllow: 'Erlauben',
    legendDeny: 'Verweigern',
    clickForReasoning: (label: string) => `${label} \u2014 Klicken f\u00fcr Begr\u00fcndung`,

    // ── Rollenauswahlmodal ─────────────────────────────────────────────────
    rolePickerHeadline: 'Benutzergruppe ausw\u00e4hlen',
    rolePickerFilter: 'Benutzergruppen filtern\u2026',
    rolePickerNoResults: 'Keine Benutzergruppen gefunden.',
    rolePickerNameHeader: 'Benutzergruppe',

    // ── Benutzerauswahlmodal ───────────────────────────────────────────────
    userPickerHeadline: 'Benutzer ausw\u00e4hlen',
    userPickerFilter: 'Benutzer filtern\u2026',
    userPickerNoResults: 'Keine Benutzer gefunden.',
    userPickerNameHeader: 'Benutzer',

    // ── Begr\u00fcndungsdialog ──────────────────────────────────────────────
    reasoningHeadline: (verb: string, nodeName: string) => `${verb}-Berechtigung f\u00fcr \u201e${nodeName}\u201c`,
    effectiveAllowed: (subject: string, verb: string, nodeName: string) =>
      `${subject} wurde die ${verb}-Berechtigung f\u00fcr \u201e${nodeName}\u201c gew\u00e4hrt.`,
    effectiveDenied: (subject: string, verb: string, nodeName: string) =>
      `${subject} wurde die ${verb}-Berechtigung f\u00fcr \u201e${nodeName}\u201c verweigert.`,
    dialogSecurityHeader: 'Sicherheit',
    defaultPermissions: 'Standardberechtigungen',
    determiningEntry: 'Dieser Eintrag hat Vorrang',
    noReasoningData: 'Keine Berechtigungsdaten f\u00fcr dieses Recht verf\u00fcgbar.',

    // ── Weiterleitungsnachricht granulare Berechtigungen ──────────────────
    redirectMessage:
      'Dokumentberechtigungen f\u00fcr diese Benutzergruppe werden durch das Advanced Permissions-Paket verwaltet. \u00d6ffnen Sie den Berechtigungseditor im Benutzerbereich, um Berechtigungen zu konfigurieren.',

    // \u2500\u2500 Dokumenttyp-Berechtigungen \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500
    role: 'Benutzergruppe',
    pickRole: 'Benutzergruppe ausw\u00e4hlen',
    user: 'Benutzer',
    pickUser: 'Benutzer ausw\u00e4hlen',
    node: 'Knoten',
    pickNode: 'Knoten ausw\u00e4hlen',
    state: 'Status',
    scope: 'Bereich',
    scope_thisNodeOnly: 'Nur dieser Knoten',
    scope_thisNodeAndDescendants: 'Dieser Knoten und Unterknoten',
    scope_descendantsOnly: 'Nur Unterknoten',

    docTypePermissions_menuLabel: 'Dokumenttyp-Berechtigungs-Editor',
    docTypePermissions_insertOptionsMenuLabel: 'Einfügeoptionen-Anzeige',
    docTypePermissions_workspaceTitle: 'Dokumenttyp-Berechtigungs-Editor',
    docTypePermissions_auditTitle: 'Einfügeoptionen-Anzeige',
    docTypePermissions_allDocTypes: 'Alle Dokumenttypen',
    docTypePermissions_verbInsert: 'Einfügen',
    docTypePermissions_documentType: 'Dokumenttyp',
    docTypePermissions_pickDocType: '\u2014 Dokumenttyp ausw\u00e4hlen \u2014',
    docTypePermissions_pickToStart: 'W\u00e4hlen Sie eine Benutzergruppe und einen Dokumenttyp aus.',
    docTypePermissions_defaultRowLabel: 'Standard (gilt \u00fcberall)',
    docTypePermissions_pendingNodeLabel: '(nicht gespeicherter Knoten)',
    docTypePermissions_addScopeNode: 'Bereichs\u00fcberschreibung hinzuf\u00fcgen',
    docTypePermissions_notSet: 'Nicht gesetzt',
    docTypePermissions_noResults: 'Keine Dokumenttypen gefunden.',
    docTypePermissions_useRoot: 'Root verwenden',
    docTypePermissions_pickedNode: 'Knoten:',
    docTypePermissions_rootLevel: 'Root-Ebene',
    docTypePermissions_reasoning: 'Begr\u00fcndung',
    docTypePermissions_defaultAllow: 'Standardm\u00e4\u00dfig erlaubt',
    docTypePermissions_viaDefault: 'aus Standardzeile',
    docTypePermissions_notInAllowedChildren: 'Nicht in der Liste der erlaubten Untertypen',
  },
} satisfies UmbLocalizationDictionary;
