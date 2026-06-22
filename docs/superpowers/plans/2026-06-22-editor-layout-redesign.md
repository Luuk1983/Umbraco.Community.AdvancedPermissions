# Editor & Viewer Layout Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the bolted-on grey description band + custom toolbar on all eight editor/viewer surfaces with a native-first layout: native `umb-body-layout` headline, a plain intro paragraph (Learn more on pilots), and a single `uui-box` that shows a centered call-to-action when the selection is incomplete and label-less selection pills + the results below when complete.

**Architecture:** Two new shared elements — `uap-page-intro` (description + optional Learn more) and `uap-selection-panel` (CTA-vs-pills container that slots each surface's results) — plus a registry/localization extension and a per-surface refactor that swaps each surface's header/toolbar markup for these components.

**Tech Stack:** TypeScript, Lit (`@umbraco-cms/backoffice/external/lit`), Umbraco backoffice (`uui-box`, `uui-button`, `umb-body-layout`, `umb-icon`), Vite.

**Spec:** `docs/superpowers/specs/2026-06-22-editor-layout-redesign-design.md`

**Verification approach:** Frontend-only, no JS test runner (per spec). Each task's gate is `npx tsc --noEmit` (+ `npm run build` once components are wired). All commands run from `src/Umbraco.Community.AdvancedPermissions.Client/`. Final task adds a manual TestSite pass (`dotnet run --project tests/Umbraco.Community.AdvancedPermissions.TestSite -p:NuGetAudit=false --launch-profile https`, then `https://localhost:7067/umbraco`, login `admin@example.test` / `test123456!`).

**Gotchas (CLAUDE.md):** `UmbExtensionManifest` is ambient (never import); `exactOptionalPropertyTypes: true` (never pass `undefined` to an optional field — use conditional spread); `consumeContext` → `ctx ?? undefined`; recursive Lit private methods need explicit return types; local imports use `.js`; `<umb-icon>` for `icon-*`.

**Native-first rule:** Compose from native components; the only custom CSS allowed is token-based layout (flex/spacing/colour via `--uui-*` vars). Do not restyle native controls.

---

## File Structure

**Create:**
- `src/help/uap-page-intro.element.ts` — description `<p>` + optional Learn more link.
- `src/help/uap-selection-panel.element.ts` — CTA-vs-pills container; emits `uap-selector-click`; slots results + actions.

**Modify:**
- `src/help/help-content.ts` — extend `SURFACE_HELP` to all 8 surfaces; make `howToDoc` optional.
- `src/localization/en.ts`, `src/localization/nl.ts` — 6 new description keys (+ any prompt keys).
- The 8 surface root elements (swap header/toolbar for the new components):
  - `src/permissions-editor/uap-permissions-editor-root.element.ts`
  - `src/access-viewer/uap-access-viewer-root.element.ts`
  - `src/doc-type-permissions/uap-doc-type-permissions-editor-root.element.ts`
  - `src/doc-type-permissions/uap-doc-type-create-audit-root.element.ts`
  - `src/library-permissions/uap-library-permissions-editor-root.element.ts`
  - `src/library-permissions/uap-element-type-permissions-editor-root.element.ts`
  - `src/library-permissions/uap-library-access-viewer-root.element.ts`
  - `src/library-permissions/uap-library-insert-viewer-root.element.ts`

**Delete:**
- `src/help/uap-page-help.element.ts` — replaced by `uap-page-intro` (only used by surfaces #1, #2 today).

---

## Task 1: Extend registry + localization

**Files:**
- Modify: `src/help/help-content.ts`
- Modify: `src/localization/en.ts`, `src/localization/nl.ts`

- [ ] **Step 1: Make `howToDoc` optional and add all 8 surfaces** in `src/help/help-content.ts`. Replace the `SurfaceHelp` interface and `SURFACE_HELP` const with:

```ts
/** Help configuration for a single editor/viewer surface. */
export interface SurfaceHelp {
  /** Localization key for the always-visible one-line description. */
  descriptionKey: string;
  /** The per-page how-to document shown in the modal's "About this page" section. Pilots only. */
  howToDoc?: HelpDocId;
}

/** Maps a workspace entityType to its help configuration. */
export const SURFACE_HELP: Record<string, SurfaceHelp> = {
  'uap-permissions-editor': {
    descriptionKey: 'uap_help_contentPermissions_description',
    howToDoc: 'content-permissions',
  },
  'uap-access-viewer': {
    descriptionKey: 'uap_help_accessViewer_description',
    howToDoc: 'access-viewer',
  },
  'uap-doc-type-permissions': { descriptionKey: 'uap_help_docTypePermissions_description' },
  'uap-doc-type-create-audit': { descriptionKey: 'uap_help_insertOptions_description' },
  'uap-library-permissions': { descriptionKey: 'uap_help_libraryPermissions_description' },
  'uap-element-type-permissions': { descriptionKey: 'uap_help_elementTypePermissions_description' },
  'uap-library-access-viewer': { descriptionKey: 'uap_help_libraryAccessViewer_description' },
  'uap-library-insert-viewer': { descriptionKey: 'uap_help_libraryInsertViewer_description' },
};
```

- [ ] **Step 2: Add the 6 new description keys to `src/localization/en.ts`.** Find the existing `help_accessViewer_description:` line and add immediately after it (inside the `uap:` object):

```ts
    help_docTypePermissions_description: 'Decide which document types each user group can create, and where.',
    help_insertOptions_description: 'Audit which document types a user or user group can create at each node.',
    help_libraryPermissions_description: 'Manage Allow/Deny permissions per user group across the Library tree.',
    help_elementTypePermissions_description: 'Control which element types each user group can use in the Library.',
    help_libraryAccessViewer_description: 'See the effective Library permissions a user or user group has at each node.',
    help_libraryInsertViewer_description: 'Audit which element types a user or user group can insert in the Library.',
```

- [ ] **Step 3: Add the matching Dutch keys to `src/localization/nl.ts`** after `help_accessViewer_description:`:

```ts
    help_docTypePermissions_description: 'Bepaal welke documenttypen elke gebruikersgroep mag aanmaken, en waar.',
    help_insertOptions_description: 'Controleer welke documenttypen een gebruiker of gebruikersgroep op elk knooppunt mag aanmaken.',
    help_libraryPermissions_description: 'Beheer Toestaan/Weigeren-machtigingen per gebruikersgroep over de Library-boom.',
    help_elementTypePermissions_description: 'Bepaal welke elementtypen elke gebruikersgroep in de Library mag gebruiken.',
    help_libraryAccessViewer_description: 'Bekijk de effectieve Library-machtigingen van een gebruiker of gebruikersgroep op elk knooppunt.',
    help_libraryInsertViewer_description: 'Controleer welke elementtypen een gebruiker of gebruikersgroep in de Library mag invoegen.',
```

> The four "Library" descriptions are best-effort; confirm the Library terminology with the maintainer (flagged in the spec).

- [ ] **Step 4: Type-check.** Run `npx tsc --noEmit`. Expected PASS.

- [ ] **Step 5: Commit.**
```bash
git add src/Umbraco.Community.AdvancedPermissions.Client/src/help/help-content.ts src/Umbraco.Community.AdvancedPermissions.Client/src/localization/en.ts src/Umbraco.Community.AdvancedPermissions.Client/src/localization/nl.ts
git commit -m "feat(layout): extend help registry to all surfaces + add descriptions (en, nl)"
```

---

## Task 2: `uap-page-intro` element

**Files:**
- Create: `src/help/uap-page-intro.element.ts`

- [ ] **Step 1: Create the element.**

```ts
import { html, css, nothing, customElement, property } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import { UMB_MODAL_MANAGER_CONTEXT } from '@umbraco-cms/backoffice/modal';
import { SURFACE_HELP } from './help-content.js';
import { UAP_HELP_MODAL } from './uap-help-modal.token.js';

/**
 * One-sentence page description rendered as native dashboard-style intro copy at the top of
 * an editor/viewer. When the surface has a how-to doc (pilot surfaces), a trailing "Learn more"
 * link opens the help modal.
 */
@customElement('uap-page-intro')
export class UapPageIntroElement extends UmbLitElement {
  #localize = new UmbLocalizationController(this);

  /** Workspace entityType identifying the surface (keys into SURFACE_HELP). */
  @property() surface = '';

  /** Headline passed to the help modal (the surface name). */
  @property() headline = '';

  #modalManager: typeof UMB_MODAL_MANAGER_CONTEXT.TYPE | undefined = undefined;

  /** Wires the modal manager so "Learn more" can open the help modal. */
  constructor() {
    super();
    this.consumeContext(UMB_MODAL_MANAGER_CONTEXT, (ctx) => {
      this.#modalManager = ctx ?? undefined;
    });
  }

  /** Opens the help modal for this surface. No-op when the surface has no how-to doc. */
  #openModal(): void {
    const cfg = SURFACE_HELP[this.surface];
    if (!cfg?.howToDoc || !this.#modalManager) return;
    this.#modalManager.open(this, UAP_HELP_MODAL, {
      data: { headline: this.headline, howToDoc: cfg.howToDoc },
    });
  }

  override render() {
    const cfg = SURFACE_HELP[this.surface];
    if (!cfg) return nothing;
    return html`
      <p class="intro">
        ${this.#localize.term(cfg.descriptionKey)}${cfg.howToDoc
          ? html` <button type="button" class="learn" @click=${this.#openModal}>
              ${this.#localize.term('uap_help_learnMore')}
            </button>`
          : nothing}
      </p>
    `;
  }

  static override styles = css`
    :host { display: block; }
    .intro {
      margin: 0 0 var(--uui-size-space-4, 12px);
      color: var(--uui-color-text-alt);
      line-height: 1.5;
    }
    .learn {
      border: none;
      background: none;
      padding: 0;
      font: inherit;
      color: var(--uui-color-interactive);
      cursor: pointer;
    }
    .learn:hover { text-decoration: underline; }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'uap-page-intro': UapPageIntroElement;
  }
}

export default UapPageIntroElement;
```

- [ ] **Step 2: Type-check.** `npx tsc --noEmit`. Expected PASS.

- [ ] **Step 3: Commit.**
```bash
git add src/Umbraco.Community.AdvancedPermissions.Client/src/help/uap-page-intro.element.ts
git commit -m "feat(layout): add uap-page-intro description component"
```

---

## Task 3: `uap-selection-panel` element

**Files:**
- Create: `src/help/uap-selection-panel.element.ts`

- [ ] **Step 1: Create the element.**

```ts
import { html, css, nothing, customElement, property } from '@umbraco-cms/backoffice/external/lit';
import type { TemplateResult } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';

/** One selectable thing (a picker). */
export interface UapSelector {
  /** Stable id forwarded in the uap-selector-click event (e.g. 'group', 'user', 'docType'). */
  id: string;
  /** Localized "Choose …" placeholder shown on the unselected button. */
  label: string;
  /** Umbraco icon name (e.g. 'icon-users'). */
  icon: string;
  /** Set when this option is the active selection. */
  selectedName?: string;
}

/** A required selection slot. One option = a single picker; 2+ options = mutually exclusive. */
export interface UapSelectorGroup {
  options: UapSelector[];
}

/**
 * Selection-and-results container. While the selection is incomplete it renders a centered
 * call-to-action (icon + prompt + the needed picker buttons). Once every group has a selection
 * it renders label-less selection pills (icon + name) at the top-left, an actions slot on the
 * right, and the default slot (the results) below. Picker activations are delegated to the host
 * via the `uap-selector-click` event.
 *
 * @fires uap-selector-click - detail `{ id }` when a picker button or pill is activated.
 * @slot - the results, shown only when the selection is complete.
 * @slot actions - right-aligned actions in the complete-state bar (e.g. save/discard).
 */
@customElement('uap-selection-panel')
export class UapSelectionPanelElement extends UmbLitElement {
  /** The required selection groups. */
  @property({ attribute: false }) groups: UapSelectorGroup[] = [];

  /** Prompt sentence shown in the call-to-action. */
  @property() promptText = '';

  /** Umbraco icon name shown above the call-to-action prompt. */
  @property() ctaIcon = 'icon-document';

  /** Localized separator between mutually-exclusive options (e.g. "or"). */
  @property() orLabel = 'or';

  /** True when every group has a selected option. */
  get #complete(): boolean {
    return this.groups.length > 0 && this.groups.every((g) => g.options.some((o) => !!o.selectedName));
  }

  /** Emits the selector-click event for the host to open the matching picker. */
  #click(id: string): void {
    this.dispatchEvent(new CustomEvent('uap-selector-click', { detail: { id }, bubbles: true, composed: true }));
  }

  /** Renders a selected option as a pill (icon + name + caret). */
  #pill(o: UapSelector): TemplateResult {
    return html`
      <uui-button look="outline" compact label=${o.selectedName ?? o.label} @click=${() => this.#click(o.id)}>
        <umb-icon name=${o.icon}></umb-icon>
        <span class="pill-name">${o.selectedName}</span>
        <umb-icon name="icon-navigation-down" class="caret"></umb-icon>
      </uui-button>
    `;
  }

  /** Renders an unselected option as a placeholder "Choose …" button. */
  #placeholder(o: UapSelector): TemplateResult {
    return html`
      <uui-button look="placeholder" label=${o.label} @click=${() => this.#click(o.id)}>${o.label}</uui-button>
    `;
  }

  /** Renders a group: its selected pill, or its option buttons joined by the "or" separator. */
  #groupControls(g: UapSelectorGroup): TemplateResult {
    const selected = g.options.find((o) => o.selectedName);
    if (selected) return this.#pill(selected);
    return html`${g.options.map(
      (o, i) => html`${i > 0 ? html`<span class="or">${this.orLabel}</span>` : nothing}${this.#placeholder(o)}`,
    )}`;
  }

  override render() {
    if (!this.#complete) {
      return html`
        <uui-box>
          <div class="cta">
            <umb-icon name=${this.ctaIcon} class="cta-icon"></umb-icon>
            <p class="cta-text">${this.promptText}</p>
            <div class="cta-ctrls">${this.groups.map((g) => this.#groupControls(g))}</div>
          </div>
        </uui-box>
      `;
    }
    return html`
      <uui-box>
        <div class="bar">
          <div class="pills">
            ${this.groups.map((g) => {
              const s = g.options.find((o) => o.selectedName);
              return s ? this.#pill(s) : nothing;
            })}
          </div>
          <div class="actions"><slot name="actions"></slot></div>
        </div>
        <slot></slot>
      </uui-box>
    `;
  }

  static override styles = css`
    :host { display: block; }
    .cta {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--uui-size-space-4, 12px);
      padding: var(--uui-size-layout-1, 24px) var(--uui-size-space-4, 12px);
      text-align: center;
    }
    .cta-icon { font-size: 2rem; color: var(--uui-color-text-alt); }
    .cta-text { margin: 0; color: var(--uui-color-text); max-width: 44ch; }
    .cta-ctrls {
      display: flex;
      align-items: center;
      gap: var(--uui-size-space-3, 9px);
      flex-wrap: wrap;
      justify-content: center;
    }
    .or { color: var(--uui-color-text-alt); font-size: 0.85rem; }
    .bar {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--uui-size-space-3, 9px);
      flex-wrap: wrap;
      margin-bottom: var(--uui-size-space-4, 12px);
      padding-bottom: var(--uui-size-space-3, 9px);
      border-bottom: 1px solid var(--uui-color-divider);
    }
    .pills { display: flex; align-items: center; gap: var(--uui-size-space-2, 6px); flex-wrap: wrap; }
    .actions { display: flex; gap: var(--uui-size-space-2, 6px); }
    .pill-name { margin: 0 2px; }
    .caret { font-size: 0.7em; margin-left: 2px; color: var(--uui-color-text-alt); }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'uap-selection-panel': UapSelectionPanelElement;
  }
}

export default UapSelectionPanelElement;
```

- [ ] **Step 2: Type-check.** `npx tsc --noEmit`. Expected PASS.

> Note: if `icon-navigation-down` renders blank in the later manual pass, swap it for `icon-arrow-down` (both are stock Umbraco icons) — caret only, non-critical.

- [ ] **Step 3: Commit.**
```bash
git add src/Umbraco.Community.AdvancedPermissions.Client/src/help/uap-selection-panel.element.ts
git commit -m "feat(layout): add uap-selection-panel (CTA + selection pills container)"
```

---

## Refactor recipe (used by Tasks 4–11)

Each surface task applies this recipe with its own parameters. The new `render()` top structure is:

```ts
override render() {
  return html`
    <umb-body-layout headline=${this.#localize.term('<HEADLINE_KEY>')}>
      <uap-page-intro surface="<ENTITY_TYPE>" headline=${this.#localize.term('<HEADLINE_KEY>')}></uap-page-intro>
      <uap-selection-panel
        .groups=${this.#selectionGroups}
        promptText=${this.#localize.term('<PROMPT_KEY>')}
        ctaIcon="<CTA_ICON>"
        orLabel=${this.#localize.term('uap_subjectOr')}
        @uap-selector-click=${(e: CustomEvent<{ id: string }>) => this.#onSelectorClick(e.detail.id)}>
        <!-- ACTIONS: editors only -->
        ${this.#renderActions()}
        <!-- RESULTS: the surface's existing loading / error / results blocks, moved here unchanged
             except their "selection exists" guard is dropped (the panel only renders this slot
             when the selection is complete). -->
        ${this.#renderResults()}
      </uap-selection-panel>
    </umb-body-layout>
    <!-- Any existing dialogs (scope dialog / reasoning dialog) stay here, AFTER umb-body-layout. -->
  `;
}
```

Recipe rules:
1. **Imports:** add `import '../help/uap-page-intro.element.js';` and `import '../help/uap-selection-panel.element.js';`. Import the `UapSelectorGroup` type from `'../help/uap-selection-panel.element.js'`. Remove any `uap-page-help`/`uap-info-icon` imports.
2. **`#selectionGroups` getter:** returns `UapSelectorGroup[]` built from the surface's selection state (per-surface code below).
3. **`#onSelectorClick(id)`:** maps the id to the surface's existing picker opener (per-surface below).
4. **`#renderResults()`:** a private method returning the surface's existing results region. Move the existing loading (`<uui-loader>`), error (`<p class="error-msg">`), and table/`.type-list` blocks into it. Drop the part of each guard that checks "a selection exists" (e.g. `this._selectedRole &&`, `this.#subject &&`) — keep the `!this._loading` / `length > 0` parts. The panel guarantees this slot only renders when complete.
5. **`#renderActions()`:** editors only — returns the existing `hasPending ? html\`<save/discard buttons>\` : nothing` block wrapped in `<div slot="actions">…</div>`. Viewers: return `nothing`.
6. **Remove:** the old `.toolbar` div and all its `uap-picker-button`/`picker-or`/`toolbar-divider`/`uap-info-icon` markup; the old empty-prompt rendering (`uap_select*Prompt` `<p class="empty-msg">` lines — the panel renders the prompt now); the now-dead CSS rules (`.toolbar`, `.picker-or`, `.toolbar-divider`, and `uap-page-help`/`uap-info-icon`-related styles). Keep `.table-wrap` / `.type-list` / table / loader / error styles.
7. Each `#selectionGroups` and `#onSelectorClick` must be added as private methods; recursive/`TemplateResult`-returning private methods need explicit return types.

After applying: `npx tsc --noEmit` then `npm run build`, then commit.

---

## Task 4: Content Permissions Editor (`uap-permissions-editor`)

**File:** `src/permissions-editor/uap-permissions-editor-root.element.ts`

Parameters: HEADLINE_KEY=`uap_editorHeadline`, ENTITY_TYPE=`uap-permissions-editor`, PROMPT_KEY=`uap_selectRolePrompt`, CTA_ICON=`icon-lock`, editor (has actions).

- [ ] **Step 1: Apply the recipe.** Add imports; remove the `uap-page-help.element.js` and `uap-info-icon.element.js` imports. Add:

```ts
get #selectionGroups(): UapSelectorGroup[] {
  return [
    { options: [{ id: 'group', label: this.#localize.term('uap_chooseRole'), icon: 'icon-users', ...(this._selectedRole ? { selectedName: this._selectedRole.name } : {}) }] },
  ];
}

#onSelectorClick(id: string): void {
  if (id === 'group') void this.#openRolePicker();
}
```

Replace the `render()` body per the recipe. `#renderActions()` returns the existing save/discard block (the `hasPending` ternary) wrapped in `<div slot="actions">`. `#renderResults()` returns the existing `${this._error ...}`, `${this._loading ...}`, and the `${this._selectedRole && !this._loading && this._treeNodes.length > 0 ? html\`<div class="table-wrap">…\` : nothing}` block — drop `this._selectedRole &&` from that guard. Remove the old `.toolbar` markup, the `uap-picker-button`, the `uap-info-icon`, and the `<p class="empty-msg">${...uap_selectRolePrompt}</p>` line. The scope dialog (`#renderDialog()`) stays after `</umb-body-layout>`.

- [ ] **Step 2: Verify.** `npx tsc --noEmit` (PASS), then `npm run build` (SUCCESS).
- [ ] **Step 3: Commit.**
```bash
git add src/Umbraco.Community.AdvancedPermissions.Client/src/permissions-editor/uap-permissions-editor-root.element.ts
git commit -m "feat(layout): native-first header + selection panel for Content Permissions Editor"
```

---

## Task 5: Access Viewer (`uap-access-viewer`)

**File:** `src/access-viewer/uap-access-viewer-root.element.ts`

Parameters: HEADLINE_KEY=`uap_viewerHeadline`, ENTITY_TYPE=`uap-access-viewer`, PROMPT_KEY=`uap_selectSubjectPrompt`, CTA_ICON=`icon-eye`, viewer (no actions → `#renderActions()` returns `nothing`).

- [ ] **Step 1: Apply the recipe.** Add imports; remove `uap-page-help`/`uap-info-icon` imports. Add (mutually-exclusive group):

```ts
get #selectionGroups(): UapSelectorGroup[] {
  return [
    {
      options: [
        { id: 'group', label: this.#localize.term('uap_chooseRole'), icon: 'icon-users', ...(this._activeSubject === 'role' && this._selectedRole ? { selectedName: this._selectedRole.name } : {}) },
        { id: 'user', label: this.#localize.term('uap_chooseUser'), icon: 'icon-user', ...(this._activeSubject === 'user' && this._selectedUser ? { selectedName: this._selectedUser.name } : {}) },
      ],
    },
  ];
}

#onSelectorClick(id: string): void {
  if (id === 'group') void this.#openRolePicker();
  else if (id === 'user') void this.#openUserPicker();
}
```

`#renderActions()` returns `nothing`. `#renderResults()` returns the existing error/loading blocks and the `${this.#subject && !this._loading && this._treeNodes.length > 0 ? html\`<div class="table-wrap">…\` : nothing}` block — drop `this.#subject &&`. Remove the `.toolbar`, both `uap-picker-button`s, the `picker-or` span, the `uap-info-icon`, and the `<p class="empty-msg">${...uap_selectSubjectPrompt}</p>`. The reasoning dialog stays after `</umb-body-layout>`.

- [ ] **Step 2: Verify.** `npx tsc --noEmit`; `npm run build`.
- [ ] **Step 3: Commit.**
```bash
git add src/Umbraco.Community.AdvancedPermissions.Client/src/access-viewer/uap-access-viewer-root.element.ts
git commit -m "feat(layout): native-first header + selection panel for Access Viewer"
```

---

## Task 6: Document Type Permissions Editor (`uap-doc-type-permissions`)

**File:** `src/doc-type-permissions/uap-doc-type-permissions-editor-root.element.ts`

Parameters: HEADLINE_KEY=`uap_docTypePermissions_workspaceTitle`, ENTITY_TYPE=`uap-doc-type-permissions`, PROMPT_KEY=`uap_docTypePermissions_pickToStart`, CTA_ICON=`icon-document`, editor (has actions).

- [ ] **Step 1: Apply the recipe.** Add imports. Add (two required groups):

```ts
get #selectionGroups(): UapSelectorGroup[] {
  return [
    { options: [{ id: 'group', label: this.#localize.term('uap_chooseRole'), icon: 'icon-users', ...(this._selectedRole ? { selectedName: this._selectedRole.name } : {}) }] },
    { options: [{ id: 'docType', label: this.#localize.term('uap_chooseDocType'), icon: this._selectedDocType?.icon ?? 'icon-document', ...(this._selectedDocType ? { selectedName: this._selectedDocType.name } : {}) }] },
  ];
}

#onSelectorClick(id: string): void {
  if (id === 'group') void this.#openRolePicker();
  else if (id === 'docType') void this.#openDocTypePicker();
}
```

`#renderActions()` returns the existing save/discard block in `<div slot="actions">`. `#renderResults()` returns the existing error/loading + the `${this._selectedRole && this._selectedDocType && !this._loading && this._treeNodes.length > 0 ? …}` block with the `this._selectedRole && this._selectedDocType &&` guard dropped. Remove the `.toolbar`, both `uap-picker-button`s, and the existing empty prompt(s). This surface has no `uap-page-help`/`uap-info-icon` to remove.

- [ ] **Step 2: Verify.** `npx tsc --noEmit`; `npm run build`.
- [ ] **Step 3: Commit.**
```bash
git add src/Umbraco.Community.AdvancedPermissions.Client/src/doc-type-permissions/uap-doc-type-permissions-editor-root.element.ts
git commit -m "feat(layout): native-first header + selection panel for Document Type Permissions Editor"
```

---

## Task 7: Insert Options Viewer / create audit (`uap-doc-type-create-audit`)

**File:** `src/doc-type-permissions/uap-doc-type-create-audit-root.element.ts`

Parameters: HEADLINE_KEY=`uap_docTypePermissions_auditTitle`, ENTITY_TYPE=`uap-doc-type-create-audit`, PROMPT_KEY=`uap_docTypePermissions_pickToStart`, CTA_ICON=`icon-eye`, viewer (no actions).

- [ ] **Step 1: Apply the recipe.** Add imports. Add (mutually-exclusive subject group + required docType group):

```ts
get #selectionGroups(): UapSelectorGroup[] {
  return [
    {
      options: [
        { id: 'group', label: this.#localize.term('uap_chooseRole'), icon: 'icon-users', ...(this._activeSubject === 'role' && this._selectedRole ? { selectedName: this._selectedRole.name } : {}) },
        { id: 'user', label: this.#localize.term('uap_chooseUser'), icon: 'icon-user', ...(this._activeSubject === 'user' && this._selectedUser ? { selectedName: this._selectedUser.name } : {}) },
      ],
    },
    { options: [{ id: 'docType', label: this.#localize.term('uap_chooseDocType'), icon: this._selectedDocType?.icon ?? 'icon-document', ...(this._selectedDocType ? { selectedName: this._selectedDocType.name } : {}) }] },
  ];
}

#onSelectorClick(id: string): void {
  if (id === 'group') void this.#openRolePicker();
  else if (id === 'user') void this.#openUserPicker();
  else if (id === 'docType') void this.#openDocTypePicker();
}
```

`#renderActions()` returns `nothing`. `#renderResults()` returns the existing error/loading + the results block guarded by `subject && docTypeSelected && !this._loading && this._treeNodes.length > 0` — drop the `subject && docTypeSelected &&` part (keep `!this._loading && this._treeNodes.length > 0`). Remove the `.toolbar`, all picker buttons, the `picker-or` span, the `toolbar-divider` span, and both empty prompts (`uap_selectSubjectPrompt`, `uap_docTypePermissions_pickToStart`).

- [ ] **Step 2: Verify.** `npx tsc --noEmit`; `npm run build`.
- [ ] **Step 3: Commit.**
```bash
git add src/Umbraco.Community.AdvancedPermissions.Client/src/doc-type-permissions/uap-doc-type-create-audit-root.element.ts
git commit -m "feat(layout): native-first header + selection panel for Insert Options Viewer"
```

---

## Task 8: Library Permissions Editor (`uap-library-permissions`)

**File:** `src/library-permissions/uap-library-permissions-editor-root.element.ts`

Parameters: HEADLINE_KEY=`uap_library_editorHeadline`, ENTITY_TYPE=`uap-library-permissions`, PROMPT_KEY=`uap_library_selectRolePrompt`, CTA_ICON=`icon-globe`, editor (has actions).

- [ ] **Step 1: Apply the recipe.** Add imports. Add:

```ts
get #selectionGroups(): UapSelectorGroup[] {
  return [
    { options: [{ id: 'group', label: this.#localize.term('uap_chooseRole'), icon: 'icon-users', ...(this._selectedRole ? { selectedName: this._selectedRole.name } : {}) }] },
  ];
}

#onSelectorClick(id: string): void {
  if (id === 'group') void this.#openRolePicker();
}
```

`#renderActions()` returns the existing save/discard block in `<div slot="actions">`. `#renderResults()` returns the existing error/loading + the `${this._selectedRole && !this._loading && this._treeNodes.length > 0 ? …}` block with `this._selectedRole &&` dropped. Remove the `.toolbar` and the empty prompt.

- [ ] **Step 2: Verify.** `npx tsc --noEmit`; `npm run build`.
- [ ] **Step 3: Commit.**
```bash
git add src/Umbraco.Community.AdvancedPermissions.Client/src/library-permissions/uap-library-permissions-editor-root.element.ts
git commit -m "feat(layout): native-first header + selection panel for Library Permissions Editor"
```

---

## Task 9: Library Element Type Permissions (`uap-element-type-permissions`)

**File:** `src/library-permissions/uap-element-type-permissions-editor-root.element.ts`

Parameters: HEADLINE_KEY=`uap_elementTypePermissions_headline`, ENTITY_TYPE=`uap-element-type-permissions`, PROMPT_KEY=`uap_library_selectRolePrompt`, CTA_ICON=`icon-thumbnail-list`, editor (has actions).

- [ ] **Step 1: Apply the recipe.** Add imports. Add (single group — the per-row `#openTypePicker` is unrelated and stays):

```ts
get #selectionGroups(): UapSelectorGroup[] {
  return [
    { options: [{ id: 'group', label: this.#localize.term('uap_chooseRole'), icon: 'icon-users', ...(this._selectedRole ? { selectedName: this._selectedRole.name } : {}) }] },
  ];
}

#onSelectorClick(id: string): void {
  if (id === 'group') void this.#openRolePicker();
}
```

`#renderActions()` returns the existing save/discard block (note this surface's discard calls `this._pending = new Set(); void this.#load();` and save calls `this.#save()`) in `<div slot="actions">`. `#renderResults()` returns the existing error/loading + the results region guarded by `this._selectedRole && !this._loading` — drop `this._selectedRole &&`, keep the inner `this._types.length > 0 ? html\`<div class="type-list">…\` : html\`<…uap_elementTypePermissions_noTypes…>\``. Remove the `.toolbar` and the no-role empty prompt. The per-row scope dialog (if any) stays after `</umb-body-layout>`.

- [ ] **Step 2: Verify.** `npx tsc --noEmit`; `npm run build`.
- [ ] **Step 3: Commit.**
```bash
git add src/Umbraco.Community.AdvancedPermissions.Client/src/library-permissions/uap-element-type-permissions-editor-root.element.ts
git commit -m "feat(layout): native-first header + selection panel for Library Element Type Permissions"
```

---

## Task 10: Library Access Viewer (`uap-library-access-viewer`)

**File:** `src/library-permissions/uap-library-access-viewer-root.element.ts`

Parameters: HEADLINE_KEY=`uap_library_accessViewerHeadline`, ENTITY_TYPE=`uap-library-access-viewer`, PROMPT_KEY=`uap_selectSubjectPrompt`, CTA_ICON=`icon-eye`, viewer (no actions).

- [ ] **Step 1: Apply the recipe.** Add imports. Add the mutually-exclusive subject group (identical shape to Task 5's `#selectionGroups`/`#onSelectorClick`). `#renderActions()` returns `nothing`. `#renderResults()` returns the existing error/loading + the `${this.#subject && !this._loading && this._treeNodes.length > 0 ? …}` block with `this.#subject &&` dropped. Remove the `.toolbar`, both pickers, the `picker-or` span, and the empty prompt. The reasoning dialog stays after `</umb-body-layout>`.

```ts
get #selectionGroups(): UapSelectorGroup[] {
  return [
    {
      options: [
        { id: 'group', label: this.#localize.term('uap_chooseRole'), icon: 'icon-users', ...(this._activeSubject === 'role' && this._selectedRole ? { selectedName: this._selectedRole.name } : {}) },
        { id: 'user', label: this.#localize.term('uap_chooseUser'), icon: 'icon-user', ...(this._activeSubject === 'user' && this._selectedUser ? { selectedName: this._selectedUser.name } : {}) },
      ],
    },
  ];
}

#onSelectorClick(id: string): void {
  if (id === 'group') void this.#openRolePicker();
  else if (id === 'user') void this.#openUserPicker();
}
```

- [ ] **Step 2: Verify.** `npx tsc --noEmit`; `npm run build`.
- [ ] **Step 3: Commit.**
```bash
git add src/Umbraco.Community.AdvancedPermissions.Client/src/library-permissions/uap-library-access-viewer-root.element.ts
git commit -m "feat(layout): native-first header + selection panel for Library Access Viewer"
```

---

## Task 11: Library Insert Viewer (`uap-library-insert-viewer`)

**File:** `src/library-permissions/uap-library-insert-viewer-root.element.ts`

Parameters: HEADLINE_KEY=`uap_libraryInsertViewer_headline`, ENTITY_TYPE=`uap-library-insert-viewer`, PROMPT_KEY=`uap_selectSubjectPrompt`, CTA_ICON=`icon-eye`, viewer (no actions).

- [ ] **Step 1: Apply the recipe.** Add imports. Add the mutually-exclusive subject group (same shape as Task 10). `#renderActions()` returns `nothing`. `#renderResults()` returns the existing error/loading + the results region guarded by `subject && !this._loading` then `this._rows.length > 0 ? html\`<div class="type-list">…\` : …` — drop the `subject &&` part. Remove the `.toolbar`, both pickers, the `picker-or` span, and the empty prompt.

```ts
get #selectionGroups(): UapSelectorGroup[] {
  return [
    {
      options: [
        { id: 'group', label: this.#localize.term('uap_chooseRole'), icon: 'icon-users', ...(this._activeSubject === 'role' && this._selectedRole ? { selectedName: this._selectedRole.name } : {}) },
        { id: 'user', label: this.#localize.term('uap_chooseUser'), icon: 'icon-user', ...(this._activeSubject === 'user' && this._selectedUser ? { selectedName: this._selectedUser.name } : {}) },
      ],
    },
  ];
}

#onSelectorClick(id: string): void {
  if (id === 'group') void this.#openRolePicker();
  else if (id === 'user') void this.#openUserPicker();
}
```

- [ ] **Step 2: Verify.** `npx tsc --noEmit`; `npm run build`.
- [ ] **Step 3: Commit.**
```bash
git add src/Umbraco.Community.AdvancedPermissions.Client/src/library-permissions/uap-library-insert-viewer-root.element.ts
git commit -m "feat(layout): native-first header + selection panel for Library Insert Viewer"
```

---

## Task 12: Delete the obsolete page-help band

**Files:**
- Delete: `src/help/uap-page-help.element.ts`

- [ ] **Step 1: Confirm no references remain.** Run `grep -rn "uap-page-help" src/` from the Client dir. Expected: no matches (Tasks 4–5 removed the imports). If any remain, remove them.
- [ ] **Step 2: Delete the file.**
```bash
git rm src/Umbraco.Community.AdvancedPermissions.Client/src/help/uap-page-help.element.ts
```
- [ ] **Step 3: Verify.** `npx tsc --noEmit` (PASS); `npm run build` (SUCCESS).
- [ ] **Step 4: Commit.**
```bash
git commit -m "refactor(layout): remove obsolete uap-page-help band"
```

---

## Task 13: Final verification

**Files:** none (verification only)

- [ ] **Step 1: Full check.** From the Client dir: `npx tsc --noEmit && npm run build`. Both succeed.
- [ ] **Step 2: Confirm no dead toolbar CSS or stray icons remain.** `grep -rn "class=\"toolbar\"\|picker-or\|toolbar-divider\|uap-info-icon\|uap-picker-button" src/` — the only remaining matches should be `uap-picker-button` if any surface still uses it intentionally (it should be fully replaced; if the element file is now unused, note it but leaving it is harmless). No `class="toolbar"` should remain in the 8 surfaces.
- [ ] **Step 3: Manual pass on the TestSite.** Start: `dotnet run --project tests/Umbraco.Community.AdvancedPermissions.TestSite -p:NuGetAudit=false --launch-profile https`; open `https://localhost:7067/umbraco` (login `admin@example.test` / `test123456!`); for each of the eight surfaces in the Users section confirm: native headline, intro copy (with "Learn more" only on Content Permissions Editor + Access Viewer), centered CTA when nothing is selected (with `or` between role/user on the dual-subject surfaces, two prompts on the doc-type ones), and label-less selection pills + results once selected. Switch culture to Dutch and re-check the descriptions/prompts.
- [ ] **Step 4: Commit any build output** only if `wwwroot` is git-tracked (it is gitignored, so normally nothing to commit).

---

## Self-Review

**Spec coverage:**
- Native header (umb-body-layout headline) → recipe + every surface task. ✅
- Description as body intro copy + Learn more pilots-only → Task 2 (`uap-page-intro`, gated on `howToDoc`), Task 1 (registry). ✅
- Single `uui-box`, CTA-when-incomplete / pills-when-complete → Task 3 (`uap-selection-panel`). ✅
- Label-less pills (icon + name + caret) → Task 3 `#pill`. ✅
- Multiple required + mutually-exclusive pickers → Task 3 `UapSelectorGroup` (1 option = single, 2 = mutually exclusive); Tasks 6/7 (two groups), Tasks 5/7/10/11 (mutually-exclusive). ✅
- Remove stray toolbar ⓘ → Tasks 4, 5 (remove `uap-info-icon`). ✅
- All 8 surfaces → Tasks 4–11. ✅
- Native-first / token-only custom CSS → components use `uui-box`/`uui-button`/`umb-body-layout`; styles are token-based layout only. ✅
- Delete obsolete band → Task 12. ✅
- Preserve reload-on-change → recipe keeps each surface's existing picker openers (which already trigger reload); only the markup changes. ✅

**Placeholder scan:** No TBD/TODO. Component + registry + localization code is complete. Per-surface tasks give complete new code (groups getter, handler) and precise, file-relative instructions for moving the existing (unchanged) results blocks — not hand-waving. The Library description strings are real (flagged for terminology confirmation, not placeholders).

**Type consistency:** `UapSelector`/`UapSelectorGroup` (Task 3) used consistently in every `#selectionGroups` getter. `uap-selector-click` detail `{ id }` matches `#onSelectorClick`. `SURFACE_HELP`/`SurfaceHelp.howToDoc?` (Task 1) consumed by `uap-page-intro` (Task 2). Surface state field names (`_selectedRole`, `_selectedUser`, `_activeSubject`, `_selectedDocType`, `#subject`) taken from the verified surface map.
