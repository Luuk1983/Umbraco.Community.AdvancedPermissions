import type { UmbConditionConfigBase, UmbConditionControllerArguments } from '@umbraco-cms/backoffice/extension-api';
import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { UmbConditionBase } from '@umbraco-cms/backoffice/extension-registry';
import { UMB_ENTITY_CONTEXT, UMB_PARENT_ENTITY_CONTEXT } from '@umbraco-cms/backoffice/entity';
import { observeMultiple } from '@umbraco-cms/backoffice/observable-api';
import { UserService } from '@umbraco-cms/backoffice/external/backend-api';
import { createPermissionCache } from './permission-cache.js';
import { createLatestRequest } from './latest-request.js';
import type { VerbRequirement } from './element-verbs.js';

/** Config shape shared by the element and element-folder permission conditions. */
export type UapElementPermissionConditionConfig<TAlias extends string> = UmbConditionConfigBase<TAlias> & VerbRequirement;

// ── Module-level cache ────────────────────────────────────────────────────────
// Keyed by node key (the endpoint always answers for the current backoffice user) and shared by every
// element, folder and element-or-folder condition instance, so ten action conditions for one node cost one
// request. Empty answers are never kept: the endpoint answers with no verbs for a node it does not know
// yet, such as a new element's draft key, and the element keeps that key once saved (#58).

/**
 * Fetches the current user's allowed verbs at a Library element or folder from Umbraco's standard
 * current-user endpoint (`GET /user/current/permissions/element`). Since Umbraco 18.1 that endpoint is
 * routed through IElementPermissionService — i.e. the package's AdvancedElementPermissionService — so the
 * verbs already reflect scope, inheritance and priority. For a folder the answer holds both the canonical
 * `Umb.Element.*` verbs and the `Umb.ElementContainer.*` ones.
 * @param nodeKey The element or folder key.
 * @returns The allowed verbs at that node; empty for a node the server does not know.
 */
async function fetchAllowedVerbs(nodeKey: string): Promise<Set<string>> {
  const { data } = await UserService.getUserCurrentPermissionsElement({
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

/** Clears all cached element-permission results. Call after saving element permissions. */
export function clearElementEffectivePermissionCache(): void {
  _cache.clear();
}

/**
 * Base for the package's replacement Library permission conditions (element, element folder, and element
 * or element folder).
 *
 * Umbraco's native conditions resolve action visibility from the bulk `/user/current` permission set,
 * which only lists nodes that have entries in Umbraco's own granular permission store — Advanced
 * Permissions keeps its entries elsewhere, so the native ancestor walk can never see them. This base
 * instead asks for the current user's permissions at the specific node, which already reflect scope,
 * inheritance and priority.
 *
 * For a not-yet-saved element (the workspace has pre-assigned a Guid that does not exist server-side, so
 * the endpoint returns an empty set) the base falls back to resolving the intended parent, mirroring the
 * inherited permissions the new node would receive.
 */
export abstract class UapElementPermissionConditionBase<TConfig extends UmbConditionConfigBase>
  extends UmbConditionBase<TConfig>
{
  #entityType: string | undefined;
  #unique: string | null | undefined;
  #parentUnique: string | null | undefined;
  /** Tracks the newest evaluation, so a slower, older answer never overwrites a newer one. */
  #latest = createLatestRequest();

  /**
   * Whether this condition decides for an entity type. For any other entity type it permits.
   * @param entityType The entity type in context.
   * @returns Whether this condition governs it.
   */
  protected abstract appliesTo(entityType: string): boolean;

  /**
   * Decides the configured requirement against the verbs allowed at the node.
   * @param allowed The verbs allowed at the node (canonical, plus container verbs for a folder).
   * @returns Whether the condition permits.
   */
  protected abstract isPermitted(allowed: ReadonlySet<string>): boolean;

  constructor(host: UmbControllerHost, args: UmbConditionControllerArguments<TConfig>) {
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
          this.#unique = unique;
          void this.#check();
        },
        'observeEntity',
      );
    });

    this.consumeContext(UMB_PARENT_ENTITY_CONTEXT, (context) => {
      this.observe(
        context?.parent,
        (parent) => {
          this.#parentUnique = parent?.unique;
          void this.#check();
        },
        'observeParent',
      );
    });
  }

  async #check(): Promise<void> {
    // Every evaluation supersedes the ones still waiting for an answer.
    const isCurrent = this.#latest.begin();

    if (this.#entityType === undefined) return;
    if (this.#unique === undefined) return;

    // Only meaningful for the entity types this condition governs; otherwise pass through.
    if (!this.appliesTo(this.#entityType)) {
      this.permitted = true;
      return;
    }

    try {
      const allowed = await this.#resolveAllowedVerbs();
      if (!isCurrent()) return;
      if (allowed === null) {
        // No resolvable node/parent context (e.g. the Library root) — don't hide actions; the server
        // still enforces every operation.
        this.permitted = true;
        return;
      }
      this.permitted = this.isPermitted(allowed);
    } catch {
      if (!isCurrent()) return;
      // Deny by default if resolution fails.
      this.permitted = false;
    }
  }

  /**
   * Resolves the allowed verb set for the current node, falling back to the parent for unsaved drafts
   * (the node's own key returns an empty set because it does not yet exist server-side). Returns
   * <c>null</c> when there is no node or parent to resolve against.
   */
  async #resolveAllowedVerbs(): Promise<Set<string> | null> {
    if (this.#unique) {
      const own = await _cache.get(this.#unique);
      if (own.size > 0) {
        return own;
      }
      // Empty = node unknown server-side (e.g. a new, unsaved element) → fall back to the parent.
    }

    if (this.#parentUnique) {
      return _cache.get(this.#parentUnique);
    }

    return null;
  }
}
