namespace Umbraco.Community.AdvancedPermissions.Controllers.Models;

/// <summary>
/// Represents a single stored permission entry returned by the API.
/// </summary>
/// <param name="Id">The unique identifier for this entry (GUID).</param>
/// <param name="NodeKey">
/// The content node key. <c>AdvancedPermissionsConstants.VirtualRootNodeKey</c> indicates a virtual-root entry.
/// </param>
/// <param name="RoleAlias">The role alias (user group alias or <c>$everyone</c>).</param>
/// <param name="Verb">The permission verb, e.g. <c>Umb.Document.Read</c>.</param>
/// <param name="State">The permission state: <c>Allow</c> or <c>Deny</c>.</param>
/// <param name="Scope">The scope: <c>ThisNodeOnly</c>, <c>ThisNodeAndDescendants</c>, or <c>DescendantsOnly</c>.</param>
/// <param name="IsPriorityOverride">
/// Whether this entry is flagged as a priority override (CSS <c>!important</c>-style escape hatch).
/// Always present; older entries (saved before the feature) report <see langword="false"/>.
/// </param>
public sealed record PermissionEntryResponseModel(
    Guid Id,
    Guid NodeKey,
    string RoleAlias,
    string Verb,
    string State,
    string Scope,
    bool IsPriorityOverride);
