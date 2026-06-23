import type { UmbLocalizationDictionary } from '@umbraco-cms/backoffice/localization-api';

export default {
  uap: {
    // ── Navegação ─────────────────────────────────────────────────────────
    sectionLabel: 'Permissões Avançadas',
    editorsSectionLabel: 'Editores',
    viewersSectionLabel: 'Visualizações',
    permissionsEditor: 'Editor de Permissões de Conteúdo',
    accessViewer: 'Visualizador de Acesso',

    // ── Comum ─────────────────────────────────────────────────────────────
    roleLabel: 'Grupo de usuários',
    rolePlaceholder: '— Selecione um grupo de usuários —',
    userLabel: 'Usuário',
    saveChanges: 'Salvar alterações',
    discard: 'Descartar',
    cancel: 'Cancelar',
    apply: 'Aplicar',
    close: 'Fechar',
    inherit: 'Herdar',
    allow: 'Permitir',
    deny: 'Negar',
    umbracoUsers: 'Todos os usuários',

    // ── Help ──────────────────────────────────────────────────────────────
    help_contentPermissions_description: 'Gerencie permissões de Permitir/Negar por grupo de usuários em toda a sua árvore de conteúdo.',
    help_accessViewer_description: 'Veja as permissões efetivas que um usuário ou grupo tem em qualquer nó, com o raciocínio completo.',
    help_docTypePermissions_description: 'Decida quais tipos de documento cada grupo de usuários pode criar, e onde.',
    help_insertOptions_description: 'Audite quais tipos de documento um usuário ou grupo de usuários pode criar em cada nó.',
    help_learnMore: 'Saiba mais',
    help_modalTitle: 'Ajuda',
    help_tabAbout: 'Sobre esta página',
    help_tabConcepts: 'Conceitos',
    help_concept_scope_tip: 'O escopo controla até onde uma regra alcança: este nó, este nó e seus descendentes, ou apenas os descendentes.',
    help_concept_priorityOverride_tip: 'A substituição de prioridade força esta entrada a prevalecer sobre a ordem de resolução normal.',
    help_concept_allowDeny_tip: 'Permitir concede a permissão, Negar a revoga, e deixá-la indefinida herda do ancestral mais próximo.',
    help_concept_reasoning_tip: 'Clique em qualquer célula para ver exatamente como a permissão foi resolvida.',

    // ── Editor de permissões ─────────────────────────────────────────────
    editorHeadline: 'Editor de Permissões de Conteúdo',
    selectRolePrompt: 'Selecione um grupo de usuários acima para gerenciar suas permissões.',
    permissionsSaved: 'Permissões salvas.',
    saveFailed: (error: string) => `Falha ao salvar: ${error}`,
    contentNodeHeader: 'Nó de conteúdo',
    contentRoot: 'Permissões padrão',
    expand: 'Expandir',
    collapse: 'Recolher',

    // ── Diálogo de permissão ──────────────────────────────────────────────
    dialogHeadline: (verb: string, nodeName: string) => `Definir permissão de ${verb} para ‘${nodeName}’`,
    descendantsSection: 'Descendentes (se diferente)',
    dialogInstructions: 'Defina a permissão para este nó. Por padrão, isso também se aplica a todos os descendentes. Use “Descendentes (se diferente)” para definir uma permissão diferente para os nós descendentes.',
    virtualRootInherit: 'Não definido (remover entrada)',
    virtualRootAllow: 'Permitir (todo o conteúdo)',
    virtualRootDeny: 'Negar (todo o conteúdo)',
    dialogResult: 'Resultado',
    previewBothInherit: 'Nenhuma permissão definida. Herda do pai.',
    previewUniform: (action: string) => `${action} neste nó e em todos os descendentes.`,
    previewNodeOnly: (action: string) => `${action} apenas neste nó. Os descendentes não são afetados por esta regra.`,
    previewDescOnly: (action: string) => `Nenhuma permissão explícita neste nó. ${action} em todos os descendentes.`,
    previewSplit: (nodeAction: string, descAction: string) => `${nodeAction} neste nó. ${descAction} em todos os descendentes.`,
    previewVirtualInherit: 'Nenhuma permissão padrão definida.',
    previewVirtualSet: (action: string) => `${action} por padrão para todo o conteúdo.`,
    previewPriorityNode: 'A substituição de prioridade está definida neste nó.',
    previewPriorityDesc: 'A substituição de prioridade está definida nos descendentes.',
    previewPriorityBoth: 'A substituição de prioridade está definida.',

    // ── Substituição de prioridade ───────────────────────────────────────
    priorityOverride: 'Substituição de prioridade',
    priorityOverrideTooltip: (permission: string, nodeName: string) =>
      `Um usuário pode pertencer a vários grupos de usuários. Normalmente, a permissão efetiva segue uma ordem de prioridade fixa. Marcar esta caixa substitui essa ordem, de modo que a configuração “${permission}” escolhida aqui quase sempre se torna o resultado para “${nodeName}”, independentemente dos outros grupos do usuário. Use com moderação.`,
    priorityOverrideBadgeTitle: 'A substituição de prioridade está definida nesta entrada',
    priorityOverrideWonTitle: 'Resolvido por substituição de prioridade',
    priorityOverrideSuppressedHeader: 'A substituição de prioridade alterou o resultado',
    priorityOverrideSuppressedHint: 'Sem ela, o resultado teria sido:',

    // ── Visualizador de acesso ────────────────────────────────────────────
    viewerHeadline: 'Visualizador de Acesso',
    byRole: 'Por grupo de usuários',
    byUser: 'Por usuário',
    chooseRole: 'Escolher grupo de usuários',
    chooseUser: 'Escolher usuário',
    selectSubjectPrompt: 'Selecione um grupo de usuários ou um usuário para ver as permissões efetivas.',
    legendAllow: 'Permitir',
    legendDeny: 'Negar',
    clickForReasoning: (label: string) => `${label} — clique para ver o motivo`,
    subjectOr: 'ou',

    // ── Modal de seleção de grupo ─────────────────────────────────────────
    rolePickerHeadline: 'Selecione um grupo de usuários',
    rolePickerFilter: 'Digite para filtrar…',
    rolePickerNoResults: 'Nenhum grupo de usuários corresponde ao filtro.',
    rolePickerNameHeader: 'Grupo de usuários',

    // ── Modal de seleção de usuário ───────────────────────────────────────
    userPickerHeadline: 'Selecione um usuário',
    userPickerFilter: 'Digite para filtrar…',
    userPickerNoResults: 'Nenhum usuário corresponde ao filtro.',
    userPickerNameHeader: 'Usuário',

    // ── Diálogo de motivo ─────────────────────────────────────────────────
    reasoningHeadline: (verb: string, nodeName: string) => `Permissão de ${verb} para “${nodeName}”`,
    effectiveAllowed: (subject: string, verb: string, nodeName: string) =>
      `${subject} recebeu permissão de ${verb} para “${nodeName}”.`,
    effectiveDenied: (subject: string, verb: string, nodeName: string) =>
      `${subject} teve a permissão de ${verb} negada para “${nodeName}”.`,
    dialogSecurityHeader: 'Segurança',
    defaultPermissions: 'Permissões padrão',
    determiningEntry: 'Esta entrada tem precedência',
    noReasoningData: 'Nenhum dado de permissão disponível para este verbo.',
    defaultAllowNote: 'Nenhuma permissão definida; isto é permitido por padrão.',
    defaultDenyNote: 'Nenhuma permissão definida; isto é negado por padrão.',

    // ── Redirecionamento de permissões granulares ────────────────────────
    redirectMessage:
      'As permissões de documento para este grupo de usuários são gerenciadas pelo pacote Advanced Permissions. Abra o Editor de Permissões na seção Usuários para configurar as permissões.',

    // ── Permissões por tipo de documento ──────────────────────────────────
    role: 'Grupo de usuários',
    pickRole: 'Escolher grupo de usuários',
    user: 'Usuário',
    pickUser: 'Escolher usuário',
    node: 'Nó',
    pickNode: 'Escolher nó',
    state: 'Status',
    scope: 'Escopo',
    scope_thisNodeOnly: 'Apenas este nó',
    scope_thisNodeAndDescendants: 'Este nó e os descendentes',
    scope_descendantsOnly: 'Apenas os descendentes',

    docTypePermissions_menuLabel: 'Editor de Permissões de Tipo de Documento',
    docTypePermissions_insertOptionsMenuLabel: 'Visualizador de Opções de Inserção',
    docTypePermissions_workspaceTitle: 'Editor de Permissões de Tipo de Documento',
    docTypePermissions_auditTitle: 'Visualizador de Opções de Inserção',
    docTypePermissions_allDocTypes: 'Todos os tipos de documento',
    docTypePermissions_verbInsert: 'Inserir',
    docTypePermissions_documentType: 'Tipo de Documento',
    chooseDocType: 'Escolher tipo de documento',
    notAnInsertOption: 'Este tipo de documento não é uma opção de inserção neste nó no momento.',
    notAnInsertOptionAllowedNote: 'Este tipo de documento não é uma opção de inserção neste nó, mas seria permitido de outra forma.',
    notAnInsertOptionDeniedNote: 'Este tipo de documento não é uma opção de inserção neste nó, mas seria negado de outra forma.',
    docTypePermissions_pickDocType: '— Selecione um tipo de documento —',
    docTypePermissions_pickToStart: 'Escolha um grupo de usuários e um tipo de documento para começar.',
    docTypePermissions_defaultRowLabel: 'Padrão (aplica-se em todos os lugares)',
    docTypePermissions_pendingNodeLabel: '(nó não salvo)',
    docTypePermissions_addScopeNode: 'Adicionar substituição de escopo',
    docTypePermissions_notSet: 'Não definido',
    docTypePermissions_noResults: 'Nenhum tipo de documento encontrado.',
    docTypePermissions_useRoot: 'Usar raiz',
    docTypePermissions_pickedNode: 'Nó:',
    docTypePermissions_rootLevel: 'Nível raiz',
    docTypePermissions_reasoning: 'Motivo',
    docTypePermissions_defaultAllow: 'Permitido por padrão',
    docTypePermissions_viaDefault: 'da linha padrão',
  },
} satisfies UmbLocalizationDictionary;
