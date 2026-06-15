import type { UmbLocalizationDictionary } from '@umbraco-cms/backoffice/localization-api';

export default {
  uap: {
    // ── Navigation ────────────────────────────────────────────────────────
    sectionLabel: 'Phân quyền nâng cao',
    editorsSectionLabel: 'Trình chỉnh sửa',
    viewersSectionLabel: 'Trình xem',
    permissionsEditor: 'Trình chỉnh sửa quyền nội dung',
    accessViewer: 'Trình xem quyền truy cập',

    // ── Common ────────────────────────────────────────────────────────────
    roleLabel: 'Nhóm người dùng',
    rolePlaceholder: '— Chọn một nhóm người dùng —',
    userLabel: 'Người dùng',
    saveChanges: 'Lưu thay đổi',
    discard: 'Bỏ',
    cancel: 'Hủy',
    apply: 'Áp dụng',
    close: 'Đóng',
    inherit: 'Kế thừa',
    allow: 'Cho phép',
    deny: 'Từ chối',
    umbracoUsers: 'Tất cả người dùng',

    // ── Permissions Editor ───────────────────────────────────────────────
    editorHeadline: 'Trình chỉnh sửa quyền nội dung',
    selectRolePrompt: 'Chọn một nhóm người dùng ở trên để quản lý quyền của nhóm đó.',
    permissionsSaved: 'Đã lưu quyền.',
    saveFailed: (error: string) => `Lưu thất bại: ${error}`,
    contentNodeHeader: 'Nút nội dung',
    contentRoot: 'Quyền mặc định',
    expand: 'Mở rộng',
    collapse: 'Thu gọn',

    // ── Permission dialog ─────────────────────────────────────────────────
    dialogHeadline: (verb: string, nodeName: string) => `Đặt quyền ${verb} cho ‘${nodeName}’`,
    descendantsSection: 'Các nút con cháu (nếu khác)',
    dialogInstructions: 'Đặt quyền cho nút này. Theo mặc định, quyền này cũng áp dụng cho tất cả các nút con cháu. Sử dụng “Các nút con cháu (nếu khác)” để đặt một quyền khác cho các nút con cháu.',
    virtualRootInherit: 'Không đặt (xóa mục)',
    virtualRootAllow: 'Cho phép (tất cả nội dung)',
    virtualRootDeny: 'Từ chối (tất cả nội dung)',
    dialogResult: 'Kết quả',
    previewBothInherit: 'Chưa đặt quyền. Kế thừa từ nút cha.',
    previewUniform: (action: string) => `${action} trên nút này và tất cả các nút con cháu.`,
    previewNodeOnly: (action: string) => `${action} chỉ trên nút này. Các nút con cháu không bị ảnh hưởng bởi quy tắc này.`,
    previewDescOnly: (action: string) => `Không có quyền rõ ràng trên nút này. ${action} trên tất cả các nút con cháu.`,
    previewSplit: (nodeAction: string, descAction: string) => `${nodeAction} trên nút này. ${descAction} trên tất cả các nút con cháu.`,
    previewVirtualInherit: 'Chưa đặt quyền mặc định.',
    previewVirtualSet: (action: string) => `${action} theo mặc định cho tất cả nội dung.`,
    previewPriorityNode: 'Ghi đè ưu tiên được đặt trên nút này.',
    previewPriorityDesc: 'Ghi đè ưu tiên được đặt trên các nút con cháu.',
    previewPriorityBoth: 'Ghi đè ưu tiên đã được đặt.',

    // ── Priority override ─────────────────────────────────────────────────
    priorityOverride: 'Ghi đè ưu tiên',
    priorityOverrideTooltip: (permission: string, nodeName: string) =>
      `Một người dùng có thể thuộc nhiều nhóm người dùng. Thông thường, quyền hiệu lực tuân theo một thứ tự ưu tiên cố định. Đánh dấu vào ô này sẽ ghi đè thứ tự đó, vì vậy cài đặt “${permission}” mà bạn chọn ở đây gần như luôn trở thành kết quả cho “${nodeName}”, bất kể các nhóm khác của người dùng. Hãy sử dụng một cách thận trọng.`,
    priorityOverrideBadgeTitle: 'Ghi đè ưu tiên được đặt trên mục này',
    priorityOverrideWonTitle: 'Được giải quyết qua ghi đè ưu tiên',
    priorityOverrideSuppressedHeader: 'Ghi đè ưu tiên đã thay đổi kết quả',
    priorityOverrideSuppressedHint: 'Nếu không có nó, kết quả sẽ là:',

    // ── Access Viewer ─────────────────────────────────────────────────────
    viewerHeadline: 'Trình xem quyền truy cập',
    byRole: 'Theo nhóm người dùng',
    byUser: 'Theo người dùng',
    chooseRole: 'Chọn nhóm người dùng',
    chooseUser: 'Chọn người dùng',
    selectSubjectPrompt: 'Chọn một nhóm người dùng hoặc người dùng để xem quyền hiệu lực.',
    legendAllow: 'Cho phép',
    legendDeny: 'Từ chối',
    clickForReasoning: (label: string) => `${label} — nhấp để xem lý giải`,
    subjectOr: 'hoặc',

    // ── Role picker modal ─────────────────────────────────────────────────
    rolePickerHeadline: 'Chọn một nhóm người dùng',
    rolePickerFilter: 'Nhập để lọc…',
    rolePickerNoResults: 'Không có nhóm người dùng nào khớp với bộ lọc.',
    rolePickerNameHeader: 'Nhóm người dùng',

    // ── User picker modal ─────────────────────────────────────────────────
    userPickerHeadline: 'Chọn một người dùng',
    userPickerFilter: 'Nhập để lọc…',
    userPickerNoResults: 'Không có người dùng nào khớp với bộ lọc.',
    userPickerNameHeader: 'Người dùng',

    // ── Reasoning dialog ──────────────────────────────────────────────────
    reasoningHeadline: (verb: string, nodeName: string) => `Quyền ${verb} cho “${nodeName}”`,
    effectiveAllowed: (subject: string, verb: string, nodeName: string) =>
      `${subject} đã được cho phép quyền ${verb} cho “${nodeName}”.`,
    effectiveDenied: (subject: string, verb: string, nodeName: string) =>
      `${subject} đã bị từ chối quyền ${verb} cho “${nodeName}”.`,
    dialogSecurityHeader: 'Bảo mật',
    defaultPermissions: 'Quyền mặc định',
    determiningEntry: 'Mục này được ưu tiên',
    noReasoningData: 'Không có dữ liệu quyền nào cho động từ này.',
    defaultAllowNote: 'Không có quyền nào được đặt, điều này được cho phép theo mặc định.',
    defaultDenyNote: 'Không có quyền nào được đặt, điều này bị từ chối theo mặc định.',

    // ── Granular permission redirect ──────────────────────────────────────
    redirectMessage:
      'Quyền tài liệu cho nhóm người dùng này được quản lý bởi gói Advanced Permissions. Mở Trình chỉnh sửa quyền trong mục Người dùng để cấu hình quyền.',

    // ── Doc-Type Permissions ──────────────────────────────────────────────
    role: 'Nhóm người dùng',
    pickRole: 'Chọn nhóm người dùng',
    user: 'Người dùng',
    pickUser: 'Chọn người dùng',
    node: 'Nút',
    pickNode: 'Chọn nút',
    state: 'Trạng thái',
    scope: 'Phạm vi',
    scope_thisNodeOnly: 'Chỉ nút này',
    scope_thisNodeAndDescendants: 'Nút này và các nút con cháu',
    scope_descendantsOnly: 'Chỉ các nút con cháu',

    docTypePermissions_menuLabel: 'Trình chỉnh sửa quyền loại tài liệu',
    docTypePermissions_insertOptionsMenuLabel: 'Trình xem tùy chọn chèn',
    docTypePermissions_workspaceTitle: 'Trình chỉnh sửa quyền loại tài liệu',
    docTypePermissions_auditTitle: 'Trình xem tùy chọn chèn',
    docTypePermissions_allDocTypes: 'Tất cả loại tài liệu',
    docTypePermissions_verbInsert: 'Chèn',
    docTypePermissions_documentType: 'Loại tài liệu',
    chooseDocType: 'Chọn loại tài liệu',
    notAnInsertOption: 'Loại tài liệu này hiện không phải là một tùy chọn chèn trên nút này.',
    notAnInsertOptionAllowedNote: 'Loại tài liệu này không phải là một tùy chọn chèn trên nút này, nhưng nếu khác thì sẽ được cho phép.',
    notAnInsertOptionDeniedNote: 'Loại tài liệu này không phải là một tùy chọn chèn trên nút này, nhưng nếu khác thì sẽ bị từ chối.',
    docTypePermissions_pickDocType: '— Chọn một loại tài liệu —',
    docTypePermissions_pickToStart: 'Chọn một nhóm người dùng và loại tài liệu để bắt đầu.',
    docTypePermissions_defaultRowLabel: 'Mặc định (áp dụng ở mọi nơi)',
    docTypePermissions_pendingNodeLabel: '(nút chưa lưu)',
    docTypePermissions_addScopeNode: 'Thêm ghi đè phạm vi',
    docTypePermissions_notSet: 'Chưa đặt',
    docTypePermissions_noResults: 'Không tìm thấy loại tài liệu nào.',
    docTypePermissions_useRoot: 'Dùng gốc',
    docTypePermissions_pickedNode: 'Nút:',
    docTypePermissions_rootLevel: 'Cấp gốc',
    docTypePermissions_reasoning: 'Lý giải',
    docTypePermissions_defaultAllow: 'Được cho phép theo mặc định',
    docTypePermissions_viaDefault: 'từ hàng mặc định',
  },
} satisfies UmbLocalizationDictionary;
