import type { UmbConditionConfigBase, UmbConditionControllerArguments, UmbExtensionCondition } from '@umbraco-cms/backoffice/extension-api';
import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { UmbConditionBase } from '@umbraco-cms/backoffice/extension-registry';
import { UMB_CURRENT_USER_CONTEXT } from '@umbraco-cms/backoffice/current-user';
import { UMB_ENTITY_CONTEXT, UMB_PARENT_ENTITY_CONTEXT } from '@umbraco-cms/backoffice/entity';
import { UMB_DOCUMENT_WORKSPACE_CONTEXT } from '@umbraco-cms/backoffice/document';
import { observeMultiple } from '@umbraco-cms/backoffice/observable-api';
import type { EffectivePermissions } from '../models/permission.models.js';
import { getEffectiveForUser } from '../api/advanced-permissions.api.js';

// ── Config type ─────────────────────────────────────────────────────────────
// Must match the shape used by all existing action manifests that reference
// 'Umb.Condition.UserPermission.Document' — allOf and oneOf arrays of verb strings.

type UapDocumentPermissionConditionConfig =
  UmbConditionConfigBase<'Umb.Condition.UserPermission.Document'> & {
    allOf?: Array<string>;
    oneOf?: Array<string>;
  };

// ── Module-level cache ──────────────────────────────────────────────────────
// Shared across all condition instances. Stores the Promise to deduplicate
// concurrent requests (e.g. 10 action conditions for the same document = 1 API call).

interface CacheEntry {
  promise: Promise<EffectivePermissions>;
  timestamp: number;
}

const _cache = new Map<string, CacheEntry>();
const CACHE_TTL_MS = 30_000;

function getCachedEffective(userKey: string, nodeKey: string): Promise<EffectivePermissions> {
  const key = `${userKey}|${nodeKey}`;
  const entry = _cache.get(key);
  if (entry && Date.now() - entry.timestamp < CACHE_TTL_MS) {
    return entry.promise;
  }

  const promise = getEffectiveForUser(userKey, nodeKey).catch((err: unknown) => {
    // Remove failed entry so the next evaluation retries
    if (_cache.get(key)?.promise === promise) {
      _cache.delete(key);
    }
    throw err;
  });

  _cache.set(key, { promise, timestamp: Date.now() });
  return promise;
}

/** Clear all cached effective permission results. Call after saving permissions. */
export function clearEffectivePermissionCache(): void {
  _cache.clear();
}

// ── Condition class ─────────────────────────────────────────────────────────

/**
 * Replacement for Umbraco's built-in UmbDocumentUserPermissionCondition.
 * Instead of reading from cached native group permissions, this condition calls
 * the Advanced Permissions /effective API to resolve permissions with full scope
 * and inheritance support.
 */
export class UapDocumentUserPermissionCondition
  extends UmbConditionBase<UapDocumentPermissionConditionConfig>
  implements UmbExtensionCondition
{
  #userKey: string | undefined;
  #entityType: string | undefined;
  #documentUnique: string | null | undefined;
  // Workspace context: only set when the condition runs inside a document workspace.
  // Used to detect the "creating a new, unsaved document" state — the backoffice
  // pre-assigns a Guid to the draft, so #documentUnique is non-null even though
  // the node doesn't exist server-side yet. In that case we must resolve
  // permissions from the intended parent instead of the draft's own key.
  #isNew: boolean | undefined;
  #parentUnique: string | null | undefined;

  // Note on the context-resolution race
  // ─────────────────────────────────────
  // UMB_ENTITY_CONTEXT typically resolves before UMB_DOCUMENT_WORKSPACE_CONTEXT,
  // so when a user opens the workspace to create a new document the condition
  // fires #checkPermissions once with #isNew still undefined (using the draft's
  // pre-assigned Guid) and then fires again once isNew resolves to true
  // (switching to the parent's key, which yields the correct inherited perms).
  //
  // We don't gate this — the server returns 200 with an empty permissions list
  // for unknown node keys, so the brief pre-workspace-context call produces a
  // valid cacheable response rather than a 404 storm. The second fire with the
  // parent key then replaces the cached entry with real permissions.

  constructor(
    host: UmbControllerHost,
    args: UmbConditionControllerArguments<UapDocumentPermissionConditionConfig>,
  ) {
    super(host, args);

    this.consumeContext(UMB_CURRENT_USER_CONTEXT, (context) => {
      this.observe(
        context?.unique,
        (unique) => {
          this.#userKey = unique ?? undefined;
          void this.#checkPermissions();
        },
        'observeUserKey',
      );
    });

    this.consumeContext(UMB_ENTITY_CONTEXT, (context) => {
      if (!context) {
        this.removeUmbControllerByAlias('observeEntity');
        return;
      }
      this.observe(
        observeMultiple([context.entityType, context.unique]),
        ([entityType, unique]) => {
          this.#entityType = entityType;
          this.#documentUnique = unique;
          void this.#checkPermissions();
        },
        'observeEntity',
      );
    });

    // Document workspace context — only resolves inside a document workspace.
    // Outside a workspace (tree actions, collections, etc.) this callback never
    // fires, and #isNew remains undefined (treated as "existing node" below).
    this.consumeContext(UMB_DOCUMENT_WORKSPACE_CONTEXT, (context) => {
      this.observe(
        context?.isNew,
        (isNew) => {
          this.#isNew = isNew;
          void this.#checkPermissions();
        },
        'observeIsNew',
      );
    });

    // Parent entity context — populated inside workspaces. Used only when
    // #isNew === true; otherwise the draft's own key is the correct target.
    this.consumeContext(UMB_PARENT_ENTITY_CONTEXT, (context) => {
      this.observe(
        context?.parent,
        (parent) => {
          this.#parentUnique = parent?.unique;
          void this.#checkPermissions();
        },
        'observeParent',
      );
    });
  }

  async #checkPermissions(): Promise<void> {
    // Wait for all contexts to be available
    if (this.#entityType === undefined) return;
    if (this.#documentUnique === undefined) return;
    if (!this.#userKey) return;

    // Non-document entities: pass through (this condition is only meaningful for documents)
    if (this.#entityType !== 'document') {
      this.permitted = true;
      return;
    }

    // Pick which node key to resolve permissions at:
    //  - Creating a new, unsaved document inside a workspace: use the parent.
    //    The draft's own Guid doesn't exist server-side yet, and the new node's
    //    effective permissions are exactly what it would inherit from its parent.
    //  - Otherwise: use the document's own key.
    let targetKey: string | null;
    if (this.#isNew === true) {
      // parentUnique === null means "creating at tree root" — no parent doc to resolve
      // against; permit (consistent with existing documentUnique === null fallback).
      if (this.#parentUnique === null || this.#parentUnique === undefined) {
        this.permitted = true;
        return;
      }
      targetKey = this.#parentUnique;
    } else {
      // New unsaved document with a null unique (no pre-assigned Guid yet): allow.
      if (this.#documentUnique === null) {
        this.permitted = true;
        return;
      }
      targetKey = this.#documentUnique;
    }

    try {
      const result = await getCachedEffective(this.#userKey, targetKey);
      const allowedVerbs = new Set(
        result.permissions.filter((p) => p.isAllowed).map((p) => p.verb),
      );
      this.#check(allowedVerbs);
    } catch {
      // Deny by default if the API call fails
      this.permitted = false;
    }
  }

  #check(allowedVerbs: Set<string>): void {
    // Logic matches the native UmbDocumentUserPermissionCondition exactly
    let allOfPermitted = true;
    let oneOfPermitted = true;

    if (this.config.allOf?.length) {
      allOfPermitted = this.config.allOf.every((v) => allowedVerbs.has(v));
    }

    if (this.config.oneOf?.length) {
      oneOfPermitted = this.config.oneOf.some((v) => allowedVerbs.has(v));
    }

    if (!allOfPermitted && !oneOfPermitted) {
      allOfPermitted = false;
      oneOfPermitted = false;
    }

    this.permitted = allOfPermitted && oneOfPermitted;
  }
}

export { UapDocumentUserPermissionCondition as api };
