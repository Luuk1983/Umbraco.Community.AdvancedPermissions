import type { UmbLocalizationDictionary } from '@umbraco-cms/backoffice/localization-api';

export default {
  uap: {
    // ── 導覽 ──────────────────────────────────────────────────────────────
    sectionLabel: '進階權限',
    editorsSectionLabel: '編輯器',
    viewersSectionLabel: '檢視器',
    permissionsEditor: '內容權限編輯器',
    accessViewer: '存取檢視器',

    // ── 一般 ──────────────────────────────────────────────────────────────
    roleLabel: '使用者群組',
    rolePlaceholder: '— 選擇使用者群組 —',
    userLabel: '使用者',
    saveChanges: '儲存變更',
    discard: '放棄',
    cancel: '取消',
    apply: '套用',
    close: '關閉',
    inherit: '繼承',
    allow: '允許',
    deny: '拒絕',
    umbracoUsers: '所有使用者',

    // ── Help ──────────────────────────────────────────────────────────────
    help_contentPermissions_description: '依使用者群組管理整個內容樹狀結構的允許/拒絕權限。',
    help_accessViewer_description: '檢視使用者或群組在任一節點上的有效權限，並提供完整原因說明。',
    help_docTypePermissions_description: '決定每個使用者群組可以建立哪些文檔類型，以及可在何處建立。',
    help_insertOptions_description: '稽核使用者或使用者群組在每個節點上可建立哪些文檔類型。',
    help_learnMore: '了解更多',
    help_modalTitle: '說明',
    help_tabAbout: '關於此頁面',
    help_tabConcepts: '概念',
    help_concept_scope_tip: '範圍控制規則的適用範圍：僅此節點、此節點及其子孫節點，或僅子孫節點。',
    help_concept_priorityOverride_tip: '優先權覆寫會強制此項目勝過正常的解析順序。',
    help_concept_allowDeny_tip: '允許會授予權限，拒絕會撤銷權限，而保持未設定則會從最近的上層節點繼承。',
    help_concept_reasoning_tip: '點選任一儲存格即可確切了解權限是如何解析出來的。',

    // ── 權限編輯器 ───────────────────────────────────────────────────────
    editorHeadline: '內容權限編輯器',
    selectRolePrompt: '請在上方選擇使用者群組以管理其權限。',
    permissionsSaved: '權限已儲存。',
    saveFailed: (error: string) => `儲存失敗：${error}`,
    contentNodeHeader: '內容節點',
    contentRoot: '預設權限',
    expand: '展開',
    collapse: '收合',

    // ── 權限對話框 ─────────────────────────────────────────────────────────
    dialogHeadline: (verb: string, nodeName: string) => `為「${nodeName}」設定「${verb}」權限`,
    descendantsSection: '子孫節點（若不同）',
    dialogInstructions: '為此節點設定權限。預設情況下，此設定也會套用至所有子孫節點。使用「子孫節點（若不同）」可為子孫節點設定不同的權限。',
    virtualRootInherit: '未設定（移除項目）',
    virtualRootAllow: '允許（所有內容）',
    virtualRootDeny: '拒絕（所有內容）',
    dialogResult: '結果',
    previewBothInherit: '未設定權限。從父節點繼承。',
    previewUniform: (action: string) => `對此節點及所有子孫節點${action}。`,
    previewNodeOnly: (action: string) => `僅對此節點${action}。子孫節點不受此規則影響。`,
    previewDescOnly: (action: string) => `此節點沒有明確的權限。對所有子孫節點${action}。`,
    previewSplit: (nodeAction: string, descAction: string) => `對此節點${nodeAction}。對所有子孫節點${descAction}。`,
    previewVirtualInherit: '未設定預設權限。',
    previewVirtualSet: (action: string) => `對所有內容預設${action}。`,
    previewPriorityNode: '此節點已設定優先權覆寫。',
    previewPriorityDesc: '子孫節點已設定優先權覆寫。',
    previewPriorityBoth: '已設定優先權覆寫。',

    // ── 優先權覆寫 ────────────────────────────────────────────────────
    priorityOverride: '優先權覆寫',
    priorityOverrideTooltip: (permission: string, nodeName: string) =>
      `一位使用者可以同時屬於多個使用者群組。一般情況下，有效權限會依照固定的優先順序決定。勾選此選項會覆寫該順序，使您在此選擇的「${permission}」設定幾乎總是成為「${nodeName}」的結果，而不論該使用者所屬的其他群組為何。請謹慎使用。`,
    priorityOverrideBadgeTitle: '此項目已設定優先權覆寫',
    priorityOverrideWonTitle: '透過優先權覆寫解析',
    priorityOverrideSuppressedHeader: '優先權覆寫已變更結果',
    priorityOverrideSuppressedHint: '若未使用，結果原本會是：',

    // ── 存取檢視器 ─────────────────────────────────────────────────────────
    viewerHeadline: '存取檢視器',
    byRole: '依使用者群組',
    byUser: '依使用者',
    chooseRole: '選擇使用者群組',
    chooseUser: '選擇使用者',
    selectSubjectPrompt: '選擇使用者群組或使用者以檢視有效權限。',
    legendAllow: '允許',
    legendDeny: '拒絕',
    clickForReasoning: (label: string) => `${label} — 點選以檢視原因`,
    subjectOr: '或',

    // ── 使用者群組選擇器對話框 ──────────────────────────────────────────────
    rolePickerHeadline: '選擇使用者群組',
    rolePickerFilter: '輸入以篩選…',
    rolePickerNoResults: '沒有符合篩選條件的使用者群組。',
    rolePickerNameHeader: '使用者群組',

    // ── 使用者選擇器對話框 ──────────────────────────────────────────────────
    userPickerHeadline: '選擇使用者',
    userPickerFilter: '輸入以篩選…',
    userPickerNoResults: '沒有符合篩選條件的使用者。',
    userPickerNameHeader: '使用者',

    // ── 原因對話框 ────────────────────────────────────────────────────────
    reasoningHeadline: (verb: string, nodeName: string) => `「${nodeName}」的「${verb}」權限`,
    effectiveAllowed: (subject: string, verb: string, nodeName: string) =>
      `${subject}對「${nodeName}」已被允許「${verb}」權限。`,
    effectiveDenied: (subject: string, verb: string, nodeName: string) =>
      `${subject}對「${nodeName}」已被拒絕「${verb}」權限。`,
    dialogSecurityHeader: '安全性',
    defaultPermissions: '預設權限',
    determiningEntry: '此項目具有優先權',
    noReasoningData: '此動作沒有可用的權限資料。',
    defaultAllowNote: '未設定任何權限，此項預設為允許。',
    defaultDenyNote: '未設定任何權限，此項預設為拒絕。',

    // ── 細部權限重新導向 ──────────────────────────────────────────────────
    redirectMessage:
      '此使用者群組的文件權限由 Advanced Permissions 套件管理。請在「使用者」區段中開啟權限編輯器以設定權限。',

    // ── 文檔類型權限 ──────────────────────────────────────────────────────
    role: '使用者群組',
    pickRole: '選擇使用者群組',
    user: '使用者',
    pickUser: '選擇使用者',
    node: '節點',
    pickNode: '選擇節點',
    state: '狀態',
    scope: '範圍',
    scope_thisNodeOnly: '僅此節點',
    scope_thisNodeAndDescendants: '此節點及子孫節點',
    scope_descendantsOnly: '僅子孫節點',

    docTypePermissions_menuLabel: '文檔類型權限編輯器',
    docTypePermissions_insertOptionsMenuLabel: '插入選項檢視器',
    docTypePermissions_workspaceTitle: '文檔類型權限編輯器',
    docTypePermissions_auditTitle: '插入選項檢視器',
    docTypePermissions_allDocTypes: '所有文檔類型',
    docTypePermissions_verbInsert: '插入',
    docTypePermissions_documentType: '文檔類型',
    chooseDocType: '選擇文檔類型',
    notAnInsertOption: '此文檔類型目前不是此節點的插入選項。',
    notAnInsertOptionAllowedNote: '此文檔類型不是此節點的插入選項，但若是的話會被允許。',
    notAnInsertOptionDeniedNote: '此文檔類型不是此節點的插入選項，但若是的話會被拒絕。',
    docTypePermissions_pickDocType: '— 選擇文檔類型 —',
    docTypePermissions_pickToStart: '選擇使用者群組與文檔類型以開始。',
    docTypePermissions_defaultRowLabel: '預設（套用至各處）',
    docTypePermissions_pendingNodeLabel: '（未儲存的節點）',
    docTypePermissions_addScopeNode: '新增範圍覆寫',
    docTypePermissions_notSet: '未設定',
    docTypePermissions_noResults: '找不到文檔類型。',
    docTypePermissions_useRoot: '使用根節點',
    docTypePermissions_pickedNode: '節點：',
    docTypePermissions_rootLevel: '根層級',
    docTypePermissions_reasoning: '原因',
    docTypePermissions_defaultAllow: '預設允許',
    docTypePermissions_viaDefault: '來自預設列',
  },
} satisfies UmbLocalizationDictionary;
