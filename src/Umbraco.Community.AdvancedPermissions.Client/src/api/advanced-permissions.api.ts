import { UserService } from '@umbraco-cms/backoffice/external/backend-api';
import { AdvancedPermissionsService } from './generated/sdk.gen.js';
import type {
  PermissionEntry,
  PermissionState,
  PermissionScope,
  TreeNode,
  EffectivePermissions,
  PathEntriesResponse,
  VerbInfo,
  RoleInfo,
  UserItem,
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
  entries: Array<{ verb: string; state: PermissionState; scope: PermissionScope }>,
): Promise<void> {
  await AdvancedPermissionsService.savePermissions({
    throwOnError: true,
    body: { nodeKey, roleAlias, entries },
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

/** Fetches all users from Umbraco's management API, sorted by name. */
export async function getUsers(): Promise<UserItem[]> {
  const { data } = await UserService.getFilterUser({ query: { take: 500 }, throwOnError: true });
  const items = data?.items ?? [];
  return items.map((u) => ({ unique: u.id, name: u.name, avatarUrls: u.avatarUrls }));
}
