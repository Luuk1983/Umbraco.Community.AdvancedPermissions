import type { UmbLocalizationDictionary } from '@umbraco-cms/backoffice/localization-api';

export default {
  uap: {
    // ── Navegación ────────────────────────────────────────────────────────
    sectionLabel: 'Permisos avanzados',
    editorsSectionLabel: 'Editores',
    viewersSectionLabel: 'Visores',
    permissionsEditor: 'Editor de permisos de contenido',
    accessViewer: 'Visor de acceso',

    // ── Común ─────────────────────────────────────────────────────────────
    roleLabel: 'Grupo de usuarios',
    rolePlaceholder: '— Selecciona un grupo de usuarios —',
    userLabel: 'Usuario',
    saveChanges: 'Guardar cambios',
    discard: 'Descartar',
    cancel: 'Cancelar',
    apply: 'Aplicar',
    close: 'Cerrar',
    inherit: 'Heredar',
    allow: 'Permitir',
    deny: 'Denegar',
    umbracoUsers: 'Todos los usuarios',

    // ── Editor de permisos ───────────────────────────────────────────────
    editorHeadline: 'Editor de permisos de contenido',
    selectRolePrompt: 'Selecciona arriba un grupo de usuarios para gestionar sus permisos.',
    permissionsSaved: 'Permisos guardados.',
    saveFailed: (error: string) => `Error al guardar: ${error}`,
    contentNodeHeader: 'Nodo de contenido',
    contentRoot: 'Permisos predeterminados',
    expand: 'Expandir',
    collapse: 'Contraer',

    // ── Diálogo de permiso ────────────────────────────────────────────────
    dialogHeadline: (verb: string, nodeName: string) => `Establecer permiso de ${verb} para ‘${nodeName}’`,
    descendantsSection: 'Descendientes (si difieren)',
    dialogInstructions: 'Establece el permiso para este nodo. De forma predeterminada, esto también se aplica a todos los descendientes. Usa “Descendientes (si difieren)” para establecer un permiso diferente para los nodos descendientes.',
    virtualRootInherit: 'No establecido (eliminar entrada)',
    virtualRootAllow: 'Permitir (todo el contenido)',
    virtualRootDeny: 'Denegar (todo el contenido)',
    dialogResult: 'Resultado',
    previewBothInherit: 'Ningún permiso establecido. Hereda del nodo principal.',
    previewUniform: (action: string) => `${action} en este nodo y todos los descendientes.`,
    previewNodeOnly: (action: string) => `${action} solo en este nodo. Los descendientes no se ven afectados por esta regla.`,
    previewDescOnly: (action: string) => `Ningún permiso explícito en este nodo. ${action} en todos los descendientes.`,
    previewSplit: (nodeAction: string, descAction: string) => `${nodeAction} en este nodo. ${descAction} en todos los descendientes.`,
    previewVirtualInherit: 'Ningún permiso predeterminado establecido.',
    previewVirtualSet: (action: string) => `${action} de forma predeterminada para todo el contenido.`,
    previewPriorityNode: 'La anulación por prioridad está activada en este nodo.',
    previewPriorityDesc: 'La anulación por prioridad está activada en los descendientes.',
    previewPriorityBoth: 'La anulación por prioridad está activada.',

    // ── Anulación por prioridad ───────────────────────────────────────
    priorityOverride: 'Anulación por prioridad',
    priorityOverrideTooltip: (permission: string, nodeName: string) =>
      `Un usuario puede pertenecer a varios grupos de usuarios. Normalmente, el permiso efectivo sigue un orden de prioridad fijo. Marcar esta casilla anula ese orden, de modo que la configuración de “${permission}” que elijas aquí casi siempre se convierte en el resultado para “${nodeName}”, independientemente de los otros grupos del usuario. Úsalo con moderación.`,
    priorityOverrideBadgeTitle: 'La anulación por prioridad está activada en esta entrada',
    priorityOverrideWonTitle: 'Resuelto mediante anulación por prioridad',
    priorityOverrideSuppressedHeader: 'La anulación por prioridad cambió el resultado',
    priorityOverrideSuppressedHint: 'Sin ella, el resultado habría sido:',

    // ── Visor de acceso ───────────────────────────────────────────────────
    viewerHeadline: 'Visor de acceso',
    byRole: 'Por grupo de usuarios',
    byUser: 'Por usuario',
    chooseRole: 'Elegir grupo de usuarios',
    chooseUser: 'Elegir usuario',
    selectSubjectPrompt: 'Selecciona un grupo de usuarios o un usuario para ver los permisos efectivos.',
    legendAllow: 'Permitir',
    legendDeny: 'Denegar',
    clickForReasoning: (label: string) => `${label} — haz clic para ver la justificación`,
    subjectOr: 'o',

    // ── Modal de selección de grupo de usuarios ───────────────────────────
    rolePickerHeadline: 'Selecciona un grupo de usuarios',
    rolePickerFilter: 'Escribe para filtrar…',
    rolePickerNoResults: 'Ningún grupo de usuarios coincide con el filtro.',
    rolePickerNameHeader: 'Grupo de usuarios',

    // ── Modal de selección de usuario ─────────────────────────────────────
    userPickerHeadline: 'Selecciona un usuario',
    userPickerFilter: 'Escribe para filtrar…',
    userPickerNoResults: 'Ningún usuario coincide con el filtro.',
    userPickerNameHeader: 'Usuario',

    // ── Diálogo de justificación ──────────────────────────────────────────
    reasoningHeadline: (verb: string, nodeName: string) => `Permiso de ${verb} para “${nodeName}”`,
    effectiveAllowed: (subject: string, verb: string, nodeName: string) =>
      `A ${subject} se le ha permitido el permiso de ${verb} para “${nodeName}”.`,
    effectiveDenied: (subject: string, verb: string, nodeName: string) =>
      `A ${subject} se le ha denegado el permiso de ${verb} para “${nodeName}”.`,
    dialogSecurityHeader: 'Seguridad',
    defaultPermissions: 'Permisos predeterminados',
    determiningEntry: 'Esta entrada tiene prioridad',
    noReasoningData: 'No hay datos de permisos disponibles para este permiso.',
    defaultAllowNote: 'No hay permisos establecidos, esto se permite de forma predeterminada.',
    defaultDenyNote: 'No hay permisos establecidos, esto se deniega de forma predeterminada.',

    // ── Mensaje de redirección de permisos granulares ─────────────────────
    redirectMessage:
      'Los permisos de documento para este grupo de usuarios se gestionan mediante el paquete Advanced Permissions. Abre el Editor de permisos en la sección Usuarios para configurar los permisos.',

    // ── Permisos de tipo de documento ─────────────────────────────────────
    role: 'Grupo de usuarios',
    pickRole: 'Elegir grupo de usuarios',
    user: 'Usuario',
    pickUser: 'Elegir usuario',
    node: 'Nodo',
    pickNode: 'Elegir nodo',
    state: 'Estado',
    scope: 'Ámbito',
    scope_thisNodeOnly: 'Solo este nodo',
    scope_thisNodeAndDescendants: 'Este nodo y los descendientes',
    scope_descendantsOnly: 'Solo los descendientes',

    docTypePermissions_menuLabel: 'Editor de permisos de tipo de documento',
    docTypePermissions_insertOptionsMenuLabel: 'Visor de opciones de inserción',
    docTypePermissions_workspaceTitle: 'Editor de permisos de tipo de documento',
    docTypePermissions_auditTitle: 'Visor de opciones de inserción',
    docTypePermissions_allDocTypes: 'Todos los tipos de documento',
    docTypePermissions_verbInsert: 'Insertar',
    docTypePermissions_documentType: 'Tipo de documento',
    chooseDocType: 'Elegir tipo de documento',
    notAnInsertOption: 'Este tipo de documento no es actualmente una opción de inserción en este nodo.',
    notAnInsertOptionAllowedNote: 'Este tipo de documento no es una opción de inserción en este nodo, pero de lo contrario se permitiría.',
    notAnInsertOptionDeniedNote: 'Este tipo de documento no es una opción de inserción en este nodo, pero de lo contrario se denegaría.',
    docTypePermissions_pickDocType: '— Selecciona un tipo de documento —',
    docTypePermissions_pickToStart: 'Elige un grupo de usuarios y un tipo de documento para empezar.',
    docTypePermissions_defaultRowLabel: 'Predeterminado (se aplica en todas partes)',
    docTypePermissions_pendingNodeLabel: '(nodo sin guardar)',
    docTypePermissions_addScopeNode: 'Añadir anulación de ámbito',
    docTypePermissions_notSet: 'No establecido',
    docTypePermissions_noResults: 'No se encontraron tipos de documento.',
    docTypePermissions_useRoot: 'Usar raíz',
    docTypePermissions_pickedNode: 'Nodo:',
    docTypePermissions_rootLevel: 'Nivel raíz',
    docTypePermissions_reasoning: 'Justificación',
    docTypePermissions_defaultAllow: 'Permitido de forma predeterminada',
    docTypePermissions_viaDefault: 'de la fila predeterminada',
  },
} satisfies UmbLocalizationDictionary;
