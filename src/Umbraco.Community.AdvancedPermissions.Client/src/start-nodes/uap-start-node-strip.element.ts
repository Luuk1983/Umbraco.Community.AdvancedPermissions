import { html, css, nothing, customElement, property } from '@umbraco-cms/backoffice/external/lit';
import type { TemplateResult } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import type { StartNodeTree, StartNodesResponse } from '../models/permission.models.js';
import { startNodeLines, startNodeSourceTerm, stripIntroTerm } from './start-node-display.js';
import type { StartNodeLine } from './start-node-display.js';

/**
 * The start-node card shown under the subject picker in all four viewers. It opens with one sentence
 * on whether start nodes limit the subject at all, then lists only the start nodes in effect, each with
 * who set it. Configured start nodes that Umbraco dropped change nothing, so they are not shown.
 * `tree` picks the content or the Library wording.
 */
@customElement('uap-start-node-strip')
export class UapStartNodeStripElement extends UmbLitElement {
  /** The subject's start nodes; null while loading or after a failure. */
  @property({ attribute: false }) data: StartNodesResponse | null = null;

  /** True while the start nodes load for a newly selected subject. */
  @property({ type: Boolean }) loading = false;

  /** True when the start nodes could not be loaded. */
  @property({ type: Boolean }) error = false;

  /** Whether the subject is a single user group (shows the combination note) or a user. */
  @property({ attribute: 'subject-kind' }) subjectKind: 'role' | 'user' = 'user';

  /** Which start nodes the card is about: the content tree's or the Library's. */
  @property() tree: StartNodeTree = 'content';

  /** One extra line under the start nodes, as a localization key (e.g. the Library create note); null for none. */
  @property({ attribute: false }) note: string | null = null;

  /** The localization controller for this element. */
  #localize = new UmbLocalizationController(this);

  override render(): TemplateResult | typeof nothing {
    if (this.error) {
      return html`<div class="strip"><span class="muted">${this.#localize.term('uap_startNodes_error')}</span></div>`;
    }
    if (this.loading || !this.data) {
      return this.loading
        ? html`<div class="strip"><span class="muted">${this.#localize.term('uap_startNodes_loading')}</span></div>`
        : nothing;
    }

    const data = this.data;
    const lines = startNodeLines(data);

    return html`
      <div class="strip">
        <p class="intro">
          <umb-icon name="icon-flag"></umb-icon>
          <span>${this.#localize.term(stripIntroTerm(data.state, this.subjectKind, this.tree))}</span>
        </p>
        ${lines.length > 0 ? html`<ul class="lines">${lines.map((l) => this.#renderLine(l))}</ul>` : nothing}
        ${this.note ? html`<p class="note">${this.#localize.term(this.note)}</p>` : nothing}
        ${this.subjectKind === 'role' && data.state !== 'NotApplicable'
          ? html`<p class="note">${this.#localize.term('uap_startNodes_roleModeNote')}</p>`
          : nothing}
      </div>
    `;
  }

  /**
   * Renders one start node in effect, with who set it.
   * @param line The start-node line.
   * @returns The list item.
   */
  #renderLine(line: StartNodeLine): TemplateResult {
    const source = startNodeSourceTerm(line);
    return html`
      <li>
        <span class="chip"><umb-icon name="icon-flag"></umb-icon>${line.name}</span>
        ${source ? html`<span class="source">${this.#localize.term(source.key, ...source.args)}</span>` : nothing}
      </li>
    `;
  }

  static override styles = css`
    :host { display: block; }
    .strip {
      padding: 8px 12px;
      margin-bottom: 12px;
      border: 1px solid var(--uui-color-border, #ddd);
      border-radius: 6px;
      background: var(--uui-color-surface-alt, #f7f7f9);
      font-size: 13px;
    }
    .intro { display: flex; align-items: flex-start; gap: 6px; margin: 0; }
    .intro umb-icon { flex-shrink: 0; margin-top: 2px; color: var(--uui-color-interactive, #3544b1); }
    .lines { list-style: none; display: flex; flex-wrap: wrap; gap: 6px 16px; margin: 6px 0 0; padding: 0 0 0 20px; }
    .lines li { display: inline-flex; align-items: center; gap: 6px; }
    .chip {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      padding: 0 8px;
      border-radius: 10px;
      background: var(--uui-color-surface, #fff);
      border: 1px solid var(--uui-color-border, #ddd);
      font-weight: 600;
    }
    .chip umb-icon { color: var(--uui-color-interactive, #3544b1); }
    .muted, .source, .note { color: var(--uui-color-text-alt, #666); }
    .note { margin: 6px 0 0 20px; font-style: italic; }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'uap-start-node-strip': UapStartNodeStripElement;
  }
}
