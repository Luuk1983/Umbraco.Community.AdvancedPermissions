const n = {
  uas: {
    // ── Navigation ────────────────────────────────────────────────────────
    sectionLabel: "Erweiterte Sicherheit",
    securityEditor: "Sicherheitseditor",
    accessViewer: "Zugriffsanzeige",
    // ── Allgemein ─────────────────────────────────────────────────────────
    roleLabel: "Rolle",
    rolePlaceholder: "— Rolle auswählen —",
    userLabel: "Benutzer",
    saveChanges: "Änderungen speichern",
    discard: "Verwerfen",
    cancel: "Abbrechen",
    apply: "Übernehmen",
    close: "Schließen",
    inherit: "Vererben",
    allow: "Erlauben",
    deny: "Verweigern",
    everyoneSuffix: "(Alle)",
    // ── Sicherheitseditor ─────────────────────────────────────────────────
    editorHeadline: "Sicherheitseditor",
    selectRolePrompt: "Wählen Sie oben eine Rolle aus, um deren Berechtigungen zu verwalten.",
    permissionsSaved: "Berechtigungen gespeichert.",
    saveFailed: (e) => `Speichern fehlgeschlagen: ${e}`,
    contentNodeHeader: "Inhaltsknoten",
    contentRoot: "Standardberechtigungen",
    expand: "Aufklappen",
    collapse: "Zuklappen",
    // ── Berechtigungsdialog ───────────────────────────────────────────────
    dialogHeadline: (e) => `Berechtigung festlegen: ${e}`,
    dialogNodeLabel: "Knoten",
    thisNodeSection: "Dieser Knoten",
    descendantsSection: "Untergeordnete Knoten",
    sameAsNode: "Wie Knoten",
    virtualRootInherit: "Nicht festgelegt (Eintrag entfernen)",
    virtualRootAllow: "Erlauben (gesamter Inhalt)",
    virtualRootDeny: "Verweigern (gesamter Inhalt)",
    // ── Zugriffsanzeige ───────────────────────────────────────────────────
    viewerHeadline: "Zugriffsanzeige",
    byRole: "Nach Rolle",
    byUser: "Nach Benutzer",
    selectSubjectPrompt: "Wählen Sie eine Rolle oder einen Benutzer aus, um die effektiven Berechtigungen anzuzeigen.",
    legendAllow: "Erlauben",
    legendDeny: "Verweigern",
    clickForReasoning: (e) => `${e} — Klicken für Begründung`,
    // ── Begr\u00fcndungsdialog ──────────────────────────────────────────────
    reasoningHeadline: (e) => `Berechtigungsbegründung: ${e}`,
    reasoningNodeLabel: "Knoten",
    resultAllowed: "Erlaubt",
    resultDenied: "Verweigert",
    resultExplicit: "explizit (direkt an diesem Knoten festgelegt)",
    resultImplicit: "implizit (vererbt oder aus Gruppenvorgaben)",
    contributingFactors: "Beitragende Faktoren:",
    groupDefault: "Gruppenvorgabe",
    fromNode: "von Knoten",
    inherited: "(vererbt)",
    noReasoningData: "Keine effektiven Berechtigungsdaten für dieses Recht verfügbar.",
    noReasoningEntries: "Keine expliziten Einträge gefunden — effektive Berechtigung stammt aus Systemvorgaben.",
    // ── Weiterleitungsnachricht granulare Berechtigungen ──────────────────
    redirectMessage: "Dokumentberechtigungen für diese Benutzergruppe werden durch das Advanced Security-Paket verwaltet. Öffnen Sie den Sicherheitseditor im Benutzerbereich, um Berechtigungen zu konfigurieren."
  }
};
export {
  n as default
};
//# sourceMappingURL=de-CYWjgqlm.js.map
