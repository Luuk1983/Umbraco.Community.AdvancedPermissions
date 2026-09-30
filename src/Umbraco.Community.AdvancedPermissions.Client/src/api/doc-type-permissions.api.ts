import { AdvancedPermissionsService } from './generated/sdk.gen.js';
import type { BatchSaveDocTypeConflictResponseModel, BatchSavedDocTypeStampModel } from './generated/types.gen.js';
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
  const { data } = await AdvancedPermissionsService.getDocTypes({
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
 * the editor, its reconciliation pass and its expand-on-demand loading all need.
 */
export async function getDocTypePermissionsForEditor(
  roleAlias: string,
  contentTypeKey: string,
  signal?: AbortSignal,
): Promise<DocTypeEditorNode[]> {
  const { data } = await AdvancedPermissionsService.getForEditor({
    throwOnError: true,
    query: { roleAlias, contentTypeKey },
    ...(signal ? { signal } : {}),
  });
  return data as DocTypeEditorNode[];
}

/** Replaces all entries for a (node, role, content-type) triple. Empty list clears. */
export async function saveDocTypePermissions(
  nodeKey: string,
  roleAlias: string,
  contentTypeKey: string,
  entries: SaveDocTypePermissionItem[],
): Promise<void> {
  await AdvancedPermissionsService.save({
    throwOnError: true,
    body: { nodeKey, roleAlias, contentTypeKey, entries },
  });
}

/**
 * Saves several node, user group and document type triples at once, all or nothing.
 *
 * Mirrors `savePermissionsBatch` in `advanced-permissions.api.ts`: resolves with `ok: false` and
 * the conflicting triples when the server refuses, rather than throwing, because a conflict is an
 * expected answer this feature exists to produce, not a failure.
 * @param nodes The triples to write.
 * @param force Whether to write through a stale stamp. Only true after the user has confirmed.
 * @returns The new stamps, or the conflicts.
 */
export async function saveDocTypePermissionsBatch(
  nodes: BatchSaveDocTypeNode[],
  force = false,
): Promise<BatchSaveDocTypeResult> {
  const { data, error, response } = await AdvancedPermissionsService.batchSaveDocTypePermissions({
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

  if (response.status === 409) {
    const body = error as BatchSaveDocTypeConflictResponseModel;
    return { ok: false, conflicts: body.conflicts as BatchSaveDocTypeConflict[] };
  }

  if (error) throw error;

  const stamps = new Map<string, string>();
  for (const saved of (data ?? []) as BatchSavedDocTypeStampModel[]) {
    stamps.set(`${saved.nodeKey}|${saved.roleAlias}|${saved.contentTypeKey}`, saved.stamp);
  }

  return { ok: true, stamps };
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

  const { data } = await AdvancedPermissionsService.auditForNode({
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
  const { data } = await AdvancedPermissionsService.pathEntries({
    throwOnError: true,
    query: { nodeKey, contentTypeKey },
    ...(signal ? { signal } : {}),
  });
  return data as DocTypePathEntriesResponse;
}
