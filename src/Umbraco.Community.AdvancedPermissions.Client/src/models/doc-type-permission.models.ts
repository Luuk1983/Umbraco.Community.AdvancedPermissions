import type { PermissionState, PermissionScope, PathNode, ReasoningStep } from './permission.models.js';

/** A non-element document type listed in the editor's doc-type picker. */
export interface DocTypeListItem {
  key: string;
  alias: string;
  name: string;
  icon: string | null;
}

/** A single stored doc-type permission entry returned from the API. */
export interface DocTypePermissionEntry {
  id: string;
  nodeKey: string;
  contentTypeKey: string;
  roleAlias: string;
  verb: string;
  state: PermissionState;
  scope: PermissionScope;
  /** Priority-override flag — same node-local semantics as `PermissionEntry.isPriorityOverride`. */
  isPriorityOverride: boolean;
}

/** Tuple-style entry shape sent to PUT. */
export interface SaveDocTypePermissionItem {
  verb: string;
  state: PermissionState;
  scope: PermissionScope;
  isPriorityOverride: boolean;
}

/** One row of the new tree-style per-node audit listing. Adds `isInAllowedChildren`. */
export interface DocTypeAuditForNodeRow {
  contentTypeKey: string;
  contentTypeAlias: string;
  contentTypeName: string;
  contentTypeIcon: string | null;
  isAllowed: boolean;
  isExplicit: boolean;
  isInAllowedChildren: boolean;
  reasoning: ReasoningStep[];
  /** True when the priority-override shortcircuit fired for this row's resolution. */
  wasPriorityOverrideActive?: boolean;
  /** Reasoning entries suppressed by the priority override path. */
  suppressedReasoning?: ReasoningStep[];
}

/** Response of `audit-for-node`: a node key plus one row per non-element doc type. */
export interface DocTypeAuditForNodeResponse {
  nodeKey: string;
  results: DocTypeAuditForNodeRow[];
}

/** A doc-type entry shown along the inheritance path in the reasoning dialog. */
export interface DocTypePathEntry {
  id: string;
  nodeKey: string;
  contentTypeKey: string;
  roleAlias: string;
  verb: string;
  state: PermissionState;
  scope: PermissionScope;
  /** Priority-override flag — surfaced so the reasoning dialog can badge override entries. */
  isPriorityOverride: boolean;
}

/** Response of `path-entries`: inheritance path plus all doc-type entries along it. */
export interface DocTypePathEntriesResponse {
  path: PathNode[];
  entries: DocTypePathEntry[];
}

/**
 * One node's stored entries for the (role, content-type) combination selected in the editor,
 * together with that node's own concurrency stamp.
 *
 * `GetForEditor` returns one of these per node that has any stored entries for the combination —
 * a node with nothing stored has no bucket at all, which the editor treats the same as an empty
 * `entries` list with the real empty-set stamp (`EMPTY_SET_STAMP` in `live/stamp.ts`), never as
 * "no stamp available": a save that omits its stamp is not concurrency-checked.
 */
export interface DocTypeEditorNode {
  /** The content node the entries are scoped to, or VIRTUAL_ROOT_NODE_KEY for the virtual root. */
  nodeKey: string;
  /** This node's stored entries for the requested (role, content-type). */
  entries: DocTypePermissionEntry[];
  /** The concurrency stamp of `entries`, sent back on save. */
  stamp: string;
}

/** One node, user group and document type triple in a batch save, with the stamp the client read. */
export interface BatchSaveDocTypeNode {
  /** The content node key, or VIRTUAL_ROOT_NODE_KEY for the virtual root. */
  nodeKey: string;
  /** The user group alias. */
  roleAlias: string;
  /** The document type the rule is about. */
  contentTypeKey: string;
  /** The replacement entries. An empty list removes all entries for the triple. */
  entries: SaveDocTypePermissionItem[];
  /** The stamp this client was given when it read these entries. */
  expectedStamp: string | undefined;
}

/** One triple's new stamp after a successful batch save. */
export interface BatchSavedDocTypeStamp {
  /** The content node key. */
  nodeKey: string;
  /** The user group alias. */
  roleAlias: string;
  /** The document type. */
  contentTypeKey: string;
  /** The stamp of what was just written. */
  stamp: string;
}

/** One triple the server refused because its stored entries moved. */
export interface BatchSaveDocTypeConflict {
  /** The content node key. */
  nodeKey: string;
  /** The user group alias. */
  roleAlias: string;
  /** The document type. */
  contentTypeKey: string;
  /** What is stored right now — what the dialog shows as "stored". */
  currentEntries: DocTypePermissionEntry[];
  /** The stamp of `currentEntries`, so a resolved conflict can retry without a further read. */
  currentStamp: string;
}

/** The result of a doc-type batch save: either it wrote, or it named what moved. */
export type BatchSaveDocTypeResult =
  | { ok: true; stamps: ReadonlyMap<string, string> }
  | { ok: false; conflicts: BatchSaveDocTypeConflict[] };
