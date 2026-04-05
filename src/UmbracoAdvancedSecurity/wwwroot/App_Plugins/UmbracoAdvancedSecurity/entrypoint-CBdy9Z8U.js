import { UMB_AUTH_CONTEXT as c } from "@umbraco-cms/backoffice/auth";
import { s as n } from "./advanced-security.api-C-RLDPi4.js";
const m = "Umb.Condition.UserPermission.Document", i = [
  "Umb.Document.Read",
  "Umb.Document.Create",
  "Umb.Document.Update",
  "Umb.Document.Delete",
  "Umb.Document.Publish",
  "Umb.Document.Unpublish",
  "Umb.Document.Duplicate",
  "Umb.Document.Move",
  "Umb.Document.Sort",
  "Umb.Document.CreateBlueprint",
  "Umb.Document.Notifications",
  "Umb.Document.CultureAndHostnames",
  "Umb.Document.PublicAccess",
  "Umb.Document.Rollback",
  "Umb.Document.Permissions"
], U = (e, o) => {
  e.consumeContext(c, (t) => {
    n(t ?? void 0);
  }), o.unregister(m), o.register({
    type: "condition",
    name: "Advanced Security Document User Permission Condition",
    alias: m,
    api: () => import("./document-user-permission.condition-CAI2T48b.js")
  });
  for (const t of i)
    o.exclude(t);
}, D = (e, o) => {
  n(void 0);
};
export {
  U as onInit,
  D as onUnload
};
//# sourceMappingURL=entrypoint-CBdy9Z8U.js.map
