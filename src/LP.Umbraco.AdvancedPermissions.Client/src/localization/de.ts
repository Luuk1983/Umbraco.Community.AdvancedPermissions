import type { UmbLocalizationDictionary } from '@umbraco-cms/backoffice/localization-api';

export default {
  uap: {
    // ── Navigation ────────────────────────────────────────────────────────
    sectionLabel: 'Erweiterte Berechtigungen',
    permissionsEditor: 'Berechtigungseditor',
    accessViewer: 'Zugriffsanzeige',

    // ── Allgemein ─────────────────────────────────────────────────────────
    roleLabel: 'Rolle',
    rolePlaceholder: '\u2014 Rolle ausw\u00e4hlen \u2014',
    userLabel: 'Benutzer',
    saveChanges: '\u00c4nderungen speichern',
    discard: 'Verwerfen',
    cancel: 'Abbrechen',
    apply: '\u00dcbernehmen',
    close: 'Schlie\u00dfen',
    inherit: 'Vererben',
    allow: 'Erlauben',
    deny: 'Verweigern',
    everyoneSuffix: '(Alle)',

    // ── Berechtigungseditor ──────────────────────────────────────────────
    editorHeadline: 'Berechtigungseditor',
    selectRolePrompt: 'W\u00e4hlen Sie oben eine Rolle aus, um deren Berechtigungen zu verwalten.',
    permissionsSaved: 'Berechtigungen gespeichert.',
    saveFailed: (error: string) => `Speichern fehlgeschlagen: ${error}`,
    contentNodeHeader: 'Inhaltsknoten',
    contentRoot: 'Standardberechtigungen',
    expand: 'Aufklappen',
    collapse: 'Zuklappen',

    // ── Berechtigungsdialog ───────────────────────────────────────────────
    dialogHeadline: (verb: string) => `Berechtigung festlegen: ${verb}`,
    dialogNodeLabel: 'Knoten',
    thisNodeSection: 'Dieser Knoten',
    descendantsSection: 'Untergeordnete Knoten',
    sameAsNode: 'Wie Knoten',
    virtualRootInherit: 'Nicht festgelegt (Eintrag entfernen)',
    virtualRootAllow: 'Erlauben (gesamter Inhalt)',
    virtualRootDeny: 'Verweigern (gesamter Inhalt)',

    // ── Zugriffsanzeige ───────────────────────────────────────────────────
    viewerHeadline: 'Zugriffsanzeige',
    byRole: 'Nach Rolle',
    byUser: 'Nach Benutzer',
    chooseRole: 'Rolle ausw\u00e4hlen',
    chooseUser: 'Benutzer ausw\u00e4hlen',
    selectSubjectPrompt: 'W\u00e4hlen Sie eine Rolle oder einen Benutzer aus, um die effektiven Berechtigungen anzuzeigen.',
    legendAllow: 'Erlauben',
    legendDeny: 'Verweigern',
    clickForReasoning: (label: string) => `${label} \u2014 Klicken f\u00fcr Begr\u00fcndung`,

    // ── Rollenauswahlmodal ─────────────────────────────────────────────────
    rolePickerHeadline: 'Rolle ausw\u00e4hlen',
    rolePickerFilter: 'Rollen filtern\u2026',
    rolePickerNoResults: 'Keine Rollen gefunden.',
    rolePickerNameHeader: 'Rolle',

    // ── Benutzerauswahlmodal ───────────────────────────────────────────────
    userPickerHeadline: 'Benutzer ausw\u00e4hlen',
    userPickerFilter: 'Benutzer filtern\u2026',
    userPickerNoResults: 'Keine Benutzer gefunden.',
    userPickerNameHeader: 'Benutzer',

    // ── Begr\u00fcndungsdialog ──────────────────────────────────────────────
    reasoningHeadline: (verb: string) => `Berechtigungsbegr\u00fcndung: ${verb}`,
    reasoningNodeLabel: 'Knoten',
    resultAllowed: 'Erlaubt',
    resultDenied: 'Verweigert',
    resultExplicit: 'explizit (direkt an diesem Knoten festgelegt)',
    resultImplicit: 'implizit (vererbt oder aus Gruppenvorgaben)',
    contributingFactors: 'Beitragende Faktoren:',
    groupDefault: 'Gruppenvorgabe',
    fromNode: 'von Knoten',
    inherited: '(vererbt)',
    noReasoningData: 'Keine effektiven Berechtigungsdaten f\u00fcr dieses Recht verf\u00fcgbar.',
    noReasoningEntries: 'Keine expliziten Eintr\u00e4ge gefunden \u2014 effektive Berechtigung stammt aus Systemvorgaben.',

    // ── Weiterleitungsnachricht granulare Berechtigungen ──────────────────
    redirectMessage:
      'Dokumentberechtigungen f\u00fcr diese Benutzergruppe werden durch das Advanced Permissions-Paket verwaltet. \u00d6ffnen Sie den Berechtigungseditor im Benutzerbereich, um Berechtigungen zu konfigurieren.',
  },
} satisfies UmbLocalizationDictionary;
