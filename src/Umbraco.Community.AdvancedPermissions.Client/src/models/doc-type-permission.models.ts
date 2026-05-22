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
}

/** Tuple-style entry shape sent to PUT. */
export interface SaveDocTypePermissionItem {
  verb: string;
  state: PermissionState;
  scope: PermissionScope;
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
}

/** Response of `path-entries`: inheritance path plus all doc-type entries along it. */
export interface DocTypePathEntriesResponse {
  path: PathNode[];
  entries: DocTypePathEntry[];
}
