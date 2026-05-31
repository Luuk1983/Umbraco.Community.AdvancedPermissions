import type { UmbLocalizationDictionary } from '@umbraco-cms/backoffice/localization-api';

export default {
  uap: {
    // ── Navigation ────────────────────────────────────────────────────────
    sectionLabel: 'Permissions avancées',
    editorsSectionLabel: 'Éditeurs',
    viewersSectionLabel: 'Visionneuses',
    permissionsEditor: 'Éditeur de permissions de contenu',
    accessViewer: 'Visionneuse d’accès',

    // ── Commun ────────────────────────────────────────────────────────────
    roleLabel: 'Groupe d’utilisateurs',
    rolePlaceholder: '— Sélectionnez un groupe d’utilisateurs —',
    userLabel: 'Utilisateur',
    saveChanges: 'Enregistrer les modifications',
    discard: 'Abandonner',
    cancel: 'Annuler',
    apply: 'Appliquer',
    close: 'Fermer',
    inherit: 'Hériter',
    allow: 'Autoriser',
    deny: 'Refuser',
    umbracoUsers: 'Tous les utilisateurs',

    // ── Éditeur de permissions ───────────────────────────────────────────
    editorHeadline: 'Éditeur de permissions de contenu',
    selectRolePrompt: 'Sélectionnez un groupe d’utilisateurs ci-dessus pour gérer ses permissions.',
    permissionsSaved: 'Permissions enregistrées.',
    saveFailed: (error: string) => `Échec de l’enregistrement : ${error}`,
    contentNodeHeader: 'Nœud de contenu',
    contentRoot: 'Permissions par défaut',
    expand: 'Développer',
    collapse: 'Réduire',

    // ── Boîte de dialogue des permissions ─────────────────────────────────
    dialogHeadline: (verb: string, nodeName: string) => `Définir la permission ${verb} pour ‘${nodeName}’`,
    descendantsSection: 'Descendants (si différent)',
    dialogInstructions: 'Définissez la permission pour ce nœud. Par défaut, elle s’applique également à tous les descendants. Utilisez “Descendants (si différent)” pour définir une permission différente pour les nœuds descendants.',
    virtualRootInherit: 'Non défini (supprimer l’entrée)',
    virtualRootAllow: 'Autoriser (tout le contenu)',
    virtualRootDeny: 'Refuser (tout le contenu)',
    dialogResult: 'Résultat',
    previewBothInherit: 'Aucune permission définie. Héritée du parent.',
    previewUniform: (action: string) => `${action} sur ce nœud et tous les descendants.`,
    previewNodeOnly: (action: string) => `${action} sur ce nœud uniquement. Les descendants ne sont pas affectés par cette règle.`,
    previewDescOnly: (action: string) => `Aucune permission explicite sur ce nœud. ${action} sur tous les descendants.`,
    previewSplit: (nodeAction: string, descAction: string) => `${nodeAction} sur ce nœud. ${descAction} sur tous les descendants.`,
    previewVirtualInherit: 'Aucune permission par défaut définie.',
    previewVirtualSet: (action: string) => `${action} par défaut pour tout le contenu.`,
    previewPriorityNode: 'Le remplacement prioritaire est défini sur ce nœud.',
    previewPriorityDesc: 'Le remplacement prioritaire est défini sur les descendants.',
    previewPriorityBoth: 'Le remplacement prioritaire est défini.',

    // ── Remplacement prioritaire ──────────────────────────────────────
    priorityOverride: 'Remplacement prioritaire',
    priorityOverrideTooltip: (permission: string, nodeName: string) =>
      `Un utilisateur peut appartenir à plusieurs groupes d’utilisateurs. Normalement, la permission effective suit un ordre de priorité fixe. Cocher cette case remplace cet ordre, de sorte que le paramètre “${permission}” que vous choisissez ici devient presque toujours le résultat pour “${nodeName}”, indépendamment des autres groupes de l’utilisateur. À utiliser avec parcimonie.`,
    priorityOverrideBadgeTitle: 'Le remplacement prioritaire est défini sur cette entrée',
    priorityOverrideWonTitle: 'Résolu via le remplacement prioritaire',
    priorityOverrideSuppressedHeader: 'Le remplacement prioritaire a modifié le résultat',
    priorityOverrideSuppressedHint: 'Sans lui, le résultat aurait été :',

    // ── Visionneuse d’accès ──────────────────────────────────────────────
    viewerHeadline: 'Visionneuse d’accès',
    byRole: 'Par groupe d’utilisateurs',
    byUser: 'Par utilisateur',
    chooseRole: 'Choisir un groupe d’utilisateurs',
    chooseUser: 'Choisir un utilisateur',
    selectSubjectPrompt: 'Sélectionnez un groupe d’utilisateurs ou un utilisateur pour afficher les permissions effectives.',
    legendAllow: 'Autoriser',
    legendDeny: 'Refuser',
    clickForReasoning: (label: string) => `${label} — cliquez pour la justification`,
    subjectOr: 'ou',

    // ── Modale de sélection de groupe d’utilisateurs ────────────────────────
    rolePickerHeadline: 'Sélectionnez un groupe d’utilisateurs',
    rolePickerFilter: 'Saisissez pour filtrer…',
    rolePickerNoResults: 'Aucun groupe d’utilisateurs ne correspond au filtre.',
    rolePickerNameHeader: 'Groupe d’utilisateurs',

    // ── Modale de sélection d’utilisateur ───────────────────────────────────
    userPickerHeadline: 'Sélectionnez un utilisateur',
    userPickerFilter: 'Saisissez pour filtrer…',
    userPickerNoResults: 'Aucun utilisateur ne correspond au filtre.',
    userPickerNameHeader: 'Utilisateur',

    // ── Boîte de dialogue de justification ──────────────────────────────────
    reasoningHeadline: (verb: string, nodeName: string) => `Permission ${verb} pour “${nodeName}”`,
    effectiveAllowed: (subject: string, verb: string, nodeName: string) =>
      `${subject} a obtenu la permission ${verb} pour “${nodeName}”.`,
    effectiveDenied: (subject: string, verb: string, nodeName: string) =>
      `${subject} s’est vu refuser la permission ${verb} pour “${nodeName}”.`,
    dialogSecurityHeader: 'Sécurité',
    defaultPermissions: 'Permissions par défaut',
    determiningEntry: 'Cette entrée a la priorité',
    noReasoningData: 'Aucune donnée de permission disponible pour ce droit.',
    defaultAllowNote: 'Aucune permission n’est définie, ceci est autorisé par défaut.',
    defaultDenyNote: 'Aucune permission n’est définie, ceci est refusé par défaut.',

    // ── Message de redirection des permissions granulaires ────────────────
    redirectMessage:
      'Les permissions de document pour ce groupe d’utilisateurs sont gérées par le package Advanced Permissions. Ouvrez l’Éditeur de permissions dans la section Utilisateurs pour configurer les permissions.',

    // ── Permissions de type de document ───────────────────────────────────
    role: 'Groupe d’utilisateurs',
    pickRole: 'Choisir un groupe d’utilisateurs',
    user: 'Utilisateur',
    pickUser: 'Choisir un utilisateur',
    node: 'Nœud',
    pickNode: 'Choisir un nœud',
    state: 'Statut',
    scope: 'Portée',
    scope_thisNodeOnly: 'Ce nœud uniquement',
    scope_thisNodeAndDescendants: 'Ce nœud et les descendants',
    scope_descendantsOnly: 'Descendants uniquement',

    docTypePermissions_menuLabel: 'Éditeur de permissions de type de document',
    docTypePermissions_insertOptionsMenuLabel: 'Visionneuse des options d’insertion',
    docTypePermissions_workspaceTitle: 'Éditeur de permissions de type de document',
    docTypePermissions_auditTitle: 'Visionneuse des options d’insertion',
    docTypePermissions_allDocTypes: 'Tous les types de document',
    docTypePermissions_verbInsert: 'Insérer',
    docTypePermissions_documentType: 'Type de document',
    chooseDocType: 'Choisir un type de document',
    notAnInsertOption: 'Ce type de document n’est actuellement pas une option d’insertion sur ce nœud.',
    notAnInsertOptionAllowedNote: 'Ce type de document n’est pas une option d’insertion sur ce nœud, mais il serait sinon autorisé.',
    notAnInsertOptionDeniedNote: 'Ce type de document n’est pas une option d’insertion sur ce nœud, mais il serait sinon refusé.',
    docTypePermissions_pickDocType: '— Sélectionnez un type de document —',
    docTypePermissions_pickToStart: 'Choisissez un groupe d’utilisateurs et un type de document pour commencer.',
    docTypePermissions_defaultRowLabel: 'Par défaut (s’applique partout)',
    docTypePermissions_pendingNodeLabel: '(nœud non enregistré)',
    docTypePermissions_addScopeNode: 'Ajouter un remplacement de portée',
    docTypePermissions_notSet: 'Non défini',
    docTypePermissions_noResults: 'Aucun type de document trouvé.',
    docTypePermissions_useRoot: 'Utiliser la racine',
    docTypePermissions_pickedNode: 'Nœud :',
    docTypePermissions_rootLevel: 'Niveau racine',
    docTypePermissions_reasoning: 'Justification',
    docTypePermissions_defaultAllow: 'Autorisé par défaut',
    docTypePermissions_viaDefault: 'depuis la ligne par défaut',
  },
} satisfies UmbLocalizationDictionary;
