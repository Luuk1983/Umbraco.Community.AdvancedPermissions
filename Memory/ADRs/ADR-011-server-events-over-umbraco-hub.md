# ADR-011: Server Events Over Umbraco's Built-In Hub

**Status**: Accepted
**Date**: 2026-09-23

## Context

Two people can have the same permissions open at once, and neither is told when the other
writes. Today that produces two distinct failures. A viewer (Access Viewer, Document Type
Create Audit) resolves effective permissions once, on load, and then keeps showing them — a
user group's membership changes, a node moves, someone edits a permission on an ancestor, and
the screen stays as it was, indistinguishable from a correct one. An editor is worse: `PUT
/permissions` *replaces* every entry for a node and user group, so a client that loaded ten
minutes ago and saves now writes its whole picture back, silently dropping anything a colleague
added in between. This is true today with no real-time feature involved at all; live updates
make it visible, they do not create it.

Fixing both needs a transport that can push a change from the server to every open tab.

## Alternatives Considered

| Option | Outcome |
|---|---|
| Run a package-owned SignalR hub | A second WebSocket per tab, a new endpoint, a new `@microsoft/signalr` dependency, and a whole authorization surface of our own to get right. |
| Poll on an interval | Simplest to build, but either wastes requests when nothing changed or adds latency proportional to the interval, and cost scales with the number of open tabs across a team. |
| **Publish onto Umbraco's built-in server-events hub (`/umbraco/serverEventHub`) via `IServerEventRouter`, consumed through `UMB_MANAGEMENT_API_SERVER_EVENT_CONTEXT`** | Chosen. |

Precedent for both halves exists in sibling projects: `ProudNerds.Umbraco.Vision` publishes its
own event sources this way, and `Umbraco.Community.UmbraDesktop` consumes core's events and
classifies them against open editors rather than acting on what the event claims.

## Decision

The package runs no hub of its own. It publishes three event sources onto Umbraco's existing
hub:

| Source | Key | Raised for |
|---|---|---|
| `AdvancedPermissions:NodePermissions` | content node key | this package's own permission writes |
| `AdvancedPermissions:DocTypePermissions` | content node key | this package's own document-type permission writes |
| `AdvancedPermissions:Access` | user, user group or node key | `UserGroupSaved`, `UserGroupDeleted`, `UserSaved`, `ContentMoved`, `ContentMovedToRecycleBin`, `ContentDeleted` |

Delivery is gated by one `IEventSourceAuthorizer` (`AdvancedPermissionsEventAuthorizer`)
covering all three sources, evaluated against `AuthorizationPolicies.SectionAccessUsers` — the
same policy the mutating controllers already require. Core delivers no source that no
authorizer claims, so this authorizer is the whole access story for these events; there is no
second place it can be got wrong. Content editors without Users-section access therefore
receive nothing.

Riding someone else's transport rather than designing it is, by itself, an unremarkable
decision — it mainly saves us writing a hub. What makes this worth recording are three
constraints that fall out of that choice, each easy to violate by accident, and each producing
a bug with no stack trace: just a screen that quietly stops updating.

### 1. Events must be raised strictly after cache invalidation

The server event for a write is raised only after the corresponding cache invalidation has
completed — never from a handler registered as a sibling of the invalidator on the same
notification, and never from a code path that publishes before the cache is told.

Umbraco runs the handlers of one notification in registration order. A client that refetches on
an event which overtook the invalidation reads the very snapshot the change was meant to
replace — and, having consumed its one notification, never asks again. Nothing corrects the
screen afterwards; it is wrong until some unrelated change happens to refresh it, if ever.
`ProudNerds.Umbraco.Vision` hit exactly this ordering bug, which is why it is called out here
before this package repeats it.

Two places enforce it:

- In `AdvancedPermissionService` (and its document-type equivalent), the
  `IEventAggregator.Publish(...)` call for a changed-permissions notification sits **after**
  the `cache.InvalidateRoleEntries` / `cache.InvalidateAllResolved` calls it follows, in the
  same method, not from a separate handler racing them.
- In `AdvancedPermissionsComposer.RegisterNotificationHandlers`, the server-event handlers
  (`AdvancedPermissionsServerEventHandler`, `AccessServerEventHandler`) are registered **last**,
  appended at the very end of the method, after every cache invalidator for the notifications
  they also handle (`UserGroupSaved`, `ContentMoved`, and so on). Do not move these registrations
  earlier when editing that method.

### 2. `ServerEvent` carries no payload

`ServerEvent` carries only `EventType`, `EventSource` and one `Guid Key` — there is no field for
"what changed to what". Every event is therefore a hint that something moved, never a statement
of what it moved to, and every consumer refetches rather than trusting the event.

This is not a limitation to design around later; a payload could not carry the answer even if
`ServerEvent` had room for one. A permission written on an *ancestor* changes the effective
permissions of every descendant a viewer happens to be showing, and the event fires once, at the
ancestor's key — there is no way to enumerate every affected descendant in the payload. Truth
always has to come from asking the server again.

### 3. The concurrency stamp is what makes the guarantee real

The live updates are the good experience; the stamp is the promise that holds even when the good
experience doesn't reach a tab — one left asleep in the background, a hub connection that
dropped, a save issued before its own event arrived. `PermissionStamp` hashes the canonical
ordered form of a node-and-user-group's stored entries (sorted by verb, then state, then scope,
then priority-override flag), and the server checks it on every save regardless of whether any
event was ever received.

The canonical form is part of the wire contract, not an implementation detail: the client is
handed a stamp when it reads and sends the same value back when it saves, so an accidental
change to the canonicalisation would reject saves that are perfectly valid. Two details of that
form are not obvious from the code alone:

- **The verb is length-prefixed**, because it is the only variable-length field. Without a
  length prefix, a verb string containing the field separator could be read as structure, and a
  single entry whose verb embedded the right bytes could canonicalise identically to two
  unrelated entries — letting two different permission sets hash the same and the concurrency
  check pass when it should fail.
- **A golden-vector test** (`PermissionStampTests.Compute_KnownInput_MatchesGoldenVector`) pins
  the hash of a known input. It exists specifically to catch an accidental change to the
  canonical form, which tests that only compare stamps against each other cannot catch.

## Consequences

- No new endpoint, no second WebSocket per tab, no `@microsoft/signalr` dependency, and no
  authorization surface of our own beyond the one authorizer above.
- The feature's correctness does not depend on the transport. If a hub connection never delivers
  a single event to a given tab, a save from that tab is still refused when it should be,
  because the stamp check runs on every save regardless of what the client believes happened.
- **Not solved: load balancing.** Core's server events use SignalR's `IHubContext` with no
  backplane configured. In a load-balanced deployment, only clients connected to the same server
  as the writer receive the event; clients on other servers see nothing until they next refetch
  some other way. This is not a new class of problem for this package — `AdvancedPermissionCache`
  is already an in-memory, per-instance cache with the same limitation — but it is stated here so
  it is not instead discovered in production and mistaken for the feature being broken.
- Handler registration order in `AdvancedPermissionsComposer.RegisterNotificationHandlers`, and
  publish-after-invalidate order inside the permission services, are now load-bearing and must
  be preserved across future edits to either.

## Tests

- `AdvancedPermissionsEventAuthorizerTests` — the authorizer claims every source the package
  publishes.
- `ServerEventHandlerTests` — each notification routes the expected `ServerEvent`, with the
  right source and key.
- `AdvancedPermissionServiceNotificationTests` — the changed-notification is published only
  after both cache invalidations have run.
- `PermissionStampTests` — canonicalisation is order-independent, every covered field changes
  the hash, the empty set hashes to one fixed value, and the golden vector pins the wire
  contract.
