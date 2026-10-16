# Start nodes in the viewers (v18)

Design, 2026-10-15. Issue #66. Target: v18 (Umbraco 18.x, package floor 18.0.0). Port of the v17
feature (PR #67, commit `cf91d85` on `v17/feature/66_start_nodes_access_viewer`), extended to
Library (element) start nodes. The v17 spec and plan in that commit are the reference; this
document records the v18 design and every place it differs.

## Problem

The viewers answer "what can this user or user group actually do here?", but they ignore start
nodes. A user whose start node is *Home* sees green Allow cells on *Shop*, a node they can never
open. Enforcement checks start nodes before advanced permissions
(`UnauthorizedMissingPathAccess`); the resolvers that feed the viewers do not, and should not.

v18 has four viewers, and all four have the gap:

| Viewer | Tree | Start nodes that apply |
|---|---|---|
| Access Viewer | content | content start nodes |
| Insert Options Viewer (doc-type create audit) | content | content start nodes |
| Library Access Viewer | Library | Library (element) start nodes |
| Library Insert Viewer (element-type create audit) | Library | Library (element) start nodes |

The Library gap is the larger one on upgraded sites: the v18 upgrade migration gives only the
Administrators group a Library start node (`AddElements.EnsureElementStartNodeColumn`). Every
other existing group has none, so its users cannot open the Library at all, while the Library
viewers show green Allow cells for them.

## How start nodes work in Umbraco 18 (verified against 18.0.0 and 18.2.1 source)

`UserExtensions.cs` is identical in 18.0.0 and 18.2.1.

**Content** works exactly as in v17:

- **User group** (`IUserGroup.StartContentId`): node id = that subtree; `-1` = root; `null` = no
  content access.
- **User** (`IUser.StartContentIds`): `[]` (the normal case) = inherit the groups' start nodes;
  otherwise user-level start nodes.
- **Combination** (`CombineStartNodes`, internal): group start nodes unioned, highest per branch
  wins; user start nodes, deepest per branch wins; then each user start node removes every group
  start node above or below it on the same branch **and is added to the result**.
  *Correction to the v17 spec:* a user-level start node does not only narrow. On a branch no group
  covers, it adds access (group *Home* + user *Shop* = *Home* and *Shop*). On a branch a group
  covers, it replaces that group start node (group root + user *Shop* = *Shop* only).
- Trashed or deleted start nodes are ignored; empty result = no access; no admin bypass.
- Ancestors of a start node are shown in the tree with `noAccess` and fail `HasPathAccess`.
- `ContentPermissions.HasPathAccess(path, ids, recycleBinId)`: null or empty ids = no access;
  `-1` = everything; a path through the recycle bin = only with root; otherwise a comma-bounded
  start node id in the path.

**Library (new in v18):**

- **User group** (`IUserGroup.StartElementId`): an element *folder* id
  (`UmbracoObjectTypes.ElementContainer`); `-1` = Library root; `null` = no Library access.
- **User** (`IUser.StartElementIds`): defaults to `[]`; same meaning as for content.
- **Combination**: `CalculateElementStartNodeIds`, which calls the same `CombineStartNodes` with
  `ElementContainer` and the element recycle bin (`-22`). Same rules as content.
- Fresh installs give the default groups `-1`; upgrades give only Administrators `-1`.
- The package's element enforcement (`AdvancedElementPermissionService`,
  `AdvancedElementContainerPermissionService`) already checks
  `HasPathAccess(path, CalculateElementStartNodeIds(...), RecycleBinElement)` first, and the
  Library root with `HasPathAccess("-1", ...)`.
- Element paths mix element and folder ids (`-1,<folder>,<element>`); start nodes are always
  folders.

**Caching and live refresh** (unchanged from v17): the combined ids are cached per user in
Umbraco's isolated user cache (2-minute sliding). Saving a user clears it. Saving a user group
publishes `UserGroupSavedNotification`, then `UserGroupWithUsersSavedNotification`; only the
second triggers `UserGroupCacheRefresher`, which clears the whole isolated `IUser` cache, content,
media and Library start nodes alike. Umbraco's scoped publisher runs all sync handlers (the cache
clear) before async handlers, so the extra route below is defensive.

**Out of reach for the viewers** (documented in the help, not modelled): pickers with
`ignoreUserStartNodes`, backoffice search. Media is out of scope (no per-node media permissions in
this package).

## What the user sees

### Start-node card

Shown under the subject picker in all four viewers. The heading names the tree: *Start nodes* in
the content viewers, *Library start nodes* in the Library viewers.

- One opening sentence on whether start nodes limit the subject (restricted, root access, no
  access, or *Start nodes don't apply to the All Users group*).
- The start nodes in effect, each with who set it (a user group, the user's own account, or
  both). As in the finished v17, configured start nodes that change nothing (covered, replaced,
  narrowed, deleted) are not listed; the server still returns every source with its status, which
  is how the card knows who set each start node.
- In user group mode: *Users get the combined start nodes of all their groups. Pick a user to see
  the real result.*
- Library Insert Viewer, restricted subject: *They can only create inside their Library start
  nodes.*

### Grid rows (Access Viewer, Insert Options Viewer, Library Access Viewer)

| Node state | Row | Cells |
|---|---|---|
| Accessible (at or under a start node) | unchanged; the start node itself gets a marker | allow / deny as today |
| Path only (ancestor of a start node) | badge *Browse only (start nodes)* | blocked |
| Not visible (outside every start node) | muted name, badge *Not visible (start nodes)* | blocked |

**Blocked** is a cell state: neutral hatched grey with a lock icon, never green or red. The Library
Access Viewer already has a hatched N/A cell for verbs that don't apply to a node kind; blocked is
told apart by the lock icon and its own tooltip. N/A wins over blocked (a verb that cannot apply
stays N/A).

The Library Access Viewer shows no Library root row (its top level is the root-level folders and
elements). The `element/effective` endpoints still classify the virtual-root key as the Library
root: Accessible only with Library root access, Path only when the subject has Library start nodes.

In the Insert Options Viewer, the row's node is the parent; every document type column on that row
gets the row's verdict.

### Library Insert Viewer rows

Element-type creation is filtered across the whole Library (no parent node), so the verdict is per
subject, not per row:

- No Library access: every row is blocked.
- Root access or restricted: rows unchanged; restricted adds the card line above.

### Blocked-cell dialog

The same reasoning dialog, in blocked mode (as v17): a grey banner with a lock
(*… this node is outside their start nodes* / *… only shown so they can browse to their start
node* / Library Insert Viewer: *… they have no access to the Library*), a note listing the start
nodes, and the path table greyed out under *If this node were inside a start node:*, still showing
the full advanced-permission reasoning.

## Server

### Start-node service

`IStartNodeAccessService` in the main package (`Services/StartNodes/`), ported from v17 with a
`StartNodeTree` parameter (`Content`, `Library`):

- `GetForUserAsync(userKey, tree)`: the effective ids from `CalculateContentStartNodeIds` or
  `CalculateElementStartNodeIds` (the value enforcement uses); names and paths read fresh with
  `IEntityService.GetAll(Document | ElementContainer, ids)`, never with an empty id array
  (`GetAll` with no ids returns everything).
- `GetForUserGroupAsync(alias, tree)`: the group's own `StartContentId` / `StartElementId`; the All
  Users group returns *not applicable*.
- `Classify(snapshot, nodePath)`: `Accessible` = `ContentPermissions.HasPathAccess` with the tree's
  recycle bin; `PathOnly` = strict comma-bounded ancestor of an effective start node; else
  `NotVisible`.

The per-tree differences live in one place, a `StartNodeTreeDefinition` record (calculation,
group field, user field, object type, recycle bin id). `StartNodeRules` stays pure and takes the
recycle bin id as a parameter instead of the hard-coded `-20`. The snapshot records its tree.

### API (additive)

- `/effective`, `/effective/by-role`, `doc-type-permissions/audit-for-node`: gain
  `startNodeAccess`, as in v17 (content tree).
- `element/effective`, `element/effective/by-role`: gain `startNodeAccess`, Library tree. The
  virtual-root key classifies the Library root (`-1`).
- `doc-type-permissions/element-types/audit`: gains `startNodeAccess`, subject-wide: `NotVisible`
  when the subject has no Library access, otherwise `Accessible`.
- New `GET start-nodes?userKey=`, `start-nodes/by-role?roleAlias=`, `element/start-nodes?userKey=`,
  `element/start-nodes/by-role?roleAlias=`: the card data. Users-section gated, like the other
  viewer endpoints.
- `element/effective/current-user` (drives the element conditions) is not changed.

Enforcement, the resolvers, the stored data and the Abstractions contract are not touched.

### Live refresh

All four viewers already refetch on an `Access` server event. They now also reload the card. The
`Access` event is additionally routed on `UserGroupWithUsersSavedNotification` (defensive, see
above).

## Error handling

- Unknown user or user group on the start-node endpoints: 404.
- Unknown node: the existing 200-with-empty-list contract stays; `startNodeAccess` is `NotVisible`.
- Unknown subject on the effective and audit endpoints: keep today's behaviour and report
  `Accessible`; the viewer never shows a restriction it could not verify.
- A failed card request only shows the card's error state; the grid is unaffected, because each
  verdict arrives with its own effective or audit response.

## Help and localization

- New terms in all 28 locale files, including the Library variants.
- Help in English and Dutch: `access-viewer.md`, `insert-options-viewer.md`,
  `library-access-viewer.md`, `library-insert-viewer.md`, and a start-node section in
  `concepts.md` (how they combine, user-level start nodes add or replace, no admin bypass, Library
  start nodes and the upgrade default, picker and search exceptions).

## Testing

Test-first throughout.

- **Unit (Umbraco-free):** rules for every researched case, for both recycle bins: single group;
  overlapping groups; sibling groups; group root plus a user start node; group *Home* plus a user
  start node on another branch (adds access); user root; user root plus a deeper node; a group with
  no start node; trashed and deleted start nodes; ancestor vs a sibling whose id is a prefix
  (`1,23` vs `1,234`); no start nodes.
- **Service and controllers** (`Umbraco.Community.AdvancedPermissions.Tests`): both trees; the
  four start-node endpoints for user, user group, All Users and unknown subjects;
  `startNodeAccess` on the content and element effective responses and both audits; the extra
  live-refresh route.
- **Client:** Vitest for the display helpers and the loader; `npx tsc --noEmit`; `npm run build`.
- **Manual, v18 test site:** a user in two groups with different content and Library start nodes,
  plus a user-level start node; check the card, all three row states, the blocked dialog, both
  content viewers and both Library viewers, a user group without Library access, and a live
  refresh after changing a group start node in another tab.

## Out of scope

- Media start nodes.
- Changing enforcement, the resolvers or the element conditions.
