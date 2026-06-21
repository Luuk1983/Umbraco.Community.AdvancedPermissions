# In-app Help System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a reusable in-app help framework (always-visible page descriptions, a details modal with per-page how-to + a shared concepts reference, and inline concept-hint icons) and apply it to the Content Permissions Editor and Access Viewer.

**Architecture:** New `src/help/` folder holds a markdown render helper, a content registry that lazy-loads per-locale Markdown docs (`?raw`), and three Lit elements: `uap-page-help` (description bar), `uap-help-modal` (token + element + manifest), and `uap-info-icon` (inline ⓘ). Long-form content lives as Markdown under `help-docs/{en,nl}/` and doubles as GitHub documentation. Short strings are `uap_help_*` localization keys.

**Tech Stack:** TypeScript, Lit (`@umbraco-cms/backoffice/external/lit`), Umbraco backoffice extension APIs, Vite (library build), `marked` (bundled).

**Spec:** `docs/superpowers/specs/2026-06-21-in-app-help-system-design.md`

**Verification approach (read first):** This is a frontend-only feature and the Client project has **no JS test runner** (the spec explicitly says not to add one just for this). The global "test-first" rule targets backend code; there is no backend change here. So each task's verification gate is **`npx tsc --noEmit`** (type-check) plus **`npm run build`**, and the final task adds manual verification on the TestSite. All commands run from `src/Umbraco.Community.AdvancedPermissions.Client/` unless stated otherwise.

**Key gotchas (from CLAUDE.md):**
- `UmbExtensionManifest` is a global ambient type — never import it.
- `exactOptionalPropertyTypes: true` — never assign/pass `undefined` to an optional field; use conditional spread or `T | undefined = undefined` for class fields.
- `consumeContext` callback passes `T | undefined` — always `ctx ?? undefined`.
- Recursive Lit private methods need explicit return type annotations.
- Use `<umb-icon name="icon-…">` for Umbraco icons, `<uui-icon>` for UUI icons.

---

## File Structure

**Create:**
- `src/Umbraco.Community.AdvancedPermissions.Client/src/help/markdown.ts` — `renderMarkdown()` + `helpLocale()`.
- `src/Umbraco.Community.AdvancedPermissions.Client/src/help/help-content.ts` — types, `SURFACE_HELP` registry, `loadHelpDoc()`.
- `src/Umbraco.Community.AdvancedPermissions.Client/src/help/uap-info-icon.element.ts` — inline ⓘ.
- `src/Umbraco.Community.AdvancedPermissions.Client/src/help/uap-help-modal.token.ts` — modal token + data types.
- `src/Umbraco.Community.AdvancedPermissions.Client/src/help/uap-help-modal.element.ts` — modal element.
- `src/Umbraco.Community.AdvancedPermissions.Client/src/help/uap-page-help.element.ts` — description bar.
- `src/Umbraco.Community.AdvancedPermissions.Client/help-docs/en/concepts.md`
- `src/Umbraco.Community.AdvancedPermissions.Client/help-docs/en/content-permissions.md`
- `src/Umbraco.Community.AdvancedPermissions.Client/help-docs/en/access-viewer.md`
- `src/Umbraco.Community.AdvancedPermissions.Client/help-docs/nl/concepts.md`
- `src/Umbraco.Community.AdvancedPermissions.Client/help-docs/nl/content-permissions.md`
- `src/Umbraco.Community.AdvancedPermissions.Client/help-docs/nl/access-viewer.md`

**Modify:**
- `src/Umbraco.Community.AdvancedPermissions.Client/package.json` — add `marked` devDependency.
- `src/Umbraco.Community.AdvancedPermissions.Client/src/manifests.ts` — register help modal manifest.
- `src/Umbraco.Community.AdvancedPermissions.Client/src/localization/en.ts` — add `uap_help_*` keys.
- `src/Umbraco.Community.AdvancedPermissions.Client/src/localization/nl.ts` — add `uap_help_*` keys.
- `src/Umbraco.Community.AdvancedPermissions.Client/src/permissions-editor/uap-permissions-editor-root.element.ts` — mount page-help + toolbar ⓘ.
- `src/Umbraco.Community.AdvancedPermissions.Client/src/access-viewer/uap-access-viewer-root.element.ts` — mount page-help + toolbar ⓘ.
- `src/Umbraco.Community.AdvancedPermissions.Client/src/shared/components/uap-permission-scope-dialog.element.ts` — tooltip-only ⓘ for scope + priority override.
- `README.md` — Documentation section.

---

## Task 1: Markdown dependency + render helper

**Files:**
- Modify: `src/Umbraco.Community.AdvancedPermissions.Client/package.json`
- Create: `src/Umbraco.Community.AdvancedPermissions.Client/src/help/markdown.ts`

- [ ] **Step 1: Add `marked` as an explicit devDependency**

Run (from `src/Umbraco.Community.AdvancedPermissions.Client/`):
```bash
npm install --save-dev marked@^18.0.4
```
Expected: `package.json` gains `"marked": "^18.0.4"` under `devDependencies`; no errors.

- [ ] **Step 2: Create the render helper**

Create `src/help/markdown.ts`:
```ts
import { marked } from 'marked';

/**
 * Resolves the help-docs locale folder for the current backoffice culture.
 * The backoffice sets `<html lang>` to the active culture; anything starting
 * with "nl" maps to Dutch, everything else falls back to English.
 *
 * @returns The locale folder name to load Markdown docs from.
 */
export function helpLocale(): 'en' | 'nl' {
  const lang = (document.documentElement.lang || 'en').toLowerCase();
  return lang.startsWith('nl') ? 'nl' : 'en';
}

/**
 * Renders first-party Markdown to an HTML string. The content is authored in
 * this repository, so it is trusted and rendered without sanitization.
 *
 * @param md The raw Markdown source.
 * @returns The rendered HTML string (empty string for empty input).
 */
export function renderMarkdown(md: string): string {
  if (!md) return '';
  return marked.parse(md, { async: false }) as string;
}
```

- [ ] **Step 3: Type-check**

Run: `npx tsc --noEmit`
Expected: PASS (no errors). If the `marked` types aren't found, confirm Step 1 added it and `node_modules/marked` exists.

- [ ] **Step 4: Commit**

```bash
git add src/Umbraco.Community.AdvancedPermissions.Client/package.json src/Umbraco.Community.AdvancedPermissions.Client/package-lock.json src/Umbraco.Community.AdvancedPermissions.Client/src/help/markdown.ts
git commit -m "feat(help): add marked dependency and markdown render helper"
```

---

## Task 2: Help content registry + loader

**Files:**
- Create: `src/Umbraco.Community.AdvancedPermissions.Client/src/help/help-content.ts`

- [ ] **Step 1: Create the registry and loader**

Create `src/help/help-content.ts`:
```ts
import { helpLocale } from './markdown.js';

/** Identifies a long-form help document (a per-page how-to or the shared concepts reference). */
export type HelpDocId = 'concepts' | 'content-permissions' | 'access-viewer';

/** Help configuration for a single editor/viewer surface. */
export interface SurfaceHelp {
  /** Localization key for the always-visible one-line description. */
  descriptionKey: string;
  /** The per-page how-to document shown in the modal's "About this page" section. */
  howToDoc: HelpDocId;
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
};

/** The shared concepts document id, shown in the modal's "Concepts" section on every surface. */
export const CONCEPTS_DOC: HelpDocId = 'concepts';

/**
 * Lazily-loaded raw Markdown keyed by path relative to this file. Vite turns each
 * `.md` file into its own chunk loaded on demand (not bundled into initial load).
 */
const docModules = import.meta.glob('../../help-docs/**/*.md', {
  query: '?raw',
  import: 'default',
}) as Record<string, () => Promise<string>>;

/**
 * Loads the raw Markdown for a document in the current backoffice locale,
 * falling back to English when a localized file is missing.
 *
 * @param doc The document id to load.
 * @returns The raw Markdown source, or an empty string if not found.
 */
export async function loadHelpDoc(doc: HelpDocId): Promise<string> {
  const locale = helpLocale();
  const localized = `../../help-docs/${locale}/${doc}.md`;
  const fallback = `../../help-docs/en/${doc}.md`;
  const loader = docModules[localized] ?? docModules[fallback];
  if (!loader) return '';
  return loader();
}
```

- [ ] **Step 2: Type-check**

Run: `npx tsc --noEmit`
Expected: PASS. (The glob matches no files yet — that's fine; it only resolves at build/runtime. `import.meta.glob` is a Vite type and is already available in this project's TS config since Vite is the bundler.)

> Note: if `tsc` reports `Property 'glob' does not exist on type 'ImportMeta'`, add `/// <reference types="vite/client" />` as the first line of `help-content.ts`.

- [ ] **Step 3: Commit**

```bash
git add src/Umbraco.Community.AdvancedPermissions.Client/src/help/help-content.ts
git commit -m "feat(help): add help content registry and per-locale markdown loader"
```

---

## Task 3: `uap-info-icon` element

**Files:**
- Create: `src/Umbraco.Community.AdvancedPermissions.Client/src/help/uap-info-icon.element.ts`

> Design note: a bare ⓘ with `text` shows a tooltip everywhere. When `concept` is set AND the icon is **not** inside a native `<dialog>`, clicking opens the help modal scrolled to that concept. Inside the native scope `<dialog>` (Task 11) the icon is used **tooltip-only** (no `concept`), because an Umbraco modal would render behind the dialog's top layer.

- [ ] **Step 1: Create the element**

Create `src/help/uap-info-icon.element.ts`:
```ts
import { html, css, customElement, property } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UMB_MODAL_MANAGER_CONTEXT } from '@umbraco-cms/backoffice/modal';
import { UAP_HELP_MODAL } from './uap-help-modal.token.js';

/**
 * Small inline help affordance. Always shows `text` as a tooltip. When `concept`
 * is set, clicking opens the help modal scrolled to that concept's anchor.
 */
@customElement('uap-info-icon')
export class UapInfoIconElement extends UmbLitElement {
  /** Short tooltip text shown on hover/focus. */
  @property() text = '';

  /** Optional concept anchor id to deep-link into the concepts reference. Empty = tooltip only. */
  @property() concept = '';

  #modalManager: typeof UMB_MODAL_MANAGER_CONTEXT.TYPE | undefined = undefined;

  /** Wires the modal manager so the icon can open the help modal on click. */
  constructor() {
    super();
    this.consumeContext(UMB_MODAL_MANAGER_CONTEXT, (ctx) => {
      this.#modalManager = ctx ?? undefined;
    });
  }

  /**
   * Opens the help modal at this icon's concept. Stops propagation so the click
   * doesn't also trigger the surrounding cell/row handler.
   *
   * @param e The click event.
   */
  #onClick(e: Event): void {
    if (!this.concept || !this.#modalManager) return;
    e.stopPropagation();
    e.preventDefault();
    this.#modalManager.open(this, UAP_HELP_MODAL, {
      data: { scrollToConcept: this.concept },
    });
  }

  override render() {
    return html`
      <button
        type="button"
        class="info"
        title=${this.text}
        aria-label=${this.text}
        @click=${this.#onClick}>
        i
      </button>
    `;
  }

  static override styles = css`
    :host { display: inline-flex; vertical-align: middle; }
    .info {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 16px;
      height: 16px;
      border-radius: 50%;
      border: none;
      background: var(--uui-color-surface-emphasis, #f0f0f0);
      color: var(--uui-color-text-alt, #666);
      font-size: 11px;
      font-style: italic;
      font-family: Georgia, 'Times New Roman', serif;
      line-height: 1;
      padding: 0;
      cursor: help;
    }
    .info:hover {
      background: var(--uui-color-default, #3544b1);
      color: var(--uui-color-default-contrast, #fff);
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'uap-info-icon': UapInfoIconElement;
  }
}

export default UapInfoIconElement;
```

- [ ] **Step 2: Type-check** (will fail until Task 4 creates the token — that's expected ordering; do Task 4 next, then re-run)

Run: `npx tsc --noEmit`
Expected: FAIL with "Cannot find module './uap-help-modal.token.js'". Proceed to Task 4, which creates it.

- [ ] **Step 3: Commit** (after Task 4 type-check passes)

Deferred — commit `uap-info-icon.element.ts` together with Task 4 (they form one compiling unit).

---

## Task 4: `uap-help-modal` token, element, and manifest

**Files:**
- Create: `src/Umbraco.Community.AdvancedPermissions.Client/src/help/uap-help-modal.token.ts`
- Create: `src/Umbraco.Community.AdvancedPermissions.Client/src/help/uap-help-modal.element.ts`
- Modify: `src/Umbraco.Community.AdvancedPermissions.Client/src/manifests.ts`

- [ ] **Step 1: Create the token**

Create `src/help/uap-help-modal.token.ts`:
```ts
import { UmbModalToken } from '@umbraco-cms/backoffice/modal';
import type { HelpDocId } from './help-content.js';

/** Input for the help modal. All fields optional so any caller can open it (e.g. an inline ⓘ). */
export interface HelpModalData {
  /** Headline shown at the top of the modal (usually the surface name). */
  headline?: string;
  /** The per-page how-to document to show in "About this page". Omit to hide that tab. */
  howToDoc?: HelpDocId;
  /** Optional concept anchor id to scroll to on open (selects the Concepts tab). */
  scrollToConcept?: string;
}

/** The help modal returns no value. */
export type HelpModalValue = never;

/** Modal token for the Advanced Permissions help modal. */
export const UAP_HELP_MODAL = new UmbModalToken<HelpModalData, HelpModalValue>(
  'UAP.Modal.Help',
  { modal: { type: 'sidebar', size: 'medium' } },
);
```

- [ ] **Step 2: Create the element**

Create `src/help/uap-help-modal.element.ts`:
```ts
import {
  html,
  css,
  customElement,
  state,
  property,
  unsafeHTML,
} from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import type { UmbModalContext } from '@umbraco-cms/backoffice/modal';
import type { HelpModalData, HelpModalValue } from './uap-help-modal.token.js';
import { CONCEPTS_DOC, loadHelpDoc } from './help-content.js';
import { renderMarkdown } from './markdown.js';

/** Which tab of the help modal is active. */
type HelpTab = 'about' | 'concepts';

/**
 * Help modal: shows the per-page how-to ("About this page") and the shared concepts
 * reference ("Concepts"), both rendered from Markdown. Can deep-link to a concept anchor.
 */
@customElement('uap-help-modal')
export class UapHelpModalElement extends UmbLitElement {
  #localize = new UmbLocalizationController(this);

  /** Modal context injected by the modal manager. */
  @property({ attribute: false })
  modalContext?: UmbModalContext<HelpModalData, HelpModalValue>;

  @state() private _tab: HelpTab = 'about';
  @state() private _hasAbout = false;
  @state() private _aboutHtml = '';
  @state() private _conceptsHtml = '';
  @state() private _loading = true;

  /** Loads both documents and selects the starting tab. */
  override async connectedCallback(): Promise<void> {
    super.connectedCallback();
    const data = this.modalContext?.data;
    this._hasAbout = !!data?.howToDoc;

    const [about, concepts] = await Promise.all([
      data?.howToDoc ? loadHelpDoc(data.howToDoc) : Promise.resolve(''),
      loadHelpDoc(CONCEPTS_DOC),
    ]);
    this._aboutHtml = renderMarkdown(about);
    this._conceptsHtml = renderMarkdown(concepts);
    this._loading = false;

    this._tab = data?.scrollToConcept || !this._hasAbout ? 'concepts' : 'about';
    if (data?.scrollToConcept) {
      const concept = data.scrollToConcept;
      void this.updateComplete.then(() => this.#scrollToConcept(concept));
    }
  }

  /**
   * Scrolls the rendered concepts content to the anchor with the given id.
   *
   * @param id The concept anchor id (matches an `<a id="…">` in concepts.md).
   */
  #scrollToConcept(id: string): void {
    const target = this.renderRoot.querySelector(`#${CSS.escape(id)}`);
    target?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  /** Closes the modal. */
  #close(): void {
    this.modalContext?.submit();
  }

  override render() {
    const headline =
      this.modalContext?.data?.headline ?? this.#localize.term('uap_help_modalTitle');
    return html`
      <umb-body-layout headline=${headline}>
        <uui-tab-group slot="navigation">
          ${this._hasAbout
            ? html`<uui-tab
                label=${this.#localize.term('uap_help_tabAbout')}
                ?active=${this._tab === 'about'}
                @click=${() => {
                  this._tab = 'about';
                }}></uui-tab>`
            : ''}
          <uui-tab
            label=${this.#localize.term('uap_help_tabConcepts')}
            ?active=${this._tab === 'concepts'}
            @click=${() => {
              this._tab = 'concepts';
            }}></uui-tab>
        </uui-tab-group>

        <uui-box>
          ${this._loading
            ? html`<div class="center"><uui-loader></uui-loader></div>`
            : html`<div class="md">
                ${unsafeHTML(this._tab === 'about' ? this._aboutHtml : this._conceptsHtml)}
              </div>`}
        </uui-box>

        <div slot="actions">
          <uui-button
            look="primary"
            label=${this.#localize.term('uap_close')}
            @click=${this.#close}>
            ${this.#localize.term('uap_close')}
          </uui-button>
        </div>
      </umb-body-layout>
    `;
  }

  static override styles = css`
    :host { display: contents; }
    .center { display: flex; justify-content: center; padding: var(--uui-size-6); }
    .md { line-height: 1.6; }
    .md h1:first-child, .md h2:first-child { margin-top: 0; }
    .md h1, .md h2, .md h3 { margin-top: 1.2em; }
    .md code {
      background: var(--uui-color-surface-emphasis, #f0f0f0);
      padding: 1px 4px;
      border-radius: 3px;
    }
    .md table { border-collapse: collapse; width: 100%; }
    .md th, .md td {
      border: 1px solid var(--uui-color-border, #ddd);
      padding: 6px 8px;
      text-align: left;
    }
    .md a { color: var(--uui-color-interactive, #3544b1); }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'uap-help-modal': UapHelpModalElement;
  }
}

export default UapHelpModalElement;
```

> If `tsc` reports that `unsafeHTML` is not exported from `@umbraco-cms/backoffice/external/lit`, import it instead from `@umbraco-cms/backoffice/external/lit` is the canonical path used in this codebase for directives (see `repeat` in `role-picker-modal.element.ts`); should it still fail, change that one import to `import { unsafeHTML } from 'lit/directives/unsafe-html.js';`.

- [ ] **Step 3: Register the modal manifest**

In `src/manifests.ts`, find the User Picker Modal block (ends at the comment `// ─── Localization ───`). Insert this new manifest immediately **after** the User Picker Modal object and **before** the `// ─── Localization ───` comment:

Find:
```ts
  {
    type: 'modal',
    alias: 'UAP.Modal.UserPicker',
    name: 'Advanced Permissions User Picker Modal',
    element: () => import('./access-viewer/user-picker-modal.element.js'),
  },

  // ─── Localization ─────────────────────────────────────────────────────────
```
Replace with:
```ts
  {
    type: 'modal',
    alias: 'UAP.Modal.UserPicker',
    name: 'Advanced Permissions User Picker Modal',
    element: () => import('./access-viewer/user-picker-modal.element.js'),
  },

  // ─── Help Modal ───────────────────────────────────────────────────────────
  {
    type: 'modal',
    alias: 'UAP.Modal.Help',
    name: 'Advanced Permissions Help Modal',
    element: () => import('./help/uap-help-modal.element.js'),
  },

  // ─── Localization ─────────────────────────────────────────────────────────
```

- [ ] **Step 4: Type-check (Tasks 3 + 4 together)**

Run: `npx tsc --noEmit`
Expected: PASS. (The localization keys referenced here resolve at runtime, not compile time, so missing keys do not fail `tsc`.)

- [ ] **Step 5: Commit (Tasks 3 + 4)**

```bash
git add src/Umbraco.Community.AdvancedPermissions.Client/src/help/uap-info-icon.element.ts src/Umbraco.Community.AdvancedPermissions.Client/src/help/uap-help-modal.token.ts src/Umbraco.Community.AdvancedPermissions.Client/src/help/uap-help-modal.element.ts src/Umbraco.Community.AdvancedPermissions.Client/src/manifests.ts
git commit -m "feat(help): add help modal (token, element, manifest) and inline info icon"
```

---

## Task 5: `uap-page-help` description bar

**Files:**
- Create: `src/Umbraco.Community.AdvancedPermissions.Client/src/help/uap-page-help.element.ts`

- [ ] **Step 1: Create the element**

Create `src/help/uap-page-help.element.ts`:
```ts
import {
  html,
  css,
  nothing,
  customElement,
  property,
} from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import { UMB_MODAL_MANAGER_CONTEXT } from '@umbraco-cms/backoffice/modal';
import { SURFACE_HELP } from './help-content.js';
import { UAP_HELP_MODAL } from './uap-help-modal.token.js';

/**
 * Always-visible help bar mounted at the top of each editor/viewer. Shows a one-line
 * description and a "Learn more" button that opens the help modal for the surface.
 */
@customElement('uap-page-help')
export class UapPageHelpElement extends UmbLitElement {
  #localize = new UmbLocalizationController(this);

  /** The workspace entityType identifying which surface this bar belongs to. */
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

  /** Opens the help modal for this surface (About + Concepts). */
  #openModal(): void {
    const cfg = SURFACE_HELP[this.surface];
    if (!cfg || !this.#modalManager) return;
    this.#modalManager.open(this, UAP_HELP_MODAL, {
      data: { headline: this.headline, howToDoc: cfg.howToDoc },
    });
  }

  override render() {
    const cfg = SURFACE_HELP[this.surface];
    if (!cfg) return nothing;
    return html`
      <div class="bar">
        <umb-icon name="icon-info" class="lead-icon"></umb-icon>
        <span class="desc">${this.#localize.term(cfg.descriptionKey)}</span>
        <uui-button
          compact
          look="secondary"
          label=${this.#localize.term('uap_help_learnMore')}
          @click=${this.#openModal}>
          ${this.#localize.term('uap_help_learnMore')}
        </uui-button>
      </div>
    `;
  }

  static override styles = css`
    :host { display: block; }
    .bar {
      display: flex;
      align-items: center;
      gap: var(--uui-size-3, 9px);
      padding: var(--uui-size-2, 6px) var(--uui-size-6, 18px);
      background: var(--uui-color-surface-alt, #f7f7f9);
      border-bottom: 1px solid var(--uui-color-border, #e0e0e0);
      font-size: 13px;
      color: var(--uui-color-text-alt, #555);
    }
    .lead-icon { flex-shrink: 0; }
    .desc { flex: 1; }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'uap-page-help': UapPageHelpElement;
  }
}

export default UapPageHelpElement;
```

- [ ] **Step 2: Type-check**

Run: `npx tsc --noEmit`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add src/Umbraco.Community.AdvancedPermissions.Client/src/help/uap-page-help.element.ts
git commit -m "feat(help): add always-visible page help description bar"
```

---

## Task 6: Localization keys (en + nl)

**Files:**
- Modify: `src/Umbraco.Community.AdvancedPermissions.Client/src/localization/en.ts`
- Modify: `src/Umbraco.Community.AdvancedPermissions.Client/src/localization/nl.ts`

- [ ] **Step 1: Add English keys**

In `src/localization/en.ts`, find:
```ts
    umbracoUsers: 'All Users',
```
Replace with:
```ts
    umbracoUsers: 'All Users',

    // ── Help ──────────────────────────────────────────────────────────────
    help_contentPermissions_description: 'Manage Allow/Deny permissions per user group across your content tree.',
    help_accessViewer_description: 'See the effective permissions a user or group has at any node, with full reasoning.',
    help_learnMore: 'Learn more',
    help_modalTitle: 'Help',
    help_tabAbout: 'About this page',
    help_tabConcepts: 'Concepts',
    help_concept_scope_tip: 'Scope controls how far a rule reaches: this node, this node and its descendants, or descendants only.',
    help_concept_priorityOverride_tip: 'Priority override forces this entry to win over the normal resolution order.',
    help_concept_allowDeny_tip: 'Allow grants the permission, Deny revokes it, and leaving it unset inherits from the nearest ancestor.',
    help_concept_reasoning_tip: 'Click any cell to see exactly how the permission was resolved.',
```

- [ ] **Step 2: Add Dutch keys**

In `src/localization/nl.ts`, find:
```ts
    umbracoUsers: 'Alle gebruikers',
```
Replace with:
```ts
    umbracoUsers: 'Alle gebruikers',

    // ── Help ──────────────────────────────────────────────────────────────
    help_contentPermissions_description: 'Beheer Toestaan/Weigeren-machtigingen per gebruikersgroep over je contentboom.',
    help_accessViewer_description: 'Bekijk de effectieve machtigingen van een gebruiker of groep op elk knooppunt, met volledige redenering.',
    help_learnMore: 'Meer informatie',
    help_modalTitle: 'Help',
    help_tabAbout: 'Over deze pagina',
    help_tabConcepts: 'Concepten',
    help_concept_scope_tip: 'Bereik bepaalt hoe ver een regel reikt: dit knooppunt, dit knooppunt en onderliggende, of alleen onderliggende.',
    help_concept_priorityOverride_tip: 'Prioriteitsoverschrijving laat deze invoer winnen boven de normale oplosvolgorde.',
    help_concept_allowDeny_tip: 'Toestaan verleent de machtiging, Weigeren trekt die in, en niet instellen erft van de dichtstbijzijnde bovenliggende.',
    help_concept_reasoning_tip: 'Klik op een cel om te zien hoe de machtiging precies is opgelost.',
```

- [ ] **Step 3: Type-check**

Run: `npx tsc --noEmit`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add src/Umbraco.Community.AdvancedPermissions.Client/src/localization/en.ts src/Umbraco.Community.AdvancedPermissions.Client/src/localization/nl.ts
git commit -m "feat(help): add help localization keys (en, nl)"
```

---

## Task 7: Concepts document (en + nl)

**Files:**
- Create: `src/Umbraco.Community.AdvancedPermissions.Client/help-docs/en/concepts.md`
- Create: `src/Umbraco.Community.AdvancedPermissions.Client/help-docs/nl/concepts.md`

> The `<a id="…">` anchors must match the concept ids used by inline ⓘ deep-links: `allow-deny`, `scope`, `default-row`, `all-users`, `priority-override`, `resolution`.

- [ ] **Step 1: Create `help-docs/en/concepts.md`**

```markdown
# Permission concepts

Advanced Permissions builds on Umbraco's own permission model and adds finer-grained control. This page explains the core concepts shared by every editor and viewer.

<a id="allow-deny"></a>
## Allow, Deny and inherit

Every permission entry is either **Allow** or **Deny**. There is no separate "inherit" value: a permission you leave **unset** simply has no entry, and the node inherits whatever applies from its nearest ancestor (or the Default permissions row).

- **Allow** grants the permission.
- **Deny** actively revokes it — including a permission inherited from an ancestor.
- **Unset (inherit)** keeps whatever the node would otherwise inherit.

Unlike Umbraco's built-in permissions, each permission is independent: you can override just one on a node and let the rest keep inheriting.

<a id="scope"></a>
## Scope

Every entry carries a **scope** that controls how far it reaches:

- **This Node Only** — applies to the node itself, not its children.
- **This Node and Descendants** — applies to the node and everything beneath it.
- **Descendants Only** — applies to the children but not the node itself.

This lets you express patterns like "lock the branch root, but leave everything beneath it open" in a single entry.

<a id="default-row"></a>
## The Default permissions row

The **Default permissions row** at the top of the editors is a virtual root that sets a baseline for all content. Entries here apply everywhere unless a more specific entry overrides them.

<a id="all-users"></a>
## The All Users Group

The **All Users Group** is a baseline that reaches every backoffice user, whatever groups they belong to. Pair it with a Deny to lock a node down across the board with a single entry. Explicit group entries can still override it.

<a id="priority-override"></a>
## Priority Override

When a user belongs to several groups, those groups can disagree, and the normal order does not always land where you want. **Priority Override** is the deliberate escape hatch: flag the entry that should win, and it does — regardless of the user's other groups. Use it sparingly.

<a id="resolution"></a>
## How conflicts are resolved

When deciding whether a user has a permission on a node, the package gathers every applicable entry from every group the user belongs to (including the All Users Group), respects each entry's scope, and applies this order, highest first:

1. **Explicit Deny** overrides everything else.
2. **Explicit Allow** overrides any inherited permission.
3. **Inherited Deny** overrides an inherited Allow.
4. **Inherited Allow**.

If no entry applies anywhere up the tree, the default is **Deny** for content permissions (**Allow** for insert options). A Priority Override entry steps outside this order and wins.
```

- [ ] **Step 2: Create `help-docs/nl/concepts.md`**

```markdown
# Machtigingsconcepten

Advanced Permissions bouwt voort op het machtigingsmodel van Umbraco en voegt fijnmazigere controle toe. Deze pagina legt de kernconcepten uit die elke editor en weergave deelt.

<a id="allow-deny"></a>
## Toestaan, Weigeren en overerven

Elke machtigingsinvoer is **Toestaan** of **Weigeren**. Er is geen aparte "overerven"-waarde: een machtiging die je **niet instelt** heeft simpelweg geen invoer, en het knooppunt erft wat van toepassing is van de dichtstbijzijnde bovenliggende (of de rij Standaardmachtigingen).

- **Toestaan** verleent de machtiging.
- **Weigeren** trekt de machtiging actief in — ook een machtiging die van een bovenliggende werd geërfd.
- **Niet ingesteld (overerven)** behoudt wat het knooppunt anders zou erven.

Anders dan bij de ingebouwde machtigingen van Umbraco staat elke machtiging los: je kunt er één op een knooppunt overschrijven en de rest laten overerven.

<a id="scope"></a>
## Bereik

Elke invoer heeft een **bereik** dat bepaalt hoe ver de regel reikt:

- **Alleen dit knooppunt** — geldt voor het knooppunt zelf, niet voor de onderliggende.
- **Dit knooppunt en onderliggende** — geldt voor het knooppunt en alles eronder.
- **Alleen onderliggende** — geldt voor de onderliggende, maar niet voor het knooppunt zelf.

Hiermee druk je patronen uit als "vergrendel de takwortel, maar laat alles eronder open" in één invoer.

<a id="default-row"></a>
## De rij Standaardmachtigingen

De **rij Standaardmachtigingen** bovenaan de editors is een virtuele wortel die een basislijn instelt voor alle content. Invoeren hier gelden overal, tenzij een specifiekere invoer ze overschrijft.

<a id="all-users"></a>
## De groep Alle gebruikers

De **groep Alle gebruikers** is een basislijn die elke backoffice-gebruiker bereikt, ongeacht de groepen waartoe hij behoort. Combineer het met een Weigeren om een knooppunt in één invoer voor iedereen te vergrendelen. Expliciete groepsinvoeren kunnen het nog steeds overschrijven.

<a id="priority-override"></a>
## Prioriteitsoverschrijving

Als een gebruiker tot meerdere groepen behoort, kunnen die het oneens zijn, en de normale volgorde landt niet altijd waar je wilt. **Prioriteitsoverschrijving** is de bewuste noodoplossing: markeer de invoer die moet winnen, en die wint — ongeacht de andere groepen van de gebruiker. Gebruik het spaarzaam.

<a id="resolution"></a>
## Hoe conflicten worden opgelost

Bij het bepalen of een gebruiker een machtiging op een knooppunt heeft, verzamelt het pakket elke toepasselijke invoer van elke groep waartoe de gebruiker behoort (inclusief de groep Alle gebruikers), respecteert het bereik van elke invoer en past deze volgorde toe, hoogste eerst:

1. **Expliciet Weigeren** overschrijft al het andere.
2. **Expliciet Toestaan** overschrijft elke geërfde machtiging.
3. **Geërfd Weigeren** overschrijft een geërfd Toestaan.
4. **Geërfd Toestaan**.

Als nergens in de boom een invoer van toepassing is, is de standaard **Weigeren** voor contentmachtigingen (**Toestaan** voor invoegopties). Een prioriteitsoverschrijving stapt buiten deze volgorde en wint.
```

- [ ] **Step 3: Commit**

```bash
git add src/Umbraco.Community.AdvancedPermissions.Client/help-docs/en/concepts.md src/Umbraco.Community.AdvancedPermissions.Client/help-docs/nl/concepts.md
git commit -m "docs(help): add shared permission concepts reference (en, nl)"
```

---

## Task 8: Pilot how-to documents (en + nl)

**Files:**
- Create: `src/Umbraco.Community.AdvancedPermissions.Client/help-docs/en/content-permissions.md`
- Create: `src/Umbraco.Community.AdvancedPermissions.Client/help-docs/en/access-viewer.md`
- Create: `src/Umbraco.Community.AdvancedPermissions.Client/help-docs/nl/content-permissions.md`
- Create: `src/Umbraco.Community.AdvancedPermissions.Client/help-docs/nl/access-viewer.md`

- [ ] **Step 1: Create `help-docs/en/content-permissions.md`**

```markdown
# Content Permissions Editor

Manage all of a user group's permission entries across the content tree in one place.

## What it's for

Decide exactly what each user group may do on each content node — and how far each rule reaches — without the all-or-nothing limitation of Umbraco's built-in permissions.

## How to use it

1. **Pick a user group** from the toolbar.
2. **Browse the content tree.** The Default permissions row at the top sets a baseline for all content; the tree shows which nodes have entries defined.
3. **Click a cell** (a node × permission) to open the scope dialog.
4. **Choose the state** for this node and, if it should differ, a separate state for descendants. Optionally flag Priority Override.
5. **Save** your changes.

See [Permission concepts](concepts.md) for Allow/Deny, scope, the Default permissions row, the All Users Group, and Priority Override.

## Examples

**Lock a node against deletion.** Add one entry: All Users Group, Deny, Delete, scope "This Node Only". The Deny wins for everyone, whatever groups they belong to.

**Carve out an exception to a broad Allow.** Editors have Allow for Publish on Home with scope "This Node and Descendants". To stop publishing under the Press Releases branch while keeping every other permission, add a single Deny for Publish on Press Releases.
```

- [ ] **Step 2: Create `help-docs/en/access-viewer.md`**

```markdown
# Access Viewer

Answer the question "what can this user or group actually do here?" — with a full explanation.

## What it's for

The Access Viewer shows the **effective** (fully resolved) permission for any user or user group at any content node, and explains exactly how each result was reached.

## How to use it

1. **Pick a user or a user group** from the toolbar.
2. **Browse the content tree.** Each cell shows whether a permission is allowed or denied for the chosen subject.
3. **Click a cell** to open its reasoning chain: which group contributed, from which node, and whether it was explicit or inherited.
4. When a **Priority Override** changed the outcome, the reasoning shows what the result would have been without it.

See [Permission concepts](concepts.md) for how permissions are resolved.
```

- [ ] **Step 3: Create `help-docs/nl/content-permissions.md`**

```markdown
# Content Permissions Editor

Beheer alle machtigingsinvoeren van een gebruikersgroep over de contentboom op één plek.

## Waarvoor het dient

Bepaal precies wat elke gebruikersgroep op elk contentknooppunt mag — en hoe ver elke regel reikt — zonder de alles-of-niets-beperking van de ingebouwde machtigingen van Umbraco.

## Hoe je het gebruikt

1. **Kies een gebruikersgroep** in de werkbalk.
2. **Blader door de contentboom.** De rij Standaardmachtigingen bovenaan stelt een basislijn in voor alle content; de boom toont welke knooppunten invoeren hebben.
3. **Klik op een cel** (een knooppunt × machtiging) om het bereikdialoogvenster te openen.
4. **Kies de status** voor dit knooppunt en, indien afwijkend, een aparte status voor onderliggende. Markeer eventueel Prioriteitsoverschrijving.
5. **Sla** je wijzigingen op.

Zie [Machtigingsconcepten](concepts.md) voor Toestaan/Weigeren, bereik, de rij Standaardmachtigingen, de groep Alle gebruikers en Prioriteitsoverschrijving.

## Voorbeelden

**Een knooppunt vergrendelen tegen verwijderen.** Voeg één invoer toe: groep Alle gebruikers, Weigeren, Verwijderen, bereik "Alleen dit knooppunt". Het Weigeren wint voor iedereen.

**Een uitzondering maken op een breed Toestaan.** Editors hebben Toestaan voor Publiceren op Home met bereik "Dit knooppunt en onderliggende". Om publiceren onder de tak Persberichten te stoppen terwijl elke andere machtiging behouden blijft, voeg je één Weigeren voor Publiceren op Persberichten toe.
```

- [ ] **Step 4: Create `help-docs/nl/access-viewer.md`**

```markdown
# Access Viewer

Beantwoord de vraag "wat kan deze gebruiker of groep hier echt doen?" — met een volledige uitleg.

## Waarvoor het dient

De Access Viewer toont de **effectieve** (volledig opgeloste) machtiging voor elke gebruiker of gebruikersgroep op elk contentknooppunt, en legt precies uit hoe elk resultaat tot stand kwam.

## Hoe je het gebruikt

1. **Kies een gebruiker of een gebruikersgroep** in de werkbalk.
2. **Blader door de contentboom.** Elke cel toont of een machtiging is toegestaan of geweigerd voor het gekozen onderwerp.
3. **Klik op een cel** om de redeneerketen te openen: welke groep bijdroeg, vanaf welk knooppunt, en of het expliciet of geërfd was.
4. Wanneer een **Prioriteitsoverschrijving** het resultaat veranderde, toont de redenering wat het zonder die overschrijving zou zijn geweest.

Zie [Machtigingsconcepten](concepts.md) voor hoe machtigingen worden opgelost.
```

- [ ] **Step 5: Commit**

```bash
git add src/Umbraco.Community.AdvancedPermissions.Client/help-docs/en/content-permissions.md src/Umbraco.Community.AdvancedPermissions.Client/help-docs/en/access-viewer.md src/Umbraco.Community.AdvancedPermissions.Client/help-docs/nl/content-permissions.md src/Umbraco.Community.AdvancedPermissions.Client/help-docs/nl/access-viewer.md
git commit -m "docs(help): add pilot how-to docs for Content Permissions Editor and Access Viewer (en, nl)"
```

---

## Task 9: Wire the Content Permissions Editor

**Files:**
- Modify: `src/Umbraco.Community.AdvancedPermissions.Client/src/permissions-editor/uap-permissions-editor-root.element.ts`

- [ ] **Step 1: Add imports**

Find (near the bottom of the import block):
```ts
import '../components/uap-picker-button.element.js';
import '../shared/components/uap-perm-block.element.js';
import '../shared/components/uap-permission-scope-dialog.element.js';
```
Replace with:
```ts
import '../components/uap-picker-button.element.js';
import '../shared/components/uap-perm-block.element.js';
import '../shared/components/uap-permission-scope-dialog.element.js';
import '../help/uap-page-help.element.js';
import '../help/uap-info-icon.element.js';
```

- [ ] **Step 2: Mount the page-help bar and a toolbar concept icon**

Find:
```ts
      <umb-body-layout headline=${this.#localize.term('uap_editorHeadline')}>
        <div class="toolbar">
          <uap-picker-button
            label=${this.#localize.term('uap_chooseRole')}
            .selectedName=${this._selectedRole?.name ?? ''}
            icon="icon-users"
            @click=${() => void this.#openRolePicker()}>
          </uap-picker-button>
```
Replace with:
```ts
      <umb-body-layout headline=${this.#localize.term('uap_editorHeadline')}>
        <uap-page-help
          surface="uap-permissions-editor"
          headline=${this.#localize.term('uap_editorHeadline')}></uap-page-help>
        <div class="toolbar">
          <uap-picker-button
            label=${this.#localize.term('uap_chooseRole')}
            .selectedName=${this._selectedRole?.name ?? ''}
            icon="icon-users"
            @click=${() => void this.#openRolePicker()}>
          </uap-picker-button>
          <uap-info-icon
            text=${this.#localize.term('uap_help_concept_allowDeny_tip')}
            concept="allow-deny"></uap-info-icon>
```

- [ ] **Step 3: Type-check**

Run: `npx tsc --noEmit`
Expected: PASS.

- [ ] **Step 4: Build**

Run: `npm run build`
Expected: build succeeds; output written to `../Umbraco.Community.AdvancedPermissions/wwwroot/App_Plugins/...`.

- [ ] **Step 5: Commit**

```bash
git add src/Umbraco.Community.AdvancedPermissions.Client/src/permissions-editor/uap-permissions-editor-root.element.ts
git commit -m "feat(help): wire page help bar and concept icon into Content Permissions Editor"
```

---

## Task 10: Wire the Access Viewer

**Files:**
- Modify: `src/Umbraco.Community.AdvancedPermissions.Client/src/access-viewer/uap-access-viewer-root.element.ts`

- [ ] **Step 1: Add imports**

Find:
```ts
import '../components/uap-picker-button.element.js';
import '../shared/components/uap-perm-block.element.js';
import '../shared/components/uap-reasoning-dialog.element.js';
```
Replace with:
```ts
import '../components/uap-picker-button.element.js';
import '../shared/components/uap-perm-block.element.js';
import '../shared/components/uap-reasoning-dialog.element.js';
import '../help/uap-page-help.element.js';
import '../help/uap-info-icon.element.js';
```

- [ ] **Step 2: Mount the page-help bar and a toolbar concept icon**

Find:
```ts
      <umb-body-layout headline=${this.#localize.term('uap_viewerHeadline')}>
        <div class="toolbar">
          <uap-picker-button
            label=${this.#localize.term('uap_chooseRole')}
            .selectedName=${this._selectedRole?.name ?? ''}
            icon="icon-users"
            @click=${() => void this.#openRolePicker()}>
          </uap-picker-button>
          <span class="picker-or">${this.#localize.term('uap_subjectOr')}</span>
          <uap-picker-button
            label=${this.#localize.term('uap_chooseUser')}
            .selectedName=${this._selectedUser?.name ?? ''}
            icon="icon-user"
            @click=${() => void this.#openUserPicker()}>
          </uap-picker-button>
        </div>
```
Replace with:
```ts
      <umb-body-layout headline=${this.#localize.term('uap_viewerHeadline')}>
        <uap-page-help
          surface="uap-access-viewer"
          headline=${this.#localize.term('uap_viewerHeadline')}></uap-page-help>
        <div class="toolbar">
          <uap-picker-button
            label=${this.#localize.term('uap_chooseRole')}
            .selectedName=${this._selectedRole?.name ?? ''}
            icon="icon-users"
            @click=${() => void this.#openRolePicker()}>
          </uap-picker-button>
          <span class="picker-or">${this.#localize.term('uap_subjectOr')}</span>
          <uap-picker-button
            label=${this.#localize.term('uap_chooseUser')}
            .selectedName=${this._selectedUser?.name ?? ''}
            icon="icon-user"
            @click=${() => void this.#openUserPicker()}>
          </uap-picker-button>
          <uap-info-icon
            text=${this.#localize.term('uap_help_concept_reasoning_tip')}
            concept="resolution"></uap-info-icon>
        </div>
```

- [ ] **Step 3: Type-check**

Run: `npx tsc --noEmit`
Expected: PASS.

- [ ] **Step 4: Build**

Run: `npm run build`
Expected: build succeeds.

- [ ] **Step 5: Commit**

```bash
git add src/Umbraco.Community.AdvancedPermissions.Client/src/access-viewer/uap-access-viewer-root.element.ts
git commit -m "feat(help): wire page help bar and concept icon into Access Viewer"
```

---

## Task 11: Tooltip-only concept icons in the scope dialog

**Files:**
- Modify: `src/Umbraco.Community.AdvancedPermissions.Client/src/shared/components/uap-permission-scope-dialog.element.ts`

> **Important:** This dialog is a native `<dialog>` shown with `showModal()` (top layer). Icons here must be **tooltip-only** (no `concept` attribute) — an Umbraco modal would render *behind* the native dialog. The dialog is shared by several editors; tooltip-only icons are safe everywhere.

- [ ] **Step 1: Add the import**

Find:
```ts
import './uap-perm-block.element.js';
```
Replace with:
```ts
import './uap-perm-block.element.js';
import '../../help/uap-info-icon.element.js';
```

- [ ] **Step 2: Add a scope tooltip icon to the descendants section title**

Find:
```ts
        <div class="dialog-section">
          <h4>${this.#localize.term('uap_descendantsSection')}</h4>
          <div class="perm-options">
            ${this.#renderTile('inherit', descSelected === 'inherit', () => {
```
Replace with:
```ts
        <div class="dialog-section">
          <h4>
            ${this.#localize.term('uap_descendantsSection')}
            <uap-info-icon text=${this.#localize.term('uap_help_concept_scope_tip')}></uap-info-icon>
          </h4>
          <div class="perm-options">
            ${this.#renderTile('inherit', descSelected === 'inherit', () => {
```

- [ ] **Step 3: Replace the priority-override "?" with a tooltip icon**

Find:
```ts
          <span class="po-label">${this.#localize.term('uap_priorityOverride')}</span>
          <span class="po-help" title=${this.#localize.term('uap_priorityOverrideTooltip', this.verb, this.nodeName)}>?</span>
```
Replace with:
```ts
          <span class="po-label">${this.#localize.term('uap_priorityOverride')}</span>
          <uap-info-icon text=${this.#localize.term('uap_help_concept_priorityOverride_tip')}></uap-info-icon>
```

- [ ] **Step 4: Type-check**

Run: `npx tsc --noEmit`
Expected: PASS. (The now-unused `.po-help` CSS rule is harmless; leave it or remove it — removing keeps the file clean.)

- [ ] **Step 5: Build**

Run: `npm run build`
Expected: build succeeds.

- [ ] **Step 6: Commit**

```bash
git add src/Umbraco.Community.AdvancedPermissions.Client/src/shared/components/uap-permission-scope-dialog.element.ts
git commit -m "feat(help): add tooltip concept icons (scope, priority override) to scope dialog"
```

---

## Task 12: README Documentation section

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Add a Documentation section**

In `README.md`, find:
```markdown
## Feedback
```
Replace with:
```markdown
## Documentation

In-depth help is available both inside the backoffice (a description bar and a help modal on each editor/viewer) and as Markdown you can read here:

- [Permission concepts](src/Umbraco.Community.AdvancedPermissions.Client/help-docs/en/concepts.md) — Allow/Deny, scope, inheritance, the All Users Group, and Priority Override.
- [Content Permissions Editor](src/Umbraco.Community.AdvancedPermissions.Client/help-docs/en/content-permissions.md)
- [Access Viewer](src/Umbraco.Community.AdvancedPermissions.Client/help-docs/en/access-viewer.md)

Dutch translations live alongside, under `help-docs/nl/`. These files are the single source of truth for both the in-app help and this documentation.

## Feedback
```

- [ ] **Step 2: Commit**

```bash
git add README.md
git commit -m "docs: link in-app help documentation from README"
```

---

## Task 13: Final verification

**Files:** none (verification only)

- [ ] **Step 1: Full type-check and build**

Run (from `src/Umbraco.Community.AdvancedPermissions.Client/`):
```bash
npx tsc --noEmit && npm run build
```
Expected: both succeed with no errors.

- [ ] **Step 2: Manual verification on the TestSite**

Run (from `tests/Umbraco.Community.AdvancedPermissions.TestSite/`):
```bash
dotnet run --urls http://localhost:5000
```
Then in the backoffice (`http://localhost:5000/umbraco`, Users section):
- Open **Content Permissions Editor**: confirm the description bar shows under the headline; click **Learn more** → modal opens with "About this page" + "Concepts" tabs, Markdown rendered. Click the toolbar ⓘ → modal opens on the **Concepts** tab scrolled to "Allow, Deny and inherit".
- Click a permission cell → scope dialog opens; confirm the ⓘ next to "Descendants (if different)" and next to "Priority override" show tooltips on hover and do **not** open a modal behind the dialog.
- Open **Access Viewer**: confirm the description bar and the toolbar ⓘ (opens Concepts at "How conflicts are resolved").
- Switch backoffice language to Dutch (user profile) and reload: confirm descriptions, tooltips, and modal content appear in Dutch.

- [ ] **Step 3: Update the GitHub issue checklist**

Tick the completed items on [issue #30](https://github.com/Luuk1983/Umbraco.Community.AdvancedPermissions/issues/30):
```bash
gh issue view 30 --repo Luuk1983/Umbraco.Community.AdvancedPermissions
```
Edit the issue body (or comment) to check off the delivered items.

- [ ] **Step 4: Final confirmation**

No commit needed if Steps 1–2 produced no changes. If the build emitted updated `wwwroot` assets that are tracked by git, commit them:
```bash
git status
git add -A
git commit -m "build(help): rebuild backoffice assets for in-app help system"
```
(If `wwwroot` build output is git-ignored, skip this.)

---

## Self-Review

**Spec coverage:**
- Always-visible description bar → Task 5 (`uap-page-help`), mounted in Tasks 9–10. ✅
- Help modal with About + Concepts → Task 4. ✅
- Inline ⓘ icons, both deep-link and tooltip → Task 3 (element), Tasks 9–10 (deep-link in toolbars), Task 11 (tooltip in scope dialog). ✅
- Concepts both inline and centralized → Tasks 3, 4, 7, 11. ✅
- Hybrid content: short strings as keys (Task 6), long-form as Markdown (Tasks 7–8). ✅
- Markdown doubles as GitHub docs → Tasks 7–8 (files), Task 12 (README links). ✅
- en + nl → Tasks 6, 7, 8. ✅
- Pilot = Content Permissions Editor + Access Viewer → Tasks 9–10. ✅
- README terminology ("All Users Group", "Default permissions row") → used in Tasks 7–8. ✅
- Markdown renderer availability risk → resolved by bundling `marked` (Task 1). ✅
- On-demand doc loading → `import.meta.glob` lazy loaders (Task 2). ✅

**Placeholder scan:** No TBD/TODO; all code and content provided in full. ✅

**Type consistency:** `HelpDocId`, `SurfaceHelp`, `SURFACE_HELP`, `CONCEPTS_DOC`, `loadHelpDoc` (Task 2) are used consistently in Tasks 3–5. `HelpModalData` (optional `headline`/`howToDoc`/`scrollToConcept`) matches usage in `uap-info-icon` (`scrollToConcept` only), `uap-page-help` (`headline` + `howToDoc`), and the modal element. Concept anchor ids (`allow-deny`, `scope`, `priority-override`, `resolution`) used in Tasks 9–11 all exist as `<a id="…">` in Task 7's `concepts.md`. ✅
