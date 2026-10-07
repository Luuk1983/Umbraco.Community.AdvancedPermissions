import type { UmbConditionConfigBase, UmbConditionControllerArguments, UmbExtensionCondition } from '@umbraco-cms/backoffice/extension-api';
import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { UmbConditionBase } from '@umbraco-cms/backoffice/extension-registry';
import { UMB_ENTITY_CONTEXT, UMB_PARENT_ENTITY_CONTEXT } from '@umbraco-cms/backoffice/entity';
import { UMB_DOCUMENT_WORKSPACE_CONTEXT } from '@umbraco-cms/backoffice/document';
import { observeMultiple } from '@umbraco-cms/backoffice/observable-api';
import { UserService } from '@umbraco-cms/backoffice/external/backend-api';
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
// Shared across all condition instances and keyed by node key (the endpoint is always
// scoped to the current backoffice user). Concurrent requests are deduplicated (e.g. 10
// action conditions for the same document = 1 API call). Empty answers are never kept:
// the endpoint answers with no verbs for a node it does not know yet, such as a new
// document's draft key, and the draft keeps that key once saved, so a remembered empty
// answer would make it read-only (#58). A node where every verb is denied also answers
// empty; not caching it only costs a repeat request for a node the user cannot use.

/**
 * Fetches the current user's effective (allowed) permission verbs for a single node from
 * Umbraco's standard per-document current-user endpoint. In Umbraco 18 this endpoint is routed
 * through IContentPermissionService (PR #22400) — i.e. our AdvancedContentPermissionService —
 * so the returned verbs already reflect Advanced Permissions' scope, inheritance and priority.
 */
async function fetchAllowedVerbs(nodeKey: string): Promise<Set<string>> {
  const { data } = await UserService.getUserCurrentPermissionsDocument({
    query: { id: [nodeKey] },
    throwOnError: true,
  });
  const entry = data?.permissions?.find((p) => p.nodeKey === nodeKey) ?? data?.permissions?.[0];
  return new Set(entry?.permissions ?? []);
}

const _cache = createPermissionCache(fetchAllowedVerbs, {
  ttlMs: 30_000,
  isCacheable: (verbs) => verbs.size > 0,
});

/**
 * Returns the current user's allowed verbs at a node, cached per node.
 * @param nodeKey The content node key.
 * @returns The allowed verbs at that node.
 */
function getCachedVerbs(nodeKey: string): Promise<Set<string>> {
  return _cache.get(nodeKey);
}

/** Clear all cached permission results. Call after saving permissions. */
export function clearEffectivePermissionCache(): void {
  _cache.clear();
}

// ── Condition class ─────────────────────────────────────────────────────────

/**
 * Replacement for Umbraco's built-in UmbDocumentUserPermissionCondition.
 *
 * The native condition resolves action visibility with a client-side ancestor-walk over the bulk
 * `/user/current` permission set. That set only contains documents that exist in Umbraco's native
 * granular permission storage — Advanced Permissions keeps its entries in its own tables, so the
 * native walk can never see them. This condition instead asks the server for the current user's
 * effective permissions at the specific node via the standard per-document current-user endpoint
 * (`GET /user/current/permissions/document`), which Umbraco 18 routes through
 * <c>IContentPermissionService</c> — i.e. <c>AdvancedContentPermissionService.GetPermissionsAsync</c>
 * — so full per-node scope and inheritance are honoured.
 */
export class UapDocumentUserPermissionCondition
  extends UmbConditionBase<UapDocumentPermissionConditionConfig>
  implements UmbExtensionCondition
{
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
  // We don't gate this — our backend returns one entry per requested key (empty when the
  // node is unknown), so the brief pre-workspace-context call produces a valid "deny"
  // rather than a 404 storm. Two things keep it harmless (#58): the cache never keeps
  // that empty answer, and an answer that arrives after a newer evaluation has started
  // is discarded, so the draft's late "no permissions" cannot overwrite the parent's answer.

  constructor(
    host: UmbControllerHost,
    args: UmbConditionControllerArguments<UapDocumentPermissionConditionConfig>,
  ) {
    super(host, args);

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

    // Wait for the entity context to be available.
    if (this.#entityType === undefined) return;
    if (this.#documentUnique === undefined) return;

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
      const allowedVerbs = await getCachedVerbs(targetKey);
      if (!isCurrent()) return;
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
