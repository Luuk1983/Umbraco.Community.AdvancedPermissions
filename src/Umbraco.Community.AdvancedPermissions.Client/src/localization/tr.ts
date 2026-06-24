import type { UmbLocalizationDictionary } from '@umbraco-cms/backoffice/localization-api';

export default {
  uap: {
    // ── Gezinme ───────────────────────────────────────────────────────────
    sectionLabel: 'Gelişmiş İzinler',
    editorsSectionLabel: 'Düzenleyiciler',
    viewersSectionLabel: 'Görüntüleyiciler',
    permissionsEditor: 'İçerik İzinleri Düzenleyicisi',
    accessViewer: 'Erişim Görüntüleyici',

    // ── Genel ─────────────────────────────────────────────────────────────
    roleLabel: 'Kullanıcı Grubu',
    rolePlaceholder: '— Bir kullanıcı grubu seçin —',
    userLabel: 'Kullanıcı',
    saveChanges: 'Değişiklikleri Kaydet',
    discard: 'Gözden çıkar',
    cancel: 'İptal',
    apply: 'Uygula',
    close: 'Kapat',
    inherit: 'Devral',
    allow: 'İzin ver',
    deny: 'Reddet',
    umbracoUsers: 'Tüm Kullanıcılar',

    // ── Help ──────────────────────────────────────────────────────────────
    help_contentPermissions_description: 'İçerik ağacınız genelinde her kullanıcı grubu için İzin ver/Reddet izinlerini yönetin.',
    help_accessViewer_description: 'Bir kullanıcının veya grubun herhangi bir düğümde sahip olduğu etkin izinleri, tam gerekçesiyle birlikte görün.',
    help_docTypePermissions_description: 'Her kullanıcı grubunun hangi belge türlerini ve nerede oluşturabileceğine karar verin.',
    help_insertOptions_description: 'Bir kullanıcının veya kullanıcı grubunun her düğümde hangi belge türlerini oluşturabileceğini denetleyin.',
    help_learnMore: 'Daha fazla bilgi',
    help_modalTitle: 'Yardım',
    help_tabAbout: 'Bu sayfa hakkında',
    help_tabConcepts: 'Kavramlar',
    help_concept_scope_tip: 'Kapsam, bir kuralın ne kadar uzağa eriştiğini denetler: bu düğüm, bu düğüm ve alt düğümleri veya yalnızca alt düğümler.',
    help_concept_priorityOverride_tip: 'Öncelik geçersiz kılma, bu kaydı normal çözümleme sırasına göre öne çıkmaya zorlar.',
    help_concept_allowDeny_tip: 'İzin ver iznini verir, Reddet onu geri alır ve ayarlanmadan bırakmak en yakın üst düğümden devralır.',
    help_concept_reasoning_tip: 'İznin tam olarak nasıl çözümlendiğini görmek için herhangi bir hücreye tıklayın.',

    // ── İzin Düzenleyicisi ───────────────────────────────────────────────
    editorHeadline: 'İçerik İzinleri Düzenleyicisi',
    selectRolePrompt: 'İzinlerini yönetmek için yukarıdan bir kullanıcı grubu seçin.',
    permissionsSaved: 'İzinler kaydedildi.',
    saveFailed: (error: string) => `Kaydetme başarısız: ${error}`,
    contentNodeHeader: 'İçerik Düğümü',
    contentRoot: 'Varsayılan izinler',
    expand: 'Genişlet',
    collapse: 'Daralt',

    // ── İzin iletişim kutusu ──────────────────────────────────────────────
    dialogHeadline: (verb: string, nodeName: string) => `‘${nodeName}’ için ${verb} iznini ayarlayın`,
    descendantsSection: 'Alt düğümler (farklıysa)',
    dialogInstructions: 'Bu düğüm için izni ayarlayın. Varsayılan olarak bu, tüm alt düğümler için de geçerlidir. Alt düğümler için farklı bir izin ayarlamak üzere “Alt düğümler (farklıysa)” seçeneğini kullanın.',
    virtualRootInherit: 'Ayarlanmadı (kaydı kaldır)',
    virtualRootAllow: 'İzin ver (tüm içerik)',
    virtualRootDeny: 'Reddet (tüm içerik)',
    dialogResult: 'Sonuç',
    previewBothInherit: 'İzin ayarlanmadı. Üst düğümden devralır.',
    previewUniform: (action: string) => `Bu düğümde ve tüm alt düğümlerde ${action}.`,
    previewNodeOnly: (action: string) => `Yalnızca bu düğümde ${action}. Alt düğümler bu kuraldan etkilenmez.`,
    previewDescOnly: (action: string) => `Bu düğümde açık bir izin yok. Tüm alt düğümlerde ${action}.`,
    previewSplit: (nodeAction: string, descAction: string) => `Bu düğümde ${nodeAction}. Tüm alt düğümlerde ${descAction}.`,
    previewVirtualInherit: 'Varsayılan izin ayarlanmadı.',
    previewVirtualSet: (action: string) => `Tüm içerik için varsayılan olarak ${action}.`,
    previewPriorityNode: 'Öncelik geçersiz kılma bu düğümde ayarlı.',
    previewPriorityDesc: 'Öncelik geçersiz kılma alt düğümlerde ayarlı.',
    previewPriorityBoth: 'Öncelik geçersiz kılma ayarlı.',

    // ── Öncelik geçersiz kılma ────────────────────────────────────────
    priorityOverride: 'Öncelik geçersiz kılma',
    priorityOverrideTooltip: (permission: string, nodeName: string) =>
      `Bir kullanıcı birden fazla kullanıcı grubuna ait olabilir. Normalde etkin izin, sabit bir öncelik sırasını izler. Bu kutuyu işaretlemek bu sırayı geçersiz kılar; böylece burada seçtiğiniz “${permission}” ayarı, kullanıcının diğer gruplarından bağımsız olarak “${nodeName}” için neredeyse her zaman sonuç olur. Dikkatli kullanın.`,
    priorityOverrideBadgeTitle: 'Bu kayıtta öncelik geçersiz kılma ayarlı',
    priorityOverrideWonTitle: 'Öncelik geçersiz kılma ile çözüldü',
    priorityOverrideSuppressedHeader: 'Öncelik geçersiz kılma sonucu değiştirdi',
    priorityOverrideSuppressedHint: 'Bu olmadan sonuç şöyle olurdu:',

    // ── Erişim Görüntüleyici ──────────────────────────────────────────────
    viewerHeadline: 'Erişim Görüntüleyici',
    byRole: 'Kullanıcı grubuna göre',
    byUser: 'Kullanıcıya göre',
    chooseRole: 'Kullanıcı grubu seçin',
    chooseUser: 'Kullanıcı seçin',
    selectSubjectPrompt: 'Etkin izinleri görüntülemek için bir kullanıcı grubu veya kullanıcı seçin.',
    legendAllow: 'İzin ver',
    legendDeny: 'Reddet',
    clickForReasoning: (label: string) => `${label} — gerekçe için tıklayın`,
    subjectOr: 'veya',

    // ── Kullanıcı grubu seçme penceresi ───────────────────────────────────
    rolePickerHeadline: 'Bir kullanıcı grubu seçin',
    rolePickerFilter: 'Filtrelemek için yazın…',
    rolePickerNoResults: 'Filtreyle eşleşen kullanıcı grubu yok.',
    rolePickerNameHeader: 'Kullanıcı Grubu',

    // ── Kullanıcı seçme penceresi ─────────────────────────────────────────
    userPickerHeadline: 'Bir kullanıcı seçin',
    userPickerFilter: 'Filtrelemek için yazın…',
    userPickerNoResults: 'Filtreyle eşleşen kullanıcı yok.',
    userPickerNameHeader: 'Kullanıcı',

    // ── Gerekçe iletişim kutusu ───────────────────────────────────────────
    reasoningHeadline: (verb: string, nodeName: string) => `“${nodeName}” için ${verb} izni`,
    effectiveAllowed: (subject: string, verb: string, nodeName: string) =>
      `${subject} için “${nodeName}” öğesinde ${verb} iznine izin verildi.`,
    effectiveDenied: (subject: string, verb: string, nodeName: string) =>
      `${subject} için “${nodeName}” öğesinde ${verb} izni reddedildi.`,
    dialogSecurityHeader: 'Güvenlik',
    defaultPermissions: 'Varsayılan izinler',
    determiningEntry: 'Bu kayıt önceliklidir',
    noReasoningData: 'Bu eylem için izin verisi yok.',
    defaultAllowNote: 'Hiçbir izin ayarlanmadı, buna varsayılan olarak izin verilir.',
    defaultDenyNote: 'Hiçbir izin ayarlanmadı, bu varsayılan olarak reddedilir.',

    // ── Ayrıntılı izinler yönlendirme mesajı ──────────────────────────────
    redirectMessage:
      'Bu kullanıcı grubu için belge izinleri Advanced Permissions paketi tarafından yönetilir. İzinleri yapılandırmak için Kullanıcılar bölümündeki İzin Düzenleyicisini açın.',

    // ── Belge Türü İzinleri ───────────────────────────────────────────────
    role: 'Kullanıcı Grubu',
    pickRole: 'Kullanıcı grubu seçin',
    user: 'Kullanıcı',
    pickUser: 'Kullanıcı seçin',
    node: 'Düğüm',
    pickNode: 'Düğüm seçin',
    state: 'Durum',
    scope: 'Kapsam',
    scope_thisNodeOnly: 'Yalnızca bu düğüm',
    scope_thisNodeAndDescendants: 'Bu düğüm ve alt düğümler',
    scope_descendantsOnly: 'Yalnızca alt düğümler',

    docTypePermissions_menuLabel: 'Belge Türü İzinleri Düzenleyicisi',
    docTypePermissions_insertOptionsMenuLabel: 'Ekleme Seçenekleri Görüntüleyici',
    docTypePermissions_workspaceTitle: 'Belge Türü İzinleri Düzenleyicisi',
    docTypePermissions_auditTitle: 'Ekleme Seçenekleri Görüntüleyici',
    docTypePermissions_allDocTypes: 'Tüm belge türleri',
    docTypePermissions_verbInsert: 'Ekle',
    docTypePermissions_documentType: 'Belge Türü',
    chooseDocType: 'Belge türü seçin',
    notAnInsertOption: 'Bu belge türü şu anda bu düğümde bir ekleme seçeneği değil.',
    notAnInsertOptionAllowedNote: 'Bu belge türü bu düğümde bir ekleme seçeneği değil, ancak aksi takdirde izin verilirdi.',
    notAnInsertOptionDeniedNote: 'Bu belge türü bu düğümde bir ekleme seçeneği değil, ancak aksi takdirde reddedilirdi.',
    docTypePermissions_pickDocType: '— Bir belge türü seçin —',
    docTypePermissions_pickToStart: 'Başlamak için bir kullanıcı grubu ve belge türü seçin.',
    docTypePermissions_defaultRowLabel: 'Varsayılan (her yerde geçerli)',
    docTypePermissions_pendingNodeLabel: '(kaydedilmemiş düğüm)',
    docTypePermissions_addScopeNode: 'Kapsam geçersiz kılma ekle',
    docTypePermissions_notSet: 'Ayarlanmadı',
    docTypePermissions_noResults: 'Belge türü bulunamadı.',
    docTypePermissions_useRoot: 'Kökü kullan',
    docTypePermissions_pickedNode: 'Düğüm:',
    docTypePermissions_rootLevel: 'Kök düzeyi',
    docTypePermissions_reasoning: 'Gerekçe',
    docTypePermissions_defaultAllow: 'Varsayılan olarak izin verildi',
    docTypePermissions_viaDefault: 'varsayılan satırdan',
  },
} satisfies UmbLocalizationDictionary;
