# Library Insert Viewer

Check which element types a user or user group can create in the Library — and why.

## What it's for

This is the read-only companion to the Library Element Type Permissions editor. Pick a subject and it lists every element type with whether that subject can create it in the Library, resolved across all their groups. Click a row to see the reasoning.

## How to use it

1. **Pick a user or a user group** from the toolbar.
2. You'll see a list of all element types, each showing whether it's allowed or denied for the chosen subject.
3. **Click a row** to open its reasoning: which group contributed the deciding rule and whether it was set explicitly or inherited.

## Good to know

- Creating element types is allowed by default, so a type with no rules anywhere shows as allowed.
- This decision is section-wide, not per node — so there's no tree here, just the flat list of element types.

See the **Concepts** tab for how permissions are resolved.
