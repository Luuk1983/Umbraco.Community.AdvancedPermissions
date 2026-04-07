import { html, css, customElement, property, nothing } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';

/**
 * Picker button — shows a placeholder button when nothing is selected,
 * or an outline button with an icon and the selected name when a selection exists.
 * Click handling is left to the parent via standard DOM @click.
 */
@customElement('uap-picker-button')
export class UapPickerButtonElement extends UmbLitElement {
  /** Placeholder text shown when nothing is selected. */
  @property() label = '';

  /** Name of the currently selected item. When set, switches to selected appearance. */
  @property() selectedName = '';

  /** Umbraco icon name shown next to the selected name (e.g. 'icon-users'). */
  @property() icon = '';

  override render() {
    if (this.selectedName) {
      return html`
        <uui-button look="outline" label=${this.selectedName}>
          ${this.icon ? html`<uui-icon name=${this.icon}></uui-icon>` : nothing}
          ${this.selectedName}
        </uui-button>
      `;
    }
    return html`
      <uui-button look="placeholder" label=${this.label}>
        ${this.label}
      </uui-button>
    `;
  }

  static override styles = css`
    :host {
      display: contents;
    }

    uui-button {
      min-width: 200px;
    }
  `;
}

export default UapPickerButtonElement;

declare global {
  interface HTMLElementTagNameMap {
    'uap-picker-button': UapPickerButtonElement;
  }
}
