# ADR-001: Permission Model Design

**Status**: Accepted
**Date**: 2026-04-04

## Context

Umbraco's built-in permission system only allows granting permissions (allow-only). We needed a richer model inspired by Sitecore: explicit Allow/Deny per verb per role per node, with inheritance through the content tree.

## Decision

### Core concepts
- **Verb**: a string key representing an action (e.g. `"Read"`, `"Write"`, `"Publish"`). Verbs are configured by the consuming application, not hardcoded.
- **State**: `Allow` or `Deny`. There is no "Inherit" row — absence of an entry means inherit from ancestors.
- **Scope**: how an entry propagates down the tree:
  - `ThisNodeOnly` — applies only to the node it is set on
  - `ThisNodeAndDescendants` — applies to the node and all its descendants
  - `DescendantsOnly` — applies to all descendants but not the node itself
- **Role**: any Umbraco user group alias, plus the reserved `$everyone` role.
- **`$everyone`**: applies to all authenticated backoffice users regardless of group membership. Evaluated before explicit role entries, so a role can override $everyone.

### Resolution algorithm

**User resolution** (`ResolveAllAsync`): combines all user's roles + `$everyone`.
1. Collect all entries for each of the user's roles and for `$everyone`.
2. For each verb, find the first applicable entry per role considering scope and distance.
3. `$everyone` entries provide a baseline; explicit role entries override them.
4. Priority: explicit deny > explicit allow > implicit deny > implicit allow > default deny.
5. Return an `EffectivePermission` with a reasoning chain explaining each decision.

**Role resolution** (`ResolveForRoleAsync`): uses **only** the specified role's own entries.
- `$everyone` is intentionally excluded — a role is a self-contained entity whose effective permissions must not be influenced by any other role, including `$everyone`.
- This ensures the Access Viewer shows what a role's own rules grant, not the intersection with system-wide defaults.
- Exception: when the selected role IS `$everyone`, only that role is used (no duplication).

### Domain model
- `AdvancedPermissionEntry`: nodeKey (null = root), roleAlias, verb, state, scope, id
- `EffectivePermission`: verb, isAllowed, isExplicit, reasoning chain
- `ReasoningStep`: description of why a specific entry was chosen or skipped

## Consequences

- Simple storage: one flat table, no complex hierarchical queries.
- Resolution is done in application code, not SQL — easy to unit test.
- Reasoning chain enables the Access Viewer UI to explain exactly why a user has or doesn't have a permission.
- `$everyone` is included in user resolution but explicitly excluded from role resolution — roles are self-contained.
- The split-entry pattern (e.g. ThisNodeOnly:Deny + DescendantsOnly:Allow on the same node) is intentional: it lets admins deny a verb at a node while allowing it for that node's descendants.
