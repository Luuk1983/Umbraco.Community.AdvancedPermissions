import type { UmbLocalizationDictionary } from '@umbraco-cms/backoffice/localization-api';

export default {
  uap: {
    // ── Navigation ────────────────────────────────────────────────────────
    sectionLabel: '高度なアクセス権限',
    editorsSectionLabel: 'エディタ',
    viewersSectionLabel: 'ビューア',
    permissionsEditor: 'コンテンツアクセス権限エディタ',
    accessViewer: 'アクセスビューア',

    // ── Common ────────────────────────────────────────────────────────────
    roleLabel: 'ユーザーグループ',
    rolePlaceholder: '— ユーザーグループを選択 —',
    userLabel: 'ユーザー',
    saveChanges: '変更を保存',
    discard: '破棄',
    cancel: 'キャンセル',
    apply: '適用',
    close: '閉じる',
    inherit: '継承',
    allow: '許可',
    deny: '拒否',
    umbracoUsers: 'すべてのユーザー',

    // ── Help ──────────────────────────────────────────────────────────────
    help_contentPermissions_description: 'コンテンツツリー全体で、ユーザーグループごとに許可／拒否のアクセス権限を管理します。',
    help_accessViewer_description: '任意のノードでユーザーまたはグループが持つ有効なアクセス権限を、完全な根拠とともに確認します。',
    help_docTypePermissions_description: '各ユーザーグループがどのドキュメントタイプを、どこで作成できるかを決定します。',
    help_insertOptions_description: '各ノードでユーザーまたはユーザーグループが作成できるドキュメントタイプを監査します。',
    help_learnMore: '詳細を見る',
    help_modalTitle: 'ヘルプ',
    help_tabAbout: 'このページについて',
    help_tabConcepts: '概念',
    help_concept_scope_tip: '適用範囲は、ルールがどこまで及ぶかを制御します：このノード、このノードと子孫、または子孫のみ。',
    help_concept_priorityOverride_tip: '優先オーバーライドは、このエントリが通常の解決順序よりも優先されるように強制します。',
    help_concept_allowDeny_tip: '許可はアクセス権限を付与し、拒否はそれを取り消します。未設定のままにすると最も近い上位ノードから継承します。',
    help_concept_reasoning_tip: '任意のセルをクリックすると、アクセス権限がどのように解決されたかを正確に確認できます。',

    // ── Permissions Editor ───────────────────────────────────────────────
    editorHeadline: 'コンテンツアクセス権限エディタ',
    selectRolePrompt: '上部でユーザーグループを選択して、そのアクセス権限を管理します。',
    permissionsSaved: 'アクセス権限を保存しました。',
    saveFailed: (error: string) => `保存に失敗しました: ${error}`,
    contentNodeHeader: 'コンテンツノード',
    contentRoot: 'デフォルトのアクセス権限',
    expand: '展開',
    collapse: '折りたたむ',

    // ── Permission dialog ─────────────────────────────────────────────────
    dialogHeadline: (verb: string, nodeName: string) => `「${nodeName}」の「${verb}」アクセス権限を設定`,
    descendantsSection: '子孫ノード（異なる場合）',
    dialogInstructions: 'このノードのアクセス権限を設定します。デフォルトでは、これはすべての子孫ノードにも適用されます。子孫ノードに異なるアクセス権限を設定するには、「子孫ノード（異なる場合）」を使用します。',
    virtualRootInherit: '未設定（エントリを削除）',
    virtualRootAllow: '許可（すべてのコンテンツ）',
    virtualRootDeny: '拒否（すべてのコンテンツ）',
    dialogResult: '結果',
    previewBothInherit: 'アクセス権限は設定されていません。親ノードから継承します。',
    previewUniform: (action: string) => `このノードとすべての子孫ノードで${action}します。`,
    previewNodeOnly: (action: string) => `このノードのみで${action}します。子孫ノードはこのルールの影響を受けません。`,
    previewDescOnly: (action: string) => `このノードには明示的なアクセス権限はありません。すべての子孫ノードで${action}します。`,
    previewSplit: (nodeAction: string, descAction: string) => `このノードで${nodeAction}します。すべての子孫ノードで${descAction}します。`,
    previewVirtualInherit: 'デフォルトのアクセス権限は設定されていません。',
    previewVirtualSet: (action: string) => `すべてのコンテンツに対してデフォルトで${action}します。`,
    previewPriorityNode: 'このノードに優先オーバーライドが設定されています。',
    previewPriorityDesc: '子孫ノードに優先オーバーライドが設定されています。',
    previewPriorityBoth: '優先オーバーライドが設定されています。',

    // ── Priority override ─────────────────────────────────────────────────
    priorityOverride: '優先オーバーライド',
    priorityOverrideTooltip: (permission: string, nodeName: string) =>
      `ユーザーは複数のユーザーグループに所属できます。通常、有効なアクセス権限は固定の優先順位に従います。このチェックボックスをオンにするとその順序が上書きされ、ここで選択した「${permission}」の設定が、ユーザーの他のグループに関係なく「${nodeName}」に対する結果となることがほとんどです。慎重に使用してください。`,
    priorityOverrideBadgeTitle: 'このエントリに優先オーバーライドが設定されています',
    priorityOverrideWonTitle: '優先オーバーライドにより解決',
    priorityOverrideSuppressedHeader: '優先オーバーライドが結果を変更しました',
    priorityOverrideSuppressedHint: 'これがなければ、結果は次のようになっていました:',

    // ── Access Viewer ─────────────────────────────────────────────────────
    viewerHeadline: 'アクセスビューア',
    byRole: 'ユーザーグループ別',
    byUser: 'ユーザー別',
    chooseRole: 'ユーザーグループを選択',
    chooseUser: 'ユーザーを選択',
    selectSubjectPrompt: '有効なアクセス権限を表示するには、ユーザーグループまたはユーザーを選択します。',
    legendAllow: '許可',
    legendDeny: '拒否',
    clickForReasoning: (label: string) => `${label} — クリックして根拠を表示`,
    subjectOr: 'または',

    // ── Role picker modal ─────────────────────────────────────────────────
    rolePickerHeadline: 'ユーザーグループを選択',
    rolePickerFilter: '入力して絞り込み…',
    rolePickerNoResults: 'フィルターに一致するユーザーグループはありません。',
    rolePickerNameHeader: 'ユーザーグループ',

    // ── User picker modal ─────────────────────────────────────────────────
    userPickerHeadline: 'ユーザーを選択',
    userPickerFilter: '入力して絞り込み…',
    userPickerNoResults: 'フィルターに一致するユーザーはありません。',
    userPickerNameHeader: 'ユーザー',

    // ── Reasoning dialog ──────────────────────────────────────────────────
    reasoningHeadline: (verb: string, nodeName: string) => `「${nodeName}」の「${verb}」アクセス権限`,
    effectiveAllowed: (subject: string, verb: string, nodeName: string) =>
      `${subject} は「${nodeName}」の「${verb}」アクセス権限が許可されています。`,
    effectiveDenied: (subject: string, verb: string, nodeName: string) =>
      `${subject} は「${nodeName}」の「${verb}」アクセス権限が拒否されています。`,
    dialogSecurityHeader: 'セキュリティ',
    defaultPermissions: 'デフォルトのアクセス権限',
    determiningEntry: 'このエントリが優先されます',
    noReasoningData: 'この権限に関するアクセス権限データはありません。',
    defaultAllowNote: 'アクセス権限が設定されていないため、デフォルトで許可されます。',
    defaultDenyNote: 'アクセス権限が設定されていないため、デフォルトで拒否されます。',

    // ── Granular permission redirect ──────────────────────────────────────
    redirectMessage:
      'このユーザーグループのドキュメントアクセス権限は Advanced Permissions パッケージによって管理されます。アクセス権限を設定するには、ユーザーセクションのアクセス権限エディタを開いてください。',

    // ── Doc-Type Permissions ──────────────────────────────────────────────
    role: 'ユーザーグループ',
    pickRole: 'ユーザーグループを選択',
    user: 'ユーザー',
    pickUser: 'ユーザーを選択',
    node: 'ノード',
    pickNode: 'ノードを選択',
    state: '状態',
    scope: '適用範囲',
    scope_thisNodeOnly: 'このノードのみ',
    scope_thisNodeAndDescendants: 'このノードと子孫',
    scope_descendantsOnly: '子孫のみ',

    docTypePermissions_menuLabel: 'ドキュメントタイプアクセス権限エディタ',
    docTypePermissions_insertOptionsMenuLabel: '挿入オプションビューア',
    docTypePermissions_workspaceTitle: 'ドキュメントタイプアクセス権限エディタ',
    docTypePermissions_auditTitle: '挿入オプションビューア',
    docTypePermissions_allDocTypes: 'すべてのドキュメントタイプ',
    docTypePermissions_verbInsert: '挿入',
    docTypePermissions_documentType: 'ドキュメントタイプ',
    chooseDocType: 'ドキュメントタイプを選択',
    notAnInsertOption: 'このドキュメントタイプは現在、このノードの挿入オプションではありません。',
    notAnInsertOptionAllowedNote: 'このドキュメントタイプはこのノードの挿入オプションではありませんが、それ以外では許可されます。',
    notAnInsertOptionDeniedNote: 'このドキュメントタイプはこのノードの挿入オプションではありませんが、それ以外では拒否されます。',
    docTypePermissions_pickDocType: '— ドキュメントタイプを選択 —',
    docTypePermissions_pickToStart: '開始するにはユーザーグループとドキュメントタイプを選択してください。',
    docTypePermissions_defaultRowLabel: 'デフォルト（すべてに適用）',
    docTypePermissions_pendingNodeLabel: '（未保存のノード）',
    docTypePermissions_addScopeNode: '適用範囲のオーバーライドを追加',
    docTypePermissions_notSet: '未設定',
    docTypePermissions_noResults: 'ドキュメントタイプが見つかりません。',
    docTypePermissions_useRoot: 'ルートを使用',
    docTypePermissions_pickedNode: 'ノード:',
    docTypePermissions_rootLevel: 'ルートレベル',
    docTypePermissions_reasoning: '根拠',
    docTypePermissions_defaultAllow: 'デフォルトで許可',
    docTypePermissions_viaDefault: 'デフォルト行から',
  },
} satisfies UmbLocalizationDictionary;
