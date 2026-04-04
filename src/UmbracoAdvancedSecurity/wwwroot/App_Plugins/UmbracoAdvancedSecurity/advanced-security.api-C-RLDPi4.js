const u = "/umbraco/management/api/v1/advanced-security";
let o;
function f(e) {
  o = e;
}
async function a(e, n) {
  const t = o == null ? void 0 : o.getOpenApiConfiguration(), r = t != null && t.token ? await t.token() : void 0, c = {
    "Content-Type": "application/json",
    ...n == null ? void 0 : n.headers
  };
  r && (c.Authorization = `Bearer ${r}`);
  const s = await fetch(`${u}${e}`, {
    credentials: (t == null ? void 0 : t.credentials) ?? "include",
    ...n,
    headers: c
  });
  if (!s.ok) {
    const i = await s.text().catch(() => "");
    throw new Error(i || `HTTP ${s.status}`);
  }
  return s;
}
async function y() {
  return (await a("/roles")).json();
}
async function d() {
  return (await a("/verbs")).json();
}
async function l(e) {
  return (await a(`/tree/root?roleAlias=${encodeURIComponent(e)}`)).json();
}
async function m(e, n) {
  return (await a(`/tree/children?parentKey=${e}&roleAlias=${encodeURIComponent(n)}`)).json();
}
async function w(e, n, t) {
  await a("/permissions", {
    method: "PUT",
    body: JSON.stringify({ nodeKey: e, roleAlias: n, entries: t })
  });
}
async function $(e, n) {
  return (await a(`/permissions?roleAlias=${encodeURIComponent(n)}`)).json();
}
async function h(e, n) {
  return (await a(`/effective?userKey=${e}&nodeKey=${n}`)).json();
}
async function g(e, n) {
  return (await a(`/effective/by-role?roleAlias=${encodeURIComponent(e)}&nodeKey=${n}`)).json();
}
export {
  d as a,
  $ as b,
  l as c,
  w as d,
  m as e,
  g as f,
  y as g,
  h,
  f as s
};
//# sourceMappingURL=advanced-security.api-C-RLDPi4.js.map
