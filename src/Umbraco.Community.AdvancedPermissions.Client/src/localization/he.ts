import type { UmbLocalizationDictionary } from '@umbraco-cms/backoffice/localization-api';

export default {
  uap: {
    // ── ניווט ─────────────────────────────────────────────────────────────
    sectionLabel: 'הרשאות מתקדמות',
    editorsSectionLabel: 'עורכים',
    viewersSectionLabel: 'מציגים',
    permissionsEditor: 'עורך הרשאות תוכן',
    accessViewer: 'מציג גישה',

    // ── כללי ──────────────────────────────────────────────────────────────
    roleLabel: 'קבוצת משתמשים',
    rolePlaceholder: '— בחר קבוצת משתמשים —',
    userLabel: 'משתמש',
    saveChanges: 'שמור שינויים',
    discard: 'בטל שינויים',
    cancel: 'בטל',
    apply: 'החל',
    close: 'סגור',
    inherit: 'ירושה',
    allow: 'אפשר',
    deny: 'מנע',
    umbracoUsers: 'כל המשתמשים',

    // ── עורך ההרשאות ──────────────────────────────────────────────────────
    editorHeadline: 'עורך הרשאות תוכן',
    selectRolePrompt: 'בחר קבוצת משתמשים למעלה כדי לנהל את ההרשאות שלה.',
    permissionsSaved: 'ההרשאות נשמרו.',
    saveFailed: (error: string) => `השמירה נכשלה: ${error}`,
    contentNodeHeader: 'צומת תוכן',
    contentRoot: 'הרשאות ברירת מחדל',
    expand: 'הרחב',
    collapse: 'כווץ',

    // ── תיבת דו-שיח להרשאות ───────────────────────────────────────────────
    dialogHeadline: (verb: string, nodeName: string) => `הגדר הרשאת ${verb} עבור ‘${nodeName}’`,
    descendantsSection: 'צאצאים (אם שונה)',
    dialogInstructions: 'הגדר את ההרשאה לצומת זה. כברירת מחדל, היא חלה גם על כל הצאצאים. השתמש ב“צאצאים (אם שונה)” כדי להגדיר הרשאה שונה לצמתים צאצאים.',
    virtualRootInherit: 'לא מוגדר (הסר ערך)',
    virtualRootAllow: 'אפשר (כל התוכן)',
    virtualRootDeny: 'מנע (כל התוכן)',
    dialogResult: 'תוצאה',
    previewBothInherit: 'לא הוגדרה הרשאה. יורשת מהצומת ההורה.',
    previewUniform: (action: string) => `${action} בצומת זה ובכל הצאצאים.`,
    previewNodeOnly: (action: string) => `${action} בצומת זה בלבד. הצאצאים אינם מושפעים מכלל זה.`,
    previewDescOnly: (action: string) => `אין הרשאה מפורשת בצומת זה. ${action} בכל הצאצאים.`,
    previewSplit: (nodeAction: string, descAction: string) => `${nodeAction} בצומת זה. ${descAction} בכל הצאצאים.`,
    previewVirtualInherit: 'לא הוגדרה הרשאת ברירת מחדל.',
    previewVirtualSet: (action: string) => `${action} כברירת מחדל עבור כל התוכן.`,
    previewPriorityNode: 'עקיפת עדיפות מוגדרת בצומת זה.',
    previewPriorityDesc: 'עקיפת עדיפות מוגדרת בצאצאים.',
    previewPriorityBoth: 'עקיפת עדיפות מוגדרת.',

    // ── עקיפת עדיפות ──────────────────────────────────────────────────────
    priorityOverride: 'עקיפת עדיפות',
    priorityOverrideTooltip: (permission: string, nodeName: string) =>
      `משתמש יכול להשתייך למספר קבוצות משתמשים. בדרך כלל, ההרשאה האפקטיבית עוקבת אחר סדר עדיפות קבוע. סימון תיבה זו עוקף את הסדר הזה, כך שההגדרה “${permission}” שתבחר כאן כמעט תמיד הופכת לתוצאה עבור “${nodeName}”, ללא קשר לקבוצות האחרות של המשתמש. השתמש במשורה.`,
    priorityOverrideBadgeTitle: 'עקיפת עדיפות מוגדרת בערך זה',
    priorityOverrideWonTitle: 'נקבע באמצעות עקיפת עדיפות',
    priorityOverrideSuppressedHeader: 'עקיפת העדיפות שינתה את התוצאה',
    priorityOverrideSuppressedHint: 'בלעדיה, התוצאה הייתה:',

    // ── מציג הגישה ────────────────────────────────────────────────────────
    viewerHeadline: 'מציג גישה',
    byRole: 'לפי קבוצת משתמשים',
    byUser: 'לפי משתמש',
    chooseRole: 'בחר קבוצת משתמשים',
    chooseUser: 'בחר משתמש',
    selectSubjectPrompt: 'בחר קבוצת משתמשים או משתמש כדי להציג את ההרשאות האפקטיביות.',
    legendAllow: 'אפשר',
    legendDeny: 'מנע',
    clickForReasoning: (label: string) => `${label} — לחץ להנמקה`,
    subjectOr: 'או',

    // ── תיבת בחירת קבוצת משתמשים ──────────────────────────────────────────
    rolePickerHeadline: 'בחר קבוצת משתמשים',
    rolePickerFilter: 'הקלד כדי לסנן…',
    rolePickerNoResults: 'אין קבוצות משתמשים התואמות לסינון.',
    rolePickerNameHeader: 'קבוצת משתמשים',

    // ── תיבת בחירת משתמש ──────────────────────────────────────────────────
    userPickerHeadline: 'בחר משתמש',
    userPickerFilter: 'הקלד כדי לסנן…',
    userPickerNoResults: 'אין משתמשים התואמים לסינון.',
    userPickerNameHeader: 'משתמש',

    // ── תיבת דו-שיח להנמקה ────────────────────────────────────────────────
    reasoningHeadline: (verb: string, nodeName: string) => `הרשאת ${verb} עבור “${nodeName}”`,
    effectiveAllowed: (subject: string, verb: string, nodeName: string) =>
      `ל${subject} ניתנה הרשאת ${verb} עבור “${nodeName}”.`,
    effectiveDenied: (subject: string, verb: string, nodeName: string) =>
      `ל${subject} נמנעה הרשאת ${verb} עבור “${nodeName}”.`,
    dialogSecurityHeader: 'אבטחה',
    defaultPermissions: 'הרשאות ברירת מחדל',
    determiningEntry: 'ערך זה גובר',
    noReasoningData: 'אין נתוני הרשאה זמינים עבור פעולה זו.',
    defaultAllowNote: 'לא הוגדרו הרשאות, פעולה זו מותרת כברירת מחדל.',
    defaultDenyNote: 'לא הוגדרו הרשאות, פעולה זו נמנעת כברירת מחדל.',

    // ── הודעת הפניה להרשאות מפורטות ───────────────────────────────────────
    redirectMessage:
      'הרשאות מסמכים עבור קבוצת משתמשים זו מנוהלות על ידי חבילת Advanced Permissions. פתח את עורך ההרשאות במקטע המשתמשים כדי להגדיר הרשאות.',

    // ── הרשאות סוג מסמך ───────────────────────────────────────────────────
    role: 'קבוצת משתמשים',
    pickRole: 'בחר קבוצת משתמשים',
    user: 'משתמש',
    pickUser: 'בחר משתמש',
    node: 'צומת',
    pickNode: 'בחר צומת',
    state: 'מצב',
    scope: 'היקף',
    scope_thisNodeOnly: 'צומת זה בלבד',
    scope_thisNodeAndDescendants: 'צומת זה והצאצאים',
    scope_descendantsOnly: 'צאצאים בלבד',

    docTypePermissions_menuLabel: 'עורך הרשאות סוג מסמך',
    docTypePermissions_insertOptionsMenuLabel: 'מציג אפשרויות הוספה',
    docTypePermissions_workspaceTitle: 'עורך הרשאות סוג מסמך',
    docTypePermissions_auditTitle: 'מציג אפשרויות הוספה',
    docTypePermissions_allDocTypes: 'כל סוגי המסמכים',
    docTypePermissions_verbInsert: 'הוסף',
    docTypePermissions_documentType: 'סוג מסמך',
    chooseDocType: 'בחר סוג מסמך',
    notAnInsertOption: 'סוג מסמך זה אינו כרגע אפשרות הוספה בצומת זה.',
    notAnInsertOptionAllowedNote: 'סוג מסמך זה אינו אפשרות הוספה בצומת זה, אך אחרת הוא היה מותר.',
    notAnInsertOptionDeniedNote: 'סוג מסמך זה אינו אפשרות הוספה בצומת זה, אך אחרת הוא היה נמנע.',
    docTypePermissions_pickDocType: '— בחר סוג מסמך —',
    docTypePermissions_pickToStart: 'בחר קבוצת משתמשים וסוג מסמך כדי להתחיל.',
    docTypePermissions_defaultRowLabel: 'ברירת מחדל (חל בכל מקום)',
    docTypePermissions_pendingNodeLabel: '(צומת לא שמור)',
    docTypePermissions_addScopeNode: 'הוסף עקיפת היקף',
    docTypePermissions_notSet: 'לא מוגדר',
    docTypePermissions_noResults: 'לא נמצאו סוגי מסמכים.',
    docTypePermissions_useRoot: 'השתמש בשורש',
    docTypePermissions_pickedNode: 'צומת:',
    docTypePermissions_rootLevel: 'רמת שורש',
    docTypePermissions_reasoning: 'הנמקה',
    docTypePermissions_defaultAllow: 'מותר כברירת מחדל',
    docTypePermissions_viaDefault: 'משורת ברירת המחדל',
  },
} satisfies UmbLocalizationDictionary;
