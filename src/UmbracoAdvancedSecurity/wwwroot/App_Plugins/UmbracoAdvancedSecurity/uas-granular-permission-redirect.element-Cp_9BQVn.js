import { html as l, css as p, property as c, customElement as m } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement as d } from "@umbraco-cms/backoffice/lit-element";
var f = Object.defineProperty, g = Object.getOwnPropertyDescriptor, n = (u, s, t, i) => {
  for (var e = i > 1 ? void 0 : i ? g(s, t) : s, o = u.length - 1, a; o >= 0; o--)
    (a = u[o]) && (e = (i ? a(s, t, e) : a(e)) || e);
  return i && e && f(s, t, e), e;
};
let r = class extends d {
  constructor() {
    super(...arguments), this.permissions = [], this.fallbackPermissions = [];
  }
  render() {
    return l`
      <div id="message">
        <uui-icon name="icon-lock"></uui-icon>
        <p>
          Document permissions for this user group are managed by the
          <strong>Advanced Security</strong> package. Open the
          <em>Advanced Security &gt; Security Editor</em> section to configure
          permissions.
        </p>
      </div>
    `;
  }
};
r.styles = p`
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
n([
  c({ attribute: !1 })
], r.prototype, "permissions", 2);
n([
  c({ attribute: !1 })
], r.prototype, "fallbackPermissions", 2);
r = n([
  m("uas-granular-permission-redirect")
], r);
const h = r;
export {
  r as UasGranularPermissionRedirectElement,
  h as default
};
//# sourceMappingURL=uas-granular-permission-redirect.element-Cp_9BQVn.js.map
