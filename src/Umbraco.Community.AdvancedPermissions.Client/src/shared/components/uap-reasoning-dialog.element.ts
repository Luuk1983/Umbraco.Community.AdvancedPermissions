import { html, css, nothing, customElement, property, query } from '@umbraco-cms/backoffice/external/lit';
import type { TemplateResult } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import type {
  EffectivePermission,
  PathNode,
  PermissionEntry,
} from '../../models/permission.models.js';
import { VIRTUAL_ROOT_NODE_KEY } from '../../models/permission.models.js';
import type { CellInfo } from '../../utils/cell-info.js';
import './uap-perm-block.element.js';

/**
 * One row in `entriesByNode`: a role alias + the entries it contributed at the row's node.
 */
export interface ReasoningRoleEntries {
  role: string;
  entries: PermissionEntry[];
}

/**
 * Shared reasoning dialog used by:
 * - the Access Viewer (per (subject, node, verb))
 * - the doc-type Create Audit (per (subject, node, content-type))
 *
 * Pure presentation. The parent owns the data: it calls `open()` after wiring the inputs,
 * may pass `loading=true` while fetching, and listens for `uap-reasoning-close` to reset
 * its own state when the user dismisses the dialog.
 */
@customElement('uap-reasoning-dialog')
export class UapReasoningDialogElement extends UmbLitElement {
  #localize = new UmbLocalizationController(this);

  /** The inheritance path from virtual root to the target node. */
  @property({ attribute: false }) path: PathNode[] = [];

  /**
   * For each node in the path, the (role alias, entries) pairs that contributed entries at
   * that level. Already filtered by the parent to only the roles relevant for this subject.
   */
  @property({ attribute: false }) entriesByNode: Map<string, ReasoningRoleEntries[]> = new Map();

  /** When true, marks deny-only roles with a star where roles disagree at the same node. */
  @property({ type: Boolean }) showStars = false;

  /** The fully resolved effective permission used to build the top banner. */
  @property({ attribute: false }) effectivePerm: EffectivePermission | null = null;

  /** The subject's display name (role or user name) used in the banner. */
  @property() subjectName = '';

  /** The verb or content-type label used in the headline + banner. */
  @property() verbLabel = '';

  /** The target node's display name used in the headline. */
  @property() nodeName = '';

  /** When true, shows a loader instead of the path table. */
  @property({ type: Boolean }) loading = false;

  /**
   * Function mapping a role alias to its display name. Defaults to identity if not supplied.
   */
  @property({ attribute: false }) roleNameLookup: (alias: string) => string = (a) => a;

  /**
   * Label for the virtual-root row in the path. Defaults to `uap_defaultPermissions`; the
   * doc-type audit can override for terminology consistency if needed.
   */
  @property() virtualRootLabel = '';

  @query('dialog') private _dialog!: HTMLDialogElement;

  /**
   * Opens the modal. Call after setting inputs (or pass `loading=true` first and update later).
   */
  open(): void {
    void this.updateComplete.then(() => this._dialog.showModal());
  }

  override render(): TemplateResult {
    return html`
      <dialog class="reasoning-dialog" @close=${this.#onClose}>
        <uui-dialog-layout headline=${this.#localize.term('uap_reasoningHeadline', this.verbLabel, this.nodeName)}>
          ${this.#renderBanner()}

          ${this.loading
            ? html`<div class="dialog-loading"><uui-loader-circle></uui-loader-circle></div>`
            : this.path.length > 0
              ? html`
                  <div class="dialog-table-wrap">
                    <table class="dialog-table">
                      <thead>
                        <tr>
                          <th class="dialog-node-header">${this.#localize.term('uap_contentNodeHeader')}</th>
                          <th class="dialog-security-header">${this.#localize.term('uap_dialogSecurityHeader')}</th>
                        </tr>
                      </thead>
                      <tbody>
                        ${this.path.map((n, i) => this.#renderPathRow(n, i))}
                      </tbody>
                    </table>
                  </div>
                `
              : html`<p class="no-reasoning">${this.#localize.term('uap_noReasoningData')}</p>`}

          <div slot="actions">
            <uui-button look="primary" @click=${() => this._dialog.close()}>
              ${this.#localize.term('uap_close')}
            </uui-button>
          </div>
        </uui-dialog-layout>
      </dialog>
    `;
  }

  /**
   * Dispatches `uap-reasoning-close` so the parent can clear its dialog state.
   */
  #onClose = (): void => {
    this.dispatchEvent(new CustomEvent('uap-reasoning-close', { bubbles: true, composed: true }));
  };

  /**
   * Renders the allow/deny summary banner. Returns nothing when the effective permission isn't
   * yet populated (which can happen briefly while the dialog opens before data lands).
   */
  #renderBanner(): TemplateResult {
    if (!this.effectivePerm) return html``;
    const isAllowed = this.effectivePerm.isAllowed;
    const cls = isAllowed ? 'result-allow' : 'result-deny';
    const icon = isAllowed ? '✓' : '✗';

    const contentPath = this.path
      .filter((n) => n.key !== VIRTUAL_ROOT_NODE_KEY)
      .map((n) => n.name)
      .join(' > ');
    const nodePath = contentPath || this.nodeName;

    const message = isAllowed
      ? this.#localize.term('uap_effectiveAllowed', this.subjectName, this.verbLabel, nodePath)
      : this.#localize.term('uap_effectiveDenied', this.subjectName, this.verbLabel, nodePath);

    return html`
      <div class="effective-banner ${cls}">
        <span class="banner-icon">${icon}</span>
        <span class="banner-text">${message}</span>
      </div>
    `;
  }

  /**
   * Renders one row of the path table: node name on the left, security column on the right.
   */
  #renderPathRow(node: PathNode, index: number): TemplateResult {
    const isVirtualRoot = node.key === VIRTUAL_ROOT_NODE_KEY;
    const label = isVirtualRoot
      ? (this.virtualRootLabel || this.#localize.term('uap_defaultPermissions'))
      : node.name;
    return html`
      <tr>
        <td class="dialog-node-cell">
          <div class="dialog-node-inner" style="--depth: ${isVirtualRoot ? 0 : index}">
            <umb-icon name=${node.icon ?? 'icon-document'}></umb-icon>
            <span class="node-name">${label}</span>
          </div>
        </td>
        ${this.#renderSecurityCell(node.key)}
      </tr>
    `;
  }

  /**
   * Renders the security column for a single node in the path. When no entries exist at this
   * level for the subject's roles, renders a single inherit indicator.
   */
  #renderSecurityCell(nodeKey: string): TemplateResult {
    const roleEntries = this.entriesByNode.get(nodeKey);

    if (!roleEntries || roleEntries.length === 0) {
      const info: CellInfo = { split: false, nodeClass: 'inherit', descClass: 'inherit' };
      return html`
        <td class="dialog-security-cell">
          <div class="security-entry">
            <uap-perm-block .info=${info}></uap-perm-block>
          </div>
        </td>
      `;
    }

    return html`
      <td class="dialog-security-cell">
        ${roleEntries.map(({ role, entries }) => {
          const roleHasDeny = entries.some((e) => e.state === 'Deny');
          const roleHasAllow = entries.some((e) => e.state === 'Allow');
          const showStar = this.showStars && roleHasDeny && !roleHasAllow;
          const state = entries[0]?.state === 'Allow'
            ? 'allow'
            : entries[0]?.state === 'Deny' ? 'deny' : 'inherit';
          const info: CellInfo = { split: false, nodeClass: state, descClass: state };
          return html`
            <div class="security-entry">
              <uap-perm-block .info=${info}></uap-perm-block>
              <span class="security-role">${this.roleNameLookup(role)}</span>
              ${showStar
                ? html`<span class="winner-star" title=${this.#localize.term('uap_determiningEntry')}>★</span>`
                : nothing}
            </div>
          `;
        })}
      </td>
    `;
  }

  static override styles = css`
    :host { display: contents; }

    .reasoning-dialog {
      border: none;
      border-radius: 8px;
      box-shadow: 0 8px 32px rgba(0, 0, 0, 0.25);
      padding: 0;
      min-width: 420px;
      max-width: 700px;
      width: max-content;
    }
    .reasoning-dialog::backdrop {
      background: rgba(0, 0, 0, 0.4);
    }

    .dialog-loading {
      display: flex;
      justify-content: center;
      padding: 24px;
    }

    .effective-banner {
      display: flex;
      align-items: flex-start;
      gap: 8px;
      padding: 10px 14px;
      border-radius: 6px;
      margin-bottom: 16px;
      font-size: 13px;
      line-height: 1.4;
    }
    .effective-banner.result-allow {
      background: color-mix(in srgb, var(--uui-color-positive, #34a853) 12%, transparent);
      border-left: 4px solid var(--uui-color-positive, #34a853);
    }
    .effective-banner.result-deny {
      background: color-mix(in srgb, var(--uui-color-danger, #ea4335) 10%, transparent);
      border-left: 4px solid var(--uui-color-danger, #ea4335);
    }
    .banner-icon {
      font-size: 16px;
      font-weight: 700;
      flex-shrink: 0;
    }
    .result-allow .banner-icon { color: var(--uui-color-positive, #34a853); }
    .result-deny .banner-icon { color: var(--uui-color-danger, #ea4335); }

    .dialog-table-wrap {
      overflow-x: auto;
      max-height: 400px;
      overflow-y: auto;
      margin-bottom: 12px;
    }
    .dialog-table {
      width: 100%;
      border-collapse: collapse;
    }
    .dialog-table thead {
      position: sticky;
      top: 0;
      z-index: 1;
    }
    .dialog-table th {
      padding: 6px 8px;
      text-align: left;
      border-bottom: 1px solid var(--uui-color-border, #ddd);
      font-weight: 600;
      font-size: 12px;
      background: var(--uui-color-surface, #fff);
      color: var(--uui-color-text-alt, #666);
      white-space: nowrap;
    }
    .dialog-table td {
      border-bottom: 1px solid var(--uui-color-border, #f0f0f0);
      vertical-align: middle;
    }
    .dialog-table tr:hover td {
      background-color: var(--uui-color-surface-emphasis, #fafafa);
    }
    .dialog-node-header { width: 50%; }

    .dialog-node-cell { padding: 0; }
    .dialog-node-inner {
      display: flex;
      align-items: center;
      gap: 4px;
      padding: 0 8px 0 calc(var(--depth, 0) * 18px + 8px);
      height: 30px;
      white-space: nowrap;
      overflow: hidden;
    }
    .dialog-node-inner umb-icon {
      font-size: 16px;
      flex-shrink: 0;
      color: var(--uui-color-text-alt, #666);
    }

    .dialog-security-cell {
      padding: 4px 8px;
    }
    .security-entry {
      display: flex;
      align-items: center;
      gap: 6px;
      padding: 2px 0;
      --uap-perm-block-height: 24px;
    }
    .security-entry uap-perm-block {
      width: auto;
      min-width: 36px;
    }
    .security-role {
      font-size: 12px;
      color: var(--uui-color-text-alt, #666);
      font-family: monospace;
    }
    .winner-star {
      color: var(--uui-color-warning, #f59e0b);
      font-size: 14px;
      flex-shrink: 0;
    }

    .no-reasoning {
      color: var(--uui-color-text-alt, #888);
      font-size: 13px;
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'uap-reasoning-dialog': UapReasoningDialogElement;
  }
}

export default UapReasoningDialogElement;
