import {
  html,
  css,
  nothing,
  customElement,
  property,
  state,
  query,
} from '@umbraco-cms/backoffice/external/lit';
import type { TemplateResult, PropertyValues } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import { composeEntries, type PendingVerbEntries } from '../../utils/compose-entries.js';
import type { CellInfo } from '../../utils/cell-info.js';
import type { PermissionScope } from '../../models/permission.models.js';
import './uap-perm-block.element.js';

/**
 * Tri-state choice on a single scope row.
 */
type TriState = 'inherit' | 'allow' | 'deny';

/**
 * Shared scope-picker dialog used by both the Permissions Editor and the new doc-type
 * Permissions Editor. The dialog owns its in-progress state (`_nodeState`, `_descState`,
 * `_sameAsNode`). The parent opens it via `open()` after setting the initial-state props,
 * and listens for the `uap-scope-apply` custom event to receive the composed entries.
 */
@customElement('uap-permission-scope-dialog')
export class UapPermissionScopeDialogElement extends UmbLitElement {
  #localize = new UmbLocalizationController(this);

  /** The action label used in the dialog headline ("Read", "Create News Article", …). */
  @property() verb = '';

  /** The node name used in the dialog headline. */
  @property() nodeName = '';

  /**
   * When true, the dialog renders the simple virtual-root layout (one row of three tiles).
   * When false, it renders the full node + descendants layout.
   */
  @property({ type: Boolean }) isVirtualRoot = false;

  /**
   * Label override for the non-virtual-root "Inherit" tile. Defaults to the `uap_inherit`
   * localization. The doc-type editor passes `"Not set"` instead because doc-type rows
   * don't inherit through the content tree the way node-level rows do.
   */
  @property() inheritLabel = '';

  /**
   * Whether the "this node" side can be set. Defaults to true. The library editor sets this false for
   * element-only verbs on a folder (the folder itself isn't an element, but the rule still applies to
   * the elements inside it), collapsing the dialog to a single descendants-scoped choice.
   */
  @property({ type: Boolean }) nodeApplicable = true;

  /**
   * Whether the "descendants" side can be set. Defaults to true. The library editor sets this false for
   * leaf elements (which have no descendants), collapsing the dialog to a single this-node choice.
   */
  @property({ type: Boolean }) descApplicable = true;

  /**
   * Initial state pushed from the parent each time the dialog opens. Internal state is
   * resynced from these via `willUpdate`, so the parent can mutate them between opens.
   */
  @property({ attribute: false }) initialNodeState: TriState = 'inherit';
  @property({ attribute: false }) initialDescState: TriState = 'inherit';
  @property({ attribute: false }) initialSameAsNode = true;

  /**
   * Initial values of the per-column priority-override checkboxes. Each side can be flagged
   * independently. A side's checkbox is only enabled when that side actually has a rule
   * (its tri-state isn't Inherit, and for the descendant side, only when it differs from the node).
   */
  @property({ attribute: false }) initialNodeIsPriorityOverride = false;
  @property({ attribute: false }) initialDescIsPriorityOverride = false;

  @state() private _nodeState: TriState = 'inherit';
  @state() private _descState: TriState = 'inherit';
  @state() private _sameAsNode = true;
  @state() private _nodeIsPriorityOverride = false;
  @state() private _descIsPriorityOverride = false;

  @query('dialog') private _dialog!: HTMLDialogElement;

  /**
   * Opens the modal. Call after setting the initial-state properties so the dialog reflects
   * the cell that was just clicked.
   */
  open(): void {
    void this.updateComplete.then(() => this._dialog.showModal());
  }

  override willUpdate(changed: PropertyValues): void {
    if (changed.has('initialNodeState')) {
      this._nodeState = this.initialNodeState;
    }
    if (changed.has('initialDescState')) {
      this._descState = this.initialDescState;
    }
    if (changed.has('initialSameAsNode')) {
      this._sameAsNode = this.initialSameAsNode;
    }
    if (changed.has('initialNodeIsPriorityOverride')) {
      this._nodeIsPriorityOverride = this.initialNodeIsPriorityOverride;
    }
    if (changed.has('initialDescIsPriorityOverride')) {
      this._descIsPriorityOverride = this.initialDescIsPriorityOverride;
    }
  }

  /**
   * Whether the dialog renders a single row of tiles rather than the node + descendants split. True for
   * the virtual root and for any cell where only one side applies (a leaf element, or an element-only
   * verb on a folder).
   */
  get #isSingle(): boolean {
    return this.isVirtualRoot || !this.nodeApplicable || !this.descApplicable;
  }

  /**
   * The scope a single-row choice maps to: the virtual root and leaf elements use
   * <c>ThisNodeAndDescendants</c> / <c>ThisNodeOnly</c>; an element-only verb on a folder applies to the
   * items inside, so it uses <c>DescendantsOnly</c>.
   */
  get #singleScope(): PermissionScope {
    if (this.isVirtualRoot) return 'ThisNodeAndDescendants';
    if (!this.descApplicable) return 'ThisNodeOnly';
    return 'DescendantsOnly';
  }

  /**
   * True when the dialog represents an element-only verb on a folder: the verb has no meaning on the
   * folder node itself, so the single choice applies to the elements inside it (descendants only). This
   * drives both the two-part (N/A on the folder + chosen state on the items inside) result preview and
   * the wording, so it doesn't read as an ordinary "no permission set on this node".
   */
  get #isElementOnlyFolder(): boolean {
    return !this.isVirtualRoot && !this.nodeApplicable && this.descApplicable;
  }

  /**
   * Builds the entries the parent should persist and emits them on `uap-scope-apply`.
   * Single-row layouts emit one entry with `#singleScope`; the split layout uses `composeEntries`.
   */
  #apply(): void {
    // A side's flag is only meaningful when that side has a rule. Force false otherwise.
    const nodeFlag = this._nodeState === 'inherit' ? false : this._nodeIsPriorityOverride;
    const descEnabled = !this._sameAsNode && this._descState !== 'inherit';
    const descFlag = descEnabled ? this._descIsPriorityOverride : false;

    let entries: PendingVerbEntries;
    if (this.#isSingle) {
      entries = this._nodeState === 'inherit'
        ? []
        : [{
            state: this._nodeState === 'allow' ? 'Allow' : 'Deny',
            scope: this.#singleScope,
            isPriorityOverride: nodeFlag,
          }];
    } else {
      entries = composeEntries(this._nodeState, this._descState, this._sameAsNode, nodeFlag, descFlag);
    }

    this.dispatchEvent(new CustomEvent('uap-scope-apply', {
      detail: { entries },
      bubbles: true,
      composed: true,
    }));
    this._dialog.close();
  }

  /**
   * Computes the preview cell info from the current dialog state.
   */
  #previewInfo(): CellInfo {
    const nodeOverride = this._nodeState !== 'inherit' && this._nodeIsPriorityOverride;
    const descEnabled = !this._sameAsNode && this._descState !== 'inherit';
    const descOverride = descEnabled && this._descIsPriorityOverride;

    if (this.#isSingle) {
      // Element-only verb on a folder: the result is genuinely two-part — N/A on the folder itself,
      // the chosen state on the items inside — so mirror that here instead of a single uniform block.
      if (this.#isElementOnlyFolder) {
        return { split: true, nodeNa: true, nodeClass: 'inherit', descClass: this._nodeState, descOverride: nodeOverride };
      }
      return { split: false, nodeClass: this._nodeState, descClass: this._nodeState, nodeOverride, descOverride: nodeOverride };
    }
    const effectiveDesc = this._sameAsNode ? this._nodeState : this._descState;
    const effectiveDescOverride = this._sameAsNode ? nodeOverride : descOverride;
    // Uniform only when state AND override flag match; otherwise the two sides are distinct.
    if (this._nodeState === effectiveDesc && nodeOverride === effectiveDescOverride) {
      return { split: false, nodeClass: this._nodeState, descClass: this._nodeState, nodeOverride, descOverride: nodeOverride };
    }
    return { split: true, nodeClass: this._nodeState, descClass: effectiveDesc, nodeOverride, descOverride: effectiveDescOverride };
  }

  /**
   * Builds the human-readable preview description for the current dialog state.
   */
  #previewDescription(): string {
    const nodeOverride = this._nodeState !== 'inherit' && this._nodeIsPriorityOverride;

    if (this.#isSingle) {
      if (this._nodeState === 'inherit') {
        if (this.#isElementOnlyFolder) return this.#localize.term('uap_previewElementFolderInherit');
        return this.#localize.term(this.isVirtualRoot ? 'uap_previewVirtualInherit' : 'uap_previewBothInherit');
      }
      const action = this._nodeState === 'allow'
        ? this.#localize.term('uap_allow')
        : this.#localize.term('uap_deny');
      let base: string;
      if (this.isVirtualRoot) base = this.#localize.term('uap_previewVirtualSet', action);
      else if (this.#isElementOnlyFolder) base = this.#localize.term('uap_previewElementFolderSet', action);
      else if (!this.descApplicable) base = this.#localize.term('uap_previewNodeOnly', action);
      else base = this.#localize.term('uap_previewDescOnly', action);
      return this.#appendOverrideNote(base, nodeOverride, nodeOverride);
    }

    const effectiveDesc = this._sameAsNode ? this._nodeState : this._descState;
    const descEnabled = !this._sameAsNode && this._descState !== 'inherit';
    const descOverride = descEnabled && this._descIsPriorityOverride;
    const effectiveDescOverride = this._sameAsNode ? nodeOverride : descOverride;

    if (this._nodeState === 'inherit' && effectiveDesc === 'inherit') {
      return this.#localize.term('uap_previewBothInherit');
    }

    const stateLabel = (s: string) => s === 'allow'
      ? this.#localize.term('uap_allow')
      : this.#localize.term('uap_deny');

    let base: string;
    if (this._nodeState === effectiveDesc && nodeOverride === effectiveDescOverride) {
      base = this.#localize.term('uap_previewUniform', stateLabel(this._nodeState));
    } else if (this._nodeState !== 'inherit' && effectiveDesc === 'inherit') {
      base = this.#localize.term('uap_previewNodeOnly', stateLabel(this._nodeState));
    } else if (this._nodeState === 'inherit' && effectiveDesc !== 'inherit') {
      base = this.#localize.term('uap_previewDescOnly', stateLabel(effectiveDesc));
    } else {
      base = this.#localize.term('uap_previewSplit', stateLabel(this._nodeState), stateLabel(effectiveDesc));
    }

    return this.#appendOverrideNote(base, nodeOverride, effectiveDescOverride);
  }

  /**
   * Appends a priority-override clarification to the preview description, naming which side(s)
   * carry the flag. Returns the base unchanged when neither side is flagged.
   */
  #appendOverrideNote(base: string, nodeOverride: boolean, descOverride: boolean): string {
    let note = '';
    if (nodeOverride && descOverride) note = this.#localize.term('uap_previewPriorityBoth');
    else if (nodeOverride) note = this.#localize.term('uap_previewPriorityNode');
    else if (descOverride) note = this.#localize.term('uap_previewPriorityDesc');
    return note ? `${base} ${note}` : base;
  }

  /**
   * Renders one option tile (Inherit / Allow / Deny) with its icon and label.
   */
  #renderTile(
    type: TriState,
    selected: boolean,
    onClick: () => void,
    label?: string,
  ): TemplateResult {
    const icons: Record<TriState, string> = {
      inherit: '—',
      allow: '✓',
      deny: '✗',
    };
    const defaultLabels: Record<TriState, string> = {
      inherit: this.inheritLabel || this.#localize.term('uap_inherit'),
      allow: this.#localize.term('uap_allow'),
      deny: this.#localize.term('uap_deny'),
    };
    return html`
      <button
        class="perm-option ${type}${selected ? ' selected' : ''}"
        type="button"
        @click=${onClick}>
        <span class="perm-option-icon">${icons[type]}</span>
        <span class="perm-option-label">${label ?? defaultLabels[type]}</span>
      </button>
    `;
  }

  /**
   * Renders the single-row layout (one row of three tiles + preview), used by the virtual root and by
   * cells where only one side applies. Virtual-root cells use the "(all content)" labels; other single
   * cells use the plain Inherit/Allow/Deny labels.
   */
  #renderSingleOptions(): TemplateResult {
    const vr = this.isVirtualRoot;
    return html`
      <div class="dialog-options">
        <div class="perm-options">
          ${this.#renderTile('inherit', this._nodeState === 'inherit',
            () => { this._nodeState = 'inherit'; },
            vr ? this.#localize.term('uap_virtualRootInherit') : undefined)}
          ${this.#renderTile('allow', this._nodeState === 'allow',
            () => { this._nodeState = 'allow'; },
            vr ? this.#localize.term('uap_virtualRootAllow') : undefined)}
          ${this.#renderTile('deny', this._nodeState === 'deny',
            () => { this._nodeState = 'deny'; },
            vr ? this.#localize.term('uap_virtualRootDeny') : undefined)}
        </div>
        ${this.#renderOverrideCheckbox('node')}
      </div>
      <div class="dialog-result">
        <h4>${this.#localize.term('uap_dialogResult')}</h4>
        ${this.#renderPreview()}
      </div>
    `;
  }

  /**
   * Renders the full layout with separate node + descendants sections.
   */
  #renderNodeOptions(): TemplateResult {
    const descSelected: TriState | null = this._sameAsNode ? null : this._descState;
    return html`
      <div class="dialog-sections">
        <div class="dialog-section">
          <h4 class="dialog-section-title" title=${this.nodeName}>${this.nodeName}</h4>
          <div class="perm-options">
            ${this.#renderTile('inherit', this._nodeState === 'inherit',
              () => { this._nodeState = 'inherit'; })}
            ${this.#renderTile('allow', this._nodeState === 'allow',
              () => { this._nodeState = 'allow'; })}
            ${this.#renderTile('deny', this._nodeState === 'deny',
              () => { this._nodeState = 'deny'; })}
          </div>
          ${this.#renderOverrideCheckbox('node')}
        </div>
        <div class="dialog-section">
          <h4>${this.#localize.term('uap_descendantsSection')}</h4>
          <div class="perm-options">
            ${this.#renderTile('inherit', descSelected === 'inherit', () => {
              if (descSelected === 'inherit') { this._sameAsNode = true; }
              else { this._sameAsNode = false; this._descState = 'inherit'; }
            })}
            ${this.#renderTile('allow', descSelected === 'allow', () => {
              if (descSelected === 'allow') { this._sameAsNode = true; }
              else { this._sameAsNode = false; this._descState = 'allow'; }
            })}
            ${this.#renderTile('deny', descSelected === 'deny', () => {
              if (descSelected === 'deny') { this._sameAsNode = true; }
              else { this._sameAsNode = false; this._descState = 'deny'; }
            })}
          </div>
          ${this.#renderOverrideCheckbox('desc')}
        </div>
      </div>
      <div class="dialog-result">
        <h4>${this.#localize.term('uap_dialogResult')}</h4>
        ${this.#renderPreview()}
      </div>
    `;
  }

  /**
   * Renders the folder + element-only-verb layout. It keeps the familiar two-panel shape, but the node
   * (left) panel can't be set — it carries an N/A message explaining the verb doesn't apply to a folder —
   * while the descendants (right) panel offers the direct Inherit/Allow/Deny choice that applies to the
   * elements inside the folder. The chosen value lives in `_nodeState` (the single-choice field) and is
   * persisted as a DescendantsOnly entry by {@link #apply}; there is no "same as node" toggle because
   * there's no node rule for the descendants to match.
   */
  #renderElementFolderOptions(): TemplateResult {
    return html`
      <div class="dialog-sections">
        <div class="dialog-section">
          <h4 class="dialog-section-title" title=${this.nodeName}>${this.nodeName}</h4>
          <div class="na-panel">
            <span class="na-chip">${this.#localize.term('uap_naChip')}</span>
            <p class="na-text">${this.#localize.term('uap_dialogElementFolderNote', this.verb)}</p>
          </div>
        </div>
        <div class="dialog-section">
          <h4>${this.#localize.term('uap_elementFolderDescSection')}</h4>
          <div class="perm-options">
            ${this.#renderTile('inherit', this._nodeState === 'inherit',
              () => { this._nodeState = 'inherit'; })}
            ${this.#renderTile('allow', this._nodeState === 'allow',
              () => { this._nodeState = 'allow'; })}
            ${this.#renderTile('deny', this._nodeState === 'deny',
              () => { this._nodeState = 'deny'; })}
          </div>
          ${this.#renderOverrideCheckbox('node')}
        </div>
      </div>
      <div class="dialog-result">
        <h4>${this.#localize.term('uap_dialogResult')}</h4>
        ${this.#renderPreview()}
      </div>
    `;
  }

  /**
   * Renders the preview block + description.
   */
  #renderPreview(): TemplateResult {
    return html`
      <div class="preview-content">
        <div class="preview-block-wrapper">
          <uap-perm-block .info=${this.#previewInfo()}></uap-perm-block>
        </div>
        <p class="preview-desc">${this.#previewDescription()}</p>
      </div>
    `;
  }

  override render(): TemplateResult {
    return html`
      <dialog class="scope-dialog">
        <uui-dialog-layout
          headline=${this.#localize.term('uap_dialogHeadline', this.verb, this.nodeName)}>
          ${!this.#isSingle
            ? html`<p class="dialog-instructions">${this.#localize.term('uap_dialogInstructions')}</p>`
            : nothing}
          ${this.#isElementOnlyFolder
            ? this.#renderElementFolderOptions()
            : this.#isSingle
              ? this.#renderSingleOptions()
              : this.#renderNodeOptions()}

          <div slot="actions">
            <uui-button label=${this.#localize.term('uap_cancel')} look="outline" @click=${() => this._dialog.close()}>
              ${this.#localize.term('uap_cancel')}
            </uui-button>
            <uui-button label=${this.#localize.term('uap_apply')} look="primary" color="positive" @click=${() => this.#apply()}>
              ${this.#localize.term('uap_apply')}
            </uui-button>
          </div>
        </uui-dialog-layout>
      </dialog>
    `;
  }

  /**
   * Renders the per-side "Priority override" checkbox. Each side is independent:
   * - 'node': enabled when the node tri-state isn't Inherit.
   * - 'desc': enabled only when there's a distinct descendant rule (not "same as node" and not Inherit).
   * A disabled checkbox is greyed because the flag has no rule to attach to on that side.
   */
  #renderOverrideCheckbox(side: 'node' | 'desc'): TemplateResult {
    const disabled = side === 'node'
      ? this._nodeState === 'inherit'
      : (this._sameAsNode || this._descState === 'inherit');
    const checked = !disabled && (side === 'node' ? this._nodeIsPriorityOverride : this._descIsPriorityOverride);
    return html`
      <div class="priority-override ${checked ? 'active' : ''}">
        <label class="po-row ${disabled ? 'disabled' : ''}">
          <input
            type="checkbox"
            .checked=${checked}
            ?disabled=${disabled}
            @change=${(e: Event) => {
              const value = (e.target as HTMLInputElement).checked;
              if (side === 'node') { this._nodeIsPriorityOverride = value; }
              else { this._descIsPriorityOverride = value; }
            }} />
          <span class="po-label">${this.#localize.term('uap_priorityOverride')}</span>
          <span class="po-help" title=${this.#localize.term('uap_priorityOverrideTooltip', this.verb, this.nodeName)}>?</span>
        </label>
      </div>
    `;
  }

  static override styles = css`
    :host { display: contents; }

    .scope-dialog {
      border: none;
      border-radius: 8px;
      box-shadow: 0 8px 32px rgba(0, 0, 0, 0.25);
      padding: 0;
      min-width: 420px;
      max-width: 540px;
    }
    .scope-dialog::backdrop {
      background: rgba(0, 0, 0, 0.4);
    }

    .dialog-instructions {
      margin: 0 0 16px;
      font-size: 13px;
      color: var(--uui-color-text-alt, #666);
      line-height: 1.5;
    }

    .dialog-options { margin-bottom: 8px; }

    .dialog-sections {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 24px;
      margin-bottom: 8px;
    }

    .dialog-section h4 {
      margin: 0 0 4px;
      font-size: 13px;
      font-weight: 600;
      color: var(--uui-color-text, #333);
    }

    .dialog-section-title {
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
      max-width: 100%;
    }

    /* ── Not-applicable node panel (folder + element-only verb) ──
       Replaces the option tiles on the node side: a hatched, muted panel that mirrors the N/A cell
       treatment and explains why the verb can't be set on the folder itself. */
    .na-panel {
      display: flex;
      flex-direction: column;
      gap: 10px;
      padding: 12px 10px;
      border: 1px dashed var(--uui-color-border, #ddd);
      border-radius: 6px;
      background: repeating-linear-gradient(
        45deg,
        transparent,
        transparent 5px,
        color-mix(in srgb, var(--uui-color-text-alt, #aaa) 6%, transparent) 5px,
        color-mix(in srgb, var(--uui-color-text-alt, #aaa) 6%, transparent) 10px
      );
    }

    .na-chip {
      align-self: flex-start;
      padding: 2px 8px;
      border-radius: 4px;
      border: 1px solid var(--uui-color-border, #ddd);
      background: var(--uui-color-surface, #fff);
      font-size: 10px;
      font-weight: 700;
      letter-spacing: 0.5px;
      color: var(--uui-color-text-alt, #888);
    }

    .na-text {
      margin: 0;
      font-size: 12px;
      line-height: 1.4;
      color: var(--uui-color-text-alt, #777);
    }

    /* ── Option tiles ─────────────────────────────────────────── */
    .perm-options {
      display: flex;
      flex-direction: column;
      gap: 6px;
    }

    .perm-option {
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 6px 10px;
      border: 2px solid var(--uui-color-border, #ddd);
      border-radius: 6px;
      cursor: pointer;
      background: none;
      width: 100%;
      text-align: left;
      font-family: inherit;
      transition: border-color 0.15s, background-color 0.15s;
    }

    .perm-option:hover {
      border-color: var(--uui-color-border-emphasis, #bbb);
    }

    .perm-option-icon {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 28px;
      height: 24px;
      border-radius: 4px;
      font-size: 14px;
      font-weight: 700;
      border: 1px solid var(--uui-color-border, #ddd);
      flex-shrink: 0;
    }

    .perm-option.inherit .perm-option-icon {
      color: var(--uui-color-text-alt, #ccc);
      border-color: var(--uui-color-border, #e8e8e8);
    }

    .perm-option.allow .perm-option-icon {
      background: color-mix(in srgb, var(--uui-color-positive, #34a853) 14%, transparent);
      color: color-mix(in srgb, var(--uui-color-positive, #1e7e34) 80%, #000);
      border-color: color-mix(in srgb, var(--uui-color-positive, #34a853) 30%, transparent);
    }

    .perm-option.deny .perm-option-icon {
      background: color-mix(in srgb, var(--uui-color-danger, #ea4335) 12%, transparent);
      color: color-mix(in srgb, var(--uui-color-danger, #c5221f) 80%, #000);
      border-color: color-mix(in srgb, var(--uui-color-danger, #ea4335) 25%, transparent);
    }

    .perm-option-label {
      font-size: 13px;
      color: var(--uui-color-text, #333);
    }

    .perm-option.selected {
      border-color: var(--uui-color-default-standalone, #1b264f);
      background: var(--uui-color-surface-emphasis, #fafafa);
    }

    .perm-option.selected.allow {
      border-color: var(--uui-color-positive, #34a853);
      background: color-mix(in srgb, var(--uui-color-positive, #34a853) 6%, transparent);
    }

    .perm-option.selected.deny {
      border-color: var(--uui-color-danger, #ea4335);
      background: color-mix(in srgb, var(--uui-color-danger, #ea4335) 4%, transparent);
    }

    /* ── Preview ──────────────────────────────────────────────── */
    .dialog-result {
      margin-top: 16px;
      padding-top: 16px;
      border-top: 1px solid var(--uui-color-border, #eee);
    }

    .dialog-result h4 {
      margin: 0 0 8px;
      font-size: 13px;
      font-weight: 600;
      color: var(--uui-color-text, #333);
    }

    .preview-block-wrapper {
      width: 64px;
      --uap-perm-block-height: 32px;
    }

    .preview-content {
      display: flex;
      align-items: center;
      gap: 12px;
    }

    .preview-desc {
      margin: 0;
      font-size: 13px;
      color: var(--uui-color-text, #333);
      line-height: 1.4;
    }

    /* ── Priority override checkbox ───────────────────────────── */
    .priority-override {
      margin-top: 16px;
      padding: 10px 12px;
      border-top: 1px solid var(--uui-color-border, #eee);
      transition: background-color 0.15s;
    }

    .priority-override.active {
      background: color-mix(in srgb, var(--uui-color-warning, #f5a524) 8%, transparent);
      border-radius: 0 0 8px 8px;
    }

    .po-row {
      display: flex;
      align-items: center;
      gap: 8px;
      cursor: pointer;
      font-size: 13px;
      color: var(--uui-color-text, #333);
    }

    .po-row.disabled {
      cursor: not-allowed;
      color: var(--uui-color-text-alt, #888);
    }

    .po-row input[type="checkbox"] {
      margin: 0;
      cursor: inherit;
    }

    .po-label {
      flex: 1;
      font-weight: 500;
    }

    .po-help {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 18px;
      height: 18px;
      border-radius: 50%;
      background: var(--uui-color-surface-emphasis, #f0f0f0);
      color: var(--uui-color-text-alt, #666);
      font-size: 11px;
      font-weight: 700;
      cursor: help;
      user-select: none;
    }

    .po-warning {
      margin: 6px 0 0;
      padding-left: 22px;
      font-size: 12px;
      color: color-mix(in srgb, var(--uui-color-warning, #b87013) 85%, #000);
      line-height: 1.4;
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'uap-permission-scope-dialog': UapPermissionScopeDialogElement;
  }
}

export default UapPermissionScopeDialogElement;
