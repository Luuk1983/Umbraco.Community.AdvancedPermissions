namespace Umbraco.Community.AdvancedPermissions.Controllers.Models;

/// <summary>
/// Represents a request to save (replace) permission entries for a node and role.
/// </summary>
/// <param name="NodeKey">
/// The content node key. Use <c>AdvancedPermissionsConstants.VirtualRootNodeKey</c> for virtual-root entries.
/// </param>
/// <param name="RoleAlias">The role alias (user group alias or <c>$everyone</c>).</param>
/// <param name="Entries">The new entries. Pass an empty list to remove all entries (revert to inherit).</param>
public sealed record SavePermissionsRequestModel(
    Guid NodeKey,
    string RoleAlias,
    IReadOnlyList<SavePermissionEntryItem> Entries);

/// <summary>
/// Represents a single permission entry within a save request.
/// </summary>
/// <param name="Verb">The permission verb, e.g. <c>Umb.Document.Read</c>.</param>
/// <param name="State">The permission state: <c>Allow</c> or <c>Deny</c>.</param>
/// <param name="Scope">The scope: <c>ThisNodeOnly</c>, <c>ThisNodeAndDescendants</c>, or <c>DescendantsOnly</c>.</param>
public sealed record SavePermissionEntryItem(
    string Verb,
    string State,
    string Scope);
