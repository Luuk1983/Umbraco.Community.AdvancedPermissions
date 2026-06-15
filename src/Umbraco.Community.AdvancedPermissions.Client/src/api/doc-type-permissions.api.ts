import * as Sdk from './generated/sdk.gen.js';
import type {
  DocTypeListItem,
  DocTypePermissionEntry,
  SaveDocTypePermissionItem,
  DocTypeAuditForNodeResponse,
  DocTypePathEntriesResponse,
} from '../models/doc-type-permission.models.js';

// Thin wrappers over the regenerated hey-api SDK. Auth + 401 retry are handled by the
// Umbraco auth context (`authContext.configureClient(client)` in entrypoint.ts).

/** Lists every non-element document type for the editor picker. */
export async function getDocTypes(signal?: AbortSignal): Promise<DocTypeListItem[]> {
  const { data } = await Sdk.getDocTypes({
    throwOnError: true,
    ...(signal ? { signal } : {}),
  });
  return data as DocTypeListItem[];
}

/** Lists every element type allowed in the Library (IsElement && AllowedInLibrary). */
export async function getElementTypes(signal?: AbortSignal): Promise<DocTypeListItem[]> {
  const { data } = await Sdk.getLibraryElementTypes({
    throwOnError: true,
    ...(signal ? { signal } : {}),
  });
  return data as DocTypeListItem[];
}

/** Gets the stored entries for the editor's selected (role, content-type). */
export async function getDocTypePermissions(
  roleAlias: string,
  contentTypeKey: string,
  signal?: AbortSignal,
): Promise<DocTypePermissionEntry[]> {
  const { data } = await Sdk.getForEditor({
    throwOnError: true,
    query: { roleAlias, contentTypeKey },
    ...(signal ? { signal } : {}),
  });
  return data as DocTypePermissionEntry[];
}

/** Replaces all entries for a (node, role, content-type) triple. Empty list clears. */
export async function saveDocTypePermissions(
  nodeKey: string,
  roleAlias: string,
  contentTypeKey: string,
  entries: SaveDocTypePermissionItem[],
): Promise<void> {
  await Sdk.putDocTypePermissions({
    throwOnError: true,
    body: { nodeKey, roleAlias, contentTypeKey, entries },
  });
}

/**
 * Tree-style audit. Returns one row per non-element doc type for a single node, including
 * `isInAllowedChildren` so the UI can show `n/a` when a type is structurally disallowed.
 * Caller supplies EITHER `userKey` or `roleAlias` (not both).
 */
export async function getDocTypeAuditForNode(
  subject: { userKey: string } | { roleAlias: string },
  nodeKey: string,
  signal?: AbortSignal,
): Promise<DocTypeAuditForNodeResponse> {
  const query: { nodeKey: string; userKey?: string; roleAlias?: string } = { nodeKey };
  if ('userKey' in subject) query.userKey = subject.userKey;
  else query.roleAlias = subject.roleAlias;

  const { data } = await Sdk.getDocTypeAudit({
    throwOnError: true,
    query,
    ...(signal ? { signal } : {}),
  });
  return data as DocTypeAuditForNodeResponse;
}

/**
 * Returns the inheritance path plus all stored doc-type entries along that path filtered to
 * one content-type. Used by the reasoning dialog of the tree-style audit.
 */
export async function getDocTypePathEntries(
  nodeKey: string,
  contentTypeKey: string,
  signal?: AbortSignal,
): Promise<DocTypePathEntriesResponse> {
  const { data } = await Sdk.getDocTypePathEntries({
    throwOnError: true,
    query: { nodeKey, contentTypeKey },
    ...(signal ? { signal } : {}),
  });
  return data as DocTypePathEntriesResponse;
}
