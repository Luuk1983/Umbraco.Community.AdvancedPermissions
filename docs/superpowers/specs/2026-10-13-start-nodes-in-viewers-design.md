# Start nodes in the viewers

Design, 2026-10-13. Issue #66. Target: v17 first (Umbraco 17.x). v18 follows separately, with its
own research against v18 source, and adds Library/element start nodes.

## Problem

The Access Viewer answers "what can this user or user group actually do here?", but it ignores
start nodes. A user whose start node is *Home* sees green Allow cells on *Shop*, a node they can
never open: the tree hides it, and every server action on it is refused. The viewer gives the
wrong answer for every node outside the start nodes.

The cause is a split that is correct in itself. Enforcement (`AdvancedContentPermissionService`)
checks start nodes first, with Umbraco's own `CalculateContentStartNodeIds` + `HasPathAccess`, and
refuses with `UnauthorizedMissingPathAccess` before advanced permissions are resolved. The resolver
(`ResolveAllAsync`), which feeds the `/effective` endpoints, knows nothing about start nodes and
should not: it answers "what do the permission entries say". The viewers only ask the resolver.

The Insert Options Viewer (doc-type create audit) has the same gap: it can show "can create a
News Article here" under a node the user cannot reach, and therefore cannot create under.

## How start nodes work in Umbraco 17 (verified against 17.7.0 source)

- **User group** (`IUserGroup.StartContentId`): a node id = that subtree; `-1` = root, everything;
  `null` = **no content access**. A deleted group start node is set to `null` by Umbraco.
- **User** (`IUser.StartContentIds`): `[]` (the normal case) = inherit the groups' start nodes;
  node ids = these **replace** every group start node on the same branch, group root included, so
  a user-level start node only ever narrows; contains `-1` = root.
- **Combination** (`UserExtensions.CombineStartNodes`, internal): union of the group start nodes,
  highest node per branch wins; user start nodes, deepest per branch wins; each user start node
  then removes group start nodes above or below it. Trashed or deleted start nodes are ignored.
  Empty result = no content access.
- **Permissions are not tied to the group that supplied a start node.** Start nodes and
  permissions are two independent unions over all of a user's groups. A Write allow from Editors
  applies inside the start node that Blog writers supplied.
- **No admin bypass.** Admins reach everything only because the Administrators group has root.
- **Ancestors of a start node** are shown in Umbraco's tree with `noAccess` (greyed, unclickable)
  so the user can browse down to the start node. They fail `HasPathAccess`: no opening, no actions.
- **Recycle bin**: only reachable with root access.
- **Caching**: the combined result is cached per user in Umbraco's isolated user cache, 2-minute
  *sliding* expiry. Saving the user clears it; saving any user group clears it for all users
  (`UserGroupCacheRefresher`, triggered by `UserGroupWithUsersSavedNotification`). Moving content
  clears nothing, but the ids stay valid; this design reads paths fresh, never from cache.
- **Outside the viewers' reach**: pickers whose data type sets `ignoreUserStartNodes`, and
  backoffice search, apply start nodes differently. Documented in the help, not modelled.
- **Media** uses the same mechanism, but Umbraco 17 has no per-node media permissions and this
  package manages none, so media is out of scope. **Library/element start nodes** do not exist in
  17.7.0.

## What the user sees

### Start-node strip

Shown under the subject picker in the Access Viewer and the Insert Options Viewer.

- The effective start nodes, each with a start-node marker: *Home*, *Blog*.
- One line per source, with its status:
  - **Editors** → *Home* (in use)
  - **Blog writers** → *Blog* (in use)
  - **Translators** → no start node (gives no content access)
  - **User-specific** → none
- A source that does not count says why: *covered by Home* (a higher start node on the same
  branch), *replaced by the user's own start node*, or *ignored: in the recycle bin or deleted*.
- Whole-subject states get one line: *Root access: every node is reachable*, *No content access*,
  and for the All Users group: *Start nodes don't apply to the All Users group*.
- In user group mode, an extra note: *Users get the combined start nodes of all their groups.
  Pick a user to see the real result.*

### Grid rows

| Node state | Row | Cells |
|---|---|---|
| Accessible (at or under a start node) | unchanged; the start node itself gets a marker | allow / deny as today |
| Path only (ancestor of a start node) | badge: can browse through, can't open | blocked |
| Not visible (outside every start node) | muted name, badge: not visible (start nodes) | blocked |

**Blocked** is a new cell state: neutral hatched grey with a lock icon, never green or red. It
answers "can they?" truthfully. The underlying advanced-permission result is not shown in the cell.

In the Insert Options Viewer, the node is the parent being created under, so the same verdict
applies to every document type column on that row.

### Blocked-cell dialog

Clicking a blocked cell opens the same reasoning dialog, in a blocked mode:

- The banner is grey with a lock, not green or red: *Jane can't use Update on Website > Shop: this
  node is outside their start nodes.* For a path-only node: *…: this node is only shown so they can
  browse to their start node.*
- An info note lists the subject's start nodes.
- The path table is greyed out under the heading *If this node were inside a start node:*, and
  still shows the full advanced-permission reasoning. Admins can preview a node before adding it
  as a start node.

## Server

### Start-node service

A new service in the main package, `IStartNodeAccessService`:

- `GetForUserAsync(userKey)`: loads the user, takes the effective start nodes from Umbraco's
  public `CalculateContentStartNodeIds` (the exact value enforcement uses), reads their paths
  fresh with `IEntityService.GetAllPaths`, and builds the source breakdown.
- `GetForUserGroupAsync(alias)`: the group's own `StartContentId` (`-1` root, `null` none). For
  `$everyone`, returns a "not applicable" result that treats every node as accessible.
- `Classify(nodePath, context)`: returns `Accessible`, `PathOnly` or `NotVisible`.
  - Accessible = Umbraco's `ContentPermissions.HasPathAccess(path, startNodeIds, RecycleBinContent)`,
    so the viewers can never disagree with enforcement.
  - PathOnly = the node is a strict ancestor of a start node, compared on comma-bounded path
    segments (not plain `StartsWith`, which Umbraco's `IsInBranchOfStartNode` gets wrong).
  - NotVisible = everything else.

**Source breakdown.** `CombineStartNodes` is internal, so it is not reimplemented. Umbraco's
final result is the truth; the breakdown only attributes it. For each group and for the user's
own start nodes:

- in the final result → *in use*;
- `null` group → *no start node*;
- not in the result, and the result holds an ancestor → *covered by <ancestor>*;
- not in the result, and a user-level start node sits on the same branch → *replaced by the user's
  own start node*;
- no path, or in the recycle bin → *ignored*.

Classification and attribution are pure static functions over ids and paths (`StartNodeRules`), so
they are unit-tested without substitutes. Classification calls Umbraco's static
`ContentPermissions.HasPathAccess` directly, which is itself pure.

### API (additive only)

- `/effective`, `/effective/by-role`: `EffectivePermissionsResponseModel` gains
  `StartNodeAccess` (`"Accessible" | "PathOnly" | "NotVisible"`), one value per node. The
  permission list is unchanged, so the dialog can still show "what would apply".
- `doc-type-permissions/audit-for-node`: the response gains the same `StartNodeAccess` for the
  audited parent node. A root audit classifies the content root (`-1`) like any node: `Accessible`
  only with root access, `PathOnly` when the subject has start nodes, matching
  `AuthorizeRootAccessAsync`. (The viewer shows no root row today; the endpoint supports it.)
- New `GET /start-nodes?userKey=` and `GET /start-nodes/by-role?roleAlias=`: the strip data
  (effective start nodes with key, name and path; per-source lines with status; whole-subject
  state). Same authorization as the other viewer endpoints.

The resolver, the enforcement service and the stored data are not touched.

## Live refresh

Both viewers already refetch everything on any `Access` server event, and v17 already routes one
on user saved, user group saved or deleted, and content moved, trashed or deleted. On a refresh
the viewers now also reload the start-node strip.

**Ordering bug to fix.** On a user group save, Umbraco publishes `UserGroupSavedNotification`
*before* `UserGroupWithUsersSavedNotification`, and only the second one clears the cached start
nodes of the group's users. The `Access` event currently fires on the first, so a viewer can
refetch and still read the old start nodes. `AccessServerEventHandler` also routes on
`UserGroupWithUsersSavedNotification`. That is an async handler, so it runs after the synchronous
cache-clearing handler for that notification. The first-hand test of this ordering is part of
the verification below.

## Error handling

- Unknown user or user group: the start-node endpoints return 404, the same as the other
  subject-keyed endpoints.
- Unknown node: the existing `200`-with-empty-list contract (ADR-009) stays. `StartNodeAccess`
  is `NotVisible` for it.
- Each node's verdict arrives inside its own effective (or audit) response, so a failed
  start-node strip request only shows the strip's error state; the grid is unaffected.
- An unknown user or user group on the effective endpoints keeps today's behaviour and reports
  `Accessible`: the viewer never shows a restriction it could not verify.

## Help and localization

- New terms in all 28 backoffice language files.
- Help text in English and Dutch: `access-viewer.md`, `insert-options-viewer.md`, and a start-node
  section in `concepts.md` (how they combine, user overrides group, no admin bypass, the picker
  and search exceptions).

## Testing

Test-first throughout.

- **Unit (Umbraco-free):** classification and attribution for every researched case: single
  group; overlapping groups; sibling groups; group root plus a user-level node; user root; user
  root plus a deeper node; a group with no start node; a trashed and a deleted start node; ancestor
  vs. a sibling whose id is a prefix (`1,23` vs `1,234`); no start nodes at all.
- **Service and controller** (`Umbraco.Community.AdvancedPermissions.Tests`): `StartNodeAccess`
  on the effective and audit responses; the start-node endpoints for user, user group,
  `$everyone` and unknown subjects; the extra live-refresh route.
- **Browser:** `npx tsc --noEmit`, plus the existing client tests.
- **Manual, v17 test site:** a user in two groups with different start nodes, plus a user-level
  start node; check the strip, all three row states, the blocked dialog, the Insert Options
  Viewer, and a live refresh after changing a group's start node in another tab.

## Out of scope

- v18 and Library/element start nodes (separate follow-up, researched against v18 source).
- Media.
- Changing enforcement or the resolver.
