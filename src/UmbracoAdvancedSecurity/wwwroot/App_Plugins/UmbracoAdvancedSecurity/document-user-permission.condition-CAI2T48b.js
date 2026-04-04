var E = (t) => {
  throw TypeError(t);
};
var y = (t, s, e) => s.has(t) || E("Cannot " + e);
var n = (t, s, e) => (y(t, s, "read from private field"), e ? e.call(t) : s.get(t)), u = (t, s, e) => s.has(t) ? E("Cannot add the same private member more than once") : s instanceof WeakSet ? s.add(t) : s.set(t, e), p = (t, s, e, r) => (y(t, s, "write to private field"), r ? r.call(t, e) : s.set(t, e), e), d = (t, s, e) => (y(t, s, "access private method"), e);
import { UmbConditionBase as U } from "@umbraco-cms/backoffice/extension-registry";
import { UMB_CURRENT_USER_CONTEXT as g } from "@umbraco-cms/backoffice/current-user";
import { UMB_ENTITY_CONTEXT as b } from "@umbraco-cms/backoffice/entity";
import { observeMultiple as O } from "@umbraco-cms/backoffice/observable-api";
import { h as _ } from "./advanced-security.api-C-RLDPi4.js";
const l = /* @__PURE__ */ new Map(), w = 3e4;
function M(t, s) {
  const e = `${t}|${s}`, r = l.get(e);
  if (r && Date.now() - r.timestamp < w)
    return r.promise;
  const i = _(t, s).catch((o) => {
    var h;
    throw ((h = l.get(e)) == null ? void 0 : h.promise) === i && l.delete(e), o;
  });
  return l.set(e, { promise: i, timestamp: Date.now() }), i;
}
function D() {
  l.clear();
}
var c, a, f, m, C, T;
class R extends U {
  constructor(e, r) {
    super(e, r);
    u(this, m);
    u(this, c);
    u(this, a);
    u(this, f);
    this.consumeContext(g, (i) => {
      this.observe(
        i == null ? void 0 : i.unique,
        (o) => {
          p(this, c, o ?? void 0), d(this, m, C).call(this);
        },
        "observeUserKey"
      );
    }), this.consumeContext(b, (i) => {
      if (!i) {
        this.removeUmbControllerByAlias("observeEntity");
        return;
      }
      this.observe(
        O([i.entityType, i.unique]),
        ([o, h]) => {
          p(this, a, o), p(this, f, h), d(this, m, C).call(this);
        },
        "observeEntity"
      );
    });
  }
}
c = new WeakMap(), a = new WeakMap(), f = new WeakMap(), m = new WeakSet(), C = async function() {
  if (n(this, a) !== void 0 && n(this, f) !== void 0 && n(this, c)) {
    if (n(this, a) !== "document") {
      this.permitted = !0;
      return;
    }
    if (n(this, f) === null) {
      this.permitted = !0;
      return;
    }
    try {
      const e = await M(n(this, c), n(this, f)), r = new Set(
        e.permissions.filter((i) => i.isAllowed).map((i) => i.verb)
      );
      d(this, m, T).call(this, r);
    } catch {
      this.permitted = !1;
    }
  }
}, T = function(e) {
  var o, h;
  let r = !0, i = !0;
  (o = this.config.allOf) != null && o.length && (r = this.config.allOf.every((v) => e.has(v))), (h = this.config.oneOf) != null && h.length && (i = this.config.oneOf.some((v) => e.has(v))), !r && !i && (r = !1, i = !1), this.permitted = r && i;
};
export {
  R as UasDocumentUserPermissionCondition,
  R as api,
  D as clearEffectivePermissionCache
};
//# sourceMappingURL=document-user-permission.condition-CAI2T48b.js.map
