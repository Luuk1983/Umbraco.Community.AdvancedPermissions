import { html as d, css as m, property as p, customElement as v } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement as f } from "@umbraco-cms/backoffice/lit-element";
import { UmbLocalizationController as _ } from "@umbraco-cms/backoffice/localization-api";
var h = Object.defineProperty, g = Object.getOwnPropertyDescriptor, c = (e) => {
  throw TypeError(e);
}, u = (e, r, a, t) => {
  for (var s = t > 1 ? void 0 : t ? g(r, a) : r, o = e.length - 1, n; o >= 0; o--)
    (n = e[o]) && (s = (t ? n(r, a, s) : n(s)) || s);
  return t && s && h(r, a, s), s;
}, P = (e, r, a) => r.has(e) || c("Cannot " + a), x = (e, r, a) => (P(e, r, "read from private field"), a ? a.call(e) : r.get(e)), y = (e, r, a) => r.has(e) ? c("Cannot add the same private member more than once") : r instanceof WeakSet ? r.add(e) : r.set(e, a), l;
let i = class extends f {
  constructor() {
    super(...arguments), y(this, l, new _(this)), this.permissions = [], this.fallbackPermissions = [];
  }
  render() {
    return d`
      <div id="message">
        <uui-icon name="icon-lock"></uui-icon>
        <p>${x(this, l).term("uas_redirectMessage")}</p>
      </div>
    `;
  }
};
l = /* @__PURE__ */ new WeakMap();
i.styles = m`
    #message {
      display: flex;
      align-items: flex-start;
      gap: var(--uui-size-space-3);
      padding: var(--uui-size-space-4);
      border: 1px solid var(--uui-color-border);
      border-radius: var(--uui-border-radius);
      background: var(--uui-color-surface-alt);
    }

    uui-icon {
      flex-shrink: 0;
      font-size: 1.2em;
      margin-top: 2px;
      color: var(--uui-color-interactive);
    }

    p {
      margin: 0;
      line-height: 1.4;
    }
  `;
u([
  p({ attribute: !1 })
], i.prototype, "permissions", 2);
u([
  p({ attribute: !1 })
], i.prototype, "fallbackPermissions", 2);
i = u([
  v("uas-granular-permission-redirect")
], i);
const z = i;
export {
  i as UasGranularPermissionRedirectElement,
  z as default
};
//# sourceMappingURL=uas-granular-permission-redirect.element-x1yxkse-.js.map
