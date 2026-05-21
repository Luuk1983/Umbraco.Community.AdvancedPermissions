import type { PermissionState, PermissionScope, ReasoningStep } from './permission.models.js';

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

/** One row of the Create Audit listing. */
export interface DocTypeCreateAuditItem {
  contentTypeKey: string;
  contentTypeAlias: string;
  contentTypeName: string;
  contentTypeIcon: string | null;
  isAllowed: boolean;
  isExplicit: boolean;
  reasoning: ReasoningStep[];
}
