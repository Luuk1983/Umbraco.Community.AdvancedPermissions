import type { UmbConditionConfigBase, UmbConditionControllerArguments, UmbExtensionCondition } from '@umbraco-cms/backoffice/extension-api';
import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { UmbConditionBase } from '@umbraco-cms/backoffice/extension-registry';
import { UMB_CURRENT_USER_CONTEXT } from '@umbraco-cms/backoffice/current-user';
import { UMB_ENTITY_CONTEXT, UMB_PARENT_ENTITY_CONTEXT } from '@umbraco-cms/backoffice/entity';
import { UMB_DOCUMENT_WORKSPACE_CONTEXT } from '@umbraco-cms/backoffice/document';
import { observeMultiple } from '@umbraco-cms/backoffice/observable-api';
import type { EffectivePermissions } from '../models/permission.models.js';
import { getEffectiveForUser } from '../api/advanced-permissions.api.js';
import { createPermissionCache } from './permission-cache.js';
import { createLatestRequest } from './latest-request.js';
import { isWorkspaceCheckOutOfPhase } from './workspace-permission-phase.js';

// ── Config type ─────────────────────────────────────────────────────────────
// Must match the shape used by all existing action manifests that reference
// 'Umb.Condition.UserPermission.Document' — allOf and oneOf arrays of verb strings.

type UapDocumentPermissionConditionConfig =
  UmbConditionConfigBase<'Umb.Condition.UserPermission.Document'> & {
    allOf?: Array<string>;
    oneOf?: Array<string>;
  };

// ── Module-level cache ──────────────────────────────────────────────────────
// Shared across all condition instances, keyed by `userKey|nodeKey`. Concurrent
// requests are deduplicated (e.g. 10 action conditions for the same document = 1 API call).
// The server answers with every verb for a node it knows and with an empty list for one it
// does not (such as a new document's draft key), so empty answers are never kept: the draft
// keeps its key once saved, and a remembered empty answer would make it read-only (#58).

const _cache = createPermissionCache<EffectivePermissions>(
  (key) => {
    const [userKey = '', nodeKey = ''] = key.split('|');
    return getEffectiveForUser(userKey, nodeKey);
  },
  { ttlMs: 30_000, isCacheable: (result) => result.permissions.length > 0 },
);

/**
 * Returns the effective permissions of a user at a node, cached per user and node.
 * @param userKey The user key.
 * @param nodeKey The content node key.
 * @returns The user's effective permissions at that node.
 */
function getCachedEffective(userKey: string, nodeKey: string): Promise<EffectivePermissions> {
  return _cache.get(`${userKey}|${nodeKey}`);
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
 *
 * NOTE (min-version swap): this deliberately does NOT use Umbraco's native
 * `GET /user/current/permissions/document` endpoint, because that endpoint only routes through
 * `IContentPermissionService` (our `AdvancedContentPermissionService`) from Umbraco 17.4.0 onward
 * (PR #22400 / issue #22351). On 17.3.x — the package's current minimum (`Umbraco.Cms [17.3.0, ...)`)
 * — the native endpoint bypasses our service and would return native permissions only. If the minimum
 * supported version is ever raised to 17.4.0+, switch this condition to the native endpoint (see the v18
 * package's document condition for the exact pattern) and retire the dependency on the custom
 * `getEffectiveForUser` call below; the `/effective` endpoint then reverts to Access-Viewer-only use.
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
  /**
   * True when this instance is one of the document workspace's own read-only checks: the
   * workspace creates those with itself as the host, which no other caller does.
   */
  #isWorkspaceCheck = false;
  /** Tracks the newest evaluation, so a slower, older answer never overwrites a newer one. */
  #latest = createLatestRequest();

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
  // valid response rather than a 404 storm. Two things keep it harmless (#58):
  // the cache never keeps that empty answer, and an answer that arrives after a
  // newer evaluation has started is discarded, so the draft's late "no
  // permissions" cannot overwrite the parent's answer.

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
      this.#isWorkspaceCheck = context !== undefined && (context as unknown) === host;
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
    // Every evaluation supersedes the ones still waiting for an answer.
    const isCurrent = this.#latest.begin();

    // Wait for all contexts to be available
    if (this.#entityType === undefined) return;
    if (this.#documentUnique === undefined) return;
    if (!this.#userKey) return;

    // Non-document entities: pass through (this condition is only meaningful for documents)
    if (this.#entityType !== 'document') {
      this.permitted = true;
      return;
    }

    // The workspace never stops its Create check after the first save, nor the previous
    // document's Update check when it moves on to creating the next one (#58). Out of
    // their phase they answer the wrong question, so they stop restricting the workspace.
    if (this.#isWorkspaceCheck && isWorkspaceCheckOutOfPhase(this.config, this.#isNew)) {
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
      if (!isCurrent()) return;
      const allowedVerbs = new Set(
        result.permissions.filter((p) => p.isAllowed).map((p) => p.verb),
      );
      this.#check(allowedVerbs);
    } catch {
      if (!isCurrent()) return;
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
