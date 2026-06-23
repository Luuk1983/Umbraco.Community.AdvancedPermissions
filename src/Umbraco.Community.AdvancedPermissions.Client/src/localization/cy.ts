import type { UmbLocalizationDictionary } from '@umbraco-cms/backoffice/localization-api';

export default {
  uap: {
    // ── Llywio ────────────────────────────────────────────────────────────
    sectionLabel: 'Hawliau Uwch',
    editorsSectionLabel: 'Golygyddion',
    viewersSectionLabel: 'Gwelyddion',
    permissionsEditor: 'Golygydd Hawliau Cynnwys',
    accessViewer: 'Gwelydd Mynediad',

    // ── Cyffredinol ───────────────────────────────────────────────────────
    roleLabel: 'Grŵp Defnyddiwr',
    rolePlaceholder: '— Dewiswch grŵp defnyddiwr —',
    userLabel: 'Defnyddiwr',
    saveChanges: 'Achub Newidiadau',
    discard: 'Gwaredu',
    cancel: 'Canslo',
    apply: 'Cymhwyso',
    close: 'Cau',
    inherit: 'Etifeddu',
    allow: 'Caniatáu',
    deny: 'Gwrthod',
    umbracoUsers: 'Pob Defnyddiwr',

    // ── Help ──────────────────────────────────────────────────────────────
    help_contentPermissions_description: 'Rheoli hawliau Caniatáu/Gwrthod fesul grŵp defnyddiwr ar draws eich coeden gynnwys.',
    help_accessViewer_description: 'Gweld yr hawliau effeithiol sydd gan ddefnyddiwr neu grŵp ar unrhyw nod, gyda rhesymu llawn.',
    help_docTypePermissions_description: 'Penderfynu pa fathau o ddogfen y gall pob grŵp defnyddiwr eu creu, a ble.',
    help_insertOptions_description: 'Archwilio pa fathau o ddogfen y gall defnyddiwr neu grŵp defnyddiwr eu creu ar bob nod.',
    help_learnMore: 'Dysgu mwy',
    help_modalTitle: 'Cymorth',
    help_tabAbout: 'Am y dudalen hon',
    help_tabConcepts: 'Cysyniadau',
    help_concept_scope_tip: 'Mae cwmpas yn rheoli pa mor bell y mae rheol yn cyrraedd: y nod hwn, y nod hwn a’i ddisgynyddion, neu ddisgynyddion yn unig.',
    help_concept_priorityOverride_tip: 'Mae diystyriad blaenoriaeth yn gorfodi’r cofnod hwn i ennill dros y drefn ddatrys arferol.',
    help_concept_allowDeny_tip: 'Mae Caniatáu yn rhoi’r hawl, mae Gwrthod yn ei dynnu’n ôl, ac mae ei adael heb ei osod yn etifeddu o’r hynafiad agosaf.',
    help_concept_reasoning_tip: 'Cliciwch ar unrhyw gell i weld yn union sut y datryswyd yr hawl.',

    // ── Golygydd Hawliau ─────────────────────────────────────────────────
    editorHeadline: 'Golygydd Hawliau Cynnwys',
    selectRolePrompt: 'Dewiswch grŵp defnyddiwr uchod i reoli ei hawliau.',
    permissionsSaved: 'Hawliau wedi’u hachub.',
    saveFailed: (error: string) => `Methodd yr achub: ${error}`,
    contentNodeHeader: 'Nod Cynnwys',
    contentRoot: 'Hawliau diofyn',
    expand: 'Ehangu',
    collapse: 'Crebachu',

    // ── Deialog hawliau ───────────────────────────────────────────────────
    dialogHeadline: (verb: string, nodeName: string) => `Gosod hawl ${verb} ar gyfer ‘${nodeName}’`,
    descendantsSection: 'Disgynyddion (os yw’n wahanol)',
    dialogInstructions: 'Gosodwch yr hawl ar gyfer y nod hwn. Yn ddiofyn, mae hyn hefyd yn gymwys i bob disgynnydd. Defnyddiwch “Disgynyddion (os yw’n wahanol)” i osod hawl wahanol ar gyfer nodau disgynnydd.',
    virtualRootInherit: 'Heb ei osod (tynnu’r cofnod)',
    virtualRootAllow: 'Caniatáu (pob cynnwys)',
    virtualRootDeny: 'Gwrthod (pob cynnwys)',
    dialogResult: 'Canlyniad',
    previewBothInherit: 'Dim hawl wedi’i osod. Yn etifeddu o’r rhiant.',
    previewUniform: (action: string) => `${action} ar y nod hwn a phob disgynnydd.`,
    previewNodeOnly: (action: string) => `${action} ar y nod hwn yn unig. Nid yw disgynyddion yn cael eu heffeithio gan y rheol hon.`,
    previewDescOnly: (action: string) => `Dim hawl benodol ar y nod hwn. ${action} ar bob disgynnydd.`,
    previewSplit: (nodeAction: string, descAction: string) => `${nodeAction} ar y nod hwn. ${descAction} ar bob disgynnydd.`,
    previewVirtualInherit: 'Dim hawl ddiofyn wedi’i gosod.',
    previewVirtualSet: (action: string) => `${action} yn ddiofyn ar gyfer pob cynnwys.`,
    previewPriorityNode: 'Mae diystyriad blaenoriaeth wedi’i osod ar y nod hwn.',
    previewPriorityDesc: 'Mae diystyriad blaenoriaeth wedi’i osod ar ddisgynyddion.',
    previewPriorityBoth: 'Mae diystyriad blaenoriaeth wedi’i osod.',

    // ── Diystyriad blaenoriaeth ───────────────────────────────────────
    priorityOverride: 'Diystyriad blaenoriaeth',
    priorityOverrideTooltip: (permission: string, nodeName: string) =>
      `Gall defnyddiwr berthyn i sawl grŵp defnyddiwr. Fel arfer, mae’r hawl effeithiol yn dilyn trefn flaenoriaeth sefydlog. Mae ticio’r blwch hwn yn diystyru’r drefn honno, fel bod y gosodiad “${permission}” a ddewiswch yma bron bob amser yn dod yn ganlyniad ar gyfer “${nodeName}”, beth bynnag fo grwpiau eraill y defnyddiwr. Defnyddiwch yn gynnil.`,
    priorityOverrideBadgeTitle: 'Mae diystyriad blaenoriaeth wedi’i osod ar y cofnod hwn',
    priorityOverrideWonTitle: 'Datryswyd trwy ddiystyriad blaenoriaeth',
    priorityOverrideSuppressedHeader: 'Newidiodd diystyriad blaenoriaeth y canlyniad',
    priorityOverrideSuppressedHint: 'Hebddo, byddai’r canlyniad wedi bod:',

    // ── Gwelydd Mynediad ──────────────────────────────────────────────────
    viewerHeadline: 'Gwelydd Mynediad',
    byRole: 'Yn ôl Grŵp Defnyddiwr',
    byUser: 'Yn ôl Defnyddiwr',
    chooseRole: 'Dewis grŵp defnyddiwr',
    chooseUser: 'Dewis defnyddiwr',
    selectSubjectPrompt: 'Dewiswch grŵp defnyddiwr neu ddefnyddiwr i weld yr hawliau effeithiol.',
    legendAllow: 'Caniatáu',
    legendDeny: 'Gwrthod',
    clickForReasoning: (label: string) => `${label} — cliciwch am reswm`,
    subjectOr: 'neu',

    // ── Modal dewis grŵp defnyddiwr ─────────────────────────────────────────
    rolePickerHeadline: 'Dewiswch grŵp defnyddiwr',
    rolePickerFilter: 'Teipiwch i hidlo…',
    rolePickerNoResults: 'Nid oes grwpiau defnyddiwr yn cyfateb i’r hidl.',
    rolePickerNameHeader: 'Grŵp Defnyddiwr',

    // ── Modal dewis defnyddiwr ──────────────────────────────────────────────
    userPickerHeadline: 'Dewiswch ddefnyddiwr',
    userPickerFilter: 'Teipiwch i hidlo…',
    userPickerNoResults: 'Nid oes defnyddwyr yn cyfateb i’r hidl.',
    userPickerNameHeader: 'Defnyddiwr',

    // ── Deialog rhesymu ─────────────────────────────────────────────────────
    reasoningHeadline: (verb: string, nodeName: string) => `Hawl ${verb} ar gyfer “${nodeName}”`,
    effectiveAllowed: (subject: string, verb: string, nodeName: string) =>
      `Mae ${subject} wedi cael caniatâd ${verb} ar gyfer “${nodeName}”.`,
    effectiveDenied: (subject: string, verb: string, nodeName: string) =>
      `Mae ${subject} wedi cael ei wrthod hawl ${verb} ar gyfer “${nodeName}”.`,
    dialogSecurityHeader: 'Diogelwch',
    defaultPermissions: 'Hawliau diofyn',
    determiningEntry: 'Mae gan y cofnod hwn flaenoriaeth',
    noReasoningData: 'Nid oes data hawliau ar gael ar gyfer y weithred hon.',
    defaultAllowNote: 'Nid oes hawliau wedi’u gosod, caniateir hyn yn ddiofyn.',
    defaultDenyNote: 'Nid oes hawliau wedi’u gosod, gwrthodir hyn yn ddiofyn.',

    // ── Neges ailgyfeirio hawliau gronynnog ─────────────────────────────────
    redirectMessage:
      'Rheolir hawliau dogfennau ar gyfer y grŵp defnyddiwr hwn gan y pecyn Advanced Permissions. Agorwch y Golygydd Hawliau yn yr adran Defnyddwyr i ffurfweddu hawliau.',

    // ── Hawliau Math o Ddogfen ──────────────────────────────────────────────
    role: 'Grŵp Defnyddiwr',
    pickRole: 'Dewis grŵp defnyddiwr',
    user: 'Defnyddiwr',
    pickUser: 'Dewis defnyddiwr',
    node: 'Nod',
    pickNode: 'Dewis nod',
    state: 'Statws',
    scope: 'Cwmpas',
    scope_thisNodeOnly: 'Y nod hwn yn unig',
    scope_thisNodeAndDescendants: 'Y nod hwn a’i ddisgynyddion',
    scope_descendantsOnly: 'Disgynyddion yn unig',

    docTypePermissions_menuLabel: 'Golygydd Hawliau Math o Ddogfen',
    docTypePermissions_insertOptionsMenuLabel: 'Gwelydd Opsiynau Mewnosod',
    docTypePermissions_workspaceTitle: 'Golygydd Hawliau Math o Ddogfen',
    docTypePermissions_auditTitle: 'Gwelydd Opsiynau Mewnosod',
    docTypePermissions_allDocTypes: 'Pob math o ddogfen',
    docTypePermissions_verbInsert: 'Mewnosod',
    docTypePermissions_documentType: 'Math o Ddogfen',
    chooseDocType: 'Dewis math o ddogfen',
    notAnInsertOption: 'Nid yw’r math o ddogfen hwn yn opsiwn mewnosod ar y nod hwn ar hyn o bryd.',
    notAnInsertOptionAllowedNote: 'Nid yw’r math o ddogfen hwn yn opsiwn mewnosod ar y nod hwn, ond fel arall byddai’n cael ei ganiatáu.',
    notAnInsertOptionDeniedNote: 'Nid yw’r math o ddogfen hwn yn opsiwn mewnosod ar y nod hwn, ond fel arall byddai’n cael ei wrthod.',
    docTypePermissions_pickDocType: '— Dewiswch fath o ddogfen —',
    docTypePermissions_pickToStart: 'Dewiswch grŵp defnyddiwr a math o ddogfen i ddechrau.',
    docTypePermissions_defaultRowLabel: 'Diofyn (yn gymwys ym mhobman)',
    docTypePermissions_pendingNodeLabel: '(nod heb ei achub)',
    docTypePermissions_addScopeNode: 'Ychwanegu diystyriad cwmpas',
    docTypePermissions_notSet: 'Heb ei osod',
    docTypePermissions_noResults: 'Ni chanfuwyd unrhyw fathau o ddogfen.',
    docTypePermissions_useRoot: 'Defnyddio’r gwraidd',
    docTypePermissions_pickedNode: 'Nod:',
    docTypePermissions_rootLevel: 'Lefel y gwraidd',
    docTypePermissions_reasoning: 'Rhesymu',
    docTypePermissions_defaultAllow: 'Caniateir yn ddiofyn',
    docTypePermissions_viaDefault: 'o’r rhes ddiofyn',
  },
} satisfies UmbLocalizationDictionary;
