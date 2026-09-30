import * as Sdk from './generated/sdk.gen.js';
import type { BatchSavedDocTypeStamp } from './generated/types.gen.js';
import { collectStamps, readConflicts } from './concurrency.js';
import type {
  DocTypeListItem,
  SaveDocTypePermissionItem,
  DocTypeAuditForNodeResponse,
  DocTypePathEntriesResponse,
  DocTypeEditorNode,
  BatchSaveDocTypeNode,
  BatchSaveDocTypeConflict,
  BatchSaveDocTypeResult,
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

/**
 * Gets the stored entries for the editor's selected (role, content-type), grouped by node with
 * each node's own concurrency stamp.
 *
 * `GetForEditor` answers for the whole (role, content-type) combination in one call — unlike the
 * node-permission tree, there is no per-level pagination to batch here, so this is the only fetch
 * the editor, its reconciliation pass and its expand-on-demand loading all need. It replaces the
 * earlier flat-list wrapper (`getDocTypePermissions`): the endpoint's response shape changed from
 * entries to per-node buckets, so the old name would have lied about its result.
 */
export async function getDocTypePermissionsForEditor(
  roleAlias: string,
  contentTypeKey: string,
  signal?: AbortSignal,
): Promise<DocTypeEditorNode[]> {
  const { data } = await Sdk.getForEditor({
    throwOnError: true,
    query: { roleAlias, contentTypeKey },
    ...(signal ? { signal } : {}),
  });
  return data as DocTypeEditorNode[];
}

/**
 * Replaces all entries for a (node, role, content-type) triple. Empty list clears.
 *
 * Always writes, because it sends no `expectedStamp` and so asks the server for no concurrency
 * check. Surfaces that must detect a concurrent change use {@link saveDocTypePermissionsBatch}.
 */
export async function saveDocTypePermissions(
  nodeKey: string,
  roleAlias: string,
  contentTypeKey: string,
  entries: SaveDocTypePermissionItem[],
): Promise<void> {
  await Sdk.putDocTypePermissions({
    throwOnError: true,
    // `force` is required by the generated request body; with no `expectedStamp` there is nothing
    // to check, so this writes unconditionally.
    body: { nodeKey, roleAlias, contentTypeKey, entries, force: false },
  });
}

/**
 * Saves several node, user group and document type triples at once, all or nothing.
 *
 * Mirrors `savePermissionsBatch` in `advanced-permissions.api.ts`: resolves with `ok: false` and
 * the conflicting triples when the server refuses, rather than throwing, because a conflict is an
 * expected answer this feature exists to produce, not a failure. A `409` that arrives without a
 * `conflicts` array does throw (see {@link readConflicts}).
 * @param nodes The triples to write.
 * @param force Whether to write through a stale stamp. Only true after the user has confirmed.
 * @returns The new stamps, or the conflicts.
 */
export async function saveDocTypePermissionsBatch(
  nodes: BatchSaveDocTypeNode[],
  force = false,
): Promise<BatchSaveDocTypeResult> {
  const { data, error, response } = await Sdk.putDocTypePermissionsBatch({
    body: {
      nodes: nodes.map((n) => ({
        nodeKey: n.nodeKey,
        roleAlias: n.roleAlias,
        contentTypeKey: n.contentTypeKey,
        entries: n.entries,
        ...(n.expectedStamp !== undefined ? { expectedStamp: n.expectedStamp } : {}),
      })),
      force,
    },
  });

  if (response?.status === 409) {
    return { ok: false, conflicts: readConflicts<BatchSaveDocTypeConflict>(error, 'Document type permission') };
  }

  if (error) throw error;

  return {
    ok: true,
    stamps: collectStamps<BatchSavedDocTypeStamp>(data, (s) => `${s.nodeKey}|${s.roleAlias}|${s.contentTypeKey}`),
  };
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
 * Tree-free audit for the Library Insert Viewer: one row per library element type with whether the
 * subject may create it (resolved section-globally at the virtual root). Reuses the per-node audit
 * response shape; `isInAllowedChildren` is always true. Caller supplies EITHER `userKey` or `roleAlias`.
 */
export async function getElementTypeAudit(
  subject: { userKey: string } | { roleAlias: string },
  signal?: AbortSignal,
): Promise<DocTypeAuditForNodeResponse> {
  const query: { userKey?: string; roleAlias?: string } = {};
  if ('userKey' in subject) query.userKey = subject.userKey;
  else query.roleAlias = subject.roleAlias;

  const { data } = await Sdk.getElementTypeAudit({
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
