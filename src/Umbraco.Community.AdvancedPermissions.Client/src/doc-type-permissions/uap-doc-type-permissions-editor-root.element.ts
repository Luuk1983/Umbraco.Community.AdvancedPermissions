import { html, css, nothing, customElement, state } from '@umbraco-cms/backoffice/external/lit';
import type { TemplateResult } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';
import { UMB_MODAL_MANAGER_CONTEXT } from '@umbraco-cms/backoffice/modal';
import { UMB_DOCUMENT_PICKER_MODAL } from '@umbraco-cms/backoffice/document';
import type { RoleInfo, PermissionState, PermissionScope } from '../models/permission.models.js';
import { VIRTUAL_ROOT_NODE_KEY } from '../models/permission.models.js';
import type { DocTypeListItem, DocTypePermissionEntry } from '../models/doc-type-permission.models.js';
import {
  getDocTypes,
  getDocTypePermissions,
  saveDocTypePermissions,
} from '../api/doc-type-permissions.api.js';
import { UAP_ROLE_PICKER_MODAL } from '../access-viewer/role-picker-modal.token.js';
import '../components/uap-picker-button.element.js';

/** The verb shipped in v1 — only one. The column on the entry exists for future verbs. */
const VERB = 'Umb.Document.CreateOfType';

/** A row in the editor — one per node that has stored entries, plus the Default row. */
interface EditorRow {
  nodeKey: string;
  /** "Default" for the virtual-root row, or the picked node's name. */
  label: string;
  /** Synthesised display state for this row. Default row defaults to Allow when no entry exists. */
  displayState: 'allow' | 'deny' | 'clear';
  /** Scope of the entry on this row (irrelevant when displayState=clear). */
  scope: PermissionScope;
  /** Whether this row has changes pending save. */
  dirty: boolean;
}

/**
 * Editor workspace element for document-type permissions.
 * Picks a role and a non-element doc-type, then renders one row per node with stored entries
 * (plus the Default row for the virtual root). Each row has an Allow / Deny / Clear control
 * plus a scope dropdown. The Default row shows Allow by default when no entry exists.
 */
@customElement('uap-doc-type-permissions-editor-root')
export class UapDocTypePermissionsEditorRootElement extends UmbLitElement {
  #localize = new UmbLocalizationController(this);

  @state() private _docTypes: DocTypeListItem[] = [];

  @state() private _selectedRole: RoleInfo | null = null;
  @state() private _selectedDocType: DocTypeListItem | null = null;

  @state() private _rows: EditorRow[] = [];
  @state() private _loading = false;
  @state() private _saving = false;
  @state() private _error: string | null = null;

  /** Map of nodeKey → display name, populated as the user adds rows from the document picker. */
  #nodeNames = new Map<string, string>();

  #notificationContext: typeof UMB_NOTIFICATION_CONTEXT.TYPE | undefined = undefined;
  #modalManager: typeof UMB_MODAL_MANAGER_CONTEXT.TYPE | undefined = undefined;
  #loadAbortController: AbortController | null = null;

  constructor() {
    super();
    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (ctx) => {
      this.#notificationContext = ctx ?? undefined;
    });
    this.consumeContext(UMB_MODAL_MANAGER_CONTEXT, (ctx) => {
      this.#modalManager = ctx ?? undefined;
    });
  }

  override connectedCallback(): void {
    super.connectedCallback();
    void this.#loadMeta();
  }

  override disconnectedCallback(): void {
    super.disconnectedCallback();
    this.#loadAbortController?.abort();
  }

  // ── Data loading ────────────────────────────────────────────────────────

  async #loadMeta(): Promise<void> {
    try {
      this._docTypes = await getDocTypes();
    } catch (err) {
      this._error = err instanceof Error ? err.message : String(err);
    }
  }

  async #loadEntries(): Promise<void> {
    if (!this._selectedRole || !this._selectedDocType) {
      this._rows = [];
      return;
    }
    this.#loadAbortController?.abort();
    this.#loadAbortController = new AbortController();

    this._loading = true;
    this._error = null;
    try {
      const entries = await getDocTypePermissions(
        this._selectedRole.alias,
        this._selectedDocType.key,
        this.#loadAbortController.signal,
      );

      this._rows = this.#buildRows(entries);
    } catch (err) {
      if (err instanceof Error && err.name === 'AbortError') return;
      this._error = err instanceof Error ? err.message : String(err);
    } finally {
      this._loading = false;
    }
  }

  /**
   * Synthesises the editor rows from stored entries. Always includes a Default row at the top,
   * even when no virtual-root entry exists — the user sees Allow as the visible default state.
   */
  #buildRows(entries: DocTypePermissionEntry[]): EditorRow[] {
    // Default row (virtual root)
    const defaultEntry = entries.find((e) => e.nodeKey === VIRTUAL_ROOT_NODE_KEY);
    const defaultRow: EditorRow = {
      nodeKey: VIRTUAL_ROOT_NODE_KEY,
      label: this.#localize.term('uap_docTypePermissions_defaultRowLabel'),
      displayState: defaultEntry ? (defaultEntry.state === 'Allow' ? 'allow' : 'deny') : 'allow',
      scope: defaultEntry?.scope ?? 'ThisNodeAndDescendants',
      dirty: false,
    };

    // One row per non-virtual-root entry
    const nodeRows: EditorRow[] = entries
      .filter((e) => e.nodeKey !== VIRTUAL_ROOT_NODE_KEY)
      .map((e) => ({
        nodeKey: e.nodeKey,
        label: this.#nodeNames.get(e.nodeKey) ?? e.nodeKey,
        displayState: e.state === 'Allow' ? 'allow' : 'deny',
        scope: e.scope,
        dirty: false,
      }));

    return [defaultRow, ...nodeRows];
  }

  // ── Pickers ─────────────────────────────────────────────────────────────

  async #pickRole(): Promise<void> {
    if (!this.#modalManager) return;
    const modal = this.#modalManager.open(this, UAP_ROLE_PICKER_MODAL, {
      data: {
        ...(this._selectedRole ? { currentRole: this._selectedRole.alias } : {}),
      },
    });
    const value = await modal.onSubmit().catch(() => undefined);
    if (!value) return;
    this._selectedRole = value.role;
    void this.#loadEntries();
  }

  #pickDocType(e: Event): void {
    const select = e.target as HTMLSelectElement;
    const key = select.value;
    this._selectedDocType = this._docTypes.find((d) => d.key === key) ?? null;
    void this.#loadEntries();
  }

  async #addScopeNode(): Promise<void> {
    if (!this.#modalManager) return;
    const modal = this.#modalManager.open(this, UMB_DOCUMENT_PICKER_MODAL, {
      data: {
        multiple: false,
        hideTreeRoot: false,
      } as any,
    });
    const value = await modal.onSubmit().catch(() => null);
    if (!value) return;
    const selection = (value as { selection: string[] }).selection;
    if (!selection || selection.length === 0) return;

    const nodeKey = selection[0]!;
    // Avoid duplicates
    if (this._rows.some((r) => r.nodeKey === nodeKey)) return;

    // We don't have the name yet — show the key until the next reload (or fall back gracefully)
    this.#nodeNames.set(nodeKey, this.#localize.term('uap_docTypePermissions_pendingNodeLabel') ?? nodeKey);

    this._rows = [
      ...this._rows,
      {
        nodeKey,
        label: this.#nodeNames.get(nodeKey) ?? nodeKey,
        displayState: 'allow',
        scope: 'ThisNodeAndDescendants',
        dirty: true,
      },
    ];
  }

  // ── Row state mutations ─────────────────────────────────────────────────

  #setRowState(nodeKey: string, displayState: 'allow' | 'deny' | 'clear'): void {
    this._rows = this._rows.map((r) =>
      r.nodeKey === nodeKey ? { ...r, displayState, dirty: true } : r,
    );
  }

  #setRowScope(nodeKey: string, scope: PermissionScope): void {
    this._rows = this._rows.map((r) =>
      r.nodeKey === nodeKey ? { ...r, scope, dirty: true } : r,
    );
  }

  // ── Save ────────────────────────────────────────────────────────────────

  /**
   * Persists dirty rows. For the default row, an Allow state is treated as a no-op (since Allow
   * IS the implicit default — no DB row needed). For non-default rows, "clear" deletes any
   * existing entry; "allow"/"deny" upserts one entry with the chosen scope.
   */
  async #save(): Promise<void> {
    if (!this._selectedRole || !this._selectedDocType) return;
    const role = this._selectedRole.alias;
    const ctKey = this._selectedDocType.key;

    this._saving = true;
    try {
      for (const row of this._rows.filter((r) => r.dirty)) {
        let entriesToSave: { verb: string; state: PermissionState; scope: PermissionScope }[];

        const isDefault = row.nodeKey === VIRTUAL_ROOT_NODE_KEY;

        if (isDefault && row.displayState === 'allow') {
          // No-op: Allow IS the implicit default for the default row. Send empty list to
          // delete any stale Deny entry that might have been there.
          entriesToSave = [];
        } else if (row.displayState === 'clear') {
          entriesToSave = [];
        } else {
          const state: PermissionState = row.displayState === 'allow' ? 'Allow' : 'Deny';
          entriesToSave = [{ verb: VERB, state, scope: row.scope }];
        }

        await saveDocTypePermissions(row.nodeKey, role, ctKey, entriesToSave);
      }

      // Mark all rows clean
      this._rows = this._rows.map((r) => ({ ...r, dirty: false }));

      this.#notificationContext?.peek('positive', {
        data: { headline: this.#localize.term('general_saved'), message: '' },
      });
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      this._error = message;
      this.#notificationContext?.peek('danger', {
        data: { headline: this.#localize.term('general_error'), message },
      });
    } finally {
      this._saving = false;
    }
  }

  // ── Render ──────────────────────────────────────────────────────────────

  override render(): TemplateResult {
    return html`
      <umb-body-layout headline=${this.#localize.term('uap_docTypePermissions_workspaceTitle')}>
        <div class="content">
          ${this.#renderPickers()}
          ${this._error
            ? html`<div class="error">${this._error}</div>`
            : nothing}
          ${this._loading
            ? html`<uui-loader></uui-loader>`
            : this._selectedRole && this._selectedDocType
              ? this.#renderTable()
              : html`<div class="hint">${this.#localize.term('uap_docTypePermissions_pickToStart')}</div>`}
        </div>

        <div slot="actions">
          ${this._selectedRole && this._selectedDocType
            ? html`
                <uui-button look="secondary" @click=${() => this.#addScopeNode()}>
                  ${this.#localize.term('uap_docTypePermissions_addScopeNode')}
                </uui-button>
                <uui-button
                  look="primary"
                  color="positive"
                  ?disabled=${this._saving || !this._rows.some((r) => r.dirty)}
                  @click=${() => this.#save()}>
                  ${this._saving ? this.#localize.term('general_saving') : this.#localize.term('general_save')}
                </uui-button>
              `
            : nothing}
        </div>
      </umb-body-layout>
    `;
  }

  #renderPickers(): TemplateResult {
    return html`
      <div class="pickers">
        <div class="picker">
          <strong>${this.#localize.term('uap_role')}:</strong>
          <uap-picker-button
            .label=${this._selectedRole?.name ?? this.#localize.term('uap_pickRole')}
            @click=${() => this.#pickRole()}>
          </uap-picker-button>
        </div>
        <div class="picker">
          <strong>${this.#localize.term('uap_docTypePermissions_documentType')}:</strong>
          <select @change=${(e: Event) => this.#pickDocType(e)} .value=${this._selectedDocType?.key ?? ''}>
            <option value="">${this.#localize.term('uap_docTypePermissions_pickDocType')}</option>
            ${this._docTypes.map(
              (dt) =>
                html`<option value=${dt.key} ?selected=${dt.key === this._selectedDocType?.key}>
                  ${dt.name}
                </option>`,
            )}
          </select>
        </div>
      </div>
    `;
  }

  #renderTable(): TemplateResult {
    return html`
      <table class="rows">
        <thead>
          <tr>
            <th>${this.#localize.term('uap_node')}</th>
            <th>${this.#localize.term('uap_state')}</th>
            <th>${this.#localize.term('uap_scope')}</th>
          </tr>
        </thead>
        <tbody>
          ${this._rows.map((r) => this.#renderRow(r))}
        </tbody>
      </table>
    `;
  }

  #renderRow(row: EditorRow): TemplateResult {
    const isDefault = row.nodeKey === VIRTUAL_ROOT_NODE_KEY;
    return html`
      <tr class=${row.dirty ? 'dirty' : ''}>
        <td>${row.label}${row.dirty ? html` <em>*</em>` : nothing}</td>
        <td>
          <uui-button-group>
            <uui-button
              look=${row.displayState === 'allow' ? 'primary' : 'secondary'}
              color="positive"
              @click=${() => this.#setRowState(row.nodeKey, 'allow')}>
              ${this.#localize.term('uap_allow')}
            </uui-button>
            <uui-button
              look=${row.displayState === 'deny' ? 'primary' : 'secondary'}
              color="danger"
              @click=${() => this.#setRowState(row.nodeKey, 'deny')}>
              ${this.#localize.term('uap_deny')}
            </uui-button>
            ${isDefault
              ? nothing
              : html`
                  <uui-button
                    look=${row.displayState === 'clear' ? 'primary' : 'secondary'}
                    @click=${() => this.#setRowState(row.nodeKey, 'clear')}>
                    ${this.#localize.term('uap_docTypePermissions_notSet')}
                  </uui-button>
                `}
          </uui-button-group>
        </td>
        <td>
          <select
            ?disabled=${row.displayState === 'clear'}
            @change=${(e: Event) => this.#setRowScope(row.nodeKey, (e.target as HTMLSelectElement).value as PermissionScope)}
            .value=${row.scope}>
            <option value="ThisNodeOnly" ?selected=${row.scope === 'ThisNodeOnly'}>
              ${this.#localize.term('uap_scope_thisNodeOnly')}
            </option>
            <option value="ThisNodeAndDescendants" ?selected=${row.scope === 'ThisNodeAndDescendants'}>
              ${this.#localize.term('uap_scope_thisNodeAndDescendants')}
            </option>
            <option value="DescendantsOnly" ?selected=${row.scope === 'DescendantsOnly'}>
              ${this.#localize.term('uap_scope_descendantsOnly')}
            </option>
          </select>
        </td>
      </tr>
    `;
  }

  static override styles = css`
    :host { display: block; }
    .content { padding: var(--uui-size-layout-1); }
    .pickers { display: flex; gap: var(--uui-size-layout-2); margin-bottom: var(--uui-size-layout-1); flex-wrap: wrap; }
    .picker { display: flex; align-items: center; gap: var(--uui-size-2); }
    .picker select { min-width: 200px; padding: var(--uui-size-1); }
    .error { color: var(--uui-color-danger); margin: var(--uui-size-1) 0; }
    .hint { color: var(--uui-color-text-alt); padding: var(--uui-size-layout-1) 0; }
    table.rows { width: 100%; border-collapse: collapse; }
    table.rows th, table.rows td { padding: var(--uui-size-1); border-bottom: 1px solid var(--uui-color-divider); text-align: left; }
    tr.dirty td { background-color: var(--uui-color-surface-alt); }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'uap-doc-type-permissions-editor-root': UapDocTypePermissionsEditorRootElement;
  }
}

export default UapDocTypePermissionsEditorRootElement;
