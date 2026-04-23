# ADR-010: Initial Migration Uses Resolver-as-Oracle and Holds Fixed Post-State Invariants

**Status**: Accepted
**Date**: 2026-04-22

## Context

On first boot (when the `AdvancedPermission` table is empty), `AdvancedPermissionsDataImport` seeds the table from native Umbraco's user-group settings so that effective permissions for existing user groups match native Umbraco immediately after install. After install, editors edit entries freely through the Permissions Editor.

Native Umbraco v17 resolves node permissions by walking the content path deepest-to-root and returning the first non-default explicit permission it finds (see `UserService.CalculatePermissionsForPathForUser`). A granular permission on a node therefore fully replaces the inherited set (defaults, or an outer granular) for that node's subtree — including the case where an inner granular re-grants a verb that an outer granular stripped.

The initial implementation matched this behaviour by emitting, at every granular node, an Allow for each verb in the granular set plus a Deny for each default verb absent from the granular set. That is correct but emits:

- Redundant Allow rows for verbs the node already inherits from defaults.
- Redundant Deny rows at nested granular nodes that simply re-assert a Deny already present on an outer granular.

Every migrated Deny is a cross-role hazard too: the resolver's priority rules (explicit Deny wins, implicit Deny wins over implicit Allow from another role) mean that a redundant Deny for one group can suppress another group's Allow for a multi-group user. Multi-group correctness is not fully achievable without changing the resolver's semantics, and that is explicitly out of scope — but reducing avoidable Denies is.

## Alternatives Considered

| Option | Outcome |
|---|---|
| Keep the original "full Allow + Deny at every granular node" logic | Correct for single-group users; emits the most rows and the most Denies. |
| Symmetric diff vs. the group's defaults only | Minimal rows for flat setups; **incorrect** in nested setups where an inner granular re-grants a verb the outer stripped (no row emitted, resolver returns the ancestor's Deny). Rejected. |
| Bespoke nearest-granular-ancestor walk inside the migration | Correct and minimal, but duplicates resolver logic and has to be kept in sync with future resolver changes. |
| **Resolver-as-oracle with per-group flush** | Chosen. |

## Decision

### Algorithm

The migration emits the minimum set of rows required to make the existing `PermissionResolver` produce the same effective permissions as native Umbraco at every granular node.

1. Seed the `$everyone` row (Allow Read, `ThisNodeAndDescendants`, `NodeKey = VirtualRootNodeKey`) and flush.
2. Paginate user groups (`IUserGroupService.GetAllAsync`) and process **each group in isolation**, flushing its rows to the DB before moving on. This bounds memory and the resolver's per-call scan to a single group's row count.
3. For each group:
   1. Emit a virtual-root Allow (T&D) for every verb in the group's `Permissions` (native defaults).
   2. For every `DocumentGranularPermission` node, resolve the root-to-node path via `IDocumentNavigationQueryService.TryGetAncestorsOrSelfKeys`. Orphaned keys (node deleted in Umbraco) are skipped with a warning log.
   3. Sort the granular nodes ancestors-first (shortest path first).
   4. For each granular node X, for every verb in `defaults ∪ V_X`:
      - Build a `PermissionResolutionContext` with the entries emitted so far for this group and X's path.
      - Ask the resolver what the current effective state is for the verb at X.
      - The desired state is `Allow` iff the verb is in `V_X`, otherwise `Deny`.
      - If current matches desired, emit nothing.
      - Otherwise emit a single `ThisNodeAndDescendants` row with the desired state. Append it to the in-memory view so descendants see it.

### Post-state invariants

The resulting rows always satisfy:

1. **Virtual-root rows are Allow-only.** Every row with `NodeKey == VirtualRootNodeKey` has `State = Allow`. No Deny at the virtual root, ever.
2. **Node-specific rows always use `ThisNodeAndDescendants`.** The migration never emits `ThisNodeOnly` or `DescendantsOnly`.
3. **`$everyone`** has exactly one row: Allow Read (T&D) at the virtual root.

### Services and notification

- Handler stays on `UmbracoApplicationStartingNotification`. Verified via `CoreRuntime.cs` (v17): `PostRuntimePremigrationsUpgradeNotification` triggers `NavigationInitializationNotificationHandler.RebuildAsync` **before** `UmbracoApplicationStartingNotification` publishes, so the navigation cache is warm when the handler runs.
- `AdvancedPermissionsDataImport` takes two additional DI dependencies: `IDocumentNavigationQueryService` (for ancestry lookup) and `IPermissionResolver` (the oracle). Both are already registered — the navigation service by Umbraco core, the resolver by `AdvancedPermissionsComposer`.
- `IContentService` is **not** used. Navigation service lookups are O(1) in-memory and sufficient.

## Consequences

- The migrated state is minimal. Every emitted Deny is necessary for single-group-user resolver correctness. Nested granular inner-node Denies that are already inherited from an outer granular are not re-emitted.
- The resolver is reused as the source of truth. Any future change to its semantics (new scope, priority tweak) is inherited by the migration automatically — there is no second algorithm to keep in sync.
- Multi-group divergence from native Umbraco remains possible: a Deny needed for one group can still collide with another group's Allow. This is an accepted limitation of the package's Deny-wins-across-roles design; reducing avoidable Denies simply shrinks the collision surface.
- Memory is bounded to one group's row count at any time. `SaveChangesAsync` is invoked once per group (plus once for the `$everyone` seed).
- Orphaned granular keys are skipped with a warning and do not fail the import. Any failure during import is logged and swallowed — an unimportable state never prevents Umbraco from starting.

## Tests

Invariants and algorithm behaviour are enforced by `AdvancedPermissionsDataImportTests`:

- `Invariant_VirtualRootRows_AreAlwaysAllow`
- `Invariant_NodeSpecificRows_AlwaysUseThisNodeAndDescendants`
- `Handle_GroupWithDefaultsOnly_EmitsVirtualRootAllowsPerVerb`
- `Handle_MultipleGroups_FlushesOncePerGroup`
- `Handle_GranularMatchesDefaults_EmitsNothingAtNode`
- `Handle_GranularStripsDefault_EmitsOnlyDenyAtNode`
- `Handle_GranularAddsBeyondDefaults_EmitsOnlyAllowAtNode`
- `Handle_NestedGranularInnerReEnablesStrippedVerb_ResolverMatchesNative`
- `Handle_NestedGranularInnerStripsFurther_EmitsMinimalEntries`
- `Handle_OrphanedGranularNode_IsSkippedAndLogged`
- `Handle_EveryoneRole_GetsSingleAllowReadAtVirtualRoot`
- `Handle_WhenTableHasExistingRows_DoesNotReimport`
- `Handle_StartupGuardTripped_WritesNothing` (parametrised)
