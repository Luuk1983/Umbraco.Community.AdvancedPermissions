import { UserService } from '@umbraco-cms/backoffice/external/backend-api';
import { AdvancedPermissionsService } from './generated/sdk.gen.js';
import type { BatchSaveConflictResponseModel, BatchSavedStampModel } from './generated/types.gen.js';
import type {
  PermissionEntry,
  PermissionState,
  PermissionScope,
  TreeNode,
  EffectivePermissions,
  PathEntriesResponse,
  PermissionEntriesWithStamp,
  VerbInfo,
  RoleInfo,
  UserItem,
  BatchSaveNode,
  BatchSaveConflict,
  BatchSaveResult,
} from '../models/permission.models.js';

// Thin wrappers over the hey-api generated SDK. Auth + 401 retry are handled
// by the Umbraco auth context via `authContext.configureClient(client)` in
// entrypoint.ts — nothing to do here per-request.
//
// The generated types model `state` / `scope` as `string` because the backend
// exposes them as plain strings in OpenAPI. Our hand-written models narrow
// them to string unions; we cast at this boundary (runtime values are
// guaranteed by the backend).

/** Returns all assignable roles (user groups + $everyone). */
export async function getRoles(signal?: AbortSignal): Promise<RoleInfo[]> {
  const { data } = await AdvancedPermissionsService.getRoles({
    throwOnError: true,
    ...(signal ? { signal } : {}),
  });
  return data;
}

/** Returns all available permission verbs with display names. */
export async function getVerbs(signal?: AbortSignal): Promise<VerbInfo[]> {
  const { data } = await AdvancedPermissionsService.getVerbs({
    throwOnError: true,
    ...(signal ? { signal } : {}),
  });
  return data;
}

/** Returns root content nodes with stored permission entries for the given role. */
export async function getTreeRoot(roleAlias: string, signal?: AbortSignal): Promise<TreeNode[]> {
  const { data } = await AdvancedPermissionsService.getRoot({
    throwOnError: true,
    query: { roleAlias },
    ...(signal ? { signal } : {}),
  });
  return data as TreeNode[];
}

/** Returns children of a content node with stored permission entries for the given role. */
export async function getTreeChildren(parentKey: string, roleAlias: string, signal?: AbortSignal): Promise<TreeNode[]> {
  const { data } = await AdvancedPermissionsService.getChildren({
    throwOnError: true,
    query: { parentKey, roleAlias },
    ...(signal ? { signal } : {}),
  });
  return data as TreeNode[];
}

/**
 * Saves (replaces) all permission entries for a specific node and role.
 * Pass an empty `entries` array to clear all entries and revert to inherited behaviour.
 * Use VIRTUAL_ROOT_NODE_KEY for virtual-root (default) entries.
 */
export async function savePermissions(
  nodeKey: string,
  roleAlias: string,
  entries: Array<{ verb: string; state: PermissionState; scope: PermissionScope; isPriorityOverride: boolean }>,
): Promise<void> {
  await AdvancedPermissionsService.savePermissions({
    throwOnError: true,
    // `force` is required by the generated request body now that the server supports optimistic
    // concurrency (Task 15 only adds the batch wrapper; this single-save path still writes
    // unconditionally, matching its behaviour before the concurrency check existed).
    body: { nodeKey, roleAlias, entries, force: false },
  });
}

/** Returns stored permission entries for a node+role combination. Use VIRTUAL_ROOT_NODE_KEY for virtual-root entries. */
export async function getPermissions(nodeKey: string, roleAlias: string, signal?: AbortSignal): Promise<PermissionEntry[]> {
  const { data } = await AdvancedPermissionsService.getPermissions({
    throwOnError: true,
    query: { nodeKey, roleAlias },
    ...(signal ? { signal } : {}),
  });
  return data as PermissionEntry[];
}

/**
 * Returns stored permission entries for a node+role combination together with their concurrency
 * stamp, read from the response's `ETag` header rather than the JSON body.
 *
 * This exists for the virtual root: the tree endpoints synthesise it client-side (it isn't a
 * real content node), so it never arrives with a `stamp` field the way `TreeNode` results do.
 * Its entries are real and are written through the same batch-save endpoint as any other node,
 * so it still needs a genuine stamp for the concurrency check to mean anything — a placeholder
 * like `''` would make every save that touches the virtual root fail as a false conflict, and
 * `undefined` would silently disable the check for it. This wrapper is how the caller gets a real
 * one to pass as `expectedStamp`.
 *
 * The server writes the stamp as a quoted strong entity-tag (RFC 9110), so the raw header value
 * is `"abc123..."` including the quotes. Those must be stripped: a batch save compares
 * `expectedStamp` against the unquoted stamp computed from the stored entries, so leaving the
 * quotes in would make the comparison fail every time, not just sometimes.
 *
 * If the server sent no `ETag` at all, this returns `stamp: ''` rather than inventing a value —
 * callers must treat an empty stamp as "no stamp available" and omit `expectedStamp` accordingly,
 * the same as if the entries had never been read.
 */
export async function getPermissionsWithStamp(
  nodeKey: string,
  roleAlias: string,
  signal?: AbortSignal,
): Promise<PermissionEntriesWithStamp> {
  const { data, response } = await AdvancedPermissionsService.getPermissions({
    throwOnError: true,
    query: { nodeKey, roleAlias },
    ...(signal ? { signal } : {}),
  });
  const rawETag = response.headers.get('ETag');
  const stamp = rawETag ? rawETag.replace(/^"|"$/g, '') : '';
  return { entries: data as PermissionEntry[], stamp };
}

/** Returns the inheritance path and raw entries for a verb along that path. */
export async function getPermissionsForPath(nodeKey: string, verb: string, signal?: AbortSignal): Promise<PathEntriesResponse> {
  const { data } = await AdvancedPermissionsService.getPermissionsForPath({
    throwOnError: true,
    query: { nodeKey, verb },
    ...(signal ? { signal } : {}),
  });
  return data as PathEntriesResponse;
}

/** Resolves effective permissions for a user at a content node. */
export async function getEffectiveForUser(userKey: string, nodeKey: string, signal?: AbortSignal): Promise<EffectivePermissions> {
  const { data } = await AdvancedPermissionsService.getEffectiveForUser({
    throwOnError: true,
    query: { userKey, nodeKey },
    ...(signal ? { signal } : {}),
  });
  return data as EffectivePermissions;
}

/** Resolves effective permissions for a role at a content node. */
export async function getEffectiveForRole(roleAlias: string, nodeKey: string, signal?: AbortSignal): Promise<EffectivePermissions> {
  const { data } = await AdvancedPermissionsService.getEffectiveForRole({
    throwOnError: true,
    query: { roleAlias, nodeKey },
    ...(signal ? { signal } : {}),
  });
  return data as EffectivePermissions;
}

/**
 * Saves several node-and-user-group pairs at once, all or nothing.
 *
 * Resolves with `ok: false` and the conflicting pairs when the server refuses, rather than
 * throwing: a conflict is an expected answer this feature exists to produce, not a failure, and
 * routing it through the error path would mean recovering the detail out of an exception. This is
 * why the call below omits `throwOnError` — unlike every other wrapper in this file.
 * @param nodes The pairs to write.
 * @param force Whether to write through a stale stamp. Only true after the user has confirmed.
 * @returns The new stamps, or the conflicts.
 */
export async function savePermissionsBatch(nodes: BatchSaveNode[], force = false): Promise<BatchSaveResult> {
  const { data, error, response } = await AdvancedPermissionsService.batchSavePermissions({
    body: {
      nodes: nodes.map((n) => ({
        nodeKey: n.nodeKey,
        roleAlias: n.roleAlias,
        entries: n.entries,
        ...(n.expectedStamp !== undefined ? { expectedStamp: n.expectedStamp } : {}),
      })),
      force,
    },
  });

  if (response.status === 409) {
    // The server sends a ProblemDetails with the conflicts as an extension member. It has to be
    // one: Umbraco's client interceptor replaces any error body that does not satisfy
    // `isProblemDetailsLike` (type, title and status) with a generic one, dropping `conflicts`.
    // If that ever happens again, fail here with a message that says so, rather than returning an
    // `ok: false` result with no conflicts and letting the caller die on a bare "not iterable".
    const conflicts = (error as Partial<BatchSaveConflictResponseModel> | undefined)?.conflicts;
    if (!Array.isArray(conflicts)) {
      throw new Error(
        'Permission save was refused with 409 but the response carried no "conflicts" array. ' +
          'The response body was probably rewritten by the Umbraco interceptor because it no longer ' +
          'looks like a ProblemDetails (type, title and status).',
      );
    }
    return { ok: false, conflicts: conflicts as BatchSaveConflict[] };
  }

  if (error) throw error;

  const stamps = new Map<string, string>();
  for (const saved of (data ?? []) as BatchSavedStampModel[]) {
    stamps.set(`${saved.nodeKey}|${saved.roleAlias}`, saved.stamp);
  }

  return { ok: true, stamps };
}

/** Fetches all users from Umbraco's management API, sorted by name. */
export async function getUsers(): Promise<UserItem[]> {
  const { data } = await UserService.getFilterUser({ query: { take: 500 }, throwOnError: true });
  const items = data?.items ?? [];
  return items.map((u) => ({ unique: u.id, name: u.name, avatarUrls: u.avatarUrls }));
}
