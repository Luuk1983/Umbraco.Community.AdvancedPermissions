# Library Element Type Permissions

Control which element types a user group may create in the Library.

## What it's for

Element types are the building blocks you can add in the Library. By default every element type is creatable. This editor lets you, per user group, hide an element type from the Library (**Deny**) or explicitly allow one (**Allow**).

An explicit Allow is useful when a user belongs to several groups: an Allow with Priority Override can win over a Deny coming from another group.

Unlike the tree-based editors, this choice is **section-wide** — it isn't tied to a particular node, because Umbraco doesn't provide a parent when you create an item in the Library.

## How to use it

1. **Pick a user group** from the toolbar.
2. You'll see a list of all element types with a single **Create in Library** column.
3. **Click a row's cell** to open the dialog.
4. **Choose Allow, Deny, or Not set.** "Not set" leaves the type creatable by default. Optionally flag Priority Override so this choice wins when the user is in several groups.
5. **Save** your changes.

## Good to know

- If the list is empty, no document types are marked as element types yet. Mark a document type as an element type and enable "Allow in Library" for it to appear here.

See the **Concepts** tab for Allow/Deny and Priority Override.
