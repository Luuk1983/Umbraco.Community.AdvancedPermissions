# Insert Options Viewer

Check whether a user or user group can create a given document type at each point in the content tree — and why.

## What it's for

This is the read-only companion to the Document Type Permissions Editor. Pick a subject and a document type, and it shows — node by node — whether that type can be created there, fully resolved across every group the user belongs to. Click any result to see the reasoning behind it.

Use it to answer "why can this editor add a News Article here but not there?" without changing anything.

## How to use it

1. **Pick a user or a user group**, and **pick a document type**, from the toolbar.
2. **Browse the content tree.** Each cell shows whether the chosen type can be created at that node (allowed or denied).
3. **Click a cell** to open its reasoning: which group contributed the deciding rule, from which node, and whether it was set explicitly or inherited.

## Good to know

- A dimmed or N/A cell means the document type isn't an allowed child type on that node, so Umbraco wouldn't offer it there even though a rule may still resolve.
- Creating is allowed by default, so a node with no rules anywhere shows as allowed.

See the **Concepts** tab for how permissions are resolved.
