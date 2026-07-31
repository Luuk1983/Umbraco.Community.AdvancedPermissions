import type { RoleInfo, UserItem } from '../models/permission.models.js';
import type { DocTypeListItem } from '../models/doc-type-permission.models.js';

/**
 * Discriminates whether a remembered subject is a user group (role) or an individual user.
 * Role-only surfaces never persist `'user'`.
 */
export type SelectionSubjectKind = 'role' | 'user';

/**
 * A persisted selection snapshot for a single editor/viewer surface. Each surface writes only
 * the fields it uses: viewers/audits persist a subject (role or user), doc-type surfaces also
 * persist the chosen document type, and role-only editors persist just the role.
 *
 * Resolved objects (not just identifiers) are stored so the selection pill can render instantly
 * on restore and the surface's existing load path can fire immediately from the alias/key.
 */
export interface StoredSelection {
  /** Which kind of subject was chosen. Absent on surfaces that have no subject picker. */
  subjectKind?: SelectionSubjectKind;
  /** The chosen user group, when `subjectKind` is `'role'`. */
  role?: RoleInfo;
  /** The chosen user, when `subjectKind` is `'user'`. */
  user?: UserItem;
  /** The chosen document type, on the doc-type surfaces. */
  docType?: DocTypeListItem;
}

/** Prefix + schema version for every stored key. Bump the version to invalidate old snapshots. */
const KEY_PREFIX = 'uap.selection.v1';

/**
 * The current backoffice user's unique id, used to key storage per user so selections are not
 * shared between accounts. Selections live in the browser's `sessionStorage`, so they survive
 * navigation between editors/viewers and page reloads within the same tab, but are forgotten when
 * the tab is closed — a genuinely new backoffice session starts blank. Populated once from the
 * entrypoint via {@link setCurrentUserKey}; until then persistence is a no-op.
 */
let currentUserKey: string | undefined;

/**
 * Records the current backoffice user's unique id for storage keying. Called from the package
 * entrypoint when the current-user context resolves. Passing a falsy value disables persistence
 * (keys can no longer be built), which is the safe default before the context is available.
 *
 * @param unique The current user's unique id, or `undefined` to disable persistence.
 */
export function setCurrentUserKey(unique: string | undefined): void {
  currentUserKey = unique || undefined;
}

/**
 * Builds the fully-qualified storage key for a surface, or `undefined` when no current user is
 * known yet (in which case callers skip persistence).
 *
 * @param surfaceId Stable identifier for the editor/viewer surface (e.g. `'access-viewer'`).
 * @returns The storage key, or `undefined` when persistence is unavailable.
 */
function keyFor(surfaceId: string): string | undefined {
  if (!currentUserKey) return undefined;
  return `${KEY_PREFIX}.${currentUserKey}.${surfaceId}`;
}

/**
 * Reads the remembered selection for a surface. Returns `undefined` when nothing is stored, when
 * persistence is unavailable, or when the stored value cannot be parsed.
 *
 * @param surfaceId Stable identifier for the surface.
 * @returns The stored selection snapshot, or `undefined`.
 */
export function loadSelection(surfaceId: string): StoredSelection | undefined {
  const key = keyFor(surfaceId);
  if (!key) return undefined;
  try {
    const raw = sessionStorage.getItem(key);
    if (!raw) return undefined;
    return JSON.parse(raw) as StoredSelection;
  } catch {
    // Corrupt JSON or storage unavailable (private mode, disabled): treat as "nothing stored".
    return undefined;
  }
}

/**
 * Persists the remembered selection for a surface. Best-effort: silently no-ops when persistence
 * is unavailable or `sessionStorage` throws (e.g. quota exceeded, private mode).
 *
 * @param surfaceId Stable identifier for the surface.
 * @param value The selection snapshot to store.
 */
export function saveSelection(surfaceId: string, value: StoredSelection): void {
  const key = keyFor(surfaceId);
  if (!key) return;
  try {
    sessionStorage.setItem(key, JSON.stringify(value));
  } catch {
    // Best-effort persistence — ignore storage failures.
  }
}

/**
 * Removes the remembered selection for a surface. Best-effort: silently no-ops when persistence
 * is unavailable or `sessionStorage` throws.
 *
 * @param surfaceId Stable identifier for the surface.
 */
export function clearSelection(surfaceId: string): void {
  const key = keyFor(surfaceId);
  if (!key) return;
  try {
    sessionStorage.removeItem(key);
  } catch {
    // Best-effort — ignore storage failures.
  }
}
