# ADR-009: `/effective` Returns 200 OK with Empty List for Unknown Node Keys

**Status**: Accepted
**Date**: 2026-04-20

## Context

The `/umbraco/management/api/v1/advanced-permissions/effective` and `.../effective/by-role` endpoints resolve the effective permissions for a user/role at a given content node. They rely on `IEntityService.GetAllPaths(UmbracoObjectTypes.Document, nodeKey)` to build the ancestor path before delegating to the resolver. Originally both endpoints returned **404 Not Found** when `GetAllPaths` came back empty.

Two backoffice flows make that 404 fire legitimately in everyday use:

1. **Creating a new content node.** The workspace pre-assigns a `Guid` to the draft *before* the first save, so the key is present in `UMB_ENTITY_CONTEXT` but has no corresponding row server-side. Every `UAP.Condition.UserPermission.Document` evaluation during that pre-save phase queries `/effective` for a key the server can't resolve — immediately producing a 404.

2. **Context-resolution race inside the workspace.** `UMB_ENTITY_CONTEXT` typically resolves before `UMB_DOCUMENT_WORKSPACE_CONTEXT`, so on first open our condition runs once with `#isNew === undefined` (using the draft key) and again once `isNew` settles to `true` (switching to the parent key). The first fire is unavoidable and hits a key the server can't resolve.

The resulting 404 storm had two concrete side-effects:
- The backoffice's standard error-handling made the document name field appear read-only in the new-node workspace.
- Conditions that retried on failure produced a rapid burst of 404s per draft.

## Alternatives Considered

| Option | Outcome |
|---|---|
| Client-side gate: wait for all contexts to settle before calling the API | Broke tree actions — `consumeContext` does **not** fire with `undefined` when a context is absent, so outside a workspace the gate never opened and the only visible tree action was "Reload children". |
| Bounded client-side timer (e.g. 250 ms) before the first call | Introduced an arbitrary delay that was user-visible in the good case; rejected. |
| Keep 404 and teach every caller to treat it as "empty" | Spreads a fiddly contract across every consumer and still leaves the name-field-read-only symptom in the backoffice. |
| **Return 200 OK with an empty `Permissions` list for unknown keys** | Chosen. |

## Decision

Both endpoints return `200 OK` with `new EffectivePermissionsResponseModel(nodeKey, [])` when `BuildPathFromRoot(nodeKey)` yields an empty list. The resolver is **not** invoked in that case — there is nothing meaningful to resolve.

Semantic justification: "the effective permissions for a node that doesn't exist" is a legitimate question with a well-defined answer — the empty set. Nothing is allowed at a node that isn't there, which is consistent with the resolver's deny-by-default contract for verbs without matching entries. This is not an error condition; it's a degenerate-but-valid query.

The client-side condition (`document-user-permission.condition.ts`) still detects the "new unsaved node" case and switches to the parent key when `isNew === true`, because resolving against the parent yields the *correct* inherited permissions for the would-be new node. The 200-empty contract is the safety net for the pre-settle fires and for anything else that legitimately asks about a key that no longer resolves (deletes, transient drafts abandoned without save, etc.).

## Consequences

- Callers that need to distinguish "node exists but has no permissions" from "node does not exist" must verify existence via another endpoint. None of the current callers need that distinction.
- The client-side permission cache treats the empty response as a real value and caches it for the standard TTL. This is correct: the next fire (with settled workspace context and the parent key) uses a different cache key and populates the real answer.
- The `[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]` attribute was removed from both endpoint actions.
- Contract is covered by `AdvancedPermissionsEffectiveControllerTests`:
  - `GetEffectiveForUser_UnknownNodeKey_Returns200OkWithEmptyPermissions`
  - `GetEffectiveForUser_UnknownNodeKey_DoesNotCallPermissionService`
  - `GetEffectiveForUser_KnownNodeKey_Returns200OkWithResolvedPermissions`
  - `GetEffectiveForRole_UnknownNodeKey_Returns200OkWithEmptyPermissions`
  - `GetEffectiveForRole_UnknownNodeKey_DoesNotCallPermissionService`
