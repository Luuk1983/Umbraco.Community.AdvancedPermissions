# ADR-005: Place umbraco-package.json in Vite's public/ Directory

**Status**: Accepted
**Date**: 2026-04-04

## Context

`umbraco-package.json` must be present in `wwwroot/App_Plugins/Umbraco.Community.AdvancedPermissions/` for Umbraco to discover the package extensions. Vite is configured with `emptyOutDir: true`, which deletes the entire output directory before each build.

## Problem

Initially `umbraco-package.json` was hand-written directly in `wwwroot/App_Plugins/Umbraco.Community.AdvancedPermissions/`. Every `npm run build` deleted it.

## Decision

Place `umbraco-package.json` in `src/Umbraco.Community.AdvancedPermissions.Client/public/umbraco-package.json`.

Vite's `public/` directory is special: files there are **copied verbatim to `outDir` after every build**, bypassing the `emptyOutDir` clean. The file ends up at `wwwroot/App_Plugins/Umbraco.Community.AdvancedPermissions/umbraco-package.json` automatically.

## Consequences

- `umbraco-package.json` is never deleted by `npm run build`.
- The file is not processed by Vite (no hashing, no bundling) — it arrives verbatim, which is correct since Umbraco reads it as-is.
- The `public/` path must match the Vite output structure; since `outDir` is the `App_Plugins/Umbraco.Community.AdvancedPermissions/` directory itself, files placed in `public/` land directly in that directory (not in a subdirectory).

## File Content

```json
{
  "name": "Umbraco Advanced Permissions",
  "extensions": [
    {
      "type": "bundle",
      "alias": "UAP.Bundle",
      "js": "/App_Plugins/Umbraco.Community.AdvancedPermissions/uas.js"
    }
  ]
}
```
