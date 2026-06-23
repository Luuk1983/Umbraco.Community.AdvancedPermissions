import type { UmbLocalizationDictionary } from '@umbraco-cms/backoffice/localization-api';

export default {
  uap: {
    // ── Навигация ─────────────────────────────────────────────────────────
    sectionLabel: 'Расширенные разрешения',
    editorsSectionLabel: 'Редакторы',
    viewersSectionLabel: 'Просмотрщики',
    permissionsEditor: 'Редактор разрешений на содержимое',
    accessViewer: 'Просмотр доступа',

    // ── Общее ─────────────────────────────────────────────────────────────
    roleLabel: 'Группа пользователей',
    rolePlaceholder: '— Выберите группу пользователей —',
    userLabel: 'Пользователь',
    saveChanges: 'Сохранить изменения',
    discard: 'Отклонить',
    cancel: 'Отмена',
    apply: 'Применить',
    close: 'Закрыть',
    inherit: 'Унаследовать',
    allow: 'Разрешить',
    deny: 'Запретить',
    umbracoUsers: 'Все пользователи',

    // ── Help ──────────────────────────────────────────────────────────────
    help_contentPermissions_description: 'Управляйте разрешениями Разрешить/Запретить для каждой группы пользователей по всему дереву содержимого.',
    help_accessViewer_description: 'Просматривайте эффективные разрешения пользователя или группы на любой странице с полным обоснованием.',
    help_docTypePermissions_description: 'Определяйте, какие типы документов может создавать каждая группа пользователей и где.',
    help_insertOptions_description: 'Проверяйте, какие типы документов пользователь или группа пользователей может создавать на каждой странице.',
    help_learnMore: 'Подробнее',
    help_modalTitle: 'Справка',
    help_tabAbout: 'Об этой странице',
    help_tabConcepts: 'Понятия',
    help_concept_scope_tip: 'Область применения определяет, как далеко распространяется правило: эта страница, эта страница и её дочерние страницы или только дочерние страницы.',
    help_concept_priorityOverride_tip: 'Приоритетное переопределение заставляет эту запись побеждать в обычном порядке разрешения.',
    help_concept_allowDeny_tip: 'Разрешить предоставляет разрешение, Запретить отзывает его, а если оставить незаданным — наследуется от ближайшей родительской страницы.',
    help_concept_reasoning_tip: 'Нажмите на любую ячейку, чтобы увидеть, как именно было разрешено разрешение.',

    // ── Редактор разрешений ───────────────────────────────────────────────
    editorHeadline: 'Редактор разрешений на содержимое',
    selectRolePrompt: 'Выберите группу пользователей выше, чтобы управлять её разрешениями.',
    permissionsSaved: 'Разрешения сохранены.',
    saveFailed: (error: string) => `Не удалось сохранить: ${error}`,
    contentNodeHeader: 'Страница содержимого',
    contentRoot: 'Разрешения по умолчанию',
    expand: 'Развернуть',
    collapse: 'Свернуть',

    // ── Диалог разрешения ─────────────────────────────────────────────────
    dialogHeadline: (verb: string, nodeName: string) => `Настроить разрешение «${verb}» для ‘${nodeName}’`,
    descendantsSection: 'Дочерние страницы (если отличаются)',
    dialogInstructions: 'Настройте разрешение для этой страницы. По умолчанию оно также применяется ко всем дочерним страницам. Используйте “Дочерние страницы (если отличаются)”, чтобы задать другое разрешение для дочерних страниц.',
    virtualRootInherit: 'Не задано (удалить запись)',
    virtualRootAllow: 'Разрешить (всё содержимое)',
    virtualRootDeny: 'Запретить (всё содержимое)',
    dialogResult: 'Результат',
    previewBothInherit: 'Разрешение не задано. Наследуется от родительской страницы.',
    previewUniform: (action: string) => `${action} для этой страницы и всех дочерних страниц.`,
    previewNodeOnly: (action: string) => `${action} только для этой страницы. На дочерние страницы это правило не влияет.`,
    previewDescOnly: (action: string) => `Нет явного разрешения для этой страницы. ${action} для всех дочерних страниц.`,
    previewSplit: (nodeAction: string, descAction: string) => `${nodeAction} для этой страницы. ${descAction} для всех дочерних страниц.`,
    previewVirtualInherit: 'Разрешение по умолчанию не задано.',
    previewVirtualSet: (action: string) => `${action} по умолчанию для всего содержимого.`,
    previewPriorityNode: 'Для этой страницы задано приоритетное переопределение.',
    previewPriorityDesc: 'Для дочерних страниц задано приоритетное переопределение.',
    previewPriorityBoth: 'Приоритетное переопределение задано.',

    // ── Приоритетное переопределение ──────────────────────────────────────
    priorityOverride: 'Приоритетное переопределение',
    priorityOverrideTooltip: (permission: string, nodeName: string) =>
      `Пользователь может входить в несколько групп пользователей. Обычно эффективное разрешение следует фиксированному порядку приоритета. Установка этого флажка переопределяет этот порядок, поэтому выбранная здесь настройка “${permission}” почти всегда становится результатом для “${nodeName}”, независимо от других групп пользователя. Используйте осторожно.`,
    priorityOverrideBadgeTitle: 'Для этой записи включено приоритетное переопределение',
    priorityOverrideWonTitle: 'Разрешено через приоритетное переопределение',
    priorityOverrideSuppressedHeader: 'Приоритетное переопределение изменило результат',
    priorityOverrideSuppressedHint: 'Без него результат был бы:',

    // ── Просмотр доступа ──────────────────────────────────────────────────
    viewerHeadline: 'Просмотр доступа',
    byRole: 'По группе пользователей',
    byUser: 'По пользователю',
    chooseRole: 'Выберите группу пользователей',
    chooseUser: 'Выберите пользователя',
    selectSubjectPrompt: 'Выберите группу пользователей или пользователя, чтобы просмотреть эффективные разрешения.',
    legendAllow: 'Разрешить',
    legendDeny: 'Запретить',
    clickForReasoning: (label: string) => `${label} — нажмите для обоснования`,
    subjectOr: 'или',

    // ── Модальное окно выбора группы ──────────────────────────────────────
    rolePickerHeadline: 'Выберите группу пользователей',
    rolePickerFilter: 'Вводите для фильтрации…',
    rolePickerNoResults: 'Нет групп пользователей, соответствующих фильтру.',
    rolePickerNameHeader: 'Группа пользователей',

    // ── Модальное окно выбора пользователя ────────────────────────────────
    userPickerHeadline: 'Выберите пользователя',
    userPickerFilter: 'Вводите для фильтрации…',
    userPickerNoResults: 'Нет пользователей, соответствующих фильтру.',
    userPickerNameHeader: 'Пользователь',

    // ── Диалог обоснования ────────────────────────────────────────────────
    reasoningHeadline: (verb: string, nodeName: string) => `Разрешение «${verb}» для “${nodeName}”`,
    effectiveAllowed: (subject: string, verb: string, nodeName: string) =>
      `${subject} разрешено разрешение «${verb}» для “${nodeName}”.`,
    effectiveDenied: (subject: string, verb: string, nodeName: string) =>
      `${subject} запрещено разрешение «${verb}» для “${nodeName}”.`,
    dialogSecurityHeader: 'Безопасность',
    defaultPermissions: 'Разрешения по умолчанию',
    determiningEntry: 'Эта запись имеет приоритет',
    noReasoningData: 'Нет данных о разрешениях для этого права.',
    defaultAllowNote: 'Разрешения не заданы, это разрешено по умолчанию.',
    defaultDenyNote: 'Разрешения не заданы, это запрещено по умолчанию.',

    // ── Сообщение о перенаправлении гранулярных разрешений ─────────────────
    redirectMessage:
      'Разрешения на документы для этой группы пользователей управляются пакетом Advanced Permissions. Откройте Редактор разрешений в разделе Пользователи, чтобы настроить разрешения.',

    // ── Разрешения для типов документов ───────────────────────────────────
    role: 'Группа пользователей',
    pickRole: 'Выберите группу пользователей',
    user: 'Пользователь',
    pickUser: 'Выберите пользователя',
    node: 'Страница',
    pickNode: 'Выберите страницу',
    state: 'Состояние',
    scope: 'Область применения',
    scope_thisNodeOnly: 'Только эта страница',
    scope_thisNodeAndDescendants: 'Эта страница и дочерние',
    scope_descendantsOnly: 'Только дочерние',

    docTypePermissions_menuLabel: 'Редактор разрешений для типов документов',
    docTypePermissions_insertOptionsMenuLabel: 'Просмотр вариантов вставки',
    docTypePermissions_workspaceTitle: 'Редактор разрешений для типов документов',
    docTypePermissions_auditTitle: 'Просмотр вариантов вставки',
    docTypePermissions_allDocTypes: 'Все типы документов',
    docTypePermissions_verbInsert: 'Вставить',
    docTypePermissions_documentType: 'Тип документа',
    chooseDocType: 'Выберите тип документа',
    notAnInsertOption: 'Этот тип документа сейчас не является вариантом вставки на этой странице.',
    notAnInsertOptionAllowedNote: 'Этот тип документа не является вариантом вставки на этой странице, но в противном случае был бы разрешён.',
    notAnInsertOptionDeniedNote: 'Этот тип документа не является вариантом вставки на этой странице, но в противном случае был бы запрещён.',
    docTypePermissions_pickDocType: '— Выберите тип документа —',
    docTypePermissions_pickToStart: 'Выберите группу пользователей и тип документа, чтобы начать.',
    docTypePermissions_defaultRowLabel: 'По умолчанию (применяется везде)',
    docTypePermissions_pendingNodeLabel: '(несохранённая страница)',
    docTypePermissions_addScopeNode: 'Добавить переопределение области',
    docTypePermissions_notSet: 'Не задано',
    docTypePermissions_noResults: 'Типы документов не найдены.',
    docTypePermissions_useRoot: 'Использовать корень',
    docTypePermissions_pickedNode: 'Страница:',
    docTypePermissions_rootLevel: 'Корневой уровень',
    docTypePermissions_reasoning: 'Обоснование',
    docTypePermissions_defaultAllow: 'Разрешено по умолчанию',
    docTypePermissions_viaDefault: 'из строки по умолчанию',
  },
} satisfies UmbLocalizationDictionary;
