# Umbraco Advanced Security

A Sitecore-inspired security model for Umbraco v17 that adds explicit Allow/Deny permissions, inheritance through the content tree, an Everyone role for global constraints, and dedicated backoffice UIs.

## Features

- **Explicit Allow/Deny** — three-state permissions (Allow, Deny, Inherit) per verb per role per node
- **Tree inheritance** — permissions propagate through the content tree with configurable scope (ThisNodeOnly, ThisNodeAndDescendants, DescendantsOnly)
- **Everyone role** — `$everyone` applies to all users regardless of group membership
- **Security Editor** — manage raw permission entries per role/node in the backoffice
- **Access Viewer** — view effective (resolved) permissions for any user or role, with reasoning

## Requirements

- Umbraco v17.3.0+
- .NET 10

## Installation

```bash
dotnet add package UmbracoAdvancedSecurity
```

Register the package in your `Program.cs`:

```csharp
builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddDeliveryApi()
    .AddAdvancedSecurity()  // <-- add this
    .Build();
```
