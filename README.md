# LP.Umbraco.AdvancedPermissions

Advanced permission management for Umbraco v17 that adds explicit Allow/Deny permissions, inheritance through the content tree, an Everyone role for global constraints, and dedicated backoffice UIs.

## Features

- **Explicit Allow/Deny** — three-state permissions (Allow, Deny, Inherit) per verb per role per content node
- **Tree inheritance** — permissions propagate through the content tree with configurable scope (ThisNodeOnly, ThisNodeAndDescendants, DescendantsOnly)
- **Everyone role** — `$everyone` applies to all users regardless of group membership; explicit role entries can override it
- **Permissions Editor** — manage raw permission entries per role/node in the backoffice
- **Access Viewer** — view effective (resolved) permissions for any user or role, with full reasoning chain showing why each permission was granted or denied

## How It Works

Permissions are stored as entries with a **verb** (e.g. Read, Write, Publish), a **state** (Allow or Deny), and a **scope** (ThisNodeOnly, ThisNodeAndDescendants, or DescendantsOnly). The resolver walks up the content tree to determine the effective permission:

1. Check for an explicit entry on the current node
2. If none found, inherit from the nearest ancestor with a matching entry
3. The `$everyone` role is evaluated first; explicit role entries override it
4. If no entry is found anywhere in the tree, the default is Deny

## Requirements

- Umbraco v17.3.0+
- .NET 10

## Installation

```bash
dotnet add package LP.Umbraco.AdvancedPermissions
```

The package auto-registers via Umbraco's `IComposer` discovery — no additional setup code is needed. After installation, two new sections appear in the Umbraco backoffice: **Permissions Editor** and **Access Viewer**.

## Configuration

No configuration is required. The package works out of the box with sensible defaults. Permission verbs are automatically derived from the available Umbraco permissions.

## License

MIT
