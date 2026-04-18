<p align="center">
  <img src="https://raw.githubusercontent.com/Luuk1983/LP.Umbraco.AdvancedPermissions/main/src/LP.Umbraco.AdvancedPermissions/package_logo_128x128.png" alt="Advanced Permissions for Umbraco" width="128" />
</p>

<h1 align="center">Advanced Permissions for Umbraco</h1>

<p align="center">
  Fine-grained, node-level permission management for Umbraco v17 — with explicit Allow/Deny, content tree inheritance, and full audit reasoning.
</p>

<p align="center">
  <a href="https://www.nuget.org/packages/LP.Umbraco.AdvancedPermissions"><img src="https://img.shields.io/nuget/v/LP.Umbraco.AdvancedPermissions" alt="NuGet" /></a>
  <a href="https://www.nuget.org/packages/LP.Umbraco.AdvancedPermissions"><img src="https://img.shields.io/nuget/dt/LP.Umbraco.AdvancedPermissions" alt="NuGet Downloads" /></a>
  <a href="https://github.com/Luuk1983/LP.Umbraco.AdvancedPermissions/blob/main/LICENSE"><img src="https://img.shields.io/github/license/Luuk1983/LP.Umbraco.AdvancedPermissions" alt="License" /></a>
</p>

---

Umbraco's built-in permission system assigns permissions at the user group level — every member of a group gets the same access everywhere. **Advanced Permissions** gives you control at the content node level, with explicit Allow and Deny states, inheritance through the content tree, and a dedicated "All Users Group" that applies constraints across all users regardless of group membership.

## Why Advanced Permissions?

| Capability | Umbraco Built-in | Advanced Permissions |
|---|---|---|
| Permission granularity | Per user group, applies everywhere | Per user group **per content node** |
| Allow / Deny | Allow only (no explicit Deny) | Explicit **Allow** and **Deny** |
| Content tree inheritance | No inheritance | Permissions **propagate down the tree** with configurable scope |
| Scope control | N/A | **This Node Only**, **This Node and Descendants**, or **Descendants Only** |
| Global constraints | No built-in mechanism | **All Users Group** — applies to every user; specific groups can override |
| Audit trail | No reasoning available | Full **reasoning chain** showing exactly why each permission was granted or denied |
| Dedicated UI | Managed within user group settings | **Permissions Editor** + **Access Viewer** as separate backoffice sections |

## Features

### Explicit Allow / Deny per Content Node

Assign Allow or Deny permissions for any verb (Read, Create, Update, Delete, Publish, and more) on any content node, for any user group. No more all-or-nothing group permissions.

### Content Tree Inheritance with Scope Control

Permissions propagate through the content tree. Choose how far each permission reaches:

- **This Node Only** — applies only to the node where it is set
- **This Node and Descendants** — applies to the node and everything below it
- **Descendants Only** — skips the current node, applies to children and below

### All Users Group

The All Users Group applies permissions to every user regardless of their group membership. Use it to set global constraints (e.g., deny Publish on a specific branch), then override with specific user group entries where needed.

### Permissions Editor

A dedicated backoffice section to manage permission entries. Select a user group, browse the content tree, and set Allow or Deny for each verb with full scope control.

<!-- TODO: Add screenshot of Permissions Editor UI -->

### Access Viewer

View the effective (resolved) permissions for any user or user group at any content node. Each permission shows a full reasoning chain explaining exactly how it was determined — which user group contributed, whether it was inherited or explicit, and from which node.

<!-- TODO: Add screenshot of Access Viewer UI -->

### Intelligent Resolution

The resolver walks the content tree from the current node upward:

1. Check for an explicit entry on the current node
2. If none found, inherit from the nearest ancestor with a matching entry
3. The All Users Group is evaluated first; specific user group entries override it
4. If no entry exists anywhere in the tree, the default is **Deny**

**Example:** Suppose the "Editors" user group has Publish set to Allow with scope "This Node and Descendants" on the Home node. A content editor in that group can publish any page under Home. Now you set Publish to Deny on the "Press Releases" node for the same group — editors can still publish everywhere under Home *except* the Press Releases branch.

## Getting Started

### Prerequisites

- Umbraco **v17.3.0** or later
- .NET **10**

### Installation

```bash
dotnet add package LP.Umbraco.AdvancedPermissions
```

The package auto-registers via Umbraco's `IComposer` discovery — **no additional setup code is needed**.

After installation, two new sections appear in the Umbraco backoffice:

- **Permissions Editor** — manage permission entries per user group and content node
- **Access Viewer** — inspect effective permissions with full reasoning

### Configuration

No configuration is required. The package works out of the box with sensible defaults. Permission verbs are automatically derived from the standard Umbraco permissions.

## Management API

All endpoints are under `/umbraco/management/api/v1/advanced-permissions/` and require backoffice authentication.

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/roles` | All assignable user groups (including All Users Group) |
| GET | `/verbs` | All permission verbs with display names |
| GET | `/tree/root?roleAlias=` | Root content nodes with permission entries for a user group |
| GET | `/tree/children?parentKey=&roleAlias=` | Child nodes with permission entries |
| GET | `/permissions?nodeKey=&roleAlias=` | Stored entries for a specific node and user group |
| PUT | `/permissions` | Save (replace) entries for a node and user group |
| GET | `/effective?userKey=&nodeKey=` | Effective permissions for a user at a node |
| GET | `/effective/by-role?roleAlias=&nodeKey=` | Effective permissions for a user group at a node |

## Feedback

Found a bug or have a feature request? Please [open an issue](https://github.com/Luuk1983/LP.Umbraco.AdvancedPermissions/issues) on GitHub.

## License

This project is licensed under the [MIT License](LICENSE).
