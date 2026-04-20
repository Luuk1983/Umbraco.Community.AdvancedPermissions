# ADR-002: Use Microsoft.NET.Sdk.Razor for Static Web Asset Publishing

**Status**: Accepted
**Date**: 2026-04-04

## Context

The main package project (`Umbraco.Community.AdvancedPermissions`) needs to expose frontend files in `wwwroot/App_Plugins/` so they are served correctly when the package is consumed by a host application. Initially the project used `Microsoft.NET.Sdk`.

## Problem

`Microsoft.NET.Sdk` does **not** publish `wwwroot/` as static web assets. When a host project references the package DLL, the `App_Plugins` files are invisible — Umbraco cannot find the JS bundle or `umbraco-package.json`.

## Decision

Change the SDK in `Umbraco.Community.AdvancedPermissions.csproj` from `Microsoft.NET.Sdk` to `Microsoft.NET.Sdk.Razor`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">
```

This is a Razor Class Library (RCL), which has first-class support for `wwwroot/` as static web assets that get published into consuming projects.

## Consequences

- `wwwroot/` content is automatically included in NuGet packages under `staticwebassets/`.
- Host projects receive the static files automatically via static web asset middleware.
- The project gains an implicit dependency on the Razor infrastructure — acceptable since Umbraco already brings this in.
- No separate packaging step needed for frontend files; `dotnet pack` handles everything.
