# Umbraco.Community.AdvancedPermissions — Claude Code Guide

## Project Overview

Umbraco v17 package implementing advanced permission management. Adds explicit Allow/Deny permission entries per role per content node, tree-based inheritance, an `$everyone` role, and dedicated backoffice UIs (Permissions Editor + Access Viewer).

## Solution Structure

```
src/
  Umbraco.Community.AdvancedPermissions.Core/       # Domain models, interfaces — no Umbraco dependency
  Umbraco.Community.AdvancedPermissions.Data/       # EF Core repository implementation (SQLite for tests)
  Umbraco.Community.AdvancedPermissions/            # Main package: composers, controllers, services, wwwroot
  Umbraco.Community.AdvancedPermissions.Client/     # Vite+TypeScript frontend (Lit web components)
tests/
  Umbraco.Community.AdvancedPermissions.Core.Tests/ # Unit tests — permission resolver logic
  Umbraco.Community.AdvancedPermissions.Data.Tests/ # Integration tests — repository with real SQLite DB
  Umbraco.Community.AdvancedPermissions.TestSite/   # Full Umbraco site for manual testing
Memory/
  Plans/                              # Implementation plans
  ADRs/                               # Architecture decision records (see below)
```

## Key Source Files

### Backend
| File | Purpose |
|------|---------|
| `Core/Models/AdvancedPermissionEntry.cs` | The core domain entity: verb, state (Allow/Deny), scope, roleAlias, nodeKey |
| `Core/Models/EffectivePermission.cs` | Resolved permission with reasoning chain |
| `Core/Interfaces/IPermissionResolver.cs` | Resolution algorithm contract |
| `Core/Interfaces/IAdvancedPermissionRepository.cs` | Data access contract |
| `Data/Repositories/AdvancedPermissionRepository.cs` | EF Core implementation |
| `Umbraco.Community.AdvancedPermissions/Services/AdvancedPermissionService.cs` | Orchestrates resolution: loads ancestors, applies $everyone, explicit, scope |
| `Umbraco.Community.AdvancedPermissions/Composing/AdvancedPermissionsComposer.cs` | DI registrations via `IComposer` auto-discovery |
| `Umbraco.Community.AdvancedPermissions/Controllers/` | 4 minimal API controllers (Meta, Permission, Effective, Tree) |
| `Umbraco.Community.AdvancedPermissions/Caching/AdvancedPermissionCache.cs` | In-memory cache with node-key invalidation |
| `Umbraco.Community.AdvancedPermissions/Notifications/AdvancedPermissionCacheInvalidator.cs` | Invalidates cache on content save/delete |

### Frontend (TypeScript + Lit)
| File | Purpose |
|------|---------|
| `Client/src/entrypoint.ts` | `backofficeEntryPoint` — wires `UMB_AUTH_CONTEXT` to API module |
| `Client/src/manifests.ts` | All extension manifests (entrypoint, section, menu, workspaces) |
| `Client/src/api/advanced-permissions.api.ts` | Typed fetch wrappers; `setAuthContext()` called from entrypoint |
| `Client/src/models/permission.models.ts` | TypeScript interfaces mirroring C# response models |
| `Client/src/permissions-editor/uap-permissions-editor-root.element.ts` | Permissions Editor — manage permissions per role/node |
| `Client/src/access-viewer/uap-access-viewer-root.element.ts` | Access Viewer — view effective permissions with reasoning |
| `Client/public/umbraco-package.json` | Umbraco package manifest; lives in `public/` so Vite copies it on every build |
| `Client/vite.config.ts` | Builds to `../Umbraco.Community.AdvancedPermissions/wwwroot/App_Plugins/Umbraco.Community.AdvancedPermissions/` |

## Management API Endpoints

Base: `GET|PUT /umbraco/management/api/v1/advanced-permissions/`

| Method | Path | Description |
|--------|------|-------------|
| GET | `/roles` | All assignable roles (user groups + `$everyone`) |
| GET | `/verbs` | All permission verbs with display names |
| GET | `/tree/root?roleAlias=` | Root content nodes with permission entries |
| GET | `/tree/children?parentKey=&roleAlias=` | Children of a node |
| GET | `/permissions?nodeKey=&roleAlias=` | Stored entries for a node+role |
| PUT | `/permissions` | Save (replace) entries for a node+role |
| GET | `/effective?userKey=&nodeKey=` | Effective permissions for a user at a node |
| GET | `/effective/by-role?roleAlias=&nodeKey=` | Effective permissions for a role at a node |

All endpoints require backoffice authentication and return 401 without it. **No `AddApplicationPart()` is needed** — Umbraco's minimal API discovery works automatically.

## Permission Model

- **Verbs**: configurable strings (e.g., `"Read"`, `"Write"`, `"Publish"`)
- **State**: `Allow` or `Deny` — no null/inherit row; absence of an entry = inherit
- **Scope**: `ThisNodeOnly`, `ThisNodeAndDescendants`, `DescendantsOnly`
- **Roles**: any user group alias, plus the special `$everyone` role
- **Resolution priority**: explicit entry on current node > inherited from nearest ancestor > default (deny)
- **`$everyone`** is evaluated first; explicit role entries can override it

## NuGet Packaging

The package ships as a single NuGet package (`Umbraco.Community.AdvancedPermissions`). Core and Data DLLs are bundled into the package via `PrivateAssets="all"` and a custom MSBuild target — they are NOT separate NuGet dependencies.

```bash
dotnet pack src/Umbraco.Community.AdvancedPermissions/  # Create NuGet package
```

## Build & Run

### Backend
```bash
dotnet build                                      # Build all projects
dotnet test                                       # Run all 39 tests (20 unit + 19 integration)
dotnet pack src/Umbraco.Community.AdvancedPermissions/   # Create NuGet package
```

### Frontend
```bash
cd src/Umbraco.Community.AdvancedPermissions.Client
npm install                           # First time only
npm run build                         # Vite build → wwwroot/App_Plugins/
npx tsc --noEmit                      # Type-check only
```

### Test Site
```bash
cd tests/Umbraco.Community.AdvancedPermissions.TestSite
dotnet run --urls http://localhost:5000
# Visit http://localhost:5000/umbraco
```

## Critical Gotchas

1. **Auth in backoffice fetches**: Always use the `backofficeEntryPoint` + `UMB_AUTH_CONTEXT` pattern. See `src/entrypoint.ts`. `getLatestToken()` is deprecated and always returns `'[redacted]'`.

2. **`UmbExtensionManifest` is a global ambient type** — never import it. Just use `Array<UmbExtensionManifest>` directly.

3. **`UmbExtensionManifestKind` IS an export** from `@umbraco-cms/backoffice/extension-registry` if you need kind-based manifests.

4. **Recursive Lit private methods** need explicit return type annotations or TypeScript infers `any`:
   ```typescript
   #renderRows(nodes: MyNode[], depth: number): TemplateResult[] { ... }
   #renderRow(node: MyNode, depth: number): TemplateResult { ... }
   ```

5. **`exactOptionalPropertyTypes: true`** — never assign `undefined` to an optional field `field?: T` explicitly. Use `field: T | undefined = undefined` for class fields that need to accept undefined.

6. **`consumeContext` callback** passes `T | undefined`. Always use `ctx ?? undefined` to satisfy `exactOptionalPropertyTypes`.

7. **`umbraco-package.json` must live in `Client/public/`** — Vite's `emptyOutDir: true` deletes everything in the output dir on each build. Files in `public/` are copied verbatim after the clean.

8. **Static web assets path**: The csproj uses `<StaticWebAssetBasePath>/</StaticWebAssetBasePath>` to override the default `/_content/Umbraco.Community.AdvancedPermissions/` path. Without it, `App_Plugins/` files would be unreachable.

9. **SDK must be `Microsoft.NET.Sdk.Razor`** — `Microsoft.NET.Sdk` does not publish `wwwroot/` as static web assets.

10. **Namespace collision with `Umbraco.Cms`**: Since the project namespace is `Umbraco.Community.AdvancedPermissions`, the compiler sees `Umbraco` as a parent namespace. Inline fully-qualified references like `Umbraco.Cms.Core.Models.IContent` used *inside* a class declared in this namespace will resolve against the enclosing `Umbraco` first and fail. Keep all `Umbraco.Cms.*` access in `using` directives at the top of the file (absolute resolution), or use `global::Umbraco.Cms.Core...` for inline references.

## Umbraco Reference Source

- Umbraco v17 backoffice source: `C:\GitHub\UmbracoVersions\v17\src\Umbraco.Web.UI.Client`
- Umbraco UI library source: `C:\GitHub\UmbracoVersions\UI-latest`
- Do not read `node_modules` — use the reference source instead.
