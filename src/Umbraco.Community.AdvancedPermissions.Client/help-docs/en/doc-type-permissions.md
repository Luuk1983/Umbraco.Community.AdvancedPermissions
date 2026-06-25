# Document Type Permissions Editor

Decide which document types a user group is allowed to create, and where in the content tree.

## What it's for

Umbraco lets you set the allowed child types of a document type, but that choice is the same for everyone. This editor goes further: it controls *per user group* whether a document type can be created under a node. So you can let one group add "News Article" pages under a section while another group can't — without changing the document type itself.

Creating is allowed by default (wherever Umbraco's own allowed-child-types permit it). This editor is a **filter, not a grant**, so it works differently from the tree-based verbs: it only ever narrows the document types Umbraco already offers. You add a **Deny** to take a type away for a group, or an **Allow** to keep it — but an Allow never makes a type appear that Umbraco doesn't already permit as a child there.

## How to use it

1. **Pick a user group** and **pick a document type** from the toolbar. Both are required before the table loads.
2. **Browse the content tree.** The Default permissions row at the top sets a baseline for the whole site; the rows below are your content nodes.
3. **Click a cell** in the Insert column to open the scope dialog.
4. **Choose Allow, Deny, or Not set** for this node — and, if descendants should differ, set a separate state for them. Optionally flag Priority Override.
5. **Save** your changes.

## Good to know

- A dimmed or N/A cell means the document type isn't an allowed child type on that node in the first place. The rule still resolves, but Umbraco wouldn't offer the type there.
- Because creating is allowed by default, you usually add **Deny** to lock specific types down. An **Allow** only ever keeps (or, with Priority Override, wins back) a type Umbraco already allows there — it can't add a type that isn't a valid child in the first place.

See the **Concepts** tab for Allow/Deny, scope, the Default permissions row, the All Users Group, and Priority Override.
