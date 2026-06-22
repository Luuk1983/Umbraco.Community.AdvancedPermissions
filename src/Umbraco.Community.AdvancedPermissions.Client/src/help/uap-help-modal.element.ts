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
