# Real-time security updates

Design, 2026-09-23. Target: v17 first, v18 to follow with the same design.

## Problem

Two people can hold the same permissions open at once, and neither is told when the other
writes. Today that produces two distinct failures.

A viewer goes quietly wrong. The Access Viewer resolves effective permissions once, on load,
and then keeps showing them. A user group's membership changes, a node moves, someone edits a
permission on an ancestor — and the screen stays as it was, indistinguishable from a correct
one. There is nothing on it to suggest it should be doubted.

An editor overwrites. `PUT /permissions` *replaces* every entry for a node and user group, so a
client that loaded ten minutes ago and saves now writes its whole picture back, silently
dropping anything a colleague added in between. This is true today with no real-time feature
involved at all; live updates make it visible, they do not create it.

## What this builds

1. Every surface — editors and viewers alike — reacts to a security change while it is open.
2. A viewer refreshes itself. It has nothing to lose, so it never asks.
3. An editor with unsaved work is never overwritten and never silently overwrites. It is told
   what collided, at cell granularity, and chooses.
4. The choice is enforced by the server, not only drawn by the client.
5. The changes are published as Umbraco notifications, so another package can act on them.

## Approach

Umbraco already ships a SignalR hub at `/umbraco/serverEventHub`. A package publishes onto it
through `IServerEventRouter`, gates delivery with an `IEventSourceAuthorizer`, and consumes it
in the backoffice through `UMB_MANAGEMENT_API_SERVER_EVENT_CONTEXT`. So this package runs no
hub of its own: no new endpoint, no `@microsoft/signalr` dependency, no second WebSocket per
tab, and no authorization surface of our own to get wrong.

`ServerEvent` carries only `EventType`, `EventSource` and one `Guid Key` — there is no payload.
That is not a constraint worth designing around, because a payload could not carry the answer
anyway: a permission written on an ancestor changes the effective permissions of every
descendant a viewer happens to be showing. The event is a hint that something moved. Truth
always comes from a refetch.

Precedent for both halves exists in sibling projects: `ProudNerds.Umbraco.Vision` publishes its
own event sources this way, and `Umbraco.Community.UmbraDesktop` consumes core's events and
classifies them against open editors rather than acting on what the event claims.

## Server

### Notifications

- `AdvancedPermissionsChangedNotification` — node key, user group alias, affected verbs.
- `DocTypePermissionsChangedNotification` — the document-type equivalent.

Raised through `IEventAggregator` by the service that performed the write, after the write has
committed and after the cache has been invalidated.

These live in the **main package**, not in `Abstractions`. `Abstractions` is a pure contract
package with no Umbraco dependency at all — it references only `MinVer` and SourceLink — and
`INotification` comes from `Umbraco.Cms.Core`. Putting them there would pull the whole CMS into
the contract package to serve consumers who, by definition, already reference Umbraco: you
cannot register a notification handler without it. So the main package is where they belong,
and nothing is lost by it.

### Event sources

| Source | Key | Raised for |
|---|---|---|
| `AdvancedPermissions:NodePermissions` | content node key | this package's own permission writes |
| `AdvancedPermissions:DocTypePermissions` | content node key | this package's own document-type permission writes |
| `AdvancedPermissions:Access` | user, user group or node key | `UserGroupSaved`, `UserGroupDeleted`, `UserSaved`, `ContentMoved`, `ContentMovedToRecycleBin`, `ContentDeleted` |

The third source exists because effective permissions move without this package's store being
touched at all. A viewer showing a user whose group membership just changed is as wrong as one
showing a node whose entries changed.

Virtual-root entries use the existing sentinel key,
`ffffffff-ffff-ffff-ffff-ffffffffffff`.

### Authorization

One `IEventSourceAuthorizer` covering all three sources, evaluated against
`AuthorizationPolicies.SectionAccessUsers` — the same policy the mutating controllers already
require. Core delivers no source that no authorizer claims, so this is the whole access story;
there is no second place where it can be got wrong.

Content editors without Users-section access therefore receive nothing. Pushing permission
changes to an ordinary editor's own session is a larger question — it touches what a content
editor is allowed to learn about the permission tree — and belongs with the client-side
effective-permission cache work, not here.

### Ordering

The server event is raised strictly after cache invalidation, never from a handler registered
as a sibling of the invalidator on the same notification.

This is the one sequencing rule in the design and it is not a detail. A client that refetches
on an event which overtook the invalidation reads the very snapshot the change was meant to
replace — and then, having consumed its notification, never asks again. The screen is wrong and
nothing will correct it. Vision hit exactly this and documents it.

### Concurrency

The editor saves several nodes at once. Today it issues one `PUT` per node in sequence with no
rollback, so a rejection on the third node leaves the first two written — a partial save is a
worse outcome than either overwriting or refusing. The concurrency check therefore needs a
batch.

`PUT /permissions/batch`, applied all-or-nothing in one transaction:

```
{ nodes: [ { nodeKey, roleAlias, entries[], expectedStamp } ], force: false }
```

- `200` returns the new stamp for each node.
- `409` returns a `ProblemDetails` naming only the conflicted nodes, each with its current
  stored entries and current stamp. That payload is what lets the dialog say "stored: deny ·
  yours: allow" truthfully rather than from a guess made when the event arrived.
- `force: true` skips the check entirely, and is only ever sent after the user has confirmed.

The single `PUT /permissions` remains, gaining an optional `expectedStamp` and `force`; omitting
the stamp keeps today's behaviour, so the published API stays backward compatible. The
document-type editor gets its own batch endpoint mirroring this one rather than sharing it —
its entries have a different shape, and folding both into one request body would make the
contract worse for each of them.

A **stamp** is a hash over the canonical ordered form of the stored entries for one node and
user group: entries sorted by verb, then state, then scope, then priority-override flag, and
hashed. No schema change, identical result on SQLite and SQL Server, and an empty set hashes to
one fixed known value rather than to nothing.

The server supplies stamps wherever it returns entries — an additive `stamp` field on the tree
and by-node response models, and an `ETag` on the bare-array `GET /permissions`. The client
never computes one. A stamp the client derived itself would need the same canonicalisation
implemented twice, in two languages, and any drift between the two would either wave through
the overwrite this feature exists to prevent or reject saves that are perfectly valid.

## Client

### The shared controller

`UapSecurityEventsController extends UmbControllerBase`, consumed by every surface. It
subscribes to `UMB_MANAGEMENT_API_SERVER_EVENT_CONTEXT`, filters to this package's three
sources, and coalesces bursts before calling its host back.

Coalescing is required, not a refinement. Publishing with descendants, emptying the recycle bin
or deleting a user group emits one event per affected entity, and a surface without coalescing
refetches once per event — each time against data already stale by the time the first response
lands. The rule is one refetch in flight per surface with at most one re-run queued behind it,
which is what `UmbraDesktop`'s `classifyCoalesced` does.

### Per surface

**Viewers** — the Access Viewer and the Document Type Create Audit. Refetch the visible set,
drop the client-side effective-permission cache, show a quiet "Updated just now" pill. They
hold nothing of the user's, so they never ask, never flag and never open a dialog.

**Editors with no unsaved changes** — identical, and silent.

**Editors with unsaved changes** — refetch the stored values and classify each node-and-verb
cell from the triple the editor already holds:

- `base` — `node.entries`, what the server said at load
- `mine` — `base` with `_pendingChanges` applied
- `theirs` — the refetched stored values

Per cell: `theirs == mine` is this editor's own write and does nothing; `theirs == base` is a
duplicate or stale event and does nothing; `mine == base` means nothing of the user's is at
stake, so the cell takes the server's value silently; anything else is a conflict.

Only conflicted cells are flagged, and the banner names how many. Nothing the user typed is
touched. "Load stored values" resolves **only the flagged cells**, so unrelated edits elsewhere
in the tree survive. "Keep my changes" dismisses the banner but leaves the flags in place, so
the save-time dialog is never the first the user hears of it.

This is simpler than the UmbraDesktop equivalent, which had to infer dirtiness by diffing two
observables on a core workspace context. Here `_pendingChanges` states it outright.

### Saving

The batch `PUT` carries each node's stamp. On `200` the editor adopts the returned stamps and
clears its pending map. On `409` it opens the confirmation dialog, built from the conflicted
nodes the response named: what is stored, what is yours, per cell. The dialog offers three
exits. Cancel abandons the save and leaves every pending change untouched. "Load stored values"
resolves the conflicted cells to what the `409` reported, drops them from the pending map, and
leaves the save unsent so the user can look before saving again. "Overwrite anyway" is the only
one that re-sends, and the only thing that ever sets `force: true`.

So the guarantee holds through every path that can reach a save: a tab that was asleep, a hub
connection that dropped, a save that beat its own event. The live updates are the good
experience; the stamp is the promise.

## Testing

Backend, test-first throughout:

- Stamp canonicalisation, including a fixed set of golden vectors and the empty-set case.
- Batch save: all-or-nothing on conflict, `409` shape and contents, `force` bypass, and that a
  rejected batch wrote nothing.
- Notifications raised after the write commits, with the right node, user group and verbs.
- Server events routed after cache invalidation, for both this package's writes and each
  Umbraco notification in the `Access` source.
- The authorizer allowing a Users-section principal and refusing one without it.

Client: a `vitest` setup, new to this repository, covering the pure modules only — the cell
classifier and the coalescing logic. No DOM tests, no component tests. Those two functions are
where a mistake silently loses someone's work; the rendering around them is not.

Manual verification in the test site, two browsers signed in as different users: a viewer left
open while the other user writes; a clean editor; a dirty editor with both a colliding and a
non-colliding edit; and a save forced through the 409.

## Not in scope

- **Load balancing.** Core's server events use SignalR's `IHubContext` with no backplane
  configured, so in a multi-server setup only clients connected to the same server receive
  them. This package's in-memory `AdvancedPermissionCache` already behaves this way, so nothing
  here adds a new class of problem — but it should be stated in the issue rather than
  discovered in production.
- **Content editors' own sessions.** Covered under Authorization above.
- **Presence.** No "who else is looking at this" indicator. Conflict is reported when it
  happens, not predicted.
- **Merging.** There is no automatic reconciliation of two edits to the same cell. A person
  chooses.

## v18

The same design, applied after v17 ships. The v18 package has more surfaces than v17, but they
all consume the same shared controller and the same classifier, so the per-surface work is
wiring rather than design. The server half is expected to port unchanged.
