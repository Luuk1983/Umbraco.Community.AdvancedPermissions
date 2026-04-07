import type { UmbAuthContext } from '@umbraco-cms/backoffice/auth';
import type {
  PermissionEntry,
  PermissionState,
  PermissionScope,
  TreeNode,
  EffectivePermissions,
  VerbInfo,
  RoleInfo,
} from '../models/permission.models.js';

const BASE = '/umbraco/management/api/v1/advanced-permissions';

let _authContext: UmbAuthContext | undefined;

/** Called from the backoffice entry point once the auth context is available. */
export function setAuthContext(ctx: UmbAuthContext | undefined): void {
  _authContext = ctx;
}

/**
 * Performs an authenticated fetch to the Advanced Permissions management API.
 * Gets the bearer token via UMB_AUTH_CONTEXT and falls back to cookie auth.
 */
async function apiFetch(path: string, options?: RequestInit): Promise<Response> {
  const openApiConfig = _authContext?.getOpenApiConfiguration();
  const token = openApiConfig?.token ? await openApiConfig.token() : undefined;

  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...(options?.headers as Record<string, string> | undefined),
  };
  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  const res = await fetch(`${BASE}${path}`, {
    credentials: openApiConfig?.credentials ?? 'include',
    ...options,
    headers,
  });
  if (!res.ok) {
    const text = await res.text().catch(() => '');
    throw new Error(text || `HTTP ${res.status}`);
  }
  return res;
}

/** Returns all assignable roles (user groups + $everyone). */
export async function getRoles(): Promise<RoleInfo[]> {
  return (await apiFetch('/roles')).json() as Promise<RoleInfo[]>;
}

/** Returns all available permission verbs with display names. */
export async function getVerbs(): Promise<VerbInfo[]> {
  return (await apiFetch('/verbs')).json() as Promise<VerbInfo[]>;
}

/** Builds a RequestInit with an optional AbortSignal, handling exactOptionalPropertyTypes. */
function withSignal(signal?: AbortSignal, extra?: RequestInit): RequestInit {
  const opts: RequestInit = { ...extra };
  if (signal) {
    opts.signal = signal;
  }
  return opts;
}

/** Returns root content nodes with stored permission entries for the given role. */
export async function getTreeRoot(roleAlias: string, signal?: AbortSignal): Promise<TreeNode[]> {
  return (await apiFetch(`/tree/root?roleAlias=${encodeURIComponent(roleAlias)}`, withSignal(signal))).json() as Promise<TreeNode[]>;
}

/** Returns children of a content node with stored permission entries for the given role. */
export async function getTreeChildren(parentKey: string, roleAlias: string, signal?: AbortSignal): Promise<TreeNode[]> {
  return (
    await apiFetch(`/tree/children?parentKey=${parentKey}&roleAlias=${encodeURIComponent(roleAlias)}`, withSignal(signal))
  ).json() as Promise<TreeNode[]>;
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
  await apiFetch('/permissions', {
    method: 'PUT',
    body: JSON.stringify({ nodeKey, roleAlias, entries }),
  });
}

/** Returns stored permission entries for a node+role combination. Use VIRTUAL_ROOT_NODE_KEY for virtual-root entries. */
export async function getPermissions(nodeKey: string, roleAlias: string, signal?: AbortSignal): Promise<PermissionEntry[]> {
  return (await apiFetch(`/permissions?nodeKey=${nodeKey}&roleAlias=${encodeURIComponent(roleAlias)}`, withSignal(signal))).json() as Promise<PermissionEntry[]>;
}

/** Resolves effective permissions for a user at a content node. */
export async function getEffectiveForUser(userKey: string, nodeKey: string, signal?: AbortSignal): Promise<EffectivePermissions> {
  return (await apiFetch(`/effective?userKey=${userKey}&nodeKey=${nodeKey}`, withSignal(signal))).json() as Promise<EffectivePermissions>;
}

/** Resolves effective permissions for a role at a content node. */
export async function getEffectiveForRole(roleAlias: string, nodeKey: string, signal?: AbortSignal): Promise<EffectivePermissions> {
  return (
    await apiFetch(`/effective/by-role?roleAlias=${encodeURIComponent(roleAlias)}&nodeKey=${nodeKey}`, withSignal(signal))
  ).json() as Promise<EffectivePermissions>;
}
