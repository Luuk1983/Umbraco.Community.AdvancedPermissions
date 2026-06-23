import type { UmbLocalizationDictionary } from '@umbraco-cms/backoffice/localization-api';

export default {
  uap: {
    // ── التنقل ────────────────────────────────────────────────────────────
    sectionLabel: 'الأذونات المتقدمة',
    editorsSectionLabel: 'المحررون',
    viewersSectionLabel: 'العارضون',
    permissionsEditor: 'محرر أذونات المحتوى',
    accessViewer: 'عارض الوصول',

    // ── عام ───────────────────────────────────────────────────────────────
    roleLabel: 'مجموعة المستخدمين',
    rolePlaceholder: '— اختر مجموعة مستخدمين —',
    userLabel: 'المستخدم',
    saveChanges: 'حفظ التغييرات',
    discard: 'تجاهل',
    cancel: 'إلغاء',
    apply: 'تطبيق',
    close: 'إغلاق',
    inherit: 'وراثة',
    allow: 'السماح',
    deny: 'منع',
    umbracoUsers: 'جميع المستخدمين',

    // ── Help ──────────────────────────────────────────────────────────────
    help_contentPermissions_description: 'أدِر أذونات السماح/المنع لكل مجموعة مستخدمين عبر شجرة المحتوى لديك.',
    help_accessViewer_description: 'اطّلع على الأذونات الفعّالة التي يملكها مستخدم أو مجموعة عند أي عقدة، مع التعليل الكامل.',
    help_docTypePermissions_description: 'حدِّد أنواع الوثائق التي يمكن لكل مجموعة مستخدمين إنشاؤها، وأين.',
    help_insertOptions_description: 'دقِّق في أنواع الوثائق التي يمكن لمستخدم أو مجموعة مستخدمين إنشاؤها عند كل عقدة.',
    help_learnMore: 'معرفة المزيد',
    help_modalTitle: 'مساعدة',
    help_tabAbout: 'حول هذه الصفحة',
    help_tabConcepts: 'المفاهيم',
    help_concept_scope_tip: 'يتحكم النطاق في مدى امتداد القاعدة: هذه العقدة، أو هذه العقدة وعناصرها الفرعية، أو العناصر الفرعية فقط.',
    help_concept_priorityOverride_tip: 'يفرض تجاوز الأولوية فوز هذا الإدخال على ترتيب الحسم المعتاد.',
    help_concept_allowDeny_tip: 'السماح يمنح الإذن، والمنع يلغيه، وتركه غير معيّن يُورِث من أقرب عنصر أصل.',
    help_concept_reasoning_tip: 'انقر على أي خلية لمعرفة كيف تم حسم الإذن بالضبط.',

    // ── محرر الأذونات ─────────────────────────────────────────────────────
    editorHeadline: 'محرر أذونات المحتوى',
    selectRolePrompt: 'اختر مجموعة مستخدمين أعلاه لإدارة أذوناتها.',
    permissionsSaved: 'تم حفظ الأذونات.',
    saveFailed: (error: string) => `فشل الحفظ: ${error}`,
    contentNodeHeader: 'عقدة المحتوى',
    contentRoot: 'الأذونات الافتراضية',
    expand: 'توسيع',
    collapse: 'طي',

    // ── مربع حوار الإذن ───────────────────────────────────────────────────
    dialogHeadline: (verb: string, nodeName: string) => `تعيين إذن “${verb}” لـ ‘${nodeName}’`,
    descendantsSection: 'العناصر الفرعية (إن كانت مختلفة)',
    dialogInstructions: 'عيّن الإذن لهذه العقدة. بشكل افتراضي ينطبق هذا أيضًا على جميع العناصر الفرعية. استخدم “العناصر الفرعية (إن كانت مختلفة)” لتعيين إذن مختلف للعناصر الفرعية.',
    virtualRootInherit: 'غير معيّن (إزالة الإدخال)',
    virtualRootAllow: 'السماح (لكل المحتوى)',
    virtualRootDeny: 'المنع (لكل المحتوى)',
    dialogResult: 'النتيجة',
    previewBothInherit: 'لم يُعيَّن أي إذن. يُورَث من العنصر الأصل.',
    previewUniform: (action: string) => `${action} على هذه العقدة وجميع العناصر الفرعية.`,
    previewNodeOnly: (action: string) => `${action} على هذه العقدة فقط. لا تتأثر العناصر الفرعية بهذه القاعدة.`,
    previewDescOnly: (action: string) => `لا يوجد إذن صريح على هذه العقدة. ${action} على جميع العناصر الفرعية.`,
    previewSplit: (nodeAction: string, descAction: string) => `${nodeAction} على هذه العقدة. ${descAction} على جميع العناصر الفرعية.`,
    previewVirtualInherit: 'لم يُعيَّن أي إذن افتراضي.',
    previewVirtualSet: (action: string) => `${action} بشكل افتراضي لكل المحتوى.`,
    previewPriorityNode: 'تجاوز الأولوية مُفعَّل على هذه العقدة.',
    previewPriorityDesc: 'تجاوز الأولوية مُفعَّل على العناصر الفرعية.',
    previewPriorityBoth: 'تجاوز الأولوية مُفعَّل.',

    // ── تجاوز الأولوية ────────────────────────────────────────────────────
    priorityOverride: 'تجاوز الأولوية',
    priorityOverrideTooltip: (permission: string, nodeName: string) =>
      `يمكن أن ينتمي المستخدم إلى عدة مجموعات مستخدمين. عادةً يتبع الإذن الفعّال ترتيب أولوية ثابتًا. تحديد هذا المربع يتجاوز ذلك الترتيب، بحيث يصبح إعداد “${permission}” الذي تختاره هنا هو النتيجة دائمًا تقريبًا لـ “${nodeName}”، بغض النظر عن مجموعات المستخدم الأخرى. استخدمه باعتدال.`,
    priorityOverrideBadgeTitle: 'تجاوز الأولوية مُفعَّل على هذا الإدخال',
    priorityOverrideWonTitle: 'تمّ الحسم عبر تجاوز الأولوية',
    priorityOverrideSuppressedHeader: 'غيّر تجاوز الأولوية النتيجة',
    priorityOverrideSuppressedHint: 'بدونه، كانت النتيجة ستكون:',

    // ── عارض الوصول ───────────────────────────────────────────────────────
    viewerHeadline: 'عارض الوصول',
    byRole: 'حسب مجموعة المستخدمين',
    byUser: 'حسب المستخدم',
    chooseRole: 'اختر مجموعة المستخدمين',
    chooseUser: 'اختر المستخدم',
    selectSubjectPrompt: 'اختر مجموعة مستخدمين أو مستخدمًا لعرض الأذونات الفعّالة.',
    legendAllow: 'السماح',
    legendDeny: 'المنع',
    clickForReasoning: (label: string) => `${label} — انقر لمعرفة السبب`,
    subjectOr: 'أو',

    // ── مربع اختيار مجموعة المستخدمين ─────────────────────────────────────
    rolePickerHeadline: 'اختر مجموعة مستخدمين',
    rolePickerFilter: 'اكتب للتصفية…',
    rolePickerNoResults: 'لا توجد مجموعات مستخدمين تطابق التصفية.',
    rolePickerNameHeader: 'مجموعة المستخدمين',

    // ── مربع اختيار المستخدم ──────────────────────────────────────────────
    userPickerHeadline: 'اختر مستخدمًا',
    userPickerFilter: 'اكتب للتصفية…',
    userPickerNoResults: 'لا يوجد مستخدمون يطابقون التصفية.',
    userPickerNameHeader: 'المستخدم',

    // ── مربع حوار السبب ───────────────────────────────────────────────────
    reasoningHeadline: (verb: string, nodeName: string) => `إذن “${verb}” لـ “${nodeName}”`,
    effectiveAllowed: (subject: string, verb: string, nodeName: string) =>
      `تمّ منح ${subject} إذن “${verb}” لـ “${nodeName}”.`,
    effectiveDenied: (subject: string, verb: string, nodeName: string) =>
      `تمّ رفض إذن “${verb}” لـ ${subject} على “${nodeName}”.`,
    dialogSecurityHeader: 'الأمان',
    defaultPermissions: 'الأذونات الافتراضية',
    determiningEntry: 'هذا الإدخال له الأولوية',
    noReasoningData: 'لا تتوفر بيانات أذونات لهذا الإذن.',
    defaultAllowNote: 'لم تُعيَّن أي أذونات، وهذا مسموح به افتراضيًا.',
    defaultDenyNote: 'لم تُعيَّن أي أذونات، وهذا ممنوع افتراضيًا.',

    // ── رسالة إعادة التوجيه للأذونات الدقيقة ───────────────────────────────
    redirectMessage:
      'تُدار أذونات الوثائق لمجموعة المستخدمين هذه بواسطة حزمة Advanced Permissions. افتح محرر الأذونات في قسم المستخدمين لتكوين الأذونات.',

    // ── أذونات نوع الوثيقة ────────────────────────────────────────────────
    role: 'مجموعة المستخدمين',
    pickRole: 'اختر مجموعة المستخدمين',
    user: 'المستخدم',
    pickUser: 'اختر المستخدم',
    node: 'العقدة',
    pickNode: 'اختر العقدة',
    state: 'الحالة',
    scope: 'النطاق',
    scope_thisNodeOnly: 'هذه العقدة فقط',
    scope_thisNodeAndDescendants: 'هذه العقدة والعناصر الفرعية',
    scope_descendantsOnly: 'العناصر الفرعية فقط',

    docTypePermissions_menuLabel: 'محرر أذونات نوع الوثيقة',
    docTypePermissions_insertOptionsMenuLabel: 'عارض خيارات الإدراج',
    docTypePermissions_workspaceTitle: 'محرر أذونات نوع الوثيقة',
    docTypePermissions_auditTitle: 'عارض خيارات الإدراج',
    docTypePermissions_allDocTypes: 'جميع أنواع الوثائق',
    docTypePermissions_verbInsert: 'إدراج',
    docTypePermissions_documentType: 'نوع الوثيقة',
    chooseDocType: 'اختر نوع الوثيقة',
    notAnInsertOption: 'نوع الوثيقة هذا ليس حاليًا خيار إدراج على هذه العقدة.',
    notAnInsertOptionAllowedNote: 'نوع الوثيقة هذا ليس خيار إدراج على هذه العقدة، لكنه كان سيُسمح به لولا ذلك.',
    notAnInsertOptionDeniedNote: 'نوع الوثيقة هذا ليس خيار إدراج على هذه العقدة، لكنه كان سيُمنع لولا ذلك.',
    docTypePermissions_pickDocType: '— اختر نوع وثيقة —',
    docTypePermissions_pickToStart: 'اختر مجموعة مستخدمين ونوع وثيقة للبدء.',
    docTypePermissions_defaultRowLabel: 'الافتراضي (ينطبق في كل مكان)',
    docTypePermissions_pendingNodeLabel: '(عقدة غير محفوظة)',
    docTypePermissions_addScopeNode: 'إضافة تجاوز للنطاق',
    docTypePermissions_notSet: 'غير معيّن',
    docTypePermissions_noResults: 'لم يُعثر على أنواع وثائق.',
    docTypePermissions_useRoot: 'استخدام الجذر',
    docTypePermissions_pickedNode: 'العقدة:',
    docTypePermissions_rootLevel: 'مستوى الجذر',
    docTypePermissions_reasoning: 'السبب',
    docTypePermissions_defaultAllow: 'مسموح به افتراضيًا',
    docTypePermissions_viaDefault: 'من الصف الافتراضي',
  },
} satisfies UmbLocalizationDictionary;
