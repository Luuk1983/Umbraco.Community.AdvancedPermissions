![Advanced Permissions for Umbraco](https://raw.githubusercontent.com/Luuk1983/Umbraco.Community.AdvancedPermissions/main/src/Umbraco.Community.AdvancedPermissions/package_logo_128x128.png)

# Advanced Permissions for Umbraco — Abstractions

The public contract for [Umbraco.Community.AdvancedPermissions](https://www.nuget.org/packages/Umbraco.Community.AdvancedPermissions): the interfaces, models, and constants that describe how advanced permissions are resolved.

[![NuGet](https://img.shields.io/nuget/v/Umbraco.Community.AdvancedPermissions.Abstractions)](https://www.nuget.org/packages/Umbraco.Community.AdvancedPermissions.Abstractions) [![License](https://img.shields.io/github/license/Luuk1983/Umbraco.Community.AdvancedPermissions)](https://github.com/Luuk1983/Umbraco.Community.AdvancedPermissions/blob/main/LICENSE)

---

Reference this package when you want to **build an add-on** (for example, tooling that reads or audits effective permissions) or **override** part of the resolution behaviour, without depending on the full implementation.

> **⚠️ This package does nothing on its own.** It contains only contracts. Install [Umbraco.Community.AdvancedPermissions](https://www.nuget.org/packages/Umbraco.Community.AdvancedPermissions) in your Umbraco site for the working package — the Permissions Editor, Access Viewer, resolution engine, and data layer. That package registers the implementations behind these abstractions.

## Installation

```bash
dotnet add package Umbraco.Community.AdvancedPermissions.Abstractions
```

## What's in here

- **Interfaces** — the resolution and data-access contracts (e.g. `IAdvancedPermissionService`, `IPermissionResolver`, `IDocTypePermissionService`).
- **Models** — the domain types (e.g. `AdvancedPermissionEntry`, `EffectivePermission`, `DocTypePermissionEntry`, `PermissionReasoning`).
- **Constants** — permission verbs and related constants.

The package has **no runtime dependencies** — it is a small, stable contract surface intended to be safe to depend on across a major version.

## Requirements

- .NET 10

## License

Licensed under the [MIT License](https://github.com/Luuk1983/Umbraco.Community.AdvancedPermissions/blob/main/LICENSE).
