import type { UmbLocalizationDictionary } from '@umbraco-cms/backoffice/localization-api';

export default {
  uap: {
    // ── Navegação ─────────────────────────────────────────────────────────
    sectionLabel: 'Permissões avançadas',
    editorsSectionLabel: 'Editores',
    viewersSectionLabel: 'Visualizadores',
    permissionsEditor: 'Editor de permissões de conteúdo',
    accessViewer: 'Visualizador de acessos',

    // ── Comum ─────────────────────────────────────────────────────────────
    roleLabel: 'Grupo de utilizadores',
    rolePlaceholder: '— Selecione um grupo de utilizadores —',
    userLabel: 'Utilizador',
    saveChanges: 'Guardar alterações',
    discard: 'Descartar',
    cancel: 'Cancelar',
    apply: 'Aplicar',
    close: 'Fechar',
    inherit: 'Herdar',
    allow: 'Permitir',
    deny: 'Negar',
    umbracoUsers: 'Todos os utilizadores',

    // ── Editor de permissões ─────────────────────────────────────────────
    editorHeadline: 'Editor de permissões de conteúdo',
    selectRolePrompt: 'Selecione acima um grupo de utilizadores para gerir as suas permissões.',
    permissionsSaved: 'Permissões guardadas.',
    saveFailed: (error: string) => `Falha ao guardar: ${error}`,
    contentNodeHeader: 'Nó de conteúdo',
    contentRoot: 'Permissões predefinidas',
    expand: 'Expandir',
    collapse: 'Recolher',

    // ── Diálogo de permissão ──────────────────────────────────────────────
    dialogHeadline: (verb: string, nodeName: string) => `Definir permissão de ${verb} para ‘${nodeName}’`,
    descendantsSection: 'Descendentes (se diferente)',
    dialogInstructions: 'Defina a permissão para este nó. Por predefinição, esta também se aplica a todos os descendentes. Utilize “Descendentes (se diferente)” para definir uma permissão diferente para os nós descendentes.',
    virtualRootInherit: 'Não definido (remover entrada)',
    virtualRootAllow: 'Permitir (todo o conteúdo)',
    virtualRootDeny: 'Negar (todo o conteúdo)',
    dialogResult: 'Resultado',
    previewBothInherit: 'Nenhuma permissão definida. Herda do nó superior.',
    previewUniform: (action: string) => `${action} neste nó e em todos os descendentes.`,
    previewNodeOnly: (action: string) => `${action} apenas neste nó. Os descendentes não são afetados por esta regra.`,
    previewDescOnly: (action: string) => `Nenhuma permissão explícita neste nó. ${action} em todos os descendentes.`,
    previewSplit: (nodeAction: string, descAction: string) => `${nodeAction} neste nó. ${descAction} em todos os descendentes.`,
    previewVirtualInherit: 'Nenhuma permissão predefinida definida.',
    previewVirtualSet: (action: string) => `${action} por predefinição para todo o conteúdo.`,
    previewPriorityNode: 'A substituição de prioridade está ativada para este nó.',
    previewPriorityDesc: 'A substituição de prioridade está ativada para os descendentes.',
    previewPriorityBoth: 'A substituição de prioridade está ativada.',

    // ── Substituição de prioridade ────────────────────────────────────
    priorityOverride: 'Substituição de prioridade',
    priorityOverrideTooltip: (permission: string, nodeName: string) =>
      `Um utilizador pode pertencer a vários grupos de utilizadores. Normalmente, a permissão efetiva segue uma ordem de prioridade fixa. Ao selecionar esta caixa, essa ordem é substituída, pelo que a definição “${permission}” que escolher aqui passa quase sempre a ser o resultado para “${nodeName}”, independentemente dos restantes grupos do utilizador. Utilize com moderação.`,
    priorityOverrideBadgeTitle: 'A substituição de prioridade está ativada para esta entrada',
    priorityOverrideWonTitle: 'Resolvido através da substituição de prioridade',
    priorityOverrideSuppressedHeader: 'A substituição de prioridade alterou o resultado',
    priorityOverrideSuppressedHint: 'Sem ela, o resultado teria sido:',

    // ── Visualizador de acessos ───────────────────────────────────────────
    viewerHeadline: 'Visualizador de acessos',
    byRole: 'Por grupo de utilizadores',
    byUser: 'Por utilizador',
    chooseRole: 'Escolher grupo de utilizadores',
    chooseUser: 'Escolher utilizador',
    selectSubjectPrompt: 'Selecione um grupo de utilizadores ou um utilizador para ver as permissões efetivas.',
    legendAllow: 'Permitir',
    legendDeny: 'Negar',
    clickForReasoning: (label: string) => `${label} — clique para ver a justificação`,
    subjectOr: 'ou',

    // ── Modal de seleção de grupo de utilizadores ─────────────────────────
    rolePickerHeadline: 'Selecione um grupo de utilizadores',
    rolePickerFilter: 'Escreva para filtrar…',
    rolePickerNoResults: 'Nenhum grupo de utilizadores corresponde ao filtro.',
    rolePickerNameHeader: 'Grupo de utilizadores',

    // ── Modal de seleção de utilizador ────────────────────────────────────
    userPickerHeadline: 'Selecione um utilizador',
    userPickerFilter: 'Escreva para filtrar…',
    userPickerNoResults: 'Nenhum utilizador corresponde ao filtro.',
    userPickerNameHeader: 'Utilizador',

    // ── Diálogo de justificação ───────────────────────────────────────────
    reasoningHeadline: (verb: string, nodeName: string) => `Permissão de ${verb} para “${nodeName}”`,
    effectiveAllowed: (subject: string, verb: string, nodeName: string) =>
      `${subject} recebeu a permissão de ${verb} para “${nodeName}”.`,
    effectiveDenied: (subject: string, verb: string, nodeName: string) =>
      `${subject} teve a permissão de ${verb} negada para “${nodeName}”.`,
    dialogSecurityHeader: 'Segurança',
    defaultPermissions: 'Permissões predefinidas',
    determiningEntry: 'Esta entrada tem precedência',
    noReasoningData: 'Não existem dados de permissão disponíveis para este verbo.',
    defaultAllowNote: 'Não estão definidas permissões; isto é permitido por predefinição.',
    defaultDenyNote: 'Não estão definidas permissões; isto é negado por predefinição.',

    // ── Mensagem de redirecionamento de permissões granulares ─────────────
    redirectMessage:
      'As permissões de documento para este grupo de utilizadores são geridas pelo pacote Advanced Permissions. Abra o Editor de permissões na secção Utilizadores para configurar as permissões.',

    // ── Permissões de tipo de documento ───────────────────────────────────
    role: 'Grupo de utilizadores',
    pickRole: 'Escolher grupo de utilizadores',
    user: 'Utilizador',
    pickUser: 'Escolher utilizador',
    node: 'Nó',
    pickNode: 'Escolher nó',
    state: 'Estado',
    scope: 'Âmbito',
    scope_thisNodeOnly: 'Apenas este nó',
    scope_thisNodeAndDescendants: 'Este nó e descendentes',
    scope_descendantsOnly: 'Apenas descendentes',

    docTypePermissions_menuLabel: 'Editor de permissões de tipo de documento',
    docTypePermissions_insertOptionsMenuLabel: 'Visualizador de opções de inserção',
    docTypePermissions_workspaceTitle: 'Editor de permissões de tipo de documento',
    docTypePermissions_auditTitle: 'Visualizador de opções de inserção',
    docTypePermissions_allDocTypes: 'Todos os tipos de documento',
    docTypePermissions_verbInsert: 'Inserir',
    docTypePermissions_documentType: 'Tipo de Documento',
    chooseDocType: 'Escolher tipo de documento',
    notAnInsertOption: 'Este tipo de documento não é atualmente uma opção de inserção neste nó.',
    notAnInsertOptionAllowedNote: 'Este tipo de documento não é uma opção de inserção neste nó, mas caso contrário seria permitido.',
    notAnInsertOptionDeniedNote: 'Este tipo de documento não é uma opção de inserção neste nó, mas caso contrário seria negado.',
    docTypePermissions_pickDocType: '— Selecione um tipo de documento —',
    docTypePermissions_pickToStart: 'Escolha um grupo de utilizadores e um tipo de documento para começar.',
    docTypePermissions_defaultRowLabel: 'Predefinição (aplica-se em todo o lado)',
    docTypePermissions_pendingNodeLabel: '(nó não guardado)',
    docTypePermissions_addScopeNode: 'Adicionar substituição de âmbito',
    docTypePermissions_notSet: 'Não definido',
    docTypePermissions_noResults: 'Nenhum tipo de documento encontrado.',
    docTypePermissions_useRoot: 'Usar raiz',
    docTypePermissions_pickedNode: 'Nó:',
    docTypePermissions_rootLevel: 'Nível de raiz',
    docTypePermissions_reasoning: 'Justificação',
    docTypePermissions_defaultAllow: 'Permitido por predefinição',
    docTypePermissions_viaDefault: 'a partir da linha predefinida',
  },
} satisfies UmbLocalizationDictionary;
