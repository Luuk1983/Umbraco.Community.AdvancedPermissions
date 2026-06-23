# Library Permissions Editor

Manage Allow/Deny permissions per user group across the Library tree.

## What it's for

The Library holds reusable items (folders and elements) that live outside the main content tree. This editor controls, per user group, what each group may do to those items — Read, Create, Update, Delete, Publish, Unpublish, Duplicate, Move and Rollback — node by node.

It works exactly like the content Permissions Editor: the same Allow/Deny states, the same inheritance down the tree, and the same scope options.

## How to use it

1. **Pick a user group** from the toolbar.
2. **Browse the Library tree.** The Default permissions row at the top sets a baseline for everything; the rows below are your folders and items.
3. **Click a cell** (an item × permission) to open the scope dialog.
4. **Choose Allow, Deny, or Not set** for this item — and a separate state for the items inside it if they should differ. Optionally flag Priority Override.
5. **Save** your changes.

## Good to know

- A hatched **N/A** cell means the permission doesn't apply to that kind of item. "Create" has no meaning on a single item (it can't contain other items), and item-only actions (Publish, Unpublish, Duplicate, Rollback) don't apply to a folder itself — only to the items inside it.

See the **Concepts** tab for Allow/Deny, scope, the Default permissions row, the All Users Group, and Priority Override.
