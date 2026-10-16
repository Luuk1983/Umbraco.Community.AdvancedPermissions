# Access Viewer

Answer the question "what can this user or group actually do here?" — with a full explanation.

## What it's for

The Access Viewer shows the **effective** (fully resolved) permission for any user or user group at any content node, and explains exactly how each result was reached.

## How to use it

1. **Pick a user or a user group** from the toolbar.
2. **Browse the content tree.** Each cell shows whether a permission is allowed or denied for the chosen subject.
3. **Click a cell** to open its reasoning chain: which group contributed, from which node, and whether it was explicit or inherited.
4. When a **Priority Override** changed the outcome, the reasoning shows what the result would have been without it.

## Start nodes

The viewer also takes the subject's **start nodes** into account, because Umbraco blocks every node outside them before any permission is checked.

- The **start-node card** under the picker says whether start nodes limit the subject, and lists the start nodes in effect with who set each one: a user group, or the user's own account. Configured start nodes that change nothing (covered by a higher one, replaced, or deleted) are not listed.
- A node the subject can't reach shows **blocked** cells (grey, hatched, with a lock) instead of allow or deny:
  - **Browse only (start nodes)**: an ancestor of a start node. Umbraco shows it in the tree so the user can browse down to their start node, but it can't be opened.
  - **Not visible (start nodes)**: outside every start node, so hidden.
- Click a blocked cell to see why, and what the permissions would be if the node were inside a start node.
- With a user group selected, the viewer uses that group's own start node. A user gets the combined start nodes of all their groups, so pick a user to see the real result.

See the **Concepts** tab for how permissions are resolved.
