import type { UmbConditionConfigBase, UmbConditionControllerArguments, UmbExtensionCondition } from '@umbraco-cms/backoffice/extension-api';
import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { UmbConditionBase } from '@umbraco-cms/backoffice/extension-registry';
import { UMB_ENTITY_CONTEXT, UMB_PARENT_ENTITY_CONTEXT } from '@umbraco-cms/backoffice/entity';
import { observeMultiple } from '@umbraco-cms/backoffice/observable-api';
import { getElementEffectiveForCurrentUser } from '../api/element-permissions.api.js';

/** Config shape shared by the element and element-folder permission conditions. */
export type UapElementPermissionConditionConfig<TAlias extends string> = UmbConditionConfigBase<TAlias> & {
  allOf?: Array<string>;
  oneOf?: Array<string>;
};

// ── Module-level cache ────────────────────────────────────────────────────────
// Keyed by node key (the endpoint is always scoped to the current backoffice user) and shared across
// every element/folder condition instance. The endpoint resolves the canonical Umb.Element.* verbs, so
// one cache entry serves both the element and folder conditions for a given node. Stores the Promise to
// deduplicate concurrent requests (e.g. 10 action conditions for the same element = 1 API call).

interface CacheEntry {
  promise: Promise<Set<string>>;
  timestamp: number;
}

const _cache = new Map<string, CacheEntry>();
const CACHE_TTL_MS = 30_000;

/** Fetches the current user's allowed canonical element verbs for a node, or an empty set if unknown. */
async function fetchAllowedVerbs(nodeKey: string): Promise<Set<string>> {
  const eff = await getElementEffectiveForCurrentUser(nodeKey);
  return new Set((eff.permissions ?? []).filter((p) => p.isAllowed).map((p) => p.verb));
}

function getCachedVerbs(nodeKey: string): Promise<Set<string>> {
  const existing = _cache.get(nodeKey);
  if (existing && Date.now() - existing.timestamp < CACHE_TTL_MS) {
    return existing.promise;
  }

  const promise = fetchAllowedVerbs(nodeKey).catch((err: unknown) => {
    if (_cache.get(nodeKey)?.promise === promise) {
      _cache.delete(nodeKey);
    }
    throw err;
  });

  _cache.set(nodeKey, { promise, timestamp: Date.now() });
  return promise;
}

/** Clears all cached element-permission results. Call after saving element permissions. */
export function clearElementEffectivePermissionCache(): void {
  _cache.clear();
}

/**
 * Base for the package's replacement element and element-folder user-permission conditions.
 *
 * Umbraco 18's native element conditions resolve action visibility from the bulk `/user/current`
 * permission set, which only reflects the native granular store — Advanced Permissions keeps its entries
 * elsewhere, and (unlike documents) Umbraco does not route the element current-user path through
 * `IElementPermissionService`. This base instead asks the package's own effective endpoint for the
 * current user's permissions at the node, which already reflects scope, inheritance and priority.
 *
 * For a not-yet-saved element (the workspace has pre-assigned a Guid that does not exist server-side, so
 * the endpoint returns an empty set) the base falls back to resolving the intended parent, mirroring the
 * inherited permissions the new node would receive.
 */
export abstract class UapElementPermissionConditionBase<TAlias extends string>
  extends UmbConditionBase<UapElementPermissionConditionConfig<TAlias>>
  implements UmbExtensionCondition
{
  #entityType: string | undefined;
  #unique: string | null | undefined;
  #parentUnique: string | null | undefined;

  /** The entity type(s) this condition governs (e.g. <c>element</c> or <c>element-folder</c>). */
  protected abstract get entityType(): string;

  /**
   * Maps a configured verb to the canonical verb the effective endpoint returns. Identity for the
   * element condition; container-to-canonical for the folder condition.
   */
  protected abstract mapVerb(verb: string): string;

  constructor(host: UmbControllerHost, args: UmbConditionControllerArguments<UapElementPermissionConditionConfig<TAlias>>) {
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
    if (this.#entityType === undefined) return;
    if (this.#unique === undefined) return;

    // Only meaningful for the entity type this condition governs; otherwise pass through.
    if (this.#entityType !== this.entityType) {
      this.permitted = true;
      return;
    }

    try {
      const allowed = await this.#resolveAllowedVerbs();
      if (allowed === null) {
        // No resolvable node/parent context — don't hide actions.
        this.permitted = true;
        return;
      }
      this.#evaluate(allowed);
    } catch {
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
      const own = await getCachedVerbs(this.#unique);
      if (own.size > 0) {
        return own;
      }
      // Empty = node unknown server-side (e.g. a new, unsaved element) → fall back to the parent.
    }

    if (this.#parentUnique) {
      return getCachedVerbs(this.#parentUnique);
    }

    return null;
  }

  /** Applies the allOf/oneOf config against the allowed set, mapping each configured verb first. */
  #evaluate(allowed: Set<string>): void {
    let allOfPermitted = true;
    let oneOfPermitted = true;

    if (this.config.allOf?.length) {
      allOfPermitted = this.config.allOf.every((v) => allowed.has(this.mapVerb(v)));
    }

    if (this.config.oneOf?.length) {
      oneOfPermitted = this.config.oneOf.some((v) => allowed.has(this.mapVerb(v)));
    }

    this.permitted = allOfPermitted && oneOfPermitted;
  }
}
