# Permission concepts

Advanced Permissions builds on Umbraco's own permission model and adds finer-grained control. This page explains the core concepts shared by every editor and viewer.

<a id="allow-deny"></a>
## Allow, Deny and inherit

Every permission entry is either **Allow** or **Deny**. There is no separate "inherit" value: a permission you leave **unset** simply has no entry, and the node inherits whatever applies from its nearest ancestor (or the Default permissions row).

- **Allow** grants the permission.
- **Deny** actively revokes it — including a permission inherited from an ancestor.
- **Unset (inherit)** keeps whatever the node would otherwise inherit.

Unlike Umbraco's built-in permissions, each permission is independent: you can override just one on a node and let the rest keep inheriting.

<a id="scope"></a>
## Scope

Every entry carries a **scope** that controls how far it reaches:

- **This Node Only** — applies to the node itself, not its children.
- **This Node and Descendants** — applies to the node and everything beneath it.
- **Descendants Only** — applies to the children but not the node itself.

This lets you express patterns like "lock the branch root, but leave everything beneath it open" in a single entry.

<a id="default-row"></a>
## The Default permissions row

The **Default permissions row** at the top of the editors is a virtual root that sets a baseline for all content. Entries here apply everywhere unless a more specific entry overrides them.

<a id="all-users"></a>
## The All Users Group

The **All Users Group** is a baseline that reaches every backoffice user, whatever groups they belong to. Pair it with a Deny to lock a node down across the board with a single entry. Explicit group entries can still override it.

<a id="priority-override"></a>
## Priority Override

When a user belongs to several groups, those groups can disagree, and the normal order does not always land where you want. **Priority Override** is the deliberate escape hatch: flag the entry that should win, and it does — regardless of the user's other groups. Use it sparingly.

<a id="resolution"></a>
## How conflicts are resolved

When deciding whether a user has a permission on a node, the package gathers every applicable entry from every group the user belongs to (including the All Users Group), respects each entry's scope, and applies this order, highest first:

1. **Explicit Deny** overrides everything else.
2. **Explicit Allow** overrides any inherited permission.
3. **Inherited Deny** overrides an inherited Allow.
4. **Inherited Allow**.

If no entry applies anywhere up the tree, the default is **Deny** for content permissions (**Allow** for insert options). A Priority Override entry steps outside this order and wins.

<a id="start-nodes"></a>
## Start nodes

Start nodes decide which part of the content tree a user can reach at all. Umbraco checks them **before** any permission.

- **A user group** has one start node, root access, or none. A group without a start node gives **no** content access.
- **Several groups** combine: the user can reach every group's start node, and a higher start node covers a lower one on the same branch.
- **Start nodes and permissions are separate.** A permission from one group also applies inside a start node that another group provides.
- **A start node set on the user account** replaces any group start node above or below it on the same branch, even root access. On a branch that no group start node covers, it adds access.
- **Administrators** reach everything only because their group has root access. There is no other exception.
- **Deleted or trashed start nodes** are ignored. If a user's own start node is deleted, the user falls back to the group start nodes.
- Some pickers can be configured to ignore start nodes, and backoffice search applies them slightly differently. The viewers show the tree and editor rules.
