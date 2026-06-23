# Document Type Permissions Editor

Decide which document types a user group is allowed to create, and where in the content tree.

## What it's for

Umbraco lets you set the allowed child types of a document type, but that choice is the same for everyone. This editor goes further: it controls *per user group* whether a document type can be created under a node. So you can let one group add "News Article" pages under a section while another group can't — without changing the document type itself.

Creating is allowed by default (wherever Umbraco's own allowed-child-types permit it). You use this editor to add **Allow** or **Deny** rules that narrow or widen that for a specific group.

## How to use it

1. **Pick a user group** and **pick a document type** from the toolbar. Both are required before the table loads.
2. **Browse the content tree.** The Default permissions row at the top sets a baseline for the whole site; the rows below are your content nodes.
3. **Click a cell** in the Insert column to open the scope dialog.
4. **Choose Allow, Deny, or Not set** for this node — and, if descendants should differ, set a separate state for them. Optionally flag Priority Override.
5. **Save** your changes.

## Good to know

- A dimmed or N/A cell means the document type isn't an allowed child type on that node in the first place. The rule still resolves, but Umbraco wouldn't offer the type there.
- Because creating is allowed by default, you usually add **Deny** to lock specific types down, or **Allow** to re-enable a type that a broader Deny removed.

See the **Concepts** tab for Allow/Deny, scope, the Default permissions row, the All Users Group, and Priority Override.
