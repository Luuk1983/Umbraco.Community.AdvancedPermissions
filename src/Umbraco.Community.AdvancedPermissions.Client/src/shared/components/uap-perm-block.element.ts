import { html, css, nothing, customElement, property } from '@umbraco-cms/backoffice/external/lit';
import type { TemplateResult } from '@umbraco-cms/backoffice/external/lit';
import { LitElement } from '@umbraco-cms/backoffice/external/lit';
import { stateIcon } from '../../utils/state-icon.js';
import type { CellInfo } from '../../utils/cell-info.js';

/**
 * Presentational permission cell. Shared by:
 * - the Permissions Editor (stored entries per (role, node, verb))
 * - the Access Viewer (effective per (subject, node, verb))
 * - the doc-type Permissions Editor (stored entries per (role, content-type, node))
 * - the doc-type Create Audit (effective + isInAllowedChildren per (subject, node, content-type))
 *
 * Display priority: `loading` > `na` > `info`. Click handling is the parent's responsibility —
 * this element only renders the visual state.
 */
@customElement('uap-perm-block')
export class UapPermBlockElement extends LitElement {
  /**
   * The cell-info describing entry state. When null and no overriding flag is set, the block
   * renders as a single inherit dash.
   */
  @property({ attribute: false }) info: CellInfo | null = null;

  /** When true, renders an animated loading placeholder regardless of other props. */
  @property({ type: Boolean }) loading = false;

  /**
   * When true (and `loading` is false), renders a grey "not applicable" cell — used by the audit
   * to show that a doc type is not in the parent's allowed-children list.
   */
  @property({ type: Boolean, attribute: 'na' }) na = false;

  /** When true, applies the dashed-border "unsaved" highlight. */
  @property({ type: Boolean }) pending = false;

  override render(): TemplateResult {
    if (this.loading) {
      return html`<div class="perm-block loading">${nothing}…</div>`;
    }
    if (this.na) {
      const cls = `perm-block uniform na${this.pending ? ' pending' : ''}`;
      return html`<div class=${cls}>${stateIcon('na')}</div>`;
    }
    const info = this.info ?? { split: false, nodeClass: 'inherit' as const, descClass: 'inherit' as const };
    const pendingCls = this.pending ? ' pending' : '';
    if (!info.split) {
      return html`<div class="perm-block uniform ${info.nodeClass}${pendingCls}">${stateIcon(info.nodeClass)}</div>`;
    }
    return html`
      <div class="perm-block split${pendingCls}">
        <span class="half ${info.nodeClass}">${stateIcon(info.nodeClass)}</span>
        <span class="half ${info.descClass}">${stateIcon(info.descClass)}</span>
      </div>
    `;
  }

  static override styles = css`
    :host {
      display: inline-block;
      width: 100%;
    }

    .perm-block {
      display: flex;
      align-items: center;
      justify-content: center;
      height: var(--uap-perm-block-height, 26px);
      border: 1px solid var(--uui-color-border, #ddd);
      border-radius: 4px;
      user-select: none;
      overflow: hidden;
      cursor: pointer;
    }

    .perm-block:hover {
      border-color: var(--uui-color-border-emphasis, #bbb);
    }

    .perm-block.uniform {
      font-size: 13px;
      font-weight: 700;
    }

    .perm-block.loading {
      color: var(--uui-color-text-alt, #aaa);
      font-size: 11px;
      font-weight: 400;
      cursor: default;
    }

    .perm-block.pending {
      border-color: var(--uui-color-warning-standalone, #f59e0b);
      border-style: dashed;
      border-width: 2px;
    }

    /* ── Uniform state variants ───────────────────────────────── */
    .perm-block.inherit {
      color: var(--uui-color-text-alt, #ccc);
      border-color: var(--uui-color-border, #e8e8e8);
    }

    .perm-block.allow {
      background: color-mix(in srgb, var(--uui-color-positive, #34a853) 14%, transparent);
      color: color-mix(in srgb, var(--uui-color-positive, #1e7e34) 80%, #000);
      border-color: color-mix(in srgb, var(--uui-color-positive, #34a853) 30%, transparent);
    }

    .perm-block.deny {
      background: color-mix(in srgb, var(--uui-color-danger, #ea4335) 12%, transparent);
      color: color-mix(in srgb, var(--uui-color-danger, #c5221f) 80%, #000);
      border-color: color-mix(in srgb, var(--uui-color-danger, #ea4335) 25%, transparent);
    }

    .perm-block.na {
      background: repeating-linear-gradient(
        45deg,
        transparent,
        transparent 4px,
        color-mix(in srgb, var(--uui-color-text-alt, #aaa) 8%, transparent) 4px,
        color-mix(in srgb, var(--uui-color-text-alt, #aaa) 8%, transparent) 8px
      );
      color: var(--uui-color-text-alt, #999);
      border-color: var(--uui-color-border, #e8e8e8);
      border-style: dotted;
      cursor: default;
    }

    /* ── Split state: two halves ──────────────────────────────── */
    .perm-block.split {
      padding: 0;
    }

    .perm-block.split > .half {
      display: flex;
      align-items: center;
      justify-content: center;
      flex: 1;
      height: 100%;
      font-size: 11px;
      font-weight: 700;
    }

    .half.inherit {
      color: var(--uui-color-text-alt, #ccc);
    }

    .half.allow {
      background: color-mix(in srgb, var(--uui-color-positive, #34a853) 14%, transparent);
      color: color-mix(in srgb, var(--uui-color-positive, #1e7e34) 80%, #000);
    }

    .half.deny {
      background: color-mix(in srgb, var(--uui-color-danger, #ea4335) 12%, transparent);
      color: color-mix(in srgb, var(--uui-color-danger, #c5221f) 80%, #000);
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'uap-perm-block': UapPermBlockElement;
  }
}

export default UapPermBlockElement;
