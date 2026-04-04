namespace UmbracoAdvancedSecurity.Core.Models;

/// <summary>
/// Represents the fully resolved effective permission for a specific (user, node, verb) combination.
/// </summary>
/// <param name="Verb">The permission verb that was resolved, e.g. <c>Umb.Document.Read</c>.</param>
/// <param name="IsAllowed">
/// <see langword="true"/> if the user is allowed to perform the action;
/// <see langword="false"/> if denied (explicitly or implicitly).
/// </param>
/// <param name="IsExplicit">
/// <see langword="true"/> if the determining permission was set directly on the target node;
/// <see langword="false"/> if inherited from an ancestor or group defaults.
/// </param>
/// <param name="Reasoning">
/// The list of role contributions that led to this result, ordered from highest to lowest priority.
/// Used by the Access Viewer to explain why this permission was resolved as it was.
/// </param>
public sealed record EffectivePermission(
    string Verb,
    bool IsAllowed,
    bool IsExplicit,
    IReadOnlyList<PermissionReasoning> Reasoning);
