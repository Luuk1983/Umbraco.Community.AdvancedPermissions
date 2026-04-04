namespace UmbracoAdvancedSecurity.Controllers.Models;

/// <summary>
/// Represents the full set of resolved effective permissions for a user or role at a content node.
/// </summary>
/// <param name="NodeKey">The content node key these permissions were resolved for.</param>
/// <param name="Permissions">The effective permission for each verb.</param>
public sealed record EffectivePermissionsResponseModel(
    Guid NodeKey,
    IReadOnlyList<EffectivePermissionItem> Permissions);

/// <summary>
/// Represents the resolved effective permission for a single verb, including reasoning.
/// </summary>
/// <param name="Verb">The permission verb, e.g. <c>Umb.Document.Read</c>.</param>
/// <param name="IsAllowed">Whether the permission is granted.</param>
/// <param name="IsExplicit">Whether the determining entry was set directly on the target node.</param>
/// <param name="Reasoning">
/// The per-role contributions that led to the final result, ordered from highest to lowest priority.
/// </param>
public sealed record EffectivePermissionItem(
    string Verb,
    bool IsAllowed,
    bool IsExplicit,
    IReadOnlyList<ReasoningItem> Reasoning);

/// <summary>
/// Describes a single role's contribution to a permission resolution result.
/// </summary>
/// <param name="ContributingRole">The role alias that contributed.</param>
/// <param name="State">The state contributed by this role: <c>Allow</c> or <c>Deny</c>.</param>
/// <param name="IsExplicit">Whether this contribution is from an entry on the target node itself.</param>
/// <param name="SourceNodeKey">The node where the entry was found, or <see langword="null"/> for group defaults.</param>
/// <param name="SourceScope">The scope of the source entry, or <see langword="null"/> for group defaults.</param>
/// <param name="IsFromGroupDefault">Whether this contribution comes from Umbraco group default permissions.</param>
public sealed record ReasoningItem(
    string ContributingRole,
    string State,
    bool IsExplicit,
    Guid? SourceNodeKey,
    string? SourceScope,
    bool IsFromGroupDefault);
