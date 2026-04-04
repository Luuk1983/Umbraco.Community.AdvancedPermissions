# UmbracoAdvancedSecurity — Claude Code Guide

## Project Overview

Umbraco v17 package implementing a Sitecore-inspired security model. Adds explicit Allow/Deny permission entries per role per content node, tree-based inheritance, an `$everyone` role, and dedicated backoffice UIs (Security Editor + Access Viewer).

## Solution Structure

```
src/
  UmbracoAdvancedSecurity.Core/       # Domain models, interfaces — no Umbraco dependency
  UmbracoAdvancedSecurity.Data/       # EF Core repository implementation (SQLite for tests)
  UmbracoAdvancedSecurity/            # Main package: composers, controllers, services, wwwroot
  UmbracoAdvancedSecurity.Client/     # Vite+TypeScript frontend (Lit web components)
tests/
  UmbracoAdvancedSecurity.Core.Tests/ # Unit tests — permission resolver logic
  UmbracoAdvancedSecurity.Data.Tests/ # Integration tests — repository with real SQLite DB
  UmbracoAdvancedSecurity.TestSite/   # Full Umbraco site for manual testing
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
| `UmbracoAdvancedSecurity/Services/AdvancedPermissionService.cs` | Orchestrates resolution: loads ancestors, applies $everyone, explicit, scope |
| `UmbracoAdvancedSecurity/Composing/AdvancedSecurityComposer.cs` | DI registrations + `AddAdvancedSecurity()` extension |
| `UmbracoAdvancedSecurity/Controllers/` | 4 minimal API controllers (Meta, Permission, Effective, Tree) |
| `UmbracoAdvancedSecurity/Caching/AdvancedPermissionCache.cs` | In-memory cache with node-key invalidation |
| `UmbracoAdvancedSecurity/Notifications/AdvancedPermissionCacheInvalidator.cs` | Invalidates cache on content save/delete |

### Frontend (TypeScript + Lit)
| File | Purpose |
|------|---------|
| `Client/src/entrypoint.ts` | `backofficeEntryPoint` — wires `UMB_AUTH_CONTEXT` to API module |
| `Client/src/manifests.ts` | All extension manifests (entrypoint, section, menu, workspaces) |
| `Client/src/api/advanced-security.api.ts` | Typed fetch wrappers; `setAuthContext()` called from entrypoint |
| `Client/src/models/permission.models.ts` | TypeScript interfaces mirroring C# response models |
| `Client/src/security-editor/uas-security-editor-root.element.ts` | Security Editor — manage permissions per role/node |
| `Client/src/access-viewer/uas-access-viewer-root.element.ts` | Access Viewer — view effective permissions with reasoning |
| `Client/public/umbraco-package.json` | Umbraco package manifest; lives in `public/` so Vite copies it on every build |
| `Client/vite.config.ts` | Builds to `../UmbracoAdvancedSecurity/wwwroot/App_Plugins/UmbracoAdvancedSecurity/` |

## Management API Endpoints

Base: `GET|PUT /umbraco/management/api/v1/advanced-security/`

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

## Build & Run

### Backend
```bash
dotnet build                          # Build all projects
dotnet test                           # Run all 39 tests (20 unit + 19 integration)
dotnet pack src/UmbracoAdvancedSecurity/  # Create NuGet package
```

### Frontend
```bash
cd src/UmbracoAdvancedSecurity.Client
npm install                           # First time only
npm run build                         # Vite build → wwwroot/App_Plugins/
npx tsc --noEmit                      # Type-check only
```

### Test Site
```bash
cd tests/UmbracoAdvancedSecurity.TestSite
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

8. **Static web assets path**: The csproj uses `<StaticWebAssetBasePath>/</StaticWebAssetBasePath>` to override the default `/_content/UmbracoAdvancedSecurity/` path. Without it, `App_Plugins/` files would be unreachable.

9. **SDK must be `Microsoft.NET.Sdk.Razor`** — `Microsoft.NET.Sdk` does not publish `wwwroot/` as static web assets.

## Umbraco Reference Source

- Umbraco v17 backoffice source: `C:\GitHub\UmbracoVersions\v17\src\Umbraco.Web.UI.Client`
- Umbraco UI library source: `C:\GitHub\UmbracoVersions\UI-latest`
- Do not read `node_modules` — use the reference source instead.
