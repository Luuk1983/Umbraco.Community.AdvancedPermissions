import type { UmbLocalizationDictionary } from '@umbraco-cms/backoffice/localization-api';

export default {
  uas: {
    // ── Navigation ────────────────────────────────────────────────────────
    sectionLabel: 'Erweiterte Sicherheit',
    securityEditor: 'Sicherheitseditor',
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

    // ── Sicherheitseditor ─────────────────────────────────────────────────
    editorHeadline: 'Sicherheitseditor',
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
    selectSubjectPrompt: 'W\u00e4hlen Sie eine Rolle oder einen Benutzer aus, um die effektiven Berechtigungen anzuzeigen.',
    legendAllow: 'Erlauben',
    legendDeny: 'Verweigern',
    clickForReasoning: (label: string) => `${label} \u2014 Klicken f\u00fcr Begr\u00fcndung`,

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
      'Dokumentberechtigungen f\u00fcr diese Benutzergruppe werden durch das Advanced Security-Paket verwaltet. \u00d6ffnen Sie den Sicherheitseditor im Benutzerbereich, um Berechtigungen zu konfigurieren.',
  },
} satisfies UmbLocalizationDictionary;
