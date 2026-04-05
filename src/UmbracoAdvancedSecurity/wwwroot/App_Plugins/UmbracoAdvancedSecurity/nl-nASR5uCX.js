const n = {
  uas: {
    // ── Navigatie ─────────────────────────────────────────────────────────
    sectionLabel: "Geavanceerde beveiliging",
    securityEditor: "Beveiligingseditor",
    accessViewer: "Toegangsweergave",
    // ── Algemeen ──────────────────────────────────────────────────────────
    roleLabel: "Rol",
    rolePlaceholder: "— Selecteer een rol —",
    userLabel: "Gebruiker",
    saveChanges: "Wijzigingen opslaan",
    discard: "Verwerpen",
    cancel: "Annuleren",
    apply: "Toepassen",
    close: "Sluiten",
    inherit: "Overnemen",
    allow: "Toestaan",
    deny: "Weigeren",
    everyoneSuffix: "(Iedereen)",
    // ── Beveiligingseditor ────────────────────────────────────────────────
    editorHeadline: "Beveiligingseditor",
    selectRolePrompt: "Selecteer hierboven een rol om de machtigingen te beheren.",
    permissionsSaved: "Machtigingen opgeslagen.",
    saveFailed: (e) => `Opslaan mislukt: ${e}`,
    contentNodeHeader: "Contentpagina",
    contentRoot: "Standaardmachtigingen",
    expand: "Uitvouwen",
    collapse: "Invouwen",
    // ── Machtigingsdialoog ────────────────────────────────────────────────
    dialogHeadline: (e) => `Machtiging instellen: ${e}`,
    dialogNodeLabel: "Pagina",
    thisNodeSection: "Deze pagina",
    descendantsSection: "Onderliggende pagina’s",
    sameAsNode: "Zelfde als pagina",
    virtualRootInherit: "Niet ingesteld (verwijder vermelding)",
    virtualRootAllow: "Toestaan (alle content)",
    virtualRootDeny: "Weigeren (alle content)",
    // ── Toegangsweergave ──────────────────────────────────────────────────
    viewerHeadline: "Toegangsweergave",
    byRole: "Op rol",
    byUser: "Op gebruiker",
    selectSubjectPrompt: "Selecteer een rol of gebruiker om de effectieve machtigingen te bekijken.",
    legendAllow: "Toestaan",
    legendDeny: "Weigeren",
    clickForReasoning: (e) => `${e} — klik voor onderbouwing`,
    // ── Onderbouwingsdialoog ──────────────────────────────────────────────
    reasoningHeadline: (e) => `Machtigingsonderbouwing: ${e}`,
    reasoningNodeLabel: "Pagina",
    resultAllowed: "Toegestaan",
    resultDenied: "Geweigerd",
    resultExplicit: "expliciet (direct ingesteld op deze pagina)",
    resultImplicit: "impliciet (overgenomen of vanuit groepsstandaarden)",
    contributingFactors: "Bijdragende factoren:",
    groupDefault: "groepsstandaard",
    fromNode: "van pagina",
    inherited: "(overgenomen)",
    noReasoningData: "Geen effectieve machtigingsgegevens beschikbaar voor dit recht.",
    noReasoningEntries: "Geen expliciete vermeldingen gevonden — effectieve machtiging komt van systeemstandaarden.",
    // ── Omleidingsbericht granulaire machtigingen ─────────────────────────
    redirectMessage: "Documentmachtigingen voor deze gebruikersgroep worden beheerd door het Advanced Security-pakket. Open de Beveiligingseditor in de sectie Gebruikers om machtigingen te configureren."
  }
};
export {
  n as default
};
//# sourceMappingURL=nl-nASR5uCX.js.map
