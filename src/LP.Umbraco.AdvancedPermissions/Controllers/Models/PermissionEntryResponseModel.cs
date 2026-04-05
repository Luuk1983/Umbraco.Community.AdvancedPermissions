namespace LP.Umbraco.AdvancedPermissions.Controllers.Models;

/// <summary>
/// Represents a single stored permission entry returned by the API.
/// </summary>
/// <param name="Id">The unique database identifier for this entry.</param>
/// <param name="NodeKey">The content node key, or <see langword="null"/> for root-level entries.</param>
/// <param name="RoleAlias">The role alias (user group alias or <c>$everyone</c>).</param>
/// <param name="Verb">The permission verb, e.g. <c>Umb.Document.Read</c>.</param>
/// <param name="State">The permission state: <c>Allow</c> or <c>Deny</c>.</param>
/// <param name="Scope">The scope: <c>ThisNodeOnly</c>, <c>ThisNodeAndDescendants</c>, or <c>DescendantsOnly</c>.</param>
public sealed record PermissionEntryResponseModel(
    int Id,
    Guid? NodeKey,
    string RoleAlias,
    string Verb,
    string State,
    string Scope);
