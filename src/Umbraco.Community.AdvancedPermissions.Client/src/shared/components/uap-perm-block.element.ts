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

  /**
   * When true, dims the allow/deny block and applies a dashed muted border, flagging a doc type
   * that resolves allow/deny but isn't currently an insert option on the node. Uniform path only;
   * the allow/deny fill (and ✓/✗ icon) is retained so the underlying state still reads.
   */
  @property({ type: Boolean, attribute: 'outside-allowed' }) outsideAllowed = false;

  /**
   * Optional tooltip text shown on hover of an override-themed cell/half. Purely informational.
   */
  @property({ attribute: 'priority-override-title' }) priorityOverrideTitle = '';

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
    // Priority-override sides are themed gold: the allow/deny colour is replaced, but the ✓/✗
    // icon still conveys the underlying state. This trades the colour cue for a clear
    // "this is an override" signal — acceptable because overrides are expected to be rare.
    const nodeOverride = info.nodeOverride === true && info.nodeClass !== 'inherit';
    const descOverride = info.descOverride === true && info.descClass !== 'inherit';
    if (!info.split) {
      const overrideCls = nodeOverride ? ' override' : '';
      const outsideCls = this.outsideAllowed ? ' outside-allowed' : '';
      const title = nodeOverride ? this.priorityOverrideTitle : '';
      return html`<div class="perm-block uniform ${info.nodeClass}${overrideCls}${outsideCls}${pendingCls}" title=${title}>${stateIcon(info.nodeClass)}</div>`;
    }
    const nodeNa = info.nodeNa === true;
    const descNa = info.descNa === true;
    return html`
      <div class="perm-block split${pendingCls}">
        <span class="half ${nodeNa ? 'na' : info.nodeClass}${nodeOverride ? ' override' : ''}" title=${nodeOverride ? this.priorityOverrideTitle : ''}>${stateIcon(nodeNa ? 'na' : info.nodeClass)}</span>
        <span class="half ${descNa ? 'na' : info.descClass}${descOverride ? ' override' : ''}" title=${descOverride ? this.priorityOverrideTitle : ''}>${stateIcon(descNa ? 'na' : info.descClass)}</span>
      </div>
    `;
  }

  static override styles = css`
    :host {
      display: inline-block;
      width: 100%;
    }

    .perm-block {
      position: relative;
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

    /* ── Priority-override amber theme (uniform block) ────────────
       Replaces the allow/deny fill with the UUI warning (amber) palette so it stays
       theme-aware in light and dark mode. The ✓/✗ icon still conveys allow vs deny.
       A stronger tint than allow/deny keeps the override visually distinct. */
    .perm-block.uniform.override {
      background: color-mix(in srgb, var(--uui-color-warning, #af7c12) 25%, transparent);
      color: var(--uui-color-warning-standalone, #a17700);
      border-color: color-mix(in srgb, var(--uui-color-warning, #af7c12) 45%, transparent);
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
      color: var(--uui-color-positive-standalone, #347d39);
      border-color: color-mix(in srgb, var(--uui-color-positive, #34a853) 30%, transparent);
    }

    .perm-block.deny {
      background: color-mix(in srgb, var(--uui-color-danger, #ea4335) 12%, transparent);
      color: var(--uui-color-danger-standalone, #ca3b37);
      border-color: color-mix(in srgb, var(--uui-color-danger, #ea4335) 25%, transparent);
    }

    /* ── Outside-allowed: resolves allow/deny but isn't an insert option on the node ──
       Drops the fill entirely so it reads as a hollow, dashed outline — clearly distinct
       from a normal (filled) allow/deny cell. The dashed border + ✓/✗ icon stay green/red
       so the underlying permission is still legible. Declared after .allow/.deny so the
       transparent background and stronger border colour win. */
    .perm-block.outside-allowed {
      background: transparent;
      border-style: dashed;
      border-width: 2px;
    }
    .perm-block.allow.outside-allowed {
      border-color: color-mix(in srgb, var(--uui-color-positive, #34a853) 55%, transparent);
    }
    .perm-block.deny.outside-allowed {
      border-color: color-mix(in srgb, var(--uui-color-danger, #ea4335) 50%, transparent);
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
      color: var(--uui-color-positive-standalone, #347d39);
    }

    .half.deny {
      background: color-mix(in srgb, var(--uui-color-danger, #ea4335) 12%, transparent);
      color: var(--uui-color-danger-standalone, #ca3b37);
    }

    /* Amber theme per half — overrides the allow/deny half colours (UUI warning palette). */
    .half.override {
      background: color-mix(in srgb, var(--uui-color-warning, #af7c12) 25%, transparent);
      color: var(--uui-color-warning-standalone, #a17700);
    }

    /* Not-applicable half — hatched, muted (mirrors the uniform .na treatment). */
    .half.na {
      background: repeating-linear-gradient(
        45deg,
        transparent,
        transparent 4px,
        color-mix(in srgb, var(--uui-color-text-alt, #aaa) 8%, transparent) 4px,
        color-mix(in srgb, var(--uui-color-text-alt, #aaa) 8%, transparent) 8px
      );
      color: var(--uui-color-text-alt, #999);
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'uap-perm-block': UapPermBlockElement;
  }
}

export default UapPermBlockElement;
