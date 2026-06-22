namespace Umbraco.Community.AdvancedPermissions.AI.Models;

/// <summary>
/// The result of a who-can query: which roles can and cannot perform a verb at a content node.
/// </summary>
/// <param name="Verb">The permission verb that was evaluated, e.g. <c>Umb.Document.Publish</c>.</param>
/// <param name="NodeKey">The content node the verb was evaluated at.</param>
/// <param name="AllowedRoles">The aliases of roles whose effective permission for the verb is allowed.</param>
/// <param name="DeniedRoles">The aliases of roles whose effective permission for the verb is denied.</param>
public sealed record WhoCanResult(
    string Verb,
    Guid NodeKey,
    IReadOnlyList<string> AllowedRoles,
    IReadOnlyList<string> DeniedRoles);
