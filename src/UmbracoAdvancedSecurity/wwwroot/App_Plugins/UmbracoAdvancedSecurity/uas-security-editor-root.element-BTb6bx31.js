import { nothing as v, html as p, css as F, state as h, query as q, customElement as B } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement as G } from "@umbraco-cms/backoffice/lit-element";
import { UmbLocalizationController as X } from "@umbraco-cms/backoffice/localization-api";
import { UMB_NOTIFICATION_CONTEXT as j } from "@umbraco-cms/backoffice/notification";
import { g as J, a as Q, b as Y, c as Z, d as ee, e as te } from "./advanced-security.api-C-RLDPi4.js";
import { clearEffectivePermissionCache as ie } from "./document-user-permission.condition-CAI2T48b.js";
var se = Object.defineProperty, oe = Object.getOwnPropertyDescriptor, R = (e) => {
  throw TypeError(e);
}, u = (e, t, i, s) => {
  for (var c = s > 1 ? void 0 : s ? oe(t, i) : t, n = e.length - 1, _; n >= 0; n--)
    (_ = e[n]) && (c = (s ? _(t, i, c) : _(c)) || c);
  return s && c && se(t, i, c), c;
}, N = (e, t, i) => t.has(e) || R("Cannot " + i), o = (e, t, i) => (N(e, t, "read from private field"), i ? i.call(e) : t.get(e)), x = (e, t, i) => t.has(e) ? R("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, i), re = (e, t, i, s) => (N(e, t, "write to private field"), t.set(e, i), i), l = (e, t, i) => (N(e, t, "access private method"), i), r, b, a, D, P, E, z, f, k, T, V, $, S, M, w, O, y, I, U, L, K;
function W(e) {
  let t = "inherit", i = "inherit";
  for (const c of e) {
    const n = c.state === "Allow" ? "allow" : "deny";
    switch (c.scope) {
      case "ThisNodeAndDescendants":
        t = n, i = n;
        break;
      case "ThisNodeOnly":
        t = n;
        break;
      case "DescendantsOnly":
        i = n;
        break;
    }
  }
  return { nodeState: t, descState: i, sameAsNode: t === i };
}
function ae(e, t, i) {
  const s = i ? e : t;
  if (e === "inherit" && s === "inherit") return [];
  if (e === s)
    return [{ state: e === "allow" ? "Allow" : "Deny", scope: "ThisNodeAndDescendants" }];
  const c = [];
  return e !== "inherit" && c.push({ state: e === "allow" ? "Allow" : "Deny", scope: "ThisNodeOnly" }), s !== "inherit" && c.push({ state: s === "allow" ? "Allow" : "Deny", scope: "DescendantsOnly" }), c;
}
let d = class extends G {
  constructor() {
    super(), x(this, a), x(this, r, new X(this)), this._roles = [], this._verbs = [], this._selectedRole = "", this._treeNodes = [], this._loading = !1, this._saving = !1, this._error = null, this._pendingChanges = /* @__PURE__ */ new Map(), this._pickerNode = null, this._pickerVerb = null, this._pickerIsVirtualRoot = !1, this._pickerNodeState = "inherit", this._pickerDescState = "inherit", this._pickerSameAsNode = !0, x(this, b), this.consumeContext(j, (e) => {
      re(this, b, e ?? void 0);
    });
  }
  connectedCallback() {
    super.connectedCallback(), l(this, a, P).call(this);
  }
  render() {
    const e = this._pendingChanges.size > 0;
    return p`
      <umb-body-layout headline=${o(this, r).term("uas_editorHeadline")}>
        <div class="toolbar">
          <div class="role-picker">
            <label>${o(this, r).term("uas_roleLabel")}:</label>
            <uui-select
              label=${o(this, r).term("uas_roleLabel")}
              placeholder=${o(this, r).term("uas_rolePlaceholder")}
              .options=${o(this, a, D)}
              @change=${(t) => {
      this._selectedRole = t.target.value, this._pendingChanges = /* @__PURE__ */ new Map(), l(this, a, E).call(this);
    }}>
            </uui-select>
          </div>
          ${e ? p`
                <uui-button look="primary" color="positive" ?loading=${this._saving} @click=${() => void l(this, a, V).call(this)}>
                  ${o(this, r).term("uas_saveChanges")}
                </uui-button>
                <uui-button look="outline" @click=${() => {
      this._pendingChanges = /* @__PURE__ */ new Map();
    }}>
                  ${o(this, r).term("uas_discard")}
                </uui-button>
              ` : v}
        </div>

        ${this._error ? p`<p class="error-msg">\u26a0 ${this._error}</p>` : v}
        ${this._loading ? p`<div class="loading"><uui-loader></uui-loader></div>` : v}
        ${this._selectedRole ? v : p`<p class="empty-msg">${o(this, r).term("uas_selectRolePrompt")}</p>`}

        ${this._selectedRole && !this._loading && this._treeNodes.length > 0 ? p`
              <div class="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th class="node-header">${o(this, r).term("uas_contentNodeHeader")}</th>
                      ${this._verbs.map((t) => p`<th class="verb-header" title=${t.verb}>${t.displayName}</th>`)}
                    </tr>
                  </thead>
                  <tbody>
                    ${l(this, a, w).call(this, this._treeNodes, 0)}
                  </tbody>
                </table>
              </div>
            ` : v}
      </umb-body-layout>

      <!-- Permission dialog — rendered outside umb-body-layout so it always layers on top -->
      ${l(this, a, U).call(this)}
    `;
  }
};
r = /* @__PURE__ */ new WeakMap();
b = /* @__PURE__ */ new WeakMap();
a = /* @__PURE__ */ new WeakSet();
D = function() {
  return [
    { name: o(this, r).term("uas_rolePlaceholder"), value: "" },
    ...this._roles.map((e) => ({
      name: `${e.name}${e.isEveryone ? ` ${o(this, r).term("uas_everyoneSuffix")}` : ""}`,
      value: e.alias,
      selected: e.alias === this._selectedRole
    }))
  ];
};
P = async function() {
  try {
    const [e, t] = await Promise.all([J(), Q()]);
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
        Y(null, this._selectedRole),
        Z(this._selectedRole)
      ]), i = {
        key: "virtual-root",
        name: o(this, r).term("uas_contentRoot"),
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
z = async function(e) {
  if (e.expanded) {
    l(this, a, f).call(this, e.key, { expanded: !1 });
    return;
  }
  if (e.children) {
    l(this, a, f).call(this, e.key, { expanded: !0 });
    return;
  }
  l(this, a, f).call(this, e.key, { loading: !0 });
  try {
    const t = await te(e.key, this._selectedRole);
    l(this, a, f).call(this, e.key, {
      expanded: !0,
      loading: !1,
      children: t.map((i) => ({ ...i, expanded: !1, loading: !1 }))
    });
  } catch (t) {
    l(this, a, f).call(this, e.key, { loading: !1 }), this._error = String(t);
  }
};
f = function(e, t, i = this._treeNodes) {
  for (let s = 0; s < i.length; s++) {
    if (i[s].key === e)
      return i[s] = { ...i[s], ...t }, this._treeNodes = [...this._treeNodes], !0;
    if (i[s].children && l(this, a, f).call(this, e, t, i[s].children))
      return !0;
  }
  return !1;
};
k = function(e, t) {
  this._pickerNode = e, this._pickerVerb = t, this._pickerIsVirtualRoot = e.key === "virtual-root";
  const i = l(this, a, S).call(this, e, t);
  if (this._pickerIsVirtualRoot) {
    const s = i[0];
    this._pickerNodeState = s ? s.state === "Allow" ? "allow" : "deny" : "inherit", this._pickerDescState = "inherit", this._pickerSameAsNode = !0;
  } else {
    const s = W(i);
    this._pickerNodeState = s.nodeState, this._pickerDescState = s.descState, this._pickerSameAsNode = s.sameAsNode;
  }
  this.updateComplete.then(() => this._scopeDialog.showModal());
};
T = function() {
  if (!this._pickerNode || !this._pickerVerb) return;
  let e;
  this._pickerIsVirtualRoot ? this._pickerNodeState === "inherit" ? e = [] : e = [{ state: this._pickerNodeState === "allow" ? "Allow" : "Deny", scope: "ThisNodeAndDescendants" }] : e = ae(this._pickerNodeState, this._pickerDescState, this._pickerSameAsNode);
  const t = this._pickerNode.key, i = this._pickerVerb, s = this._pendingChanges.get(t) ?? /* @__PURE__ */ new Map();
  s.set(i, e), this._pendingChanges = new Map(this._pendingChanges).set(t, s), this._scopeDialog.close();
};
V = async function() {
  var e, t;
  if (!(!this._pendingChanges.size || !this._selectedRole || this._saving)) {
    this._saving = !0;
    try {
      for (const [i, s] of this._pendingChanges) {
        const c = l(this, a, $).call(this, i);
        if (!c) continue;
        const n = /* @__PURE__ */ new Map();
        for (const g of c.entries) {
          const m = n.get(g.verb) ?? [];
          m.push({ verb: g.verb, state: g.state, scope: g.scope }), n.set(g.verb, m);
        }
        for (const [g, m] of s)
          m.length === 0 ? n.delete(g) : n.set(g, m.map((A) => ({ verb: g, state: A.state, scope: A.scope })));
        const _ = [...n.values()].flat(), C = i === "virtual-root" ? null : i;
        await ee(C, this._selectedRole, _);
        const H = _.map((g, m) => ({
          id: m,
          nodeKey: C,
          roleAlias: this._selectedRole,
          verb: g.verb,
          state: g.state,
          scope: g.scope
        }));
        l(this, a, f).call(this, i, { entries: H });
      }
      this._pendingChanges = /* @__PURE__ */ new Map(), ie(), (e = o(this, b)) == null || e.peek("positive", { data: { message: o(this, r).term("uas_permissionsSaved") } });
    } catch (i) {
      (t = o(this, b)) == null || t.peek("danger", { data: { message: o(this, r).term("uas_saveFailed", String(i)) } });
    } finally {
      this._saving = !1;
    }
  }
};
$ = function(e, t = this._treeNodes) {
  for (const i of t) {
    if (i.key === e) return i;
    if (i.children) {
      const s = l(this, a, $).call(this, e, i.children);
      if (s) return s;
    }
  }
  return null;
};
S = function(e, t) {
  const i = this._pendingChanges.get(e.key);
  return i != null && i.has(t) ? i.get(t) : e.entries.filter((s) => s.verb === t);
};
M = function(e) {
  if (e.length === 0)
    return { split: !1, nodeClass: "inherit", descClass: "inherit" };
  const t = W(e), i = (s) => s;
  return t.sameAsNode ? { split: !1, nodeClass: i(t.nodeState), descClass: i(t.nodeState) } : { split: !0, nodeClass: i(t.nodeState), descClass: i(t.descState) };
};
w = function(e, t) {
  return e.flatMap((i) => [
    l(this, a, O).call(this, i, t),
    ...i.expanded && i.children ? l(this, a, w).call(this, i.children, t + 1) : []
  ]);
};
O = function(e, t) {
  const i = this._pendingChanges.has(e.key);
  return p`
      <tr class=${i ? "row-pending" : ""}>
        <td class="node-cell">
          <div class="node-inner" style="--depth: ${t}">
            ${e.hasChildren || e.children ? p`<uui-button compact look="default"
                  label=${e.expanded ? o(this, r).term("uas_collapse") : o(this, r).term("uas_expand")}
                  @click=${() => void l(this, a, z).call(this, e)}>
                  ${e.loading ? p`<uui-loader-circle></uui-loader-circle>` : e.expanded ? "▾" : "▸"}
                </uui-button>` : p`<span class="expand-spacer"></span>`}
            <umb-icon name=${e.icon ?? "icon-document"}></umb-icon>
            <span class="node-name">${e.name}</span>
          </div>
        </td>
        ${this._verbs.map((s) => l(this, a, I).call(this, e, s.verb))}
      </tr>
    `;
};
y = function(e) {
  return e === "allow" ? "✓" : e === "deny" ? "✗" : "—";
};
I = function(e, t) {
  var _;
  const i = l(this, a, S).call(this, e, t), c = ((_ = this._pendingChanges.get(e.key)) == null ? void 0 : _.has(t)) ?? !1 ? " pending" : "";
  if (i.length === 0)
    return p`
        <td class="perm-td" title=${t} @click=${() => l(this, a, k).call(this, e, t)}>
          <div class="perm-block uniform inherit${c}">\u2014</div>
        </td>
      `;
  const n = l(this, a, M).call(this, i);
  return n.split ? p`
      <td class="perm-td" title=${t} @click=${() => l(this, a, k).call(this, e, t)}>
        <div class="perm-block split${c}">
          <span class="half ${n.nodeClass}">${l(this, a, y).call(this, n.nodeClass)}</span>
          <span class="half ${n.descClass}">${l(this, a, y).call(this, n.descClass)}</span>
        </div>
      </td>
    ` : p`
        <td class="perm-td" title=${t} @click=${() => l(this, a, k).call(this, e, t)}>
          <div class="perm-block uniform ${n.nodeClass}${c}">${l(this, a, y).call(this, n.nodeClass)}</div>
        </td>
      `;
};
U = function() {
  var t, i;
  const e = ((t = this._pickerVerb) == null ? void 0 : t.split(".").pop()) ?? "";
  return p`
      <dialog
        class="scope-dialog"
        @close=${() => {
    this._pickerNode = null, this._pickerVerb = null;
  }}>
        <uui-dialog-layout
          headline=${o(this, r).term("uas_dialogHeadline", e)}>
          <p class="dialog-node">
            ${o(this, r).term("uas_dialogNodeLabel")}: <strong>${((i = this._pickerNode) == null ? void 0 : i.name) ?? ""}</strong>
          </p>

          ${this._pickerIsVirtualRoot ? l(this, a, L).call(this) : l(this, a, K).call(this)}

          <div slot="actions">
            <uui-button look="outline" @click=${() => this._scopeDialog.close()}>
              ${o(this, r).term("uas_cancel")}
            </uui-button>
            <uui-button look="primary" color="positive" @click=${() => l(this, a, T).call(this)}>
              ${o(this, r).term("uas_apply")}
            </uui-button>
          </div>
        </uui-dialog-layout>
      </dialog>
    `;
};
L = function() {
  return p`
      <div class="dialog-options">
        <uui-radio-group
          .value=${this._pickerNodeState}
          @change=${(e) => {
    this._pickerNodeState = e.target.value;
  }}>
          <uui-radio value="inherit" label=${o(this, r).term("uas_virtualRootInherit")} class="opt-inherit"></uui-radio>
          <uui-radio value="allow" label=${o(this, r).term("uas_virtualRootAllow")} class="opt-allow"></uui-radio>
          <uui-radio value="deny" label=${o(this, r).term("uas_virtualRootDeny")} class="opt-deny"></uui-radio>
        </uui-radio-group>
      </div>
    `;
};
K = function() {
  return p`
      <div class="dialog-sections">
        <!-- This node -->
        <div class="dialog-section">
          <h4>${o(this, r).term("uas_thisNodeSection")}</h4>
          <uui-radio-group
            .value=${this._pickerNodeState}
            @change=${(e) => {
    this._pickerNodeState = e.target.value;
  }}>
            <uui-radio value="inherit" label=${o(this, r).term("uas_inherit")} class="opt-inherit"></uui-radio>
            <uui-radio value="allow" label=${o(this, r).term("uas_allow")} class="opt-allow"></uui-radio>
            <uui-radio value="deny" label=${o(this, r).term("uas_deny")} class="opt-deny"></uui-radio>
          </uui-radio-group>
        </div>

        <!-- Descendants -->
        <div class="dialog-section">
          <h4>${o(this, r).term("uas_descendantsSection")}</h4>
          <uui-toggle
            label=${o(this, r).term("uas_sameAsNode")}
            ?checked=${this._pickerSameAsNode}
            @change=${(e) => {
    this._pickerSameAsNode = e.target.checked, this._pickerSameAsNode && (this._pickerDescState = this._pickerNodeState);
  }}>${o(this, r).term("uas_sameAsNode")}</uui-toggle>
          <uui-radio-group
            class=${this._pickerSameAsNode ? "radio-disabled" : ""}
            .value=${this._pickerSameAsNode ? this._pickerNodeState : this._pickerDescState}
            @change=${(e) => {
    this._pickerSameAsNode || (this._pickerDescState = e.target.value);
  }}>
            <uui-radio value="inherit" label=${o(this, r).term("uas_inherit")} class="opt-inherit" ?disabled=${this._pickerSameAsNode}></uui-radio>
            <uui-radio value="allow" label=${o(this, r).term("uas_allow")} class="opt-allow" ?disabled=${this._pickerSameAsNode}></uui-radio>
            <uui-radio value="deny" label=${o(this, r).term("uas_deny")} class="opt-deny" ?disabled=${this._pickerSameAsNode}></uui-radio>
          </uui-radio-group>
        </div>
      </div>
    `;
};
d.styles = F`
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

    tr.row-pending td {
      background-color: color-mix(in srgb, oklch(85% 0.15 90) 12%, transparent);
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
    }

    .perm-block {
      display: flex;
      align-items: center;
      justify-content: center;
      height: 26px;
      border: 1px solid var(--uui-color-border, #ddd);
      border-radius: 4px;
      cursor: pointer;
      user-select: none;
      overflow: hidden;
    }

    .perm-block:hover {
      border-color: var(--uui-color-border-emphasis, #bbb);
    }

    .perm-block.pending {
      border-color: var(--uui-color-warning-standalone, #f59e0b);
      border-style: dashed;
      border-width: 2px;
    }

    /* ── Uniform block ────────────────────────────────────────── */
    .perm-block.uniform {
      font-size: 13px;
      font-weight: 700;
    }

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

    /* ── Split block — two halves ─────────────────────────────── */
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

    /* ── Permission dialog ────────────────────────────────────── */
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

    .dialog-node {
      margin: 0 0 16px;
      font-size: 12px;
      color: var(--uui-color-text-alt, #666);
    }

    .dialog-options {
      margin-bottom: 8px;
    }

    .dialog-sections {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 24px;
      margin-bottom: 8px;
    }

    .dialog-section h4 {
      margin: 0 0 8px;
      font-size: 13px;
      font-weight: 600;
      color: var(--uui-color-text, #333);
    }

    .dialog-section uui-toggle {
      margin-bottom: 8px;
    }

    .radio-disabled {
      opacity: 0.4;
      pointer-events: none;
    }

    uui-radio.opt-allow {
      --uui-radio-border-color: color-mix(in srgb, var(--uui-color-positive, #34a853) 50%, transparent);
    }

    uui-radio.opt-deny {
      --uui-radio-border-color: color-mix(in srgb, var(--uui-color-danger, #ea4335) 50%, transparent);
    }
  `;
u([
  h()
], d.prototype, "_roles", 2);
u([
  h()
], d.prototype, "_verbs", 2);
u([
  h()
], d.prototype, "_selectedRole", 2);
u([
  h()
], d.prototype, "_treeNodes", 2);
u([
  h()
], d.prototype, "_loading", 2);
u([
  h()
], d.prototype, "_saving", 2);
u([
  h()
], d.prototype, "_error", 2);
u([
  h()
], d.prototype, "_pendingChanges", 2);
u([
  h()
], d.prototype, "_pickerNode", 2);
u([
  h()
], d.prototype, "_pickerVerb", 2);
u([
  h()
], d.prototype, "_pickerIsVirtualRoot", 2);
u([
  h()
], d.prototype, "_pickerNodeState", 2);
u([
  h()
], d.prototype, "_pickerDescState", 2);
u([
  h()
], d.prototype, "_pickerSameAsNode", 2);
u([
  q(".scope-dialog")
], d.prototype, "_scopeDialog", 2);
d = u([
  B("uas-security-editor-root")
], d);
const he = d;
export {
  d as UasSecurityEditorRootElement,
  he as default
};
//# sourceMappingURL=uas-security-editor-root.element-BTb6bx31.js.map
