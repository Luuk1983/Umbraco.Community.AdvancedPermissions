# ADR-004: Backoffice Auth via Entry Point + UMB_AUTH_CONTEXT

**Status**: Accepted
**Date**: 2026-04-04

## Context

The management API endpoints require a bearer token. We initially used `credentials: 'include'` (cookie auth) on all fetch calls based on documentation suggesting Umbraco 17 uses httpOnly cookies. However, this alone was insufficient — the API returned 401 without an Authorization header.

## Investigation

- `getLatestToken()` on the auth context is **deprecated** and always returns the string `'[redacted]'` in Umbraco 17.
- The correct method is `authContext.getOpenApiConfiguration()`, which returns `UmbOpenApiConfiguration`:
  ```typescript
  interface UmbOpenApiConfiguration {
    base?: string;
    credentials?: 'include' | 'omit' | 'same-origin';
    token: () => Promise<string | undefined>;  // async token getter — never stale
  }
  ```
- `token` is a function returning a Promise, not a string — it always returns a fresh token.

## Decision

### 1. Register a `backofficeEntryPoint` extension

```typescript
// manifests.ts
{
  type: 'backofficeEntryPoint',
  alias: 'UAP.EntryPoint',
  name: 'Umbraco Advanced Permissions Entry Point',
  js: () => import('./entrypoint.js'),
}
```

### 2. Consume `UMB_AUTH_CONTEXT` in the entry point

```typescript
// entrypoint.ts
import { UMB_AUTH_CONTEXT } from '@umbraco-cms/backoffice/auth';
import { setAuthContext } from './api/advanced-permissions.api.js';

export const onInit: UmbEntryPointOnInit = (_host, _extensionRegistry) => {
  _host.consumeContext(UMB_AUTH_CONTEXT, (authContext) => {
    setAuthContext(authContext ?? undefined);
  });
};
```

### 3. Use the token in every fetch

```typescript
// advanced-permissions.api.ts
const openApiConfig = _authContext?.getOpenApiConfiguration();
const token = openApiConfig?.token ? await openApiConfig.token() : undefined;
if (token) headers['Authorization'] = `Bearer ${token}`;
```

## Consequences

- Auth is automatically handled for all API calls without needing each component to know about authentication.
- Token is fetched fresh on each request — no stale token risk.
- If the auth context is not yet available (race condition during early init), the request falls back to `credentials: 'include'`, which fails gracefully with a 401 rather than crashing.
- `UmbAuthContext` is imported as a type from `@umbraco-cms/backoffice/auth` (not `extension-api`).
