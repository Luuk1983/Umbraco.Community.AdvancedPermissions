import { nothing as h, html as s, css as A, state as d, query as U, customElement as S } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement as T } from "@umbraco-cms/backoffice/lit-element";
import { UMB_NOTIFICATION_CONTEXT as V } from "@umbraco-cms/backoffice/notification";
import { g as D, a as M, c as O, f as K, h as j, e as B } from "./advanced-security.api-C-RLDPi4.js";
var F = Object.defineProperty, I = Object.getOwnPropertyDescriptor, $ = (e) => {
  throw TypeError(e);
}, c = (e, t, i, r) => {
  for (var l = r > 1 ? void 0 : r ? I(t, i) : t, u = e.length - 1, x; u >= 0; u--)
    (x = e[u]) && (l = (r ? x(t, i, l) : x(l)) || l);
  return r && l && F(t, i, l), l;
}, b = (e, t, i) => t.has(e) || $("Cannot " + i), g = (e, t, i) => (b(e, t, "read from private field"), i ? i.call(e) : t.get(e)), w = (e, t, i) => t.has(e) ? $("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, i), L = (e, t, i, r) => (b(e, t, "write to private field"), t.set(e, i), i), a = (e, t, i) => (b(e, t, "access private method"), i), m, o, k, f, N, v, y, C, p, P, _, R, E, z;
let n = class extends T {
  constructor() {
    super(), w(this, o), this._roles = [], this._verbs = [], this._mode = "role", this._selectedRole = "", this._resolvedUserKey = "", this._treeNodes = [], this._loading = !1, this._error = null, this._reasoningNode = null, this._reasoningVerb = null, this._reasoningPerm = null, w(this, m), this.consumeContext(V, (e) => {
      L(this, m, e ?? void 0);
    });
  }
  connectedCallback() {
    super.connectedCallback(), a(this, o, k).call(this);
  }
  render() {
    var e, t;
    return s`
      <umb-body-layout headline="Access Viewer">
        <div class="toolbar">
          <!-- Mode toggle -->
          <uui-button-group>
            <uui-button
              look=${this._mode === "role" ? "primary" : "secondary"}
              label="By Role"
              @click=${() => {
      this._mode = "role", this._treeNodes = [];
    }}>
              By Role
            </uui-button>
            <uui-button
              look=${this._mode === "user" ? "primary" : "secondary"}
              label="By User"
              @click=${() => {
      this._mode = "user", this._treeNodes = [];
    }}>
              By User
            </uui-button>
          </uui-button-group>

          ${this._mode === "role" ? s`
                <div class="subject-picker">
                  <label>Role:</label>
                  <uui-select
                    label="Role"
                    placeholder="— Select a role —"
                    .options=${g(this, o, N)}
                    @change=${(i) => {
      this._selectedRole = i.target.value, a(this, o, v).call(this);
    }}>
                  </uui-select>
                </div>
              ` : s`
                <div class="subject-picker">
                  <label>User:</label>
                  <umb-user-input
                    max="1"
                    @change=${(i) => {
      const r = i.target.value ?? "";
      this._resolvedUserKey = r, r ? a(this, o, v).call(this) : this._treeNodes = [];
    }}>
                  </umb-user-input>
                </div>
              `}
        </div>

        <div class="legend">
          <span class="legend-item allow">Allow</span>
          <span class="legend-item deny">Deny</span>
        </div>

        ${this._error ? s`<p class="error-msg">⚠ ${this._error}</p>` : h}
        ${this._loading ? s`<div class="loading"><uui-loader></uui-loader></div>` : h}
        ${g(this, o, f) ? h : s`<p class="empty-msg">Select a role or user to view effective permissions.</p>`}

        ${g(this, o, f) && !this._loading && this._treeNodes.length > 0 ? s`
              <div class="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th class="node-header">Content Node</th>
                      ${this._verbs.map((i) => s`<th class="verb-header" title=${i.verb}>${i.displayName}</th>`)}
                    </tr>
                  </thead>
                  <tbody>
                    ${a(this, o, _).call(this, this._treeNodes, 0)}
                  </tbody>
                </table>
              </div>
            ` : h}
      </umb-body-layout>

      <!-- Reasoning modal dialog -->
      <dialog
        class="reasoning-dialog"
        @close=${() => {
      this._reasoningNode = null, this._reasoningVerb = null, this._reasoningPerm = null;
    }}>
        <uui-dialog-layout
          headline="Permission Reasoning: ${((e = this._reasoningVerb) == null ? void 0 : e.split(".").pop()) ?? ""}">
          <p class="dialog-node">Node: <strong>${((t = this._reasoningNode) == null ? void 0 : t.name) ?? ""}</strong></p>

          ${this._reasoningPerm ? s`
                <div class="reasoning-result ${this._reasoningPerm.isAllowed ? "result-allow" : "result-deny"}">
                  <strong>${this._reasoningPerm.isAllowed ? "Allowed" : "Denied"}</strong>
                  — ${this._reasoningPerm.isExplicit ? "explicit (set directly on this node)" : "implicit (inherited or from group defaults)"}
                </div>
                <h3 class="reasoning-list-title">Contributing factors:</h3>
                ${this._reasoningPerm.reasoning.length > 0 ? s`<ul class="reasoning-list">
                      ${this._reasoningPerm.reasoning.map((i) => a(this, o, z).call(this, i))}
                    </ul>` : s`<p class="no-reasoning">No explicit entries found — effective permission comes from system defaults.</p>`}
              ` : s`<p class="no-reasoning">No effective permission data available for this verb.</p>`}

          <div slot="actions">
            <uui-button look="primary" @click=${() => this._reasoningDialog.close()}>Close</uui-button>
          </div>
        </uui-dialog-layout>
      </dialog>
    `;
  }
};
m = /* @__PURE__ */ new WeakMap();
o = /* @__PURE__ */ new WeakSet();
k = async function() {
  try {
    const [e, t] = await Promise.all([D(), M()]);
    this._roles = e, this._verbs = t;
  } catch (e) {
    this._error = String(e);
  }
};
f = function() {
  return this._mode === "role" ? this._selectedRole : this._resolvedUserKey;
};
N = function() {
  return [
    { name: "— Select a role —", value: "" },
    ...this._roles.map((e) => ({
      name: `${e.name}${e.isEveryone ? " (Everyone)" : ""}`,
      value: e.alias,
      selected: e.alias === this._selectedRole
    }))
  ];
};
v = async function() {
  if (g(this, o, f)) {
    this._loading = !0, this._error = null, this._treeNodes = [];
    try {
      const e = await O("$everyone");
      this._treeNodes = e.map((t) => ({
        key: t.key,
        name: t.name,
        icon: t.icon,
        hasChildren: t.hasChildren,
        expanded: !1,
        loading: !1,
        effectivePerms: null
      })), await Promise.all(this._treeNodes.map((t) => a(this, o, y).call(this, t)));
    } catch (e) {
      this._error = String(e);
    } finally {
      this._loading = !1;
    }
  }
};
y = async function(e) {
  if (g(this, o, f))
    try {
      const t = this._mode === "role" ? await K(this._selectedRole, e.key) : await j(this._resolvedUserKey, e.key), i = /* @__PURE__ */ new Map();
      for (const r of t.permissions)
        i.set(r.verb, r);
      e.effectivePerms = i, this._treeNodes = [...this._treeNodes];
    } catch {
    }
};
C = async function(e) {
  var t;
  if (e.expanded) {
    a(this, o, p).call(this, e.key, { expanded: !1 });
    return;
  }
  if (e.children) {
    a(this, o, p).call(this, e.key, { expanded: !0 });
    return;
  }
  a(this, o, p).call(this, e.key, { loading: !0 });
  try {
    const r = (await B(e.key, "$everyone")).map((l) => ({
      key: l.key,
      name: l.name,
      icon: l.icon,
      hasChildren: l.hasChildren,
      expanded: !1,
      loading: !1,
      effectivePerms: null
    }));
    a(this, o, p).call(this, e.key, { expanded: !0, loading: !1, children: r }), await Promise.all(r.map((l) => a(this, o, y).call(this, l)));
  } catch (i) {
    a(this, o, p).call(this, e.key, { loading: !1 }), (t = g(this, m)) == null || t.peek("danger", { data: { message: String(i) } });
  }
};
p = function(e, t, i = this._treeNodes) {
  for (let r = 0; r < i.length; r++) {
    if (i[r].key === e)
      return i[r] = { ...i[r], ...t }, this._treeNodes = [...this._treeNodes], !0;
    if (i[r].children && a(this, o, p).call(this, e, t, i[r].children))
      return !0;
  }
  return !1;
};
P = function(e, t) {
  var r;
  const i = ((r = e.effectivePerms) == null ? void 0 : r.get(t)) ?? null;
  this._reasoningNode = e, this._reasoningVerb = t, this._reasoningPerm = i, this.updateComplete.then(() => this._reasoningDialog.showModal());
};
_ = function(e, t) {
  return e.flatMap((i) => [
    a(this, o, R).call(this, i, t),
    ...i.expanded && i.children ? a(this, o, _).call(this, i.children, t + 1) : []
  ]);
};
R = function(e, t) {
  return s`
      <tr>
        <td class="node-cell" style="--depth: ${t}">
          ${e.hasChildren || e.children ? s`<uui-button compact look="default" label=${e.expanded ? "Collapse" : "Expand"} @click=${() => void a(this, o, C).call(this, e)}>
                ${e.loading ? s`<uui-loader-circle></uui-loader-circle>` : e.expanded ? "▾" : "▸"}
              </uui-button>` : s`<span class="expand-spacer"></span>`}
          <umb-icon name=${e.icon ?? "icon-document"}></umb-icon>
          <span class="node-name">${e.name}</span>
        </td>
        ${this._verbs.map((i) => a(this, o, E).call(this, e, i.verb))}
      </tr>
    `;
};
E = function(e, t) {
  if (!e.effectivePerms)
    return s`<td class="perm-cell loading-cell" title=${t}>…</td>`;
  const i = e.effectivePerms.get(t), r = (i == null ? void 0 : i.isAllowed) ?? !1, l = r ? "allow" : "deny", u = r ? "Allow" : "Deny";
  return s`
      <td
        class="perm-cell ${l}"
        title="${u} — click for reasoning"
        @click=${() => a(this, o, P).call(this, e, t)}>
        ${u}
      </td>
    `;
};
z = function(e) {
  const t = e.state === "Allow" ? "step-allow" : "step-deny";
  return s`
      <li class="reasoning-step ${t}">
        <span class="step-state">${e.state}</span>
        <span class="step-role">${e.contributingRole}</span>
        ${e.isFromGroupDefault ? s`<span class="step-source">group default</span>` : e.sourceNodeKey ? s`<span class="step-source">
                from node · ${e.sourceScope ?? ""}
                ${e.isExplicit ? "" : "(inherited)"}
              </span>` : h}
        ${e.isExplicit ? h : s`<span class="step-implicit">implicit</span>`}
      </li>
    `;
};
n.styles = A`
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

    .subject-picker {
      display: flex;
      align-items: center;
      gap: 8px;
    }

    .subject-picker label {
      font-weight: 600;
      white-space: nowrap;
    }

    .subject-picker uui-select {
      min-width: 240px;
    }

    /* ── Legend ───────────────────────────────────────────────── */
    .legend {
      display: flex;
      gap: 16px;
      flex-wrap: wrap;
      padding: 8px 18px;
      font-size: 11px;
      background: var(--uui-color-surface-alt, #f8f8f8);
      border-bottom: 1px solid var(--uui-color-border, #eee);
    }

    .legend-item {
      padding: 2px 8px;
      border-radius: 4px;
      font-weight: 600;
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

    /* ── Effective permission cells ───────────────────────────── */
    .perm-cell {
      text-align: center;
      padding: 5px 4px;
      cursor: pointer;
      font-size: 13px;
      font-weight: 700;
      min-width: 72px;
      user-select: none;
    }

    .perm-cell.loading-cell {
      color: var(--uui-color-text-alt, #aaa);
      font-size: 11px;
      font-weight: 400;
    }

    .perm-cell.allow {
      background: color-mix(in srgb, var(--uui-color-positive, #34a853) 30%, transparent);
      color: color-mix(in srgb, var(--uui-color-positive, #1e7e34) 90%, #000);
    }

    .perm-cell.deny {
      background: color-mix(in srgb, var(--uui-color-danger, #ea4335) 25%, transparent);
      color: color-mix(in srgb, var(--uui-color-danger, #c5221f) 90%, #000);
    }

    .perm-cell:hover {
      opacity: 0.8;
    }

    /* ── Legend items (reuse cell colors) ─────────────────────── */
    .legend-item.allow {
      background: color-mix(in srgb, var(--uui-color-positive, #34a853) 30%, transparent);
      color: color-mix(in srgb, var(--uui-color-positive, #1e7e34) 90%, #000);
    }
    .legend-item.deny {
      background: color-mix(in srgb, var(--uui-color-danger, #ea4335) 25%, transparent);
      color: color-mix(in srgb, var(--uui-color-danger, #c5221f) 90%, #000);
    }

    /* ── Reasoning dialog ─────────────────────────────────────── */
    .reasoning-dialog {
      border: none;
      border-radius: 8px;
      box-shadow: 0 8px 32px rgba(0, 0, 0, 0.25);
      padding: 0;
      min-width: 360px;
      max-width: 540px;
    }

    .reasoning-dialog::backdrop {
      background: rgba(0, 0, 0, 0.4);
    }

    .dialog-node {
      margin: 0 0 16px;
      font-size: 12px;
      color: var(--uui-color-text-alt, #666);
    }

    .reasoning-result {
      padding: 10px 14px;
      border-radius: 6px;
      margin-bottom: 16px;
      font-size: 14px;
    }

    .reasoning-result.result-allow {
      background: color-mix(in srgb, var(--uui-color-positive, #34a853) 20%, transparent);
      border-left: 4px solid var(--uui-color-positive, #34a853);
    }

    .reasoning-result.result-deny {
      background: color-mix(in srgb, var(--uui-color-danger, #ea4335) 15%, transparent);
      border-left: 4px solid var(--uui-color-danger, #ea4335);
    }

    .reasoning-list-title {
      font-size: 13px;
      font-weight: 600;
      margin: 0 0 8px;
    }

    .reasoning-list {
      list-style: none;
      padding: 0;
      margin: 0 0 20px;
      display: flex;
      flex-direction: column;
      gap: 6px;
    }

    .reasoning-step {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 6px 10px;
      border-radius: 4px;
      font-size: 12px;
      flex-wrap: wrap;
    }

    .reasoning-step.step-allow {
      background: color-mix(in srgb, var(--uui-color-positive, #34a853) 12%, transparent);
    }

    .reasoning-step.step-deny {
      background: color-mix(in srgb, var(--uui-color-danger, #ea4335) 10%, transparent);
    }

    .step-state {
      font-weight: 700;
      min-width: 36px;
    }

    .step-role {
      font-weight: 600;
      font-family: monospace;
    }

    .step-source,
    .step-implicit {
      font-size: 11px;
      color: var(--uui-color-text-alt, #777);
    }

    .no-reasoning {
      color: var(--uui-color-text-alt, #888);
      font-size: 13px;
    }

    .dialog-actions {
      display: flex;
      justify-content: flex-end;
    }
  `;
c([
  d()
], n.prototype, "_roles", 2);
c([
  d()
], n.prototype, "_verbs", 2);
c([
  d()
], n.prototype, "_mode", 2);
c([
  d()
], n.prototype, "_selectedRole", 2);
c([
  d()
], n.prototype, "_resolvedUserKey", 2);
c([
  d()
], n.prototype, "_treeNodes", 2);
c([
  d()
], n.prototype, "_loading", 2);
c([
  d()
], n.prototype, "_error", 2);
c([
  d()
], n.prototype, "_reasoningNode", 2);
c([
  d()
], n.prototype, "_reasoningVerb", 2);
c([
  d()
], n.prototype, "_reasoningPerm", 2);
c([
  U(".reasoning-dialog")
], n.prototype, "_reasoningDialog", 2);
n = c([
  S("uas-access-viewer-root")
], n);
const H = n;
export {
  n as UasAccessViewerRootElement,
  H as default
};
//# sourceMappingURL=uas-access-viewer-root.element-DiJbNBTI.js.map
