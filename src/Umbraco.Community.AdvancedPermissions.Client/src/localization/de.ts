import type { UmbLocalizationDictionary } from '@umbraco-cms/backoffice/localization-api';

export default {
  uap: {
    // ── Navigation ────────────────────────────────────────────────────────
    sectionLabel: 'Erweiterte Berechtigungen',
    permissionsEditor: 'Berechtigungseditor',
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
    editorHeadline: 'Berechtigungseditor',
    selectRolePrompt: 'W\u00e4hlen Sie oben eine Benutzergruppe aus, um deren Berechtigungen zu verwalten.',
    permissionsSaved: 'Berechtigungen gespeichert.',
    saveFailed: (error: string) => `Speichern fehlgeschlagen: ${error}`,
    contentNodeHeader: 'Inhaltsknoten',
    contentRoot: 'Standardberechtigungen',
    expand: 'Aufklappen',
    collapse: 'Zuklappen',

    // ── Berechtigungsdialog ───────────────────────────────────────────────
    dialogHeadline: (verb: string, nodeName: string) => `${verb}-Berechtigung f\u00fcr \u201e${nodeName}\u201c festlegen`,
    descendantsSection: '\u00dcberschreibung untergeordneter Knoten',
    dialogInstructions: 'Legen Sie die Berechtigung f\u00fcr diesen Knoten fest. Standardm\u00e4\u00dfig gilt dies auch f\u00fcr alle untergeordneten Knoten. Verwenden Sie die \u00dcberschreibung, um eine andere Berechtigung f\u00fcr untergeordnete Knoten festzulegen.',
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
  },
} satisfies UmbLocalizationDictionary;
