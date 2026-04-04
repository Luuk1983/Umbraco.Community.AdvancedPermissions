import { UMB_AUTH_CONTEXT as s } from "@umbraco-cms/backoffice/auth";
import { s as i } from "./advanced-security.api-C-RLDPi4.js";
const n = "Umb.Condition.UserPermission.Document", c = (t, o) => {
  t.consumeContext(s, (e) => {
    i(e ?? void 0);
  }), o.unregister(n), o.register({
    type: "condition",
    name: "Advanced Security Document User Permission Condition",
    alias: n,
    api: () => import("./document-user-permission.condition-CAI2T48b.js")
  });
}, d = (t, o) => {
  i(void 0);
};
export {
  c as onInit,
  d as onUnload
};
//# sourceMappingURL=entrypoint-Jm72DZvJ.js.map
