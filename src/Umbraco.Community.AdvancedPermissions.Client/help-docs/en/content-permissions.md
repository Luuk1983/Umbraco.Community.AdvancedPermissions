# Content Permissions Editor

Manage all of a user group's permission entries across the content tree in one place.

## What it's for

Decide exactly what each user group may do on each content node — and how far each rule reaches — without the all-or-nothing limitation of Umbraco's built-in permissions.

## How to use it

1. **Pick a user group** from the toolbar.
2. **Browse the content tree.** The Default permissions row at the top sets a baseline for all content; the tree shows which nodes have entries defined.
3. **Click a cell** (a node × permission) to open the scope dialog.
4. **Choose the state** for this node and, if it should differ, a separate state for descendants. Optionally flag Priority Override.
5. **Save** your changes.

See the **Concepts** tab for Allow/Deny, scope, the Default permissions row, the All Users Group, and Priority Override.

## Examples

**Lock a node against deletion.** Add one entry: All Users Group, Deny, Delete, scope "This Node Only". The Deny wins for everyone, whatever groups they belong to.

**Carve out an exception to a broad Allow.** Editors have Allow for Publish on Home with scope "This Node and Descendants". To stop publishing under the Press Releases branch while keeping every other permission, add a single Deny for Publish on Press Releases.
