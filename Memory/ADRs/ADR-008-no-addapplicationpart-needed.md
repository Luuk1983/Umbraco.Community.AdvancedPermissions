# ADR-008: AddApplicationPart() Is Not Needed for Management API Endpoints

**Status**: Accepted
**Date**: 2026-04-04

## Context

Early in development, `AddMvcCore().AddApplicationPart(typeof(AdvancedPermissionsControllerBase).Assembly)` was added to the test site's `Program.cs` after endpoints returned 404. It was believed that Umbraco's MVC discovery did not automatically find controllers in referenced class libraries.

## Investigation

After running the test site **without** the `AddApplicationPart` call, all 8 endpoints correctly returned 401 (authentication required), not 404. The 404 responses that triggered the original addition were caused by a stale process running on the same port — a different instance without the controllers registered.

## Decision

Do **not** call `AddApplicationPart()` for this package. Umbraco v17's minimal API discovery and/or MVC controller discovery correctly picks up controllers from referenced class library assemblies without it.

The consuming application's `Program.cs` only needs the standard Umbraco builder — the package auto-registers via `IComposer` discovery:

```csharp
builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddDeliveryApi()
    .Build();
```

## Consequences

- Simpler integration: no extension method needed, the package auto-discovers via `IComposer`.
- Do not re-add `AddApplicationPart` if endpoints appear to return 404 — first check for port conflicts and stale processes.
