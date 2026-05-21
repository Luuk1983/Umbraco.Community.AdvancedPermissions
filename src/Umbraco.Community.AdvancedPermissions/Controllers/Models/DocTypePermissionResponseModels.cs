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
/// One row of the Create Audit listing.
/// </summary>
/// <param name="ContentTypeKey">The candidate doc-type key.</param>
/// <param name="ContentTypeAlias">The doc-type alias.</param>
/// <param name="ContentTypeName">The doc-type name.</param>
/// <param name="ContentTypeIcon">The doc-type icon alias, if any.</param>
/// <param name="IsAllowed">Whether the audited user may create the doc-type under the audited parent.</param>
/// <param name="IsExplicit">Whether the determining entry sits on the parent itself.</param>
/// <param name="Reasoning">The contributing entries in precedence order.</param>
public sealed record DocTypeCreateAuditItemResponseModel(
    Guid ContentTypeKey,
    string ContentTypeAlias,
    string ContentTypeName,
    string? ContentTypeIcon,
    bool IsAllowed,
    bool IsExplicit,
    IReadOnlyList<ReasoningItem> Reasoning);
