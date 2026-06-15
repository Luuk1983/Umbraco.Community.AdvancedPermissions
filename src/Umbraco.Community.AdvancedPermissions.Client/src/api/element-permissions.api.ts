import * as Sdk from './generated/sdk.gen.js';
import type {
  PermissionEntry,
  PermissionState,
  PermissionScope,
  EffectivePermissions,
  PathEntriesResponse,
  VerbInfo,
} from '../models/permission.models.js';
import type { ElementTreeNode } from '../models/element-permission.models.js';

// Thin wrappers over the hey-api generated SDK for the library (element) permission endpoints.
// Auth + 401 retry are handled by the Umbraco auth context via `authContext.configureClient(client)`
// in entrypoint.ts. The generated types model `state`/`scope` as `string`; our hand-written models
// narrow them to unions, so we cast at this boundary (runtime values are guaranteed by the backend).

/** Returns all canonical element permission verbs with display names. */
export async function getElementVerbs(signal?: AbortSignal): Promise<VerbInfo[]> {
  const { data } = await Sdk.getElementVerbs({
    throwOnError: true,
    ...(signal ? { signal } : {}),
  });
  return data;
}

/** Returns root library nodes (folders + root elements) with stored entries for the given role. */
export async function getElementTreeRoot(roleAlias: string, signal?: AbortSignal): Promise<ElementTreeNode[]> {
  const { data } = await Sdk.getElementRoot({
    throwOnError: true,
    query: { roleAlias },
    ...(signal ? { signal } : {}),
  });
  return data as ElementTreeNode[];
}

/** Returns children of a library folder with stored entries for the given role. */
export async function getElementTreeChildren(
  parentKey: string,
  roleAlias: string,
  signal?: AbortSignal,
): Promise<ElementTreeNode[]> {
  const { data } = await Sdk.getElementChildren({
    throwOnError: true,
    query: { parentKey, roleAlias },
    ...(signal ? { signal } : {}),
  });
  return data as ElementTreeNode[];
}

/** Returns stored element permission entries for a specific node and role. */
export async function getElementPermissions(
  nodeKey: string,
  roleAlias: string,
  signal?: AbortSignal,
): Promise<PermissionEntry[]> {
  const { data } = await Sdk.getElementPermissions({
    throwOnError: true,
    query: { nodeKey, roleAlias },
    ...(signal ? { signal } : {}),
  });
  return data as PermissionEntry[];
}

/**
 * Saves (replaces) all element permission entries for a node and role. Pass an empty `entries` array to
 * clear all entries and revert to inherited behaviour. Use VIRTUAL_ROOT_NODE_KEY for virtual-root entries.
 */
export async function saveElementPermissions(
  nodeKey: string,
  roleAlias: string,
  entries: Array<{ verb: string; state: PermissionState; scope: PermissionScope; isPriorityOverride: boolean }>,
): Promise<void> {
  await Sdk.putElementPermissions({
    throwOnError: true,
    body: { nodeKey, roleAlias, entries },
  });
}

/** Removes a specific element permission entry (reverts to inherit). */
export async function deleteElementPermission(nodeKey: string, roleAlias: string, verb: string): Promise<void> {
  await Sdk.deleteElementPermission({
    throwOnError: true,
    query: { nodeKey, roleAlias, verb },
  });
}

/** Returns the inheritance path and stored element entries for a verb along that path. */
export async function getElementPermissionsForPath(
  nodeKey: string,
  verb: string,
  signal?: AbortSignal,
): Promise<PathEntriesResponse> {
  const { data } = await Sdk.getElementPermissionsForPath({
    throwOnError: true,
    query: { nodeKey, verb },
    ...(signal ? { signal } : {}),
  });
  return data as PathEntriesResponse;
}

/** Resolves effective element permissions for a user at a node. */
export async function getElementEffectiveForUser(
  userKey: string,
  nodeKey: string,
  signal?: AbortSignal,
): Promise<EffectivePermissions> {
  const { data } = await Sdk.getElementEffectiveForUser({
    throwOnError: true,
    query: { userKey, nodeKey },
    ...(signal ? { signal } : {}),
  });
  return data as EffectivePermissions;
}

/** Resolves effective element permissions for a role at a node. */
export async function getElementEffectiveForRole(
  roleAlias: string,
  nodeKey: string,
  signal?: AbortSignal,
): Promise<EffectivePermissions> {
  const { data } = await Sdk.getElementEffectiveForRole({
    throwOnError: true,
    query: { roleAlias, nodeKey },
    ...(signal ? { signal } : {}),
  });
  return data as EffectivePermissions;
}

/**
 * Resolves effective element permissions for the CURRENT backoffice user at a node. This is the seam the
 * custom element/element-folder conditions use to gate UI action visibility — Umbraco 18 does not route
 * the native element current-user path through IElementPermissionService (the element form of #22351).
 */
export async function getElementEffectiveForCurrentUser(
  nodeKey: string,
  signal?: AbortSignal,
): Promise<EffectivePermissions> {
  const { data } = await Sdk.getElementEffectiveForCurrentUser({
    throwOnError: true,
    query: { nodeKey },
    ...(signal ? { signal } : {}),
  });
  return data as EffectivePermissions;
}
