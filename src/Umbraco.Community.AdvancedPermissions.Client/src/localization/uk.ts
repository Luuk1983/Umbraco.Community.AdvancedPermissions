import type { UmbLocalizationDictionary } from '@umbraco-cms/backoffice/localization-api';

export default {
  uap: {
    // ── Навігація ─────────────────────────────────────────────────────────
    sectionLabel: 'Розширені права доступу',
    editorsSectionLabel: 'Редактори',
    viewersSectionLabel: 'Перегляди',
    permissionsEditor: 'Редактор прав доступу до вмісту',
    accessViewer: 'Перегляд доступу',

    // ── Загальне ──────────────────────────────────────────────────────────
    roleLabel: 'Група користувачів',
    rolePlaceholder: '— Виберіть групу користувачів —',
    userLabel: 'Користувач',
    saveChanges: 'Зберегти зміни',
    discard: 'Відхилити',
    cancel: 'Відміна',
    apply: 'Застосувати',
    close: 'Закрити',
    inherit: 'Успадкувати',
    allow: 'Дозволити',
    deny: 'Заборонити',
    umbracoUsers: 'Усі користувачі',

    // ── Help ──────────────────────────────────────────────────────────────
    help_contentPermissions_description: 'Керуйте правами «Дозволити»/«Заборонити» для кожної групи користувачів у дереві вмісту.',
    help_accessViewer_description: 'Перегляньте дійсні права доступу, які користувач або група має на будь-якому вузлі, з повним поясненням.',
    help_docTypePermissions_description: 'Визначте, які типи документів може створювати кожна група користувачів і де саме.',
    help_insertOptions_description: 'Перевірте, які типи документів користувач або група користувачів може створювати на кожному вузлі.',
    help_learnMore: 'Докладніше',
    help_modalTitle: 'Довідка',
    help_tabAbout: 'Про цю сторінку',
    help_tabConcepts: 'Поняття',
    help_concept_scope_tip: 'Область застосування визначає, наскільки далеко поширюється правило: цей вузол, цей вузол та його дочірні вузли або лише дочірні вузли.',
    help_concept_priorityOverride_tip: 'Перевизначення пріоритету змушує цей запис мати перевагу над звичайним порядком визначення.',
    help_concept_allowDeny_tip: '«Дозволити» надає право доступу, «Заборонити» скасовує його, а якщо залишити незаданим — успадковується від найближчого батьківського вузла.',
    help_concept_reasoning_tip: 'Натисніть будь-яку клітинку, щоб побачити, як саме було визначено право доступу.',

    // ── Редактор прав доступу ─────────────────────────────────────────────
    editorHeadline: 'Редактор прав доступу до вмісту',
    selectRolePrompt: 'Виберіть групу користувачів вище, щоб керувати її правами доступу.',
    permissionsSaved: 'Права доступу збережено.',
    saveFailed: (error: string) => `Не вдалося зберегти: ${error}`,
    contentNodeHeader: 'Вузол вмісту',
    contentRoot: 'Права доступу за замовчуванням',
    expand: 'Розгорнути',
    collapse: 'Згорнути',

    // ── Діалог прав доступу ───────────────────────────────────────────────
    dialogHeadline: (verb: string, nodeName: string) => `Налаштувати право «${verb}» для ‘${nodeName}’`,
    descendantsSection: 'Дочірні вузли (якщо відрізняються)',
    dialogInstructions: 'Налаштуйте право доступу для цього вузла. За замовчуванням це також застосовується до всіх дочірніх вузлів. Скористайтеся пунктом «Дочірні вузли (якщо відрізняються)», щоб задати інше право доступу для дочірніх вузлів.',
    virtualRootInherit: 'Не задано (видалити запис)',
    virtualRootAllow: 'Дозволити (увесь вміст)',
    virtualRootDeny: 'Заборонити (увесь вміст)',
    dialogResult: 'Результат',
    previewBothInherit: 'Право доступу не задано. Успадковується від батьківського вузла.',
    previewUniform: (action: string) => `${action} для цього вузла та всіх дочірніх вузлів.`,
    previewNodeOnly: (action: string) => `${action} лише для цього вузла. Дочірні вузли не зазнають впливу цього правила.`,
    previewDescOnly: (action: string) => `Немає явного права доступу для цього вузла. ${action} для всіх дочірніх вузлів.`,
    previewSplit: (nodeAction: string, descAction: string) => `${nodeAction} для цього вузла. ${descAction} для всіх дочірніх вузлів.`,
    previewVirtualInherit: 'Право доступу за замовчуванням не задано.',
    previewVirtualSet: (action: string) => `${action} за замовчуванням для всього вмісту.`,
    previewPriorityNode: 'Перевизначення пріоритету ввімкнено для цього вузла.',
    previewPriorityDesc: 'Перевизначення пріоритету ввімкнено для дочірніх вузлів.',
    previewPriorityBoth: 'Перевизначення пріоритету ввімкнено.',

    // ── Перевизначення пріоритету ─────────────────────────────────────
    priorityOverride: 'Перевизначення пріоритету',
    priorityOverrideTooltip: (permission: string, nodeName: string) =>
      `Користувач може належати до кількох груп користувачів. Зазвичай дійсне право доступу визначається фіксованим порядком пріоритету. Якщо встановити цей прапорець, цей порядок перевизначається, тож вибране тут налаштування «${permission}» майже завжди стає результатом для «${nodeName}», незалежно від інших груп користувача. Використовуйте помірно.`,
    priorityOverrideBadgeTitle: 'Перевизначення пріоритету ввімкнено для цього запису',
    priorityOverrideWonTitle: 'Визначено через перевизначення пріоритету',
    priorityOverrideSuppressedHeader: 'Перевизначення пріоритету змінило результат',
    priorityOverrideSuppressedHint: 'Без нього результат був би:',

    // ── Перегляд доступу ──────────────────────────────────────────────────
    viewerHeadline: 'Перегляд доступу',
    byRole: 'За групою користувачів',
    byUser: 'За користувачем',
    chooseRole: 'Виберіть групу користувачів',
    chooseUser: 'Виберіть користувача',
    selectSubjectPrompt: 'Виберіть групу користувачів або користувача, щоб переглянути дійсні права доступу.',
    legendAllow: 'Дозволити',
    legendDeny: 'Заборонити',
    clickForReasoning: (label: string) => `${label} — натисніть для пояснення`,
    subjectOr: 'або',

    // ── Модальне вікно вибору групи користувачів ──────────────────────────
    rolePickerHeadline: 'Виберіть групу користувачів',
    rolePickerFilter: 'Введіть для фільтрування…',
    rolePickerNoResults: 'Немає груп користувачів, що відповідають фільтру.',
    rolePickerNameHeader: 'Група користувачів',

    // ── Модальне вікно вибору користувача ─────────────────────────────────
    userPickerHeadline: 'Виберіть користувача',
    userPickerFilter: 'Введіть для фільтрування…',
    userPickerNoResults: 'Немає користувачів, що відповідають фільтру.',
    userPickerNameHeader: 'Користувач',

    // ── Діалог пояснення ──────────────────────────────────────────────────
    reasoningHeadline: (verb: string, nodeName: string) => `Право «${verb}» для “${nodeName}”`,
    effectiveAllowed: (subject: string, verb: string, nodeName: string) =>
      `${subject} надано право «${verb}» для “${nodeName}”.`,
    effectiveDenied: (subject: string, verb: string, nodeName: string) =>
      `${subject} відмовлено в праві «${verb}» для “${nodeName}”.`,
    dialogSecurityHeader: 'Безпека',
    defaultPermissions: 'Права доступу за замовчуванням',
    determiningEntry: 'Цей запис має пріоритет',
    noReasoningData: 'Немає даних про права доступу для цього дозволу.',
    defaultAllowNote: 'Права доступу не задано, це дозволено за замовчуванням.',
    defaultDenyNote: 'Права доступу не задано, це заборонено за замовчуванням.',

    // ── Повідомлення про перенаправлення атомарних прав доступу ───────────
    redirectMessage:
      'Права доступу до документів для цієї групи користувачів керуються пакетом Advanced Permissions. Відкрийте Редактор прав доступу в розділі Користувачі, щоб налаштувати права доступу.',

    // ── Права доступу за типом документа ──────────────────────────────────
    role: 'Група користувачів',
    pickRole: 'Виберіть групу користувачів',
    user: 'Користувач',
    pickUser: 'Виберіть користувача',
    node: 'Вузол',
    pickNode: 'Виберіть вузол',
    state: 'Стан',
    scope: 'Область застосування',
    scope_thisNodeOnly: 'Лише цей вузол',
    scope_thisNodeAndDescendants: 'Цей вузол та дочірні',
    scope_descendantsOnly: 'Лише дочірні',

    docTypePermissions_menuLabel: 'Редактор прав доступу за типом документа',
    docTypePermissions_insertOptionsMenuLabel: 'Перегляд параметрів вставлення',
    docTypePermissions_workspaceTitle: 'Редактор прав доступу за типом документа',
    docTypePermissions_auditTitle: 'Перегляд параметрів вставлення',
    docTypePermissions_allDocTypes: 'Усі типи документів',
    docTypePermissions_verbInsert: 'Вставити',
    docTypePermissions_documentType: 'Тип документа',
    chooseDocType: 'Виберіть тип документа',
    notAnInsertOption: 'Цей тип документа наразі не є параметром вставлення на цьому вузлі.',
    notAnInsertOptionAllowedNote: 'Цей тип документа не є параметром вставлення на цьому вузлі, але в іншому разі його було б дозволено.',
    notAnInsertOptionDeniedNote: 'Цей тип документа не є параметром вставлення на цьому вузлі, але в іншому разі його було б заборонено.',
    docTypePermissions_pickDocType: '— Виберіть тип документа —',
    docTypePermissions_pickToStart: 'Виберіть групу користувачів і тип документа, щоб почати.',
    docTypePermissions_defaultRowLabel: 'За замовчуванням (застосовується всюди)',
    docTypePermissions_pendingNodeLabel: '(незбережений вузол)',
    docTypePermissions_addScopeNode: 'Додати перевизначення області застосування',
    docTypePermissions_notSet: 'Не задано',
    docTypePermissions_noResults: 'Типів документів не знайдено.',
    docTypePermissions_useRoot: 'Використати корінь',
    docTypePermissions_pickedNode: 'Вузол:',
    docTypePermissions_rootLevel: 'Кореневий рівень',
    docTypePermissions_reasoning: 'Пояснення',
    docTypePermissions_defaultAllow: 'Дозволено за замовчуванням',
    docTypePermissions_viaDefault: 'із рядка за замовчуванням',
  },
} satisfies UmbLocalizationDictionary;
