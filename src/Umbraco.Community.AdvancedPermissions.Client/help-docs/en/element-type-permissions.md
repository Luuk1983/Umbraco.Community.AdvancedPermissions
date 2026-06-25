# Library Element Type Permissions

Control which element types a user group may create in the Library.

## What it's for

Element types are the building blocks you can add in the Library. By default every element type is creatable. This editor lets you, per user group, hide an element type from the Library (**Deny**) or keep one explicitly (**Allow**).

It is a **filter, not a grant**, so it works differently from the tree-based verbs: it only ever narrows the element types Umbraco already offers in the Library. A Deny hides a type; an Allow never makes a type appear that isn't a valid Library element type — it only keeps one creatable and, with Priority Override, lets that choice win over a Deny coming from another of the user's groups.

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
