import { client } from './generated/client.gen.js';
import type {
  DocTypeListItem,
  DocTypePermissionEntry,
  SaveDocTypePermissionItem,
  DocTypeCreateAuditItem,
} from '../models/doc-type-permission.models.js';

// Direct calls against the auth-configured hey-api client. The SDK class
// (`AdvancedPermissionsService`) hasn't been regenerated since adding the
// new endpoints, so we hit the URLs directly using the same security pipeline.
//
// Once the SDK is regenerated (next time `npm run generate:client` runs against
// a running TestSite), these functions can be switched to the typed wrappers.

const BASE = '/umbraco/management/api/v1/advanced-permissions';

const SECURITY = [{ scheme: 'bearer' as const, type: 'http' as const }];

/** Lists every non-element document type for the editor picker. */
export async function getDocTypes(signal?: AbortSignal): Promise<DocTypeListItem[]> {
  const { data } = await client.get<DocTypeListItem[], unknown, true>({
    url: `${BASE}/doc-type-permissions/doc-types`,
    security: SECURITY,
    throwOnError: true,
    ...(signal ? { signal } : {}),
  });
  return data;
}

/** Gets the stored entries for the editor's selected (role, content-type). */
export async function getDocTypePermissions(
  roleAlias: string,
  contentTypeKey: string,
  signal?: AbortSignal,
): Promise<DocTypePermissionEntry[]> {
  const { data } = await client.get<DocTypePermissionEntry[], unknown, true>({
    url: `${BASE}/doc-type-permissions`,
    security: SECURITY,
    throwOnError: true,
    query: { roleAlias, contentTypeKey },
    ...(signal ? { signal } : {}),
  });
  return data;
}

/** Replaces all entries for a (node, role, content-type) triple. */
export async function saveDocTypePermissions(
  nodeKey: string,
  roleAlias: string,
  contentTypeKey: string,
  entries: SaveDocTypePermissionItem[],
): Promise<void> {
  await client.put<unknown, unknown, true>({
    url: `${BASE}/doc-type-permissions`,
    security: SECURITY,
    throwOnError: true,
    body: { nodeKey, roleAlias, contentTypeKey, entries },
  });
}

/** Returns the Create Audit listing for a user under a given parent. */
export async function getDocTypeCreateAudit(
  userKey: string,
  parentKey: string,
  signal?: AbortSignal,
): Promise<DocTypeCreateAuditItem[]> {
  const { data } = await client.get<DocTypeCreateAuditItem[], unknown, true>({
    url: `${BASE}/doc-type-permissions/audit`,
    security: SECURITY,
    throwOnError: true,
    query: { userKey, parentKey },
    ...(signal ? { signal } : {}),
  });
  return data;
}
