import type { UmbLocalizationDictionary } from '@umbraco-cms/backoffice/localization-api';

export default {
  uap: {
    // ── Navigation ────────────────────────────────────────────────────────
    sectionLabel: '高级权限',
    editorsSectionLabel: '编辑器',
    viewersSectionLabel: '查看器',
    permissionsEditor: '内容权限编辑器',
    accessViewer: '访问查看器',

    // ── Common ────────────────────────────────────────────────────────────
    roleLabel: '用户组',
    rolePlaceholder: '— 选择一个用户组 —',
    userLabel: '用户',
    saveChanges: '保存更改',
    discard: '丢弃',
    cancel: '取消',
    apply: '应用',
    close: '关闭',
    inherit: '继承',
    allow: '允许',
    deny: '拒绝',
    umbracoUsers: '所有用户',

    // ── Help ──────────────────────────────────────────────────────────────
    help_contentPermissions_description: '跨内容树为每个用户组管理“允许/拒绝”权限。',
    help_accessViewer_description: '查看用户或用户组在任意节点上的有效权限，并提供完整的推理过程。',
    help_docTypePermissions_description: '决定每个用户组可以创建哪些文档类型，以及在何处创建。',
    help_insertOptions_description: '审核用户或用户组可以在每个节点上创建哪些文档类型。',
    help_learnMore: '了解更多',
    help_modalTitle: '帮助',
    help_tabAbout: '关于此页面',
    help_tabConcepts: '概念',
    help_concept_scope_tip: '范围控制规则的作用范围：仅此节点、此节点及其后代节点，或仅后代节点。',
    help_concept_priorityOverride_tip: '优先级覆盖会强制此条目优先于正常的解析顺序。',
    help_concept_allowDeny_tip: '“允许”授予该权限，“拒绝”撤销该权限，不设置则从最近的祖先节点继承。',
    help_concept_reasoning_tip: '点击任意单元格可查看该权限的具体解析过程。',

    // ── Permissions Editor ───────────────────────────────────────────────
    editorHeadline: '内容权限编辑器',
    selectRolePrompt: '在上方选择一个用户组以管理其权限。',
    permissionsSaved: '权限已保存。',
    saveFailed: (error: string) => `保存失败：${error}`,
    contentNodeHeader: '内容节点',
    contentRoot: '默认权限',
    expand: '展开',
    collapse: '收起',

    // ── Permission dialog ─────────────────────────────────────────────────
    dialogHeadline: (verb: string, nodeName: string) => `为‘${nodeName}’设置“${verb}”权限`,
    descendantsSection: '后代节点（如有不同）',
    dialogInstructions: '为此节点设置权限。默认情况下，这也会应用于所有后代节点。使用“后代节点（如有不同）”可为后代节点设置不同的权限。',
    virtualRootInherit: '未设置（移除条目）',
    virtualRootAllow: '允许（所有内容）',
    virtualRootDeny: '拒绝（所有内容）',
    dialogResult: '结果',
    previewBothInherit: '未设置权限。从父节点继承。',
    previewUniform: (action: string) => `${action}此节点及所有后代节点。`,
    previewNodeOnly: (action: string) => `仅${action}此节点。后代节点不受此规则影响。`,
    previewDescOnly: (action: string) => `此节点没有显式权限。${action}所有后代节点。`,
    previewSplit: (nodeAction: string, descAction: string) => `${nodeAction}此节点。${descAction}所有后代节点。`,
    previewVirtualInherit: '未设置默认权限。',
    previewVirtualSet: (action: string) => `默认${action}所有内容。`,
    previewPriorityNode: '此节点已设置优先级覆盖。',
    previewPriorityDesc: '后代节点已设置优先级覆盖。',
    previewPriorityBoth: '已设置优先级覆盖。',

    // ── Priority override ─────────────────────────────────────────────────
    priorityOverride: '优先级覆盖',
    priorityOverrideTooltip: (permission: string, nodeName: string) =>
      `一个用户可以属于多个用户组。通常，有效权限会遵循固定的优先级顺序。勾选此框将覆盖该顺序，使您在此处选择的“${permission}”设置几乎总是成为“${nodeName}”的结果，而不论该用户所属的其他组如何。请谨慎使用。`,
    priorityOverrideBadgeTitle: '此条目已设置优先级覆盖',
    priorityOverrideWonTitle: '通过优先级覆盖解析',
    priorityOverrideSuppressedHeader: '优先级覆盖改变了结果',
    priorityOverrideSuppressedHint: '如果没有它，结果将会是：',

    // ── Access Viewer ─────────────────────────────────────────────────────
    viewerHeadline: '访问查看器',
    byRole: '按用户组',
    byUser: '按用户',
    chooseRole: '选择用户组',
    chooseUser: '选择用户',
    selectSubjectPrompt: '选择一个用户组或用户以查看有效权限。',
    legendAllow: '允许',
    legendDeny: '拒绝',
    clickForReasoning: (label: string) => `${label} — 点击查看推理`,
    subjectOr: '或',

    // ── Role picker modal ─────────────────────────────────────────────────
    rolePickerHeadline: '选择一个用户组',
    rolePickerFilter: '输入以过滤…',
    rolePickerNoResults: '没有与过滤条件匹配的用户组。',
    rolePickerNameHeader: '用户组',

    // ── User picker modal ─────────────────────────────────────────────────
    userPickerHeadline: '选择一个用户',
    userPickerFilter: '输入以过滤…',
    userPickerNoResults: '没有与过滤条件匹配的用户。',
    userPickerNameHeader: '用户',

    // ── Reasoning dialog ──────────────────────────────────────────────────
    reasoningHeadline: (verb: string, nodeName: string) => `“${nodeName}”的“${verb}”权限`,
    effectiveAllowed: (subject: string, verb: string, nodeName: string) =>
      `${subject}已被允许对“${nodeName}”的“${verb}”权限。`,
    effectiveDenied: (subject: string, verb: string, nodeName: string) =>
      `${subject}已被拒绝对“${nodeName}”的“${verb}”权限。`,
    dialogSecurityHeader: '安全',
    defaultPermissions: '默认权限',
    determiningEntry: '此条目优先',
    noReasoningData: '此权限没有可用的权限数据。',
    defaultAllowNote: '未设置任何权限，默认允许。',
    defaultDenyNote: '未设置任何权限，默认拒绝。',

    // ── Granular permission redirect ──────────────────────────────────────
    redirectMessage:
      '此用户组的文档权限由 Advanced Permissions 包管理。请在“用户”区域打开权限编辑器以配置权限。',

    // ── Doc-Type Permissions ──────────────────────────────────────────────
    role: '用户组',
    pickRole: '选择用户组',
    user: '用户',
    pickUser: '选择用户',
    node: '节点',
    pickNode: '选择节点',
    state: '状态',
    scope: '范围',
    scope_thisNodeOnly: '仅此节点',
    scope_thisNodeAndDescendants: '此节点及后代节点',
    scope_descendantsOnly: '仅后代节点',

    docTypePermissions_menuLabel: '文档类型权限编辑器',
    docTypePermissions_insertOptionsMenuLabel: '插入选项查看器',
    docTypePermissions_workspaceTitle: '文档类型权限编辑器',
    docTypePermissions_auditTitle: '插入选项查看器',
    docTypePermissions_allDocTypes: '所有文档类型',
    docTypePermissions_verbInsert: '插入',
    docTypePermissions_documentType: '文档类型',
    chooseDocType: '选择文档类型',
    notAnInsertOption: '此文档类型当前不是该节点上的插入选项。',
    notAnInsertOptionAllowedNote: '此文档类型不是该节点上的插入选项，否则将被允许。',
    notAnInsertOptionDeniedNote: '此文档类型不是该节点上的插入选项，否则将被拒绝。',
    docTypePermissions_pickDocType: '— 选择一个文档类型 —',
    docTypePermissions_pickToStart: '选择一个用户组和文档类型以开始。',
    docTypePermissions_defaultRowLabel: '默认（应用于所有位置）',
    docTypePermissions_pendingNodeLabel: '（未保存的节点）',
    docTypePermissions_addScopeNode: '添加范围覆盖',
    docTypePermissions_notSet: '未设置',
    docTypePermissions_noResults: '未找到文档类型。',
    docTypePermissions_useRoot: '使用根节点',
    docTypePermissions_pickedNode: '节点：',
    docTypePermissions_rootLevel: '根级别',
    docTypePermissions_reasoning: '推理',
    docTypePermissions_defaultAllow: '默认允许',
    docTypePermissions_viaDefault: '来自默认行',
  },
} satisfies UmbLocalizationDictionary;
