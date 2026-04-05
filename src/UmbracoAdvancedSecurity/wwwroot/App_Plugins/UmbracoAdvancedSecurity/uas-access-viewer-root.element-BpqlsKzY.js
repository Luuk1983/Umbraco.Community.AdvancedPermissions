import { nothing as g, html as l, css as A, state as u, query as D, customElement as M } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement as S } from "@umbraco-cms/backoffice/lit-element";
import { UmbLocalizationController as T } from "@umbraco-cms/backoffice/localization-api";
import { UMB_NOTIFICATION_CONTEXT as V } from "@umbraco-cms/backoffice/notification";
import { g as L, a as O, c as j, f as F, h as K, e as I } from "./advanced-security.api-C-RLDPi4.js";
var W = Object.defineProperty, H = Object.getOwnPropertyDescriptor, k = (e) => {
  throw TypeError(e);
}, p = (e, t, r, o) => {
  for (var d = o > 1 ? void 0 : o ? H(t, r) : t, m = e.length - 1, b; m >= 0; m--)
    (b = e[m]) && (d = (o ? b(t, r, d) : b(d)) || d);
  return o && d && W(t, r, d), d;
}, w = (e, t, r) => t.has(e) || k("Cannot " + r), i = (e, t, r) => (w(e, t, "read from private field"), r ? r.call(e) : t.get(e)), x = (e, t, r) => t.has(e) ? k("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, r), G = (e, t, r, o) => (w(e, t, "write to private field"), t.set(e, r), r), n = (e, t, r) => (w(e, t, "access private method"), r), s, _, a, N, f, P, v, $, R, h, z, y, C, E, U;
let c = class extends S {
  constructor() {
    super(), x(this, a), x(this, s, new T(this)), this._roles = [], this._verbs = [], this._mode = "role", this._selectedRole = "", this._resolvedUserKey = "", this._treeNodes = [], this._loading = !1, this._error = null, this._reasoningNode = null, this._reasoningVerb = null, this._reasoningPerm = null, x(this, _), this.consumeContext(V, (e) => {
      G(this, _, e ?? void 0);
    });
  }
  connectedCallback() {
    super.connectedCallback(), n(this, a, N).call(this);
  }
  render() {
    var e, t;
    return l`
      <umb-body-layout headline=${i(this, s).term("uas_viewerHeadline")}>
        <div class="toolbar">
          <!-- Mode toggle -->
          <uui-button-group>
            <uui-button
              look=${this._mode === "role" ? "primary" : "secondary"}
              label=${i(this, s).term("uas_byRole")}
              @click=${() => {
      this._mode = "role", this._treeNodes = [];
    }}>
              ${i(this, s).term("uas_byRole")}
            </uui-button>
            <uui-button
              look=${this._mode === "user" ? "primary" : "secondary"}
              label=${i(this, s).term("uas_byUser")}
              @click=${() => {
      this._mode = "user", this._treeNodes = [];
    }}>
              ${i(this, s).term("uas_byUser")}
            </uui-button>
          </uui-button-group>

          ${this._mode === "role" ? l`
                <div class="subject-picker">
                  <label>${i(this, s).term("uas_roleLabel")}:</label>
                  <uui-select
                    label=${i(this, s).term("uas_roleLabel")}
                    placeholder=${i(this, s).term("uas_rolePlaceholder")}
                    .options=${i(this, a, P)}
                    @change=${(r) => {
      this._selectedRole = r.target.value, n(this, a, v).call(this);
    }}>
                  </uui-select>
                </div>
              ` : l`
                <div class="subject-picker">
                  <label>${i(this, s).term("uas_userLabel")}:</label>
                  <umb-user-input
                    max="1"
                    @change=${(r) => {
      const o = r.target.value ?? "";
      this._resolvedUserKey = o, o ? n(this, a, v).call(this) : this._treeNodes = [];
    }}>
                  </umb-user-input>
                </div>
              `}
        </div>

        <div class="legend">
          <span class="legend-item allow">${i(this, s).term("uas_legendAllow")}</span>
          <span class="legend-item deny">${i(this, s).term("uas_legendDeny")}</span>
        </div>

        ${this._error ? l`<p class="error-msg">\u26a0 ${this._error}</p>` : g}
        ${this._loading ? l`<div class="loading"><uui-loader></uui-loader></div>` : g}
        ${i(this, a, f) ? g : l`<p class="empty-msg">${i(this, s).term("uas_selectSubjectPrompt")}</p>`}

        ${i(this, a, f) && !this._loading && this._treeNodes.length > 0 ? l`
              <div class="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th class="node-header">${i(this, s).term("uas_contentNodeHeader")}</th>
                      ${this._verbs.map((r) => l`<th class="verb-header" title=${r.verb}>${r.displayName}</th>`)}
                    </tr>
                  </thead>
                  <tbody>
                    ${n(this, a, y).call(this, this._treeNodes, 0)}
                  </tbody>
                </table>
              </div>
            ` : g}
      </umb-body-layout>

      <!-- Reasoning modal dialog -->
      <dialog
        class="reasoning-dialog"
        @close=${() => {
      this._reasoningNode = null, this._reasoningVerb = null, this._reasoningPerm = null;
    }}>
        <uui-dialog-layout
          headline=${i(this, s).term("uas_reasoningHeadline", ((e = this._reasoningVerb) == null ? void 0 : e.split(".").pop()) ?? "")}>
          <p class="dialog-node">
            ${i(this, s).term("uas_reasoningNodeLabel")}: <strong>${((t = this._reasoningNode) == null ? void 0 : t.name) ?? ""}</strong>
          </p>

          ${this._reasoningPerm ? l`
                <div class="reasoning-result ${this._reasoningPerm.isAllowed ? "result-allow" : "result-deny"}">
                  <strong>${this._reasoningPerm.isAllowed ? i(this, s).term("uas_resultAllowed") : i(this, s).term("uas_resultDenied")}</strong>
                  \u2014 ${this._reasoningPerm.isExplicit ? i(this, s).term("uas_resultExplicit") : i(this, s).term("uas_resultImplicit")}
                </div>
                <h3 class="reasoning-list-title">${i(this, s).term("uas_contributingFactors")}</h3>
                ${this._reasoningPerm.reasoning.length > 0 ? l`<ul class="reasoning-list">
                      ${this._reasoningPerm.reasoning.map((r) => n(this, a, U).call(this, r))}
                    </ul>` : l`<p class="no-reasoning">${i(this, s).term("uas_noReasoningEntries")}</p>`}
              ` : l`<p class="no-reasoning">${i(this, s).term("uas_noReasoningData")}</p>`}

          <div slot="actions">
            <uui-button look="primary" @click=${() => this._reasoningDialog.close()}>
              ${i(this, s).term("uas_close")}
            </uui-button>
          </div>
        </uui-dialog-layout>
      </dialog>
    `;
  }
};
s = /* @__PURE__ */ new WeakMap();
_ = /* @__PURE__ */ new WeakMap();
a = /* @__PURE__ */ new WeakSet();
N = async function() {
  try {
    const [e, t] = await Promise.all([L(), O()]);
    this._roles = e, this._verbs = t;
  } catch (e) {
    this._error = String(e);
  }
};
f = function() {
  return this._mode === "role" ? this._selectedRole : this._resolvedUserKey;
};
P = function() {
  return [
    { name: i(this, s).term("uas_rolePlaceholder"), value: "" },
    ...this._roles.map((e) => ({
      name: `${e.name}${e.isEveryone ? ` ${i(this, s).term("uas_everyoneSuffix")}` : ""}`,
      value: e.alias,
      selected: e.alias === this._selectedRole
    }))
  ];
};
v = async function() {
  if (i(this, a, f)) {
    this._loading = !0, this._error = null, this._treeNodes = [];
    try {
      const e = await j("$everyone");
      this._treeNodes = e.map((t) => ({
        key: t.key,
        name: t.name,
        icon: t.icon,
        hasChildren: t.hasChildren,
        expanded: !1,
        loading: !1,
        effectivePerms: null
      })), await Promise.all(this._treeNodes.map((t) => n(this, a, $).call(this, t)));
    } catch (e) {
      this._error = String(e);
    } finally {
      this._loading = !1;
    }
  }
};
$ = async function(e) {
  if (i(this, a, f))
    try {
      const t = this._mode === "role" ? await F(this._selectedRole, e.key) : await K(this._resolvedUserKey, e.key), r = /* @__PURE__ */ new Map();
      for (const o of t.permissions)
        r.set(o.verb, o);
      e.effectivePerms = r, this._treeNodes = [...this._treeNodes];
    } catch {
    }
};
R = async function(e) {
  var t;
  if (e.expanded) {
    n(this, a, h).call(this, e.key, { expanded: !1 });
    return;
  }
  if (e.children) {
    n(this, a, h).call(this, e.key, { expanded: !0 });
    return;
  }
  n(this, a, h).call(this, e.key, { loading: !0 });
  try {
    const o = (await I(e.key, "$everyone")).map((d) => ({
      key: d.key,
      name: d.name,
      icon: d.icon,
      hasChildren: d.hasChildren,
      expanded: !1,
      loading: !1,
      effectivePerms: null
    }));
    n(this, a, h).call(this, e.key, { expanded: !0, loading: !1, children: o }), await Promise.all(o.map((d) => n(this, a, $).call(this, d)));
  } catch (r) {
    n(this, a, h).call(this, e.key, { loading: !1 }), (t = i(this, _)) == null || t.peek("danger", { data: { message: String(r) } });
  }
};
h = function(e, t, r = this._treeNodes) {
  for (let o = 0; o < r.length; o++) {
    if (r[o].key === e)
      return r[o] = { ...r[o], ...t }, this._treeNodes = [...this._treeNodes], !0;
    if (r[o].children && n(this, a, h).call(this, e, t, r[o].children))
      return !0;
  }
  return !1;
};
z = function(e, t) {
  var o;
  const r = ((o = e.effectivePerms) == null ? void 0 : o.get(t)) ?? null;
  this._reasoningNode = e, this._reasoningVerb = t, this._reasoningPerm = r, this.updateComplete.then(() => this._reasoningDialog.showModal());
};
y = function(e, t) {
  return e.flatMap((r) => [
    n(this, a, C).call(this, r, t),
    ...r.expanded && r.children ? n(this, a, y).call(this, r.children, t + 1) : []
  ]);
};
C = function(e, t) {
  return l`
      <tr>
        <td class="node-cell">
          <div class="node-inner" style="--depth: ${t}">
            ${e.hasChildren || e.children ? l`<uui-button compact look="default"
                  label=${e.expanded ? i(this, s).term("uas_collapse") : i(this, s).term("uas_expand")}
                  @click=${() => void n(this, a, R).call(this, e)}>
                  ${e.loading ? l`<uui-loader-circle></uui-loader-circle>` : e.expanded ? "▾" : "▸"}
                </uui-button>` : l`<span class="expand-spacer"></span>`}
            <umb-icon name=${e.icon ?? "icon-document"}></umb-icon>
            <span class="node-name">${e.name}</span>
          </div>
        </td>
        ${this._verbs.map((r) => n(this, a, E).call(this, e, r.verb))}
      </tr>
    `;
};
E = function(e, t) {
  if (!e.effectivePerms)
    return l`<td class="perm-td" title=${t}><div class="perm-block loading">\u2026</div></td>`;
  const r = e.effectivePerms.get(t), o = (r == null ? void 0 : r.isAllowed) ?? !1, d = o ? "allow" : "deny", m = o ? "✓" : "✗";
  return l`
      <td class="perm-td" title=${i(this, s).term("uas_clickForReasoning", o ? i(this, s).term("uas_allow") : i(this, s).term("uas_deny"))}
        @click=${() => n(this, a, z).call(this, e, t)}>
        <div class="perm-block ${d}">${m}</div>
      </td>
    `;
};
U = function(e) {
  const t = e.state === "Allow" ? "step-allow" : "step-deny";
  return l`
      <li class="reasoning-step ${t}">
        <span class="step-state">${e.state === "Allow" ? i(this, s).term("uas_allow") : i(this, s).term("uas_deny")}</span>
        <span class="step-role">${e.contributingRole}</span>
        ${e.isFromGroupDefault ? l`<span class="step-source">${i(this, s).term("uas_groupDefault")}</span>` : e.sourceNodeKey ? l`<span class="step-source">
                ${i(this, s).term("uas_fromNode")} \u00b7 ${e.sourceScope ?? ""}
                ${e.isExplicit ? "" : i(this, s).term("uas_inherited")}
              </span>` : g}
        ${e.isExplicit ? g : l`<span class="step-implicit">${i(this, s).term("uas_resultImplicit")}</span>`}
      </li>
    `;
};
c.styles = A`
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
      overflow-x: auto;
    }

    table {
      width: 100%;
      border-collapse: collapse;
      table-layout: fixed;
      font-size: 12px;
    }

    thead {
      position: sticky;
      top: 0;
      z-index: 2;
    }

    th {
      padding: 6px 4px;
      text-align: center;
      border-bottom: 1px solid var(--uui-color-border, #ddd);
      font-weight: 600;
      font-size: 11px;
      line-height: 1.2;
      background: var(--uui-color-surface, #fff);
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
      color: var(--uui-color-text-alt, #666);
    }

    th.node-header {
      width: 40%;
      text-align: left;
      padding-left: 8px;
      position: sticky;
      left: 0;
      z-index: 3;
      font-size: 12px;
      color: var(--uui-color-text, #333);
    }

    td {
      border-bottom: 1px solid var(--uui-color-border, #f0f0f0);
    }

    tr:hover td {
      background-color: var(--uui-color-surface-emphasis, #fafafa);
    }

    /* ── Node cell ────────────────────────────────────────────── */
    td.node-cell {
      padding: 0;
      position: sticky;
      left: 0;
      background: inherit;
      vertical-align: middle;
    }

    .node-inner {
      display: flex;
      align-items: center;
      gap: 4px;
      padding: 0 8px 0 calc(var(--depth, 0) * 18px + 8px);
      height: 32px;
      white-space: nowrap;
      overflow: hidden;
    }

    .expand-spacer {
      width: 16px;
      flex-shrink: 0;
    }

    .node-name {
      font-size: 12px;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    /* ── Permission blocks ────────────────────────────────────── */
    .perm-td {
      padding: 3px;
      text-align: center;
      vertical-align: middle;
      cursor: pointer;
    }

    .perm-block {
      display: flex;
      align-items: center;
      justify-content: center;
      height: 26px;
      border: 1px solid var(--uui-color-border, #ddd);
      border-radius: 4px;
      user-select: none;
      overflow: hidden;
      font-size: 13px;
      font-weight: 700;
    }

    .perm-block:hover {
      border-color: var(--uui-color-border-emphasis, #bbb);
    }

    .perm-block.loading {
      color: var(--uui-color-text-alt, #aaa);
      font-size: 11px;
      font-weight: 400;
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

    /* ── Legend ───────────────────────────────────────────────── */
    .legend-item.allow {
      background: color-mix(in srgb, var(--uui-color-positive, #34a853) 14%, transparent);
      color: color-mix(in srgb, var(--uui-color-positive, #1e7e34) 80%, #000);
      border: 1px solid color-mix(in srgb, var(--uui-color-positive, #34a853) 30%, transparent);
      border-radius: 4px;
    }
    .legend-item.deny {
      background: color-mix(in srgb, var(--uui-color-danger, #ea4335) 12%, transparent);
      color: color-mix(in srgb, var(--uui-color-danger, #c5221f) 80%, #000);
      border: 1px solid color-mix(in srgb, var(--uui-color-danger, #ea4335) 25%, transparent);
      border-radius: 4px;
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
p([
  u()
], c.prototype, "_roles", 2);
p([
  u()
], c.prototype, "_verbs", 2);
p([
  u()
], c.prototype, "_mode", 2);
p([
  u()
], c.prototype, "_selectedRole", 2);
p([
  u()
], c.prototype, "_resolvedUserKey", 2);
p([
  u()
], c.prototype, "_treeNodes", 2);
p([
  u()
], c.prototype, "_loading", 2);
p([
  u()
], c.prototype, "_error", 2);
p([
  u()
], c.prototype, "_reasoningNode", 2);
p([
  u()
], c.prototype, "_reasoningVerb", 2);
p([
  u()
], c.prototype, "_reasoningPerm", 2);
p([
  D(".reasoning-dialog")
], c.prototype, "_reasoningDialog", 2);
c = p([
  M("uas-access-viewer-root")
], c);
const Y = c;
export {
  c as UasAccessViewerRootElement,
  Y as default
};
//# sourceMappingURL=uas-access-viewer-root.element-BpqlsKzY.js.map
