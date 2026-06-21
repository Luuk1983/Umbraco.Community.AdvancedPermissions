# In-app Help System — Design

- **Date:** 2026-06-21
- **Status:** Approved (pending spec review)
- **Author:** Luuk Peters (with Claude)
- **Scope of first pass:** Reusable help framework + pilot on two surfaces (Content Permissions Editor, Access Viewer)

## Problem

The package ships eight backoffice editor/viewer surfaces (Content Permissions Editor, Document Type Permissions Editor, Library Permissions Editor, Library Element Type Permissions, Access Viewer, Insert Options Viewer, Library Access Viewer, Library Insert Viewer). They are powerful but dense, and today **none of them carry any explanatory text**. New users face a wall of options with no in-context guidance.

Three kinds of confusion exist, in priority order for this work:

- **B — "What does *this* page do and how do I use it?"** (high priority)
- **C — "What do these *concepts* mean?"** — Allow/Deny, inherit, scope, the All Users Group, priority override (high priority; cross-cutting)
- **A — "Which editor do I even open?"** (lower priority; not over-invested in)

The concepts (C) are the same across surfaces, so once the framework and the concepts reference exist, extending help to the remaining surfaces is mostly content authoring.

## Goals

- Every editor/viewer shows a short, always-visible description of what it is for.
- A details modal offers per-page how-to plus a shared concepts reference.
- Concepts are also explained **inline**, exactly where the confusion happens, via small ⓘ icons that can deep-link into the concepts reference.
- Long-form help content doubles as **GitHub-rendered documentation** that can be linked from the README, issues, and support replies — a single source of truth so docs and in-app help cannot drift apart.
- Built as a long-run, maintained feature.

## Non-goals

- No backend changes. Help content is static and bundled/loaded client-side; no new API endpoints.
- No orientation/landing page across surfaces in this pass (confusion A is deferred).
- No new JS test runner introduced solely for this feature.
- No rework of the existing editors/viewers beyond mounting the help components.

## Decisions

1. **Weight B and C; keep A light.**
2. **Concepts delivered both ways:** inline ⓘ icons *and* a centralized concepts reference in the modal (the ⓘ can deep-link into it).
3. **Content/localization is hybrid:**
   - **Short strings** (always-visible descriptions, inline tooltips) → `uap_help_*` localization keys, authored in `en` + `nl`, other ~28 locales auto-fall back to English.
   - **Long-form bodies** (per-page how-to, shared concepts) → **Markdown** files, authored in `en` + `nl`, rendered in the modal.
4. **Long-form content is Markdown** (not HTML partials) so the same files serve as GitHub documentation.
5. **First pass = framework + two pilot surfaces** (Content Permissions Editor, Access Viewer); the remaining six follow as mostly-content changes.

## Architecture — components

All new code under `src/Umbraco.Community.AdvancedPermissions.Client/src/help/`, `uap-` prefixed to match convention.

### `uap-page-help` — always-visible description bar
A slim element rendering a one-line description plus a "Learn more" button. Mounts at the top of each workspace, just inside `umb-body-layout`, above the existing `.toolbar`. Takes a `helpKey` property identifying the surface; pulls its short description from a localization key. Compact (single line, muted text) so it does not eat vertical space on the dense permission tables.

### `uap-help-modal` (+ token + manifest)
Registered as a `modal` manifest exactly like the existing `UAP.Modal.RolePicker` / `UAP.Modal.UserPicker`. Two sections:
- **"About this page"** — the per-page how-to for the surface that opened it.
- **"Concepts"** — the shared concepts reference.

Modal data accepts: which page opened it (selects the how-to doc) and an optional concept id to scroll to (so an inline ⓘ can deep-link).

### `uap-info-icon` — inline ⓘ
A tiny clickable icon showing a short tooltip/popover (from a localization key) and, when the concept has more depth, a "Learn more" link that opens `uap-help-modal` jumped to that concept. Sprinkled onto the highest-confusion spots.

### Content loader + registry (`help-content.ts`)
Maps each surface (keyed by its workspace `entityType`, e.g. `uap-permissions-editor`) to `{ descriptionKey, howToDoc }`, plus a single shared `conceptsDoc`. Long-form bodies are loaded **on demand** when the modal opens (not bundled into initial load). Inline ⓘ icons reference a concept id the modal can scroll to.

### Markdown rendering
The modal must render Markdown. First choice: reuse a renderer already exposed by the backoffice (`marked` is used internally by Umbraco's markdown editor and may be importable as an external). Fallback: a tiny bundled renderer. Content is first-party, so trust/sanitization is acceptable. **To be confirmed against the v18 reference source during implementation.**

## Content model

### File layout (in-project, dual-purpose)
```
src/Umbraco.Community.AdvancedPermissions.Client/help-docs/
  en/
    concepts.md
    content-permissions.md
    access-viewer.md
  nl/
    concepts.md
    content-permissions.md
    access-viewer.md
```
Kept inside the client project so Vite imports cleanly (`?raw`, on demand). They remain ordinary `.md` files GitHub renders, so the README links straight to them.

### Terminology (mirror the README)
- **"All Users Group"** for the `$everyone` role (not `$everyone` in user-facing text).
- **"Default permissions row"** for the virtual root baseline.

### `concepts.md` outline (distilled from the README)
- Allow vs Deny vs *inherit* (absence of an entry).
- Override one permission, inherit the rest.
- Scope: This Node Only / This Node and Descendants / Descendants Only.
- Inheritance from the nearest ancestor and the Default permissions row.
- The All Users Group baseline and how explicit groups override it.
- Priority Override (the gold badge): what it is, when it wins, use sparingly.
- Resolution order: explicit Deny > explicit Allow > inherited Deny > inherited Allow; default Deny for content (Allow for insert options).

### Pilot how-to docs
- `content-permissions.md`: what the page is for; workflow (pick group, browse tree, set Allow/Deny/inherit per node+verb, Default row, scope dialog, priority override checkbox); worked examples (lock a node against deletion; carve an exception to a broad Allow). Cross-links into `concepts.md`.
- `access-viewer.md`: what the page is for; workflow (pick user or group, choose node, read allowed/denied, click a cell for the reasoning chain, priority-override visibility). Cross-links into `concepts.md`.

### Short strings (localization keys, `en` + `nl`)
- Per-page description, e.g. `uap_help_contentPermissions_description`, `uap_help_accessViewer_description`.
- Inline tooltip texts, e.g. `uap_help_concept_scope_tip`, `uap_help_concept_priorityOverride_tip`, `uap_help_concept_allowDeny_tip`, etc.

### Inline ⓘ placement (pilot)
- **Content Permissions Editor:** the scope dialog options (biggest confusion), the Allow/Deny/inherit cell legend, the priority-override indicator, the Default permissions row, the All Users Group.
- **Access Viewer:** the role-vs-user subject choice, the reasoning indicators, the priority-override indicator in the reasoning dialog.

(Exact final set finalized during implementation; the framework makes adding/removing icons cheap.)

## Rollout / build order

1. **Framework** — `uap-page-help`, `uap-help-modal` (+ token + manifest), `uap-info-icon`, `help-content.ts`, markdown loader/renderer, new `uap_help_*` keys in `en` + `nl`.
2. **`concepts.md`** (en + nl) — distilled from the README.
3. **Pilot wiring** — description bar + modal + inline ⓘ on Content Permissions Editor and Access Viewer; write both how-to docs.
4. **README** — add a "Documentation" section linking the rendered docs.

The remaining six surfaces are a follow-up: each is a description string, a how-to doc, and a small set of ⓘ icons — a repeatable, low-risk fill-in.

## GitHub issue & branch

- Create a GitHub issue describing the feature, the pilot scope, the decisions, and a checklist. Confirm the repo `origin` via `gh` first.
- Create a new local branch off `main`, e.g. `feature/in-app-help-system`.
- These are the first two steps of the implementation plan.

## Verification

Frontend-only feature, so:
- `npx tsc --noEmit` type-check.
- `npm run build`.
- Manual verification on the TestSite: open both pilot pages; confirm the description bar, modal sections, Markdown rendering, and ⓘ deep-links work.
- No backend tests (no backend change). No new JS test runner unless the markdown renderer turns out complex enough to warrant a focused unit test.

## Maintenance

Help content is a maintained part of the codebase. After any change to a surface's behavior, layout, options, or the permission concepts, re-check and update that surface's help: the description string, inline ⓘ tooltips, per-page how-to doc, and the shared concepts doc — in both `en` and `nl`. The README is a third place to keep aligned (same terminology, same concepts). Recorded as a persistent memory (`help-content-maintenance`).

## Risks / open items

- **Markdown renderer availability** in the v18 backoffice externals — confirm during implementation; bundle a tiny fallback if needed.
- **Vite `?raw` on-demand import** of the markdown docs — confirm the chunking works as expected so docs are not pulled into initial load.
- **README drift** — the README documents only the 4 original surfaces; the 4 Library/element surfaces are undocumented there. Out of scope for the pilot, but worth a cleanup when rolling out the rest.
