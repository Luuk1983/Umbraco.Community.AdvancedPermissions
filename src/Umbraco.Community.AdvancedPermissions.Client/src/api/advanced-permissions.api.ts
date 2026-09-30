import { UserService } from '@umbraco-cms/backoffice/external/backend-api';
import * as Sdk from './generated/sdk.gen.js';
import type { BatchSavedStamp } from './generated/types.gen.js';
import { collectStamps, readConflicts, stampFromETag } from './concurrency.js';
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
  const { data } = await Sdk.getRoles({
    throwOnError: true,
    ...(signal ? { signal } : {}),
  });
  return data;
}

/** Returns all available permission verbs with display names. */
export async function getVerbs(signal?: AbortSignal): Promise<VerbInfo[]> {
  const { data } = await Sdk.getVerbs({
    throwOnError: true,
    ...(signal ? { signal } : {}),
  });
  return data;
}

/** Returns root content nodes with stored permission entries for the given role. */
export async function getTreeRoot(roleAlias: string, signal?: AbortSignal): Promise<TreeNode[]> {
  const { data } = await Sdk.getRoot({
    throwOnError: true,
    query: { roleAlias },
    ...(signal ? { signal } : {}),
  });
  return data as TreeNode[];
}

/** Returns children of a content node with stored permission entries for the given role. */
export async function getTreeChildren(parentKey: string, roleAlias: string, signal?: AbortSignal): Promise<TreeNode[]> {
  const { data } = await Sdk.getChildren({
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
 *
 * This single-save path always writes, because it sends no `expectedStamp` and so asks the server
 * for no concurrency check. Surfaces that must detect a concurrent change use
 * {@link savePermissionsBatch} instead.
 */
export async function savePermissions(
  nodeKey: string,
  roleAlias: string,
  entries: Array<{ verb: string; state: PermissionState; scope: PermissionScope; isPriorityOverride: boolean }>,
): Promise<void> {
  await Sdk.putPermissions({
    throwOnError: true,
    // `force` is required by the generated request body now that the server supports optimistic
    // concurrency; with no `expectedStamp` there is nothing to check, so this writes unconditionally.
    body: { nodeKey, roleAlias, entries, force: false },
  });
}

/** Returns stored permission entries for a node+role combination. Use VIRTUAL_ROOT_NODE_KEY for virtual-root entries. */
export async function getPermissions(nodeKey: string, roleAlias: string, signal?: AbortSignal): Promise<PermissionEntry[]> {
  const { data } = await Sdk.getPermissions({
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
 * The header is read through {@link stampFromETag}, which strips the quotes the server wraps the
 * stamp in. If the server sent no `ETag` at all, this returns `stamp: ''` rather than inventing a
 * value — callers must treat an empty stamp as "no stamp available" and omit `expectedStamp`
 * accordingly, the same as if the entries had never been read.
 */
export async function getPermissionsWithStamp(
  nodeKey: string,
  roleAlias: string,
  signal?: AbortSignal,
): Promise<PermissionEntriesWithStamp> {
  const { data, response } = await Sdk.getPermissions({
    throwOnError: true,
    query: { nodeKey, roleAlias },
    ...(signal ? { signal } : {}),
  });
  return { entries: data as PermissionEntry[], stamp: stampFromETag(response.headers) };
}

/** Returns the inheritance path and raw entries for a verb along that path. */
export async function getPermissionsForPath(nodeKey: string, verb: string, signal?: AbortSignal): Promise<PathEntriesResponse> {
  const { data } = await Sdk.getPermissionsForPath({
    throwOnError: true,
    query: { nodeKey, verb },
    ...(signal ? { signal } : {}),
  });
  return data as PathEntriesResponse;
}

/** Resolves effective permissions for a user at a content node. */
export async function getEffectiveForUser(userKey: string, nodeKey: string, signal?: AbortSignal): Promise<EffectivePermissions> {
  const { data } = await Sdk.getEffectiveForUser({
    throwOnError: true,
    query: { userKey, nodeKey },
    ...(signal ? { signal } : {}),
  });
  return data as EffectivePermissions;
}

/** Resolves effective permissions for a role at a content node. */
export async function getEffectiveForRole(roleAlias: string, nodeKey: string, signal?: AbortSignal): Promise<EffectivePermissions> {
  const { data } = await Sdk.getEffectiveForRole({
    throwOnError: true,
    query: { roleAlias, nodeKey },
    ...(signal ? { signal } : {}),
  });
  return data as EffectivePermissions;
}

/**
 * Saves several content node-and-user-group pairs at once, all or nothing.
 *
 * Resolves with `ok: false` and the conflicting pairs when the server refuses, rather than
 * throwing: a conflict is an expected answer this feature exists to produce, not a failure, and
 * routing it through the error path would mean recovering the detail out of an exception. This is
 * why the call below omits `throwOnError` — unlike every other wrapper in this file. A `409` that
 * arrives without a `conflicts` array does throw (see {@link readConflicts}).
 * @param nodes The pairs to write.
 * @param force Whether to write through a stale stamp. Only true after the user has confirmed.
 * @returns The new stamps, or the conflicts.
 */
export async function savePermissionsBatch(nodes: BatchSaveNode[], force = false): Promise<BatchSaveResult> {
  const { data, error, response } = await Sdk.putPermissionsBatch({
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

  if (response?.status === 409) {
    return { ok: false, conflicts: readConflicts<BatchSaveConflict>(error, 'Permission') };
  }

  if (error) throw error;

  return { ok: true, stamps: collectStamps<BatchSavedStamp>(data, (s) => `${s.nodeKey}|${s.roleAlias}`) };
}

/** Fetches all users from Umbraco's management API, sorted by name. */
export async function getUsers(): Promise<UserItem[]> {
  const { data } = await UserService.getFilterUser({ query: { take: 500 }, throwOnError: true });
  const items = data?.items ?? [];
  return items.map((u) => ({ unique: u.id, name: u.name, avatarUrls: u.avatarUrls }));
}
