import type { UmbLocalizationDictionary } from '@umbraco-cms/backoffice/localization-api';

export default {
  uap: {
    // ── Navigation ────────────────────────────────────────────────────────
    sectionLabel: '고급 권한',
    editorsSectionLabel: '편집기',
    viewersSectionLabel: '뷰어',
    permissionsEditor: '콘텐츠 권한 편집기',
    accessViewer: '접근 권한 뷰어',

    // ── Common ────────────────────────────────────────────────────────────
    roleLabel: '사용자 그룹',
    rolePlaceholder: '— 사용자 그룹 선택 —',
    userLabel: '사용자',
    saveChanges: '변경사항 저장',
    discard: '변경 취소',
    cancel: '취소',
    apply: '적용',
    close: '닫기',
    inherit: '상속',
    allow: '허용',
    deny: '거부',
    umbracoUsers: '모든 사용자',

    // ── Help ──────────────────────────────────────────────────────────────
    help_contentPermissions_description: '콘텐츠 트리 전반에서 사용자 그룹별 허용/거부 권한을 관리합니다.',
    help_accessViewer_description: '사용자 또는 그룹이 임의의 노드에서 갖는 유효 권한을 전체 근거와 함께 확인합니다.',
    help_docTypePermissions_description: '각 사용자 그룹이 생성할 수 있는 문서 유형과 생성 위치를 결정합니다.',
    help_insertOptions_description: '사용자 또는 사용자 그룹이 각 노드에서 생성할 수 있는 문서 유형을 감사합니다.',
    help_learnMore: '자세히 알아보기',
    help_modalTitle: '도움말',
    help_tabAbout: '이 페이지 정보',
    help_tabConcepts: '개념',
    help_concept_scope_tip: '범위는 규칙이 미치는 범위를 제어합니다: 이 노드, 이 노드 및 하위 항목, 또는 하위 항목만.',
    help_concept_priorityOverride_tip: '우선순위 재정의는 일반적인 결정 순서보다 이 항목이 우선하도록 강제합니다.',
    help_concept_allowDeny_tip: '허용은 권한을 부여하고, 거부는 권한을 취소하며, 설정하지 않으면 가장 가까운 상위 항목에서 상속합니다.',
    help_concept_reasoning_tip: '셀을 클릭하면 권한이 어떻게 결정되었는지 정확히 확인할 수 있습니다.',

    // ── Permissions Editor ───────────────────────────────────────────────
    editorHeadline: '콘텐츠 권한 편집기',
    selectRolePrompt: '권한을 관리하려면 위에서 사용자 그룹을 선택하세요.',
    permissionsSaved: '권한이 저장되었습니다.',
    saveFailed: (error: string) => `저장 실패: ${error}`,
    contentNodeHeader: '콘텐츠 노드',
    contentRoot: '기본 권한',
    expand: '펼치기',
    collapse: '접기',

    // ── Permission dialog ─────────────────────────────────────────────────
    dialogHeadline: (verb: string, nodeName: string) => `‘${nodeName}’에 대한 ${verb} 권한 설정`,
    descendantsSection: '하위 항목 (다를 경우)',
    dialogInstructions: '이 노드에 대한 권한을 설정합니다. 기본적으로 모든 하위 항목에도 적용됩니다. 하위 노드에 다른 권한을 설정하려면 “하위 항목 (다를 경우)”을 사용하세요.',
    virtualRootInherit: '설정 안 함 (항목 제거)',
    virtualRootAllow: '허용 (모든 콘텐츠)',
    virtualRootDeny: '거부 (모든 콘텐츠)',
    dialogResult: '결과',
    previewBothInherit: '권한이 설정되지 않았습니다. 상위 항목에서 상속합니다.',
    previewUniform: (action: string) => `이 노드 및 모든 하위 항목에 ${action}.`,
    previewNodeOnly: (action: string) => `이 노드에만 ${action}. 하위 항목은 이 규칙의 영향을 받지 않습니다.`,
    previewDescOnly: (action: string) => `이 노드에는 명시적 권한이 없습니다. 모든 하위 항목에 ${action}.`,
    previewSplit: (nodeAction: string, descAction: string) => `이 노드에 ${nodeAction}. 모든 하위 항목에 ${descAction}.`,
    previewVirtualInherit: '기본 권한이 설정되지 않았습니다.',
    previewVirtualSet: (action: string) => `모든 콘텐츠에 기본적으로 ${action}.`,
    previewPriorityNode: '이 노드에 우선순위 재정의가 설정되어 있습니다.',
    previewPriorityDesc: '하위 항목에 우선순위 재정의가 설정되어 있습니다.',
    previewPriorityBoth: '우선순위 재정의가 설정되어 있습니다.',

    // ── Priority override ─────────────────────────────────────────────────
    priorityOverride: '우선순위 재정의',
    priorityOverrideTooltip: (permission: string, nodeName: string) =>
      `한 사용자는 여러 사용자 그룹에 속할 수 있습니다. 일반적으로 유효 권한은 고정된 우선순위 순서를 따릅니다. 이 확인란을 선택하면 해당 순서를 재정의하므로, 여기서 선택한 “${permission}” 설정이 사용자의 다른 그룹과 관계없이 “${nodeName}”에 대해 거의 항상 결과가 됩니다. 신중하게 사용하세요.`,
    priorityOverrideBadgeTitle: '이 항목에 우선순위 재정의가 설정되어 있습니다',
    priorityOverrideWonTitle: '우선순위 재정의로 결정됨',
    priorityOverrideSuppressedHeader: '우선순위 재정의로 결과가 변경되었습니다',
    priorityOverrideSuppressedHint: '재정의가 없었다면 결과는 다음과 같았을 것입니다:',

    // ── Access Viewer ─────────────────────────────────────────────────────
    viewerHeadline: '접근 권한 뷰어',
    byRole: '사용자 그룹별',
    byUser: '사용자별',
    chooseRole: '사용자 그룹 선택',
    chooseUser: '사용자 선택',
    selectSubjectPrompt: '유효 권한을 보려면 사용자 그룹 또는 사용자를 선택하세요.',
    legendAllow: '허용',
    legendDeny: '거부',
    clickForReasoning: (label: string) => `${label} — 근거를 보려면 클릭`,
    subjectOr: '또는',

    // ── Role picker modal ─────────────────────────────────────────────────
    rolePickerHeadline: '사용자 그룹 선택',
    rolePickerFilter: '필터링하려면 입력…',
    rolePickerNoResults: '필터와 일치하는 사용자 그룹이 없습니다.',
    rolePickerNameHeader: '사용자 그룹',

    // ── User picker modal ─────────────────────────────────────────────────
    userPickerHeadline: '사용자 선택',
    userPickerFilter: '필터링하려면 입력…',
    userPickerNoResults: '필터와 일치하는 사용자가 없습니다.',
    userPickerNameHeader: '사용자',

    // ── Reasoning dialog ──────────────────────────────────────────────────
    reasoningHeadline: (verb: string, nodeName: string) => `“${nodeName}”에 대한 ${verb} 권한`,
    effectiveAllowed: (subject: string, verb: string, nodeName: string) =>
      `${subject}에게 “${nodeName}”에 대한 ${verb} 권한이 허용되었습니다.`,
    effectiveDenied: (subject: string, verb: string, nodeName: string) =>
      `${subject}에게 “${nodeName}”에 대한 ${verb} 권한이 거부되었습니다.`,
    dialogSecurityHeader: '보안',
    defaultPermissions: '기본 권한',
    determiningEntry: '이 항목이 우선합니다',
    noReasoningData: '이 동작에 대한 권한 데이터가 없습니다.',
    defaultAllowNote: '설정된 권한이 없으며, 기본적으로 허용됩니다.',
    defaultDenyNote: '설정된 권한이 없으며, 기본적으로 거부됩니다.',

    // ── Granular permission redirect ──────────────────────────────────────
    redirectMessage:
      '이 사용자 그룹의 문서 권한은 Advanced Permissions 패키지에서 관리합니다. 권한을 구성하려면 사용자 섹션에서 권한 편집기를 여세요.',

    // ── Doc-Type Permissions ──────────────────────────────────────────────
    role: '사용자 그룹',
    pickRole: '사용자 그룹 선택',
    user: '사용자',
    pickUser: '사용자 선택',
    node: '노드',
    pickNode: '노드 선택',
    state: '상태',
    scope: '범위',
    scope_thisNodeOnly: '이 노드만',
    scope_thisNodeAndDescendants: '이 노드 및 하위 항목',
    scope_descendantsOnly: '하위 항목만',

    docTypePermissions_menuLabel: '문서 유형 권한 편집기',
    docTypePermissions_insertOptionsMenuLabel: '삽입 옵션 뷰어',
    docTypePermissions_workspaceTitle: '문서 유형 권한 편집기',
    docTypePermissions_auditTitle: '삽입 옵션 뷰어',
    docTypePermissions_allDocTypes: '모든 문서 유형',
    docTypePermissions_verbInsert: '삽입',
    docTypePermissions_documentType: '문서 유형',
    chooseDocType: '문서 유형 선택',
    notAnInsertOption: '이 문서 유형은 현재 이 노드의 삽입 옵션이 아닙니다.',
    notAnInsertOptionAllowedNote: '이 문서 유형은 이 노드의 삽입 옵션이 아니지만, 그렇지 않으면 허용됩니다.',
    notAnInsertOptionDeniedNote: '이 문서 유형은 이 노드의 삽입 옵션이 아니지만, 그렇지 않으면 거부됩니다.',
    docTypePermissions_pickDocType: '— 문서 유형 선택 —',
    docTypePermissions_pickToStart: '시작하려면 사용자 그룹과 문서 유형을 선택하세요.',
    docTypePermissions_defaultRowLabel: '기본 (모든 곳에 적용)',
    docTypePermissions_pendingNodeLabel: '(저장되지 않은 노드)',
    docTypePermissions_addScopeNode: '범위 재정의 추가',
    docTypePermissions_notSet: '설정 안 함',
    docTypePermissions_noResults: '문서 유형을 찾을 수 없습니다.',
    docTypePermissions_useRoot: '루트 사용',
    docTypePermissions_pickedNode: '노드:',
    docTypePermissions_rootLevel: '루트 수준',
    docTypePermissions_reasoning: '근거',
    docTypePermissions_defaultAllow: '기본적으로 허용됨',
    docTypePermissions_viaDefault: '기본 행에서',
  },
} satisfies UmbLocalizationDictionary;
