# ADR-006: UmbExtensionManifest Is a Global Ambient Type

**Status**: Accepted
**Date**: 2026-04-04

## Context

When typing manifest arrays, we initially imported `UmbExtensionManifest` from `@umbraco-cms/backoffice/extension-registry`. TypeScript complained that the package had no such export.

## Investigation

`UmbExtensionManifest` is declared in `extension-registry/models/types.d.ts` inside a `declare global {}` block:

```typescript
declare global {
  type UmbExtensionManifest = UnionOfProperties<UmbExtensionManifestMap>;
}
```

It is an **ambient global type** — it is available everywhere without importing, identical to how Umbraco's own internal packages use it (confirmed by inspecting `v17/src/Umbraco.Web.UI.Client/examples/`).

## Decision

Never import `UmbExtensionManifest`. Use it directly:

```typescript
// manifests.ts — no import needed
const manifests: Array<UmbExtensionManifest> = [ ... ];
```

## Related: UmbExtensionManifestKind

`UmbExtensionManifestKind` IS a named export from `@umbraco-cms/backoffice/extension-registry` (defined in `registry.d.ts`). Import it explicitly if needed for kind-based manifests:

```typescript
import type { UmbExtensionManifestKind } from '@umbraco-cms/backoffice/extension-registry';
const manifests: Array<UmbExtensionManifest | UmbExtensionManifestKind> = [ ... ];
```

## Consequences

- Removing the import avoids TS2305 errors.
- Future sessions: do not add an import for `UmbExtensionManifest` — TypeScript will reject it.
