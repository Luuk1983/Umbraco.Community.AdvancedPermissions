import { nothing as b, html as r, css as U, state as h, query as W, customElement as L } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement as q } from "@umbraco-cms/backoffice/lit-element";
import { UMB_NOTIFICATION_CONTEXT as B } from "@umbraco-cms/backoffice/notification";
import { g as F, a as G, b as X, c as H, d as J, e as Q } from "./advanced-security.api-C-RLDPi4.js";
import { clearEffectivePermissionCache as Y } from "./document-user-permission.condition-CAI2T48b.js";
var Z = Object.defineProperty, j = Object.getOwnPropertyDescriptor, R = (e) => {
  throw TypeError(e);
}, p = (e, t, i, s) => {
  for (var a = s > 1 ? void 0 : s ? j(t, i) : t, c = e.length - 1, u; c >= 0; c--)
    (u = e[c]) && (a = (s ? u(t, i, a) : u(a)) || a);
  return s && a && Z(t, i, a), a;
}, $ = (e, t, i) => t.has(e) || R("Cannot " + i), v = (e, t, i) => ($(e, t, "read from private field"), i ? i.call(e) : t.get(e)), T = (e, t, i) => t.has(e) ? R("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, i), ee = (e, t, i, s) => ($(e, t, "write to private field"), t.set(e, i), i), l = (e, t, i) => ($(e, t, "access private method"), i), m, o, S, A, N, O, E, P, f, x, V, M, w, C, z, k, I, K;
const te = [
  { value: "inherit", label: "Inherit (remove entry)" },
  { value: "allow-nd", label: "Allow — This node and descendants", state: "Allow", scope: "ThisNodeAndDescendants" },
  { value: "allow-n", label: "Allow — This node only", state: "Allow", scope: "ThisNodeOnly" },
  { value: "allow-d", label: "Allow — Descendants only", state: "Allow", scope: "DescendantsOnly" },
  { value: "deny-nd", label: "Deny — This node and descendants", state: "Deny", scope: "ThisNodeAndDescendants" },
  { value: "deny-n", label: "Deny — This node only", state: "Deny", scope: "ThisNodeOnly" },
  { value: "deny-d", label: "Deny — Descendants only", state: "Deny", scope: "DescendantsOnly" }
], ie = [
  { value: "inherit", label: "Not set (remove entry)" },
  { value: "allow-nd", label: "Allow (all content)", state: "Allow", scope: "ThisNodeAndDescendants" },
  { value: "deny-nd", label: "Deny (all content)", state: "Deny", scope: "ThisNodeAndDescendants" }
];
function se(e) {
  return e === "ThisNodeAndDescendants" ? "N+D" : e === "ThisNodeOnly" ? "N" : "D";
}
let n = class extends q {
  constructor() {
    super(), T(this, o), this._roles = [], this._verbs = [], this._selectedRole = "", this._treeNodes = [], this._loading = !1, this._saving = !1, this._error = null, this._pendingChanges = /* @__PURE__ */ new Map(), this._pickerNode = null, this._pickerVerb = null, this._pickerValue = "inherit", T(this, m), this.consumeContext(B, (e) => {
      ee(this, m, e ?? void 0);
    });
  }
  connectedCallback() {
    super.connectedCallback(), l(this, o, O).call(this);
  }
  render() {
    var t, i;
    const e = this._pendingChanges.size > 0;
    return r`
      <umb-body-layout headline="Security Editor">
        <div class="toolbar">
          <div class="role-picker">
            <label>Role:</label>
            <uui-select
              label="Role"
              placeholder="— Select a role —"
              .options=${v(this, o, A)}
              @change=${(s) => {
      this._selectedRole = s.target.value, this._pendingChanges = /* @__PURE__ */ new Map(), l(this, o, E).call(this);
    }}>
            </uui-select>
          </div>
          ${e ? r`
                <uui-button look="primary" color="positive" ?loading=${this._saving} @click=${() => void l(this, o, M).call(this)}>
                  Save Changes
                </uui-button>
                <uui-button look="outline" @click=${() => {
      this._pendingChanges = /* @__PURE__ */ new Map();
    }}>
                  Discard
                </uui-button>
              ` : b}
        </div>

        ${this._error ? r`<p class="error-msg">⚠ ${this._error}</p>` : b}
        ${this._loading ? r`<div class="loading"><uui-loader></uui-loader></div>` : b}
        ${this._selectedRole ? b : r`<p class="empty-msg">Select a role above to manage its permissions.</p>`}

        ${this._selectedRole && !this._loading && this._treeNodes.length > 0 ? r`
              <div class="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th class="node-header">Content Node</th>
                      ${this._verbs.map((s) => r`<th class="verb-header" title=${s.verb}>${s.displayName}</th>`)}
                    </tr>
                  </thead>
                  <tbody>
                    ${l(this, o, k).call(this, this._treeNodes, 0)}
                  </tbody>
                </table>
              </div>
            ` : b}
      </umb-body-layout>

      <!-- Scope picker modal — rendered outside umb-body-layout so it always layers on top -->
      <dialog
        class="scope-dialog"
        @close=${() => {
      this._pickerNode = null, this._pickerVerb = null;
    }}>
        <uui-dialog-layout
          headline="Set Permission: ${((t = this._pickerVerb) == null ? void 0 : t.split(".").pop()) ?? ""}">
          <p class="dialog-node">Node: <strong>${((i = this._pickerNode) == null ? void 0 : i.name) ?? ""}</strong></p>
          <div class="dialog-options">
            <uui-radio-group
              .value=${this._pickerValue}
              @change=${(s) => {
      this._pickerValue = s.target.value;
    }}>
              ${v(this, o, N).map(
      (s) => r`
                  <uui-radio
                    value=${s.value}
                    label=${s.label}
                    class=${s.state === "Allow" ? "opt-allow" : s.state === "Deny" ? "opt-deny" : "opt-inherit"}>
                  </uui-radio>
                `
    )}
            </uui-radio-group>
          </div>
          <div slot="actions">
            <uui-button look="outline" @click=${() => this._scopeDialog.close()}>Cancel</uui-button>
            <uui-button look="primary" color="positive" @click=${() => l(this, o, V).call(this)}>Apply</uui-button>
          </div>
        </uui-dialog-layout>
      </dialog>
    `;
  }
};
m = /* @__PURE__ */ new WeakMap();
o = /* @__PURE__ */ new WeakSet();
S = function() {
  const e = this._roles.find((t) => t.alias === this._selectedRole);
  return new Set((e == null ? void 0 : e.defaultVerbs) ?? []);
};
A = function() {
  return [
    { name: "— Select a role —", value: "" },
    ...this._roles.map((e) => ({
      name: `${e.name}${e.isEveryone ? " (Everyone)" : ""}`,
      value: e.alias,
      selected: e.alias === this._selectedRole
    }))
  ];
};
N = function() {
  var e;
  return ((e = this._pickerNode) == null ? void 0 : e.key) === "virtual-root" ? ie : te;
};
O = async function() {
  try {
    const [e, t] = await Promise.all([F(), G()]);
    this._roles = e, this._verbs = t;
  } catch (e) {
    this._error = String(e);
  }
};
E = async function() {
  if (this._selectedRole) {
    this._loading = !0, this._error = null, this._treeNodes = [];
    try {
      const [e, t] = await Promise.all([
        X(null, this._selectedRole),
        H(this._selectedRole)
      ]), i = {
        key: "virtual-root",
        name: "Content",
        icon: "icon-folder",
        hasChildren: !1,
        entries: e,
        expanded: !1,
        loading: !1
      };
      this._treeNodes = [i, ...t.map((s) => ({ ...s, expanded: !1, loading: !1 }))];
    } catch (e) {
      this._error = String(e);
    } finally {
      this._loading = !1;
    }
  }
};
P = async function(e) {
  if (e.expanded) {
    l(this, o, f).call(this, e.key, { expanded: !1 });
    return;
  }
  if (e.children) {
    l(this, o, f).call(this, e.key, { expanded: !0 });
    return;
  }
  l(this, o, f).call(this, e.key, { loading: !0 });
  try {
    const t = await Q(e.key, this._selectedRole);
    l(this, o, f).call(this, e.key, {
      expanded: !0,
      loading: !1,
      children: t.map((i) => ({ ...i, expanded: !1, loading: !1 }))
    });
  } catch (t) {
    l(this, o, f).call(this, e.key, { loading: !1 }), this._error = String(t);
  }
};
f = function(e, t, i = this._treeNodes) {
  for (let s = 0; s < i.length; s++) {
    if (i[s].key === e)
      return i[s] = { ...i[s], ...t }, this._treeNodes = [...this._treeNodes], !0;
    if (i[s].children && l(this, o, f).call(this, e, t, i[s].children))
      return !0;
  }
  return !1;
};
x = function(e, t) {
  this._pickerNode = e, this._pickerVerb = t, this._pickerValue = l(this, o, z).call(this, l(this, o, C).call(this, e, t)), this.updateComplete.then(() => this._scopeDialog.showModal());
};
V = function() {
  if (!this._pickerNode || !this._pickerVerb) return;
  const e = v(this, o, N).find((c) => c.value === this._pickerValue), t = (e == null ? void 0 : e.state) != null ? [{ state: e.state, scope: e.scope }] : [], i = this._pickerNode.key, s = this._pickerVerb, a = this._pendingChanges.get(i) ?? /* @__PURE__ */ new Map();
  a.set(s, t), this._pendingChanges = new Map(this._pendingChanges).set(i, a), this._scopeDialog.close();
};
M = async function() {
  var e, t;
  if (!(!this._pendingChanges.size || !this._selectedRole || this._saving)) {
    this._saving = !0;
    try {
      for (const [i, s] of this._pendingChanges) {
        const a = l(this, o, w).call(this, i);
        if (!a) continue;
        const c = /* @__PURE__ */ new Map();
        for (const d of a.entries) {
          const _ = c.get(d.verb) ?? [];
          _.push({ verb: d.verb, state: d.state, scope: d.scope }), c.set(d.verb, _);
        }
        for (const [d, _] of s)
          _.length === 0 ? c.delete(d) : c.set(d, _.map((D) => ({ verb: d, state: D.state, scope: D.scope })));
        const u = [...c.values()].flat(), y = i === "virtual-root" ? null : i;
        await J(y, this._selectedRole, u);
        const g = u.map((d, _) => ({
          id: _,
          nodeKey: y,
          roleAlias: this._selectedRole,
          verb: d.verb,
          state: d.state,
          scope: d.scope
        }));
        l(this, o, f).call(this, i, { entries: g });
      }
      this._pendingChanges = /* @__PURE__ */ new Map(), Y(), (e = v(this, m)) == null || e.peek("positive", { data: { message: "Permissions saved." } });
    } catch (i) {
      (t = v(this, m)) == null || t.peek("danger", { data: { message: `Save failed: ${String(i)}` } });
    } finally {
      this._saving = !1;
    }
  }
};
w = function(e, t = this._treeNodes) {
  for (const i of t) {
    if (i.key === e) return i;
    if (i.children) {
      const s = l(this, o, w).call(this, e, i.children);
      if (s) return s;
    }
  }
  return null;
};
C = function(e, t) {
  const i = this._pendingChanges.get(e.key);
  return i != null && i.has(t) ? i.get(t) : e.entries.filter((s) => s.verb === t);
};
z = function(e) {
  if (e.length === 0) return "inherit";
  const t = e[0];
  return `${t.state === "Deny" ? "deny" : "allow"}-${{
    ThisNodeAndDescendants: "nd",
    ThisNodeOnly: "n",
    DescendantsOnly: "d"
  }[t.scope] ?? "nd"}`;
};
k = function(e, t) {
  return e.flatMap((i) => [
    l(this, o, I).call(this, i, t),
    ...i.expanded && i.children ? l(this, o, k).call(this, i.children, t + 1) : []
  ]);
};
I = function(e, t) {
  const i = this._pendingChanges.has(e.key);
  return r`
      <tr class=${i ? "row-pending" : ""}>
        <td class="node-cell" style="--depth: ${t}">
          ${e.hasChildren || e.children ? r`<uui-button compact look="default" label=${e.expanded ? "Collapse" : "Expand"} @click=${() => void l(this, o, P).call(this, e)}>
                ${e.loading ? r`<uui-loader-circle></uui-loader-circle>` : e.expanded ? "▾" : "▸"}
              </uui-button>` : r`<span class="expand-spacer"></span>`}
          <umb-icon name=${e.icon ?? "icon-document"}></umb-icon>
          <span class="node-name">${e.name}</span>
        </td>
        ${this._verbs.map((s) => l(this, o, K).call(this, e, s.verb))}
      </tr>
    `;
};
K = function(e, t) {
  var y;
  const i = l(this, o, C).call(this, e, t), s = ((y = this._pendingChanges.get(e.key)) == null ? void 0 : y.has(t)) ?? !1;
  if (i.length === 0) {
    const g = e.key === "virtual-root" && v(this, o, S).has(t);
    return r`
        <td
          class="perm-cell ${g ? "default-allow" : "inherit"} ${s ? "cell-pending" : ""}"
          title=${t}
          @click=${() => l(this, o, x).call(this, e, t)}>
          ${g ? "Allow" : "—"}
        </td>
      `;
  }
  const a = i.some((g) => g.state === "Deny"), c = [...new Set(i.map((g) => se(g.scope)))].join("/"), u = `${a ? "D" : "A"} ${c}`;
  return r`
      <td
        class="perm-cell ${a ? "deny" : "allow"} ${s ? "cell-pending" : ""}"
        title=${t}
        @click=${() => l(this, o, x).call(this, e, t)}>
        ${u}
      </td>
    `;
};
n.styles = U`
    :host {
      display: block;
      height: 100%;
    }

    /* ── Toolbar ──────────────────────────────────────────────── */
    .toolbar {
      display: flex;
      align-items: center;
      gap: var(--uui-size-4, 12px);
      padding: var(--uui-size-3, 9px) var(--uui-size-6, 18px);
      background: var(--uui-color-surface, #fff);
      border-bottom: 1px solid var(--uui-color-border, #e0e0e0);
      flex-wrap: wrap;
    }

    .role-picker {
      display: flex;
      align-items: center;
      gap: 8px;
    }

    .role-picker label {
      font-weight: 600;
      white-space: nowrap;
    }

    .role-picker uui-select {
      min-width: 240px;
    }

    .loading {
      display: flex;
      justify-content: center;
      padding: 32px;
    }

    .error-msg {
      padding: 12px 18px;
      color: var(--uui-color-danger, #b91c1c);
    }

    .empty-msg {
      padding: 32px 18px;
      color: var(--uui-color-text-alt, #888);
    }

    /* ── Table ────────────────────────────────────────────────── */
    .table-wrap {
      overflow: auto;
    }

    table {
      border-collapse: collapse;
      width: 100%;
      min-width: max-content;
      font-size: 13px;
    }

    thead {
      position: sticky;
      top: 0;
      z-index: 2;
    }

    th {
      padding: 8px 10px;
      text-align: left;
      border-bottom: 2px solid var(--uui-color-border, #ddd);
      white-space: nowrap;
      font-weight: 600;
      background: var(--uui-color-surface-alt, #f5f5f5);
    }

    th.node-header {
      min-width: 240px;
      position: sticky;
      left: 0;
      z-index: 3;
    }

    th.verb-header {
      min-width: 72px;
      text-align: center;
    }

    td {
      border-bottom: 1px solid var(--uui-color-border, #eee);
    }

    tr:hover td {
      background-color: var(--uui-color-surface-emphasis, #f8f8f8);
    }

    tr.row-pending td {
      background-color: color-mix(in srgb, oklch(85% 0.15 90) 15%, transparent);
    }

    /* ── Node cell ────────────────────────────────────────────── */
    td.node-cell {
      padding: 5px 8px 5px calc(var(--depth, 0) * 20px + 8px);
      display: flex;
      align-items: center;
      gap: 6px;
      min-width: 240px;
      position: sticky;
      left: 0;
      background: inherit;
    }

    .expand-spacer {
      width: 16px;
      flex-shrink: 0;
    }

    .node-name {
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
      max-width: 180px;
    }

    /* ── Permission cells ─────────────────────────────────────── */
    .perm-cell {
      text-align: center;
      padding: 5px 4px;
      cursor: pointer;
      font-size: 11px;
      font-weight: 700;
      min-width: 72px;
      user-select: none;
    }

    .perm-cell:hover {
      opacity: 0.8;
    }

    .perm-cell.inherit {
      color: var(--uui-color-text-alt, #aaa);
    }

    .perm-cell.allow {
      background: color-mix(in srgb, var(--uui-color-positive, #34a853) 22%, transparent);
      color: color-mix(in srgb, var(--uui-color-positive, #1e7e34) 90%, #000);
    }

    .perm-cell.deny {
      background: color-mix(in srgb, var(--uui-color-danger, #ea4335) 18%, transparent);
      color: color-mix(in srgb, var(--uui-color-danger, #c5221f) 90%, #000);
    }

    .perm-cell.default-allow {
      background: color-mix(in srgb, var(--uui-color-positive, #34a853) 10%, transparent);
      color: color-mix(in srgb, var(--uui-color-positive, #1e7e34) 60%, #888);
      border: 1px dashed color-mix(in srgb, var(--uui-color-positive, #34a853) 40%, transparent);
      font-style: italic;
    }

    .perm-cell.cell-pending {
      outline: 2px dashed var(--uui-color-warning-standalone, #f59e0b);
      outline-offset: -2px;
    }

    /* ── Scope dialog ─────────────────────────────────────────── */
    .scope-dialog {
      border: none;
      border-radius: 8px;
      box-shadow: 0 8px 32px rgba(0, 0, 0, 0.25);
      padding: 0;
      min-width: 320px;
      max-width: 480px;
    }

    .scope-dialog::backdrop {
      background: rgba(0, 0, 0, 0.4);
    }

    .dialog-node {
      margin: 0 0 16px;
      font-size: 12px;
      color: var(--uui-color-text-alt, #666);
    }

    .dialog-options {
      margin-bottom: 8px;
    }

    uui-radio.opt-allow {
      --uui-radio-border-color: color-mix(in srgb, var(--uui-color-positive, #34a853) 50%, transparent);
    }

    uui-radio.opt-deny {
      --uui-radio-border-color: color-mix(in srgb, var(--uui-color-danger, #ea4335) 50%, transparent);
    }
  `;
p([
  h()
], n.prototype, "_roles", 2);
p([
  h()
], n.prototype, "_verbs", 2);
p([
  h()
], n.prototype, "_selectedRole", 2);
p([
  h()
], n.prototype, "_treeNodes", 2);
p([
  h()
], n.prototype, "_loading", 2);
p([
  h()
], n.prototype, "_saving", 2);
p([
  h()
], n.prototype, "_error", 2);
p([
  h()
], n.prototype, "_pendingChanges", 2);
p([
  h()
], n.prototype, "_pickerNode", 2);
p([
  h()
], n.prototype, "_pickerVerb", 2);
p([
  h()
], n.prototype, "_pickerValue", 2);
p([
  W(".scope-dialog")
], n.prototype, "_scopeDialog", 2);
n = p([
  L("uas-security-editor-root")
], n);
const ce = n;
export {
  n as UasSecurityEditorRootElement,
  ce as default
};
//# sourceMappingURL=uas-security-editor-root.element-B8sCfOMl.js.map
