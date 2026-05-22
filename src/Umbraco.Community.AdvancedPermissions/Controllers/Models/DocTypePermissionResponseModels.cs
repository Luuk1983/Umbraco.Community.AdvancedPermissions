namespace Umbraco.Community.AdvancedPermissions.Controllers.Models;

/// <summary>
/// A single stored doc-type permission entry returned by the API.
/// </summary>
/// <param name="Id">The entry's identifier.</param>
/// <param name="NodeKey">
/// The content node the entry is scoped to. <c>AdvancedPermissionsConstants.VirtualRootNodeKey</c>
/// indicates the virtual root row.
/// </param>
/// <param name="ContentTypeKey">The doc-type the entry concerns.</param>
/// <param name="RoleAlias">The role alias (user group or <c>$everyone</c>).</param>
/// <param name="Verb">The verb (v1: only <c>Umb.Document.CreateOfType</c>).</param>
/// <param name="State">The state, as a string: <c>Allow</c> or <c>Deny</c>.</param>
/// <param name="Scope">The scope, as a string.</param>
public sealed record DocTypePermissionEntryResponseModel(
    Guid Id,
    Guid NodeKey,
    Guid ContentTypeKey,
    string RoleAlias,
    string Verb,
    string State,
    string Scope);

/// <summary>
/// Request body for saving a (node, role, content-type) triple's entries.
/// </summary>
/// <param name="NodeKey">
/// The node key the entries apply to. Use
/// <c>AdvancedPermissionsConstants.VirtualRootNodeKey</c> for the virtual-root row.
/// </param>
/// <param name="RoleAlias">The role alias.</param>
/// <param name="ContentTypeKey">The doc-type key.</param>
/// <param name="Entries">The verb-state-scope tuples. Empty list clears all entries for the triple.</param>
public sealed record SaveDocTypePermissionsRequestModel(
    Guid NodeKey,
    string RoleAlias,
    Guid ContentTypeKey,
    IReadOnlyList<SavePermissionEntryItem> Entries);

/// <summary>
/// A non-element doc-type that may appear in the editor's "document type" picker.
/// </summary>
/// <param name="Key">The doc-type key.</param>
/// <param name="Alias">The doc-type alias.</param>
/// <param name="Name">The doc-type's display name.</param>
/// <param name="Icon">The doc-type's icon alias, if any.</param>
public sealed record DocTypeListItemModel(
    Guid Key,
    string Alias,
    string Name,
    string? Icon);

/// <summary>
/// Per-doc-type result row for the tree-style "audit for node" endpoint. Includes an
/// <c>IsInAllowedChildren</c> flag so the UI can render `n/a` for doc-types not in the parent's
/// allowed-children list, distinct from a resolver-driven deny.
/// </summary>
public sealed record DocTypeAuditForNodeRowResponseModel(
    Guid ContentTypeKey,
    string ContentTypeAlias,
    string ContentTypeName,
    string? ContentTypeIcon,
    bool IsAllowed,
    bool IsExplicit,
    bool IsInAllowedChildren,
    IReadOnlyList<ReasoningItem> Reasoning);

/// <summary>
/// Top-level response of <c>GET /doc-type-permissions/audit-for-node</c>: the audited node
/// key plus one row per non-element doc type.
/// </summary>
/// <param name="NodeKey">The audited node (or virtual root).</param>
/// <param name="Results">One row per non-element doc-type.</param>
public sealed record DocTypeAuditForNodeResponseModel(
    Guid NodeKey,
    IReadOnlyList<DocTypeAuditForNodeRowResponseModel> Results);

/// <summary>
/// A doc-type permission entry shown along the inheritance path in the reasoning dialog.
/// </summary>
public sealed record DocTypePathEntryResponseModel(
    Guid Id,
    Guid NodeKey,
    Guid ContentTypeKey,
    string RoleAlias,
    string Verb,
    string State,
    string Scope);

/// <summary>
/// Response for <c>GET /doc-type-permissions/path-entries</c>: the inheritance path plus all
/// stored doc-type entries along that path filtered to the requested content-type.
/// </summary>
public sealed record DocTypePathEntriesResponseModel(
    IReadOnlyList<PathNodeModel> Path,
    IReadOnlyList<DocTypePathEntryResponseModel> Entries);
