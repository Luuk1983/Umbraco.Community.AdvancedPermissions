# ADR-011: Keep custom user / user-group picker modals (restyled) instead of switching to Umbraco v18 native pickers

**Status**: Accepted
**Date**: 2026-05-31

## Context

The Access Viewer and the Document-Type Create Audit workspaces both let an admin pick a
**single user group ("role")** or a **single user** as the subject whose effective permissions
are shown. These are implemented as two custom sidebar modals:

- `access-viewer/role-picker-modal.element.ts` (token `UAP.Modal.RolePicker`)
- `access-viewer/user-picker-modal.element.ts` (token `UAP.Modal.UserPicker`)

opened from a shared `components/uap-picker-button.element.ts`.

After the upgrade to **Umbraco 18.0.0-rc1**, these modals rendered with an unwanted
**horizontal scrollbar** along the bottom, and their styling had drifted from v18's look. The
modals had been built with a custom `uui-table` + a custom sticky `#filter-bar` and hard-coded
sizes/colours (`36px` avatars, `#3544b1`, a `53px` sticky offset, `color-mix(...)`). That layout
no longer fit v18's UUI defaults.

This prompted the question: should we drop the custom modals in favour of Umbraco v18's **native**
user / user-group pickers to reduce custom code?

## What v18 ships natively

| Concern | Native API (v18) |
|---------|------------------|
| User input | `umb-user-input` (`@umbraco-cms/backoffice/user`), single-select via `max="1"` |
| User picker modal | `UMB_USER_PICKER_MODAL` → generic `Umb.Modal.CollectionItemPicker`, supports `pickableFilter` |
| User item lookup | `UmbUserItemRepository` (`Umb.Repository.User.Item`) → `{ unique, name, avatarUrls, kind, entityType }` |
| User-group input | `umb-user-group-input` (`@umbraco-cms/backoffice/user-group`), single-select via `max="1"` |
| User-group picker modal | `UMB_USER_GROUP_PICKER_MODAL` → custom `UmbUserGroupPickerModalElement` |
| User-group item lookup | `UmbUserGroupItemRepository` (`Umb.Repository.UserGroupItem`) → `{ unique, name, icon }` |

## Decision

**Keep both modals custom, but rebuild their layout to mirror v18's native picker modal**
(`umb-body-layout` → `uui-box` → a full-width `uui-input` search field + a `repeat()` of
`uui-ref-node` rows). Do **not** switch to the native pickers.

### Why not native

1. **The role picker must offer the virtual "All Users Group" (`$everyone`).** This is a synthetic
   entry returned by our `/roles` endpoint, **not** a real Umbraco user group. The native
   user-group picker (`UmbUserGroupPickerModalElement`) builds its list **only** from
   `UmbUserGroupCollectionRepository` and exposes **no** mechanism to inject, prepend, or supply a
   custom data source. So a native group picker physically cannot include `$everyone`. A hybrid
   (native picker + a separate "$everyone" toggle) was rejected as worse UX and *more* code, not
   less. See [[critical_gotchas]] and the permission model in [[project_overview]].

2. **Consistency of the matched subject pair.** The two pickers are presented side by side
   ("choose a user group **or** a user") behind identical `uap-picker-button`s. Since the role
   picker is forced to stay custom (point 1), making *only* the user picker native would split the
   pair into two different interaction models (native's select-then-"Choose" vs. our
   click-to-select-and-close). Keeping both custom keeps the pair symmetric.

3. **Our API already returns the display shape we need.** `getRoles()`/`getUsers()` return
   `{ alias|unique, name, avatarUrls, isEveryone }` directly. Switching to native would mean taking
   a bare `selection[0]` GUID and re-resolving the name/avatar/icon through
   `UmbUserItemRepository` / `UmbUserGroupItemRepository` — adding wiring rather than removing it.

4. **Better single-select UX.** Our modals submit immediately on row click; the native pickers
   require a second "Choose" click.

### What changed in the rebuild (fixes the scrollbar + drift)

- Replaced the custom `uui-table` + sticky `#filter-bar` with the native pattern: a single
  `uui-box` containing a `width:100%` `uui-input type="search"` (with a `search` icon prepend and
  `umbFocus()`) and a `repeat()` of `uui-ref-node` rows. This is the exact structure of
  `UmbUserGroupPickerModalElement`, so the horizontal-overflow simply does not occur.
- Rows use `uui-ref-node selectable select-only`. Because `deselectable` defaults `true`, clicking
  the already-selected row fires `deselected` (not `selected`), so **both** `@selected` and
  `@deselected` are wired to the same "pick this one + submit" handler — matching how Umbraco's own
  ref-based pickers handle the events.
- User rows show a `uui-avatar` (`name` + `img-src`); role rows show a `umb-icon`
  (`icon-globe` for All Users, `icon-users` otherwise).
- All hard-coded sizes/colours were replaced with design tokens (`--uui-size-space-*`,
  `--uui-size-*`, `--uui-color-border`, `--uui-color-text-alt`). Rounded styling is inherited from
  UUI automatically.
- **Defensive `uui-ref-node { min-width: 0 }`**: `UUIRefNodeElement` ships a `min-width: 250px`
  that would re-introduce a horizontal scrollbar inside the narrow sidebar; relaxing it to `0`
  prevents that. (The remaining ~2px per-row `scrollWidth` is the absolutely-positioned
  `#select-border` selection glow and is clipped by `uui-box` — it never reaches the scroll
  container.)

## Verification

Verified in the TestSite over HTTPS (Umbraco 18.0.0-rc1, unattended v17→v18 upgrade applied):
both modals open with **no horizontal scrollbar** (`umb-body-layout` / `#main` / `uui-box` /
`uui-input` all report `scrollWidth === clientWidth`), render with native rounded styling, the
**"All Users" entry is present and selectable** in the role picker, and selecting a row closes the
modal and updates the picker button.

## Consequences

- Slightly more custom code than a pure-native approach, but it is the only way to keep `$everyone`
  selectable and the subject pair consistent.
- The modals now track v18's native picker structure, so future UUI styling changes are inherited
  rather than re-drifting.
- If Umbraco later exposes a custom-data-source extension point on the user-group picker, revisit
  point 1 — the role picker could then become native with `$everyone` prepended.
