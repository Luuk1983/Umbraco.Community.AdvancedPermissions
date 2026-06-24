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

  /**
   * True when the call-to-action should stack each group on its own row: only for a genuine
   * and/or mix — more than one group AND at least one group is mutually exclusive (has an "or").
   * Simple cases (a single group, or several both-required single-option groups) stay inline.
   */
  get #stacked(): boolean {
    return this.groups.length > 1 && this.groups.some((g) => g.options.length > 1);
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

  /**
   * Renders a group's controls. When nothing is selected, every option is a "Choose …" button
   * joined by the "or" separator. When an option IS selected, its pill is shown — plus, for a
   * mutually-exclusive group, the remaining option(s) as "Choose …" buttons so the user can still
   * switch type (e.g. swap a chosen user for a user group) in both the empty and the results view.
   */
  #groupControls(g: UapSelectorGroup): TemplateResult {
    const selected = g.options.find((o) => o.selectedName);
    if (!selected) {
      return html`${g.options.map(
        (o, i) => html`${i > 0 ? html`<span class="or">${this.orLabel}</span>` : nothing}${this.#placeholder(o)}`,
      )}`;
    }
    const others = g.options.filter((o) => o !== selected);
    return html`${this.#pill(selected)}${others.map(
      (o) => html`<span class="or">${this.orLabel}</span>${this.#placeholder(o)}`,
    )}`;
  }

  override render() {
    if (!this.#complete) {
      return html`
        <uui-box>
          <div class="cta">
            <umb-icon name=${this.ctaIcon} class="cta-icon"></umb-icon>
            <p class="cta-text">${this.promptText}</p>
            <div class="cta-ctrls ${this.#stacked ? 'stacked' : ''}">
              ${this.groups.map((g) => html`<div class="cta-slot">${this.#groupControls(g)}</div>`)}
            </div>
          </div>
        </uui-box>
      `;
    }
    return html`
      <uui-box>
        <div class="bar">
          <div class="pills">
            ${this.groups.map((g) => this.#groupControls(g))}
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
    .cta-ctrls.stacked { flex-direction: column; }
    .cta-slot {
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
