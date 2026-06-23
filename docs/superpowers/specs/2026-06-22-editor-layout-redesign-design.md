# Editor & Viewer Layout Redesign — Design

- **Date:** 2026-06-22
- **Status:** Approved (pending spec review)
- **Branch:** `feature/in-app-help-system` (continues the in-app help work)
- **Related spec:** `docs/superpowers/specs/2026-06-21-in-app-help-system-design.md`

## Problem

The in-app help work added a grey description band (`uap-page-help`) and, combined with the pre-existing `.toolbar`, the editor/viewer pages now read as a stack of bolted-on bands floating in the content area rather than one coherent page. Specifically:

- The description sits in a full-width grey strip that doesn't feel part of the page.
- The picker `.toolbar` is a custom-styled band that "never felt in place".
- A concept ⓘ icon sits awkwardly next to the picker.
- On wide screens the primary selector is easy to miss.

The root cause is structural: `umb-body-layout` provides a native header bar and a padded main area meant to hold `uui-box` cards, but the package dumps custom bands as raw children of the main area instead.

A hard constraint from the maintainer: **use native Umbraco components wherever possible and avoid custom styling.** Umbraco 18 changed control styling (e.g. more rounded corners); custom-styled chrome drifts out of sync on upgrades. Custom styling is only acceptable where genuinely unavoidable, and must be token-based.

## Goals

- Make every editor/viewer read as one coherent, professional, Umbraco-native page.
- Compose from native components (`umb-body-layout`, `uui-box`, `uui-button`) so future token/style changes flow in automatically.
- Keep a one-sentence description visible on every surface.
- Make the primary selector impossible to miss, at any screen width.
- Handle surfaces with one picker, multiple required pickers, and mutually-exclusive pickers consistently.

## Non-goals

- No change to the permission **table** itself or the `uap-perm-block` cells (out of scope; not what felt wrong).
- No change to the help **modal** or the scope-dialog tooltip ⓘ icons (those stay).
- No backend changes.
- Not introducing a JS test runner.

## Design decisions

1. **Native header.** Keep `umb-body-layout`'s native `headline` (the page title). No custom header chrome.
2. **Description as body intro copy.** A plain muted `<p>` (token colour `--uui-color-text-alt`) at the top of the main area — the way Umbraco's own dashboards open — not a band or a header subtitle. On the two pilot surfaces only, a "Learn more" link is appended that opens the help modal.
3. **One `uui-box` holds selection + results.** The picker(s) and the results table live together in a single native `uui-box`, with two states:
   - **Incomplete** (not all required selections made): the box body shows a **centered call-to-action** — an icon, a prompt sentence, and the needed picker button(s). Mutually-exclusive options are joined with "or". Already-chosen selections in a multi-picker surface render as pills inside the CTA; the prompt asks only for what's missing.
   - **Complete** (all required selections made): the selection(s) render as **label-less pills** (picker icon + chosen name + a chevron to re-open the picker) at the **top-left** of the box body, with the results table below.
4. **No labels on selected pills.** The picker icon (users / user / document) is self-describing, so a separate "User group" label is redundant and is dropped. This is also what makes multiple/mutually-exclusive pickers read cleanly.
5. **Remove the stray toolbar ⓘ** (the allow-deny concept icon on the Content Permissions Editor and the reasoning icon on the Access Viewer). Concept help now lives behind "Learn more" / the modal.
6. **Scope: all eight surfaces** adopt this layout. The "Learn more" link and how-to docs stay pilot-only (Content Permissions Editor, Access Viewer); the other six show the description with no link.

## Native components used

| Concern | Native component | Custom styling |
|---|---|---|
| Page header / title | `umb-body-layout` `headline` | none |
| Description intro | plain `<p>` | colour + margin (tokens) only |
| Card wrapping selection + results | `uui-box` | none |
| Picker buttons / selected pills | `uui-button` (`look="placeholder"` / `look="outline"`) + `uui-icon`/`umb-icon` | none (drop the old `min-width`) |
| "Learn more" link | `uui-button look="default" compact` (or anchor with `--uui-color-interactive`) | none |
| Centered CTA layout | div with flex | token-based flex/spacing only |

No custom toolbar bar, no custom band, no hand-built header subtitle. The only custom CSS is token-based layout (the intro `<p>`, the centered CTA's flex column, the pills row's flex) — none of it restyles a native control, so v18's rounded corners and future token changes apply automatically.

## Component architecture

### `uap-page-intro` (new, replaces the `uap-page-help` band)
Renders the description `<p>` plus, when the surface has a how-to doc, a trailing "Learn more" link that opens `UAP_HELP_MODAL`. Looks up `descriptionKey` + `howToDoc` from the help registry by surface `entityType`. Mounts as the first child of the `umb-body-layout` main area.

### `uap-selection-panel` (new, shared by all eight surfaces)
Encapsulates the incomplete-vs-complete behaviour so it isn't duplicated eight times. Presentational + delegating:

```ts
/** One selectable thing (a picker). */
interface UapSelector {
  id: string;            // 'group' | 'user' | 'docType' | 'elementType' | ...
  label: string;         // localized; used in "Choose {label}" and aria-label
  icon: string;          // umb icon, e.g. 'icon-users'
  selectedName?: string; // set when chosen
}
/** A required selection slot. One option = a single picker; 2+ options = mutually exclusive. */
interface UapSelectorGroup {
  options: UapSelector[];
}
```

- **Props:** `groups: UapSelectorGroup[]`, `promptText: string`, `ctaIcon: string`.
- **Derived:** `complete` = every group has one option with a `selectedName`.
- **Event:** dispatches `uap-selector-click` with `{ id }` when a picker button/pill is activated; the host surface opens the matching modal, updates its state, and passes refreshed `groups` back.
- **Slot:** `default` — the results (table); rendered only when `complete`.
- **Render:**
  - Incomplete → centered CTA: `ctaIcon` + `promptText` + each group's controls (selected options as pills, unselected as `Choose {label}` buttons; mutually-exclusive options separated by a localized "or").
  - Complete → a top-left pills row (one pill per group: icon + `selectedName` + chevron) followed by the slotted results.

Loading and error states remain owned by each surface, rendered in place of the slotted results.

### Per-surface refactor
Each surface element:
- Replaces its `umb-body-layout headline=… ` + grey `uap-page-help` + `.toolbar` markup with: `umb-body-layout headline=…` → `uap-page-intro` → `uap-selection-panel` (wrapping its existing results table in the panel's slot).
- Builds its `groups` descriptors from its existing selection state and forwards `uap-selector-click` to its existing picker-modal logic.
- Drops the custom `.toolbar` styles, the `uap-picker-button` `min-width`, and (pilots) the stray `uap-info-icon` in the toolbar.

### Per-surface selector configuration
Inferred from the surfaces; **verify each against its current element during implementation**:

| Surface (entityType) | Selection groups |
|---|---|
| Content Permissions Editor (`uap-permissions-editor`) | [ group ] |
| Access Viewer (`uap-access-viewer`) | [ group **or** user ] |
| Document Type Permissions Editor (`uap-doc-type-permissions`) | [ group ], [ document type ] |
| Insert Options Viewer (`uap-doc-type-create-audit`) | [ group **or** user ], [ document type ] |
| Library Permissions Editor (`uap-library-permissions`) | [ group ] |
| Library Element Type Permissions (`uap-element-type-permissions`) | [ group ] — element type is a per-row cell, not a toolbar picker |
| Library Access Viewer (`uap-library-access-viewer`) | [ group **or** user ] |
| Library Insert Viewer (`uap-library-insert-viewer`) | [ group **or** user ] |

*(Verified against each element on 2026-06-22.)*

## Registry & localization

- `SURFACE_HELP` extends to **all eight** surfaces: every entry has a `descriptionKey`; `howToDoc` stays only on the two pilots (drives the "Learn more" link). `howToDoc` becomes optional.
- New `uap_*` localization keys (en + nl): a one-sentence description per surface (six new), a CTA prompt per surface, the "or" separator, "Learn more" (exists), and aria labels for the pills/pickers (e.g. "Change user group").
- Picker button labels reuse existing per-surface strings where present.

## Verification

Frontend-only. Per task: `npx tsc --noEmit` + `npm run build`. Final: manual check on the TestSite (now much easier — the layout is exactly what changed): each surface shows the native header, intro copy (Learn more on pilots), the centered CTA when empty, and label-less pills + table once selected; mutually-exclusive and two-required surfaces behave per the table above. Run the TestSite with `-p:NuGetAudit=false` (pre-existing audit blocker, tracked separately).

## Risks / open items

- **Per-surface selector config** is inferred above; the implementer must confirm each surface's actual pickers (some Library surfaces may differ) when refactoring.
- **`uap-selection-panel` event flow** must preserve each surface's existing reload-on-change behaviour (re-selecting a group reloads the table). The refactor must not regress that.
- **`uui-box` empty header:** when a surface has no box headline, ensure the box renders cleanly without an empty header bar (the selection lives in the body, not `header-actions`).
- The redesign rides on the same branch as the help system; both ship together.
