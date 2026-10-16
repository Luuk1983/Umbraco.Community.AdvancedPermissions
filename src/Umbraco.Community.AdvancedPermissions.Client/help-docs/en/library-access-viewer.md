# Library Access Viewer

See the effective Library permissions a user or user group has at each item — with full reasoning.

## What it's for

This is the read-only companion to the Library Permissions Editor. It shows the fully resolved permission for any user or user group on every library item, combined across all the groups they belong to, and explains how each result was reached.

## How to use it

1. **Pick a user or a user group** from the toolbar.
2. **Browse the Library tree.** Each cell shows whether a permission is allowed or denied for the chosen subject.
3. **Click a cell** to open its reasoning: which group contributed, from which item, and whether the rule was set explicitly or inherited.

## Good to know

- A hatched **N/A** cell means the permission doesn't apply to that kind of item — for example "Create" on a single item, or item-only actions (Publish, Unpublish, Duplicate, Rollback) on a folder.

## Library start nodes

The viewer also takes the subject's **Library start nodes** into account, because Umbraco blocks every Library folder and item outside them before any permission is checked.

- The **start-node card** under the picker says whether Library start nodes limit the subject, and lists the Library start nodes in effect with who set each one.
- A folder or item the subject can't reach shows **blocked** cells (grey, hatched, with a lock) instead of allow or deny: **Browse only (start nodes)** for a folder above a start node, **Not visible (start nodes)** for everything outside them. Permissions that never apply to a folder or an item stay N/A.
- Click a blocked cell to see why, and what the permissions would be if it were inside a start node.

See the **Concepts** tab for how permissions are resolved.
