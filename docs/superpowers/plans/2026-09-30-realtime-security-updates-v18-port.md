# Real-time security updates — v18 port plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bring the v17 real-time security updates feature to v18, across its larger surface area.

**Reference implementation:** the v17 worktree at
`C:\GitHub\UmbracoAdvancedSecurity\.claude\worktrees\realtime-security-access-signalr-de47d1`,
branch `claude/realtime-security-access-signalr-de47d1` (PR #56 into `v17/main`). Its design spec
and plan are at `docs/superpowers/specs/2026-09-23-realtime-security-updates-design.md` and
`docs/superpowers/plans/2026-09-23-realtime-security-updates.md`, and `Memory/ADRs/ADR-011-*`
records the decisions. **Read those for the "why"; this plan covers only what differs in v18.**

---

## What v18 changes

### More surfaces: eight, not four

| Surface | Kind | Directory |
|---|---|---|
| Access Viewer | viewer | `access-viewer/` |
| Doc Type Create Audit | viewer | `doc-type-permissions/` |
| Library Access Viewer | viewer | `library-permissions/` |
| Library Insert Viewer | viewer | `library-permissions/` |
| Permissions Editor | editor | `permissions-editor/` |
| Doc Type Permissions Editor | editor | `doc-type-permissions/` |
| Library Permissions Editor | editor | `library-permissions/` |
| Element Type Permissions Editor | editor | `library-permissions/` |

### A third permission family, but not a third shape

v18 adds **element / library permissions**. They are keyed by **node plus user group** — the same
two-part key as content permissions — so they need no new thinking about stamps or conflicts.
Document-type permissions remain the only three-part key (node, user group, document type).

### Shared base classes — do the work once

v18 has refactored what v17 duplicated:

- `NodePermissionRepositoryBase<TEntity>` — base of `AdvancedPermissionRepository` and
  `ElementPermissionRepository`
- `NodePermissionServiceBase` — base of `AdvancedPermissionService` and `ElementNodePermissionService`
- `INodePermissionRepository` / `INodePermissionService` — the shared contracts, which
  `IAdvancedPermissionRepository`, `IElementPermissionRepository`, `IAdvancedPermissionService` and
  `IElementNodePermissionService` all extend as marker interfaces

**So `SaveManyAsync`, the notification publish and the cache-invalidation ordering go into the base
once and both families inherit them.** Only the doc-type repository and service need their own copy,
exactly as in v17. Putting any of this into a concrete class instead of the base is a mistake — it
would silently leave one family unprotected.

---

## Task list

### Phase A — the shared core, copied verbatim

These files contain nothing version-specific. Copy them from the v17 branch rather than rewriting,
so the two versions cannot drift, and bring their tests with them.

- [ ] **A1: `PermissionStamp` + tests.** `Core/Concurrency/PermissionStamp.cs` and
      `Core.Tests/Concurrency/PermissionStampTests.cs`. The golden vector must still pass unchanged
      after the copy — if it does not, something was altered in transit.
- [ ] **A2: client pure modules + vitest.** `vitest.config.ts`, the `test`/`test:watch` scripts, and
      `src/live/`: `conflict.ts`, `coalesce.ts`, `reconcile.ts`, `refresh-phase.ts`, `navigation.ts`,
      `unsaved-changes-guard.ts`, `security-events.controller.ts`, `uap-live-banner.element.ts`,
      `uap-live-refresh.element.ts`, `uap-conflict-dialog.element.ts`, plus every `*.test.ts`
      alongside them. All 39 client tests must pass after the copy.

### Phase B — server

- [ ] **B1: stamp on the read endpoints.** Add `Stamp` wherever entries are returned, for all three
      families, plus the `ETag` on bare-array GETs. Mirror v17's `PermissionStampExtensions`.
- [ ] **B2: `SaveManyAsync`.** On `INodePermissionRepository` and `NodePermissionRepositoryBase`
      (covering content **and** element), and separately on the doc-type repository. Use
      `ExecuteDeleteAsync` inside an explicit transaction, with the duplicate-key guard.
- [ ] **B3: notifications.** `AdvancedPermissionsChangedNotification`,
      `ElementPermissionsChangedNotification` and `DocTypePermissionsChangedNotification`. Raise from
      `NodePermissionServiceBase` and the doc-type service, **strictly after cache invalidation**,
      isolated in try/catch that rethrows `OperationCanceledException`.
- [ ] **B4: batch endpoints.** For all three families, with the `409` as a **ProblemDetails**
      carrying `conflicts` in `Extensions` — see ADR-011 for why anything else is discarded by
      Umbraco's client interceptor. Extract one shared `TryMapEntries`; do not copy the validation.
- [ ] **B5: server events.** Sources for all three families plus `Access`, one
      `IEventSourceAuthorizer`, the handlers, and composer registration **last** in
      `RegisterNotificationHandlers`.

### Phase C — client

- [ ] **C1: API layer.** Regenerate the typed client; add batch wrappers and stamp-bearing reads for
      all three families. A `409` must resolve, not throw.
- [ ] **C2: localization.** Every key from v17, in all 28 files, **unprefixed** — the dictionary has a
      `uap:` section and Umbraco composes terms as `section_key`.
- [ ] **C3: the four viewers.** Live refresh in place, no flash, the shared refresh control. No
      flags, no dialogs — a viewer holds nothing of the user's.
- [ ] **C4: the four editors.** Reconciliation via the shared `reconcileNode`, sticky conflict flags,
      the banner, the batch save with the `409` dialog, and the unsaved-changes guard on
      navigate-away, tab close, every selection change and clearing the selection.

### Phase D — finishing

- [ ] **D1: docs.** Port ADR-011, and update the README and `help-docs/{en,nl}/` for the new surfaces.
- [ ] **D2: full verification.** Build, all four test projects, `tsc`, `vitest`, `npm run build`, then
      the manual two-window matrix against all four editors.

---

## Rules carried over — every one of these was a real defect in v17

Do not rediscover these:

1. **Raise events strictly after cache invalidation.** A client refetching on an event that overtook
   the invalidation reads the snapshot the change replaced, and never asks again.
2. **Never advance a conflicted cell's baseline or stamp.** Either one makes the next pass see
   `no-change`, clear the flag, and let the save pass the concurrency check silently.
3. **Identity cannot be inferred from value equality.** A cell has about seven values, so a colleague
   landing on the pending value is routine. There is no `own-write` verdict; the save echo is handled
   by not reconciling while `_saving`.
4. **Conflict flags are sticky.** They clear only on load-stored, a successful save, discard, or a
   selection change.
5. **Resolving a conflict must re-read the server.** Entries are frozen for conflicted verbs, so
   applying a stamp without re-reading leaves the old value under the new stamp, and the next save
   silently reverts somebody else's work.
6. **A `409` body must look like ProblemDetails** (`type`, `title`, `status`) or Umbraco's client
   interceptor replaces it wholesale.
7. **Length-prefix the verb in the stamp's canonical form**, or a verb containing a separator makes
   two different permission sets hash alike.
8. **Use `ExecuteDeleteAsync`** for the batch delete, not tracked removal — EF's command ordering has
   varied across versions and the unique index makes an ordinary save collide.
9. **Isolate every notification publish** in try/catch. The write has already committed; letting a
   handler's exception escape reports a false failure for a save that succeeded.
10. **Localization keys go in unprefixed.** `liveUpdated:`, not `uap_liveUpdated:`.

## Anything found here that also affects v17

PR #56 is open and unmerged. If a defect found during this port also exists in v17, fix it there too
rather than filing it — the branch is still live.
