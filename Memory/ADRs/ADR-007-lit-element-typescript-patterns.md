# ADR-007: Lit Element TypeScript Patterns for Umbraco Backoffice Components

**Status**: Accepted
**Date**: 2026-04-04

## Context

During development of the backoffice web components, several non-obvious TypeScript patterns were required to satisfy the strict tsconfig (`exactOptionalPropertyTypes: true`, `noImplicitAny: true`).

## Decisions and Patterns

### 1. Base class: UmbLitElement

Always extend `UmbLitElement` from `@umbraco-cms/backoffice/lit-element`, not bare `LitElement`. This provides `consumeContext()` support.

```typescript
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
```

### 2. Imports: use Umbraco's re-exports, not lit directly

```typescript
import { html, css, nothing, customElement, state, query } from '@umbraco-cms/backoffice/external/lit';
import type { TemplateResult } from '@umbraco-cms/backoffice/external/lit';
```

### 3. Context consumption with exactOptionalPropertyTypes

`consumeContext` callback passes `T | undefined`. The field must be declared as `T | undefined = undefined` (not `T?`), and the callback must use `?? undefined` to handle the undefined case explicitly:

```typescript
// WRONG (TS2322 with exactOptionalPropertyTypes):
#notificationContext?: typeof UMB_NOTIFICATION_CONTEXT.TYPE;
this.consumeContext(UMB_NOTIFICATION_CONTEXT, (ctx) => {
  this.#notificationContext = ctx; // ctx is T | undefined, field is T?
});

// CORRECT:
#notificationContext: typeof UMB_NOTIFICATION_CONTEXT.TYPE | undefined = undefined;
this.consumeContext(UMB_NOTIFICATION_CONTEXT, (ctx) => {
  this.#notificationContext = ctx ?? undefined;
});
```

### 4. Recursive rendering methods need explicit return types

TypeScript cannot infer return types of mutually recursive private methods (TS7023/TS7024). Always annotate:

```typescript
#renderRows(nodes: MyNode[], depth: number): TemplateResult[] {
  return nodes.flatMap((node) => [
    this.#renderRow(node, depth),
    ...(node.expanded && node.children ? this.#renderRows(node.children, depth + 1) : []),
  ]);
}

#renderRow(node: MyNode, depth: number): TemplateResult {
  return html`...`;
}
```

### 5. Notifications

```typescript
import { UMB_NOTIFICATION_CONTEXT } from '@umbraco-cms/backoffice/notification';

this.#notificationContext?.peek('positive', { data: { message: 'Saved.' } });
this.#notificationContext?.peek('danger', { data: { message: 'Error.' } });
```

### 6. Dialogs (scope picker, reasoning modal)

Use native `<dialog>` with `showModal()`. Works in shadow DOM via the browser's top layer.

```typescript
@query('.my-dialog') private _myDialog!: HTMLDialogElement;

// Open:
void this.updateComplete.then(() => this._myDialog.showModal());

// Close from within:
<button @click=${() => this._myDialog.close()}>Close</button>
```

## Consequences

Following these patterns avoids all known TypeScript errors in the current tsconfig. Document any new patterns discovered during bugfixing in this ADR.
