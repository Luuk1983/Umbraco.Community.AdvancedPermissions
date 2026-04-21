namespace Umbraco.Community.AdvancedPermissions.Controllers.Models;

/// <summary>
/// Represents a node in the inheritance path from root to target.
/// </summary>
/// <param name="Key">The unique key of the content node.</param>
/// <param name="Name">The display name of the content node.</param>
/// <param name="Icon">The content type icon, if available.</param>
public sealed record PathNodeModel(Guid Key, string Name, string? Icon);

/// <summary>
/// Response model for the permissions-for-path endpoint.
/// Contains the ordered path from virtual root to the target node,
/// plus all stored permission entries along that path for a specific verb.
/// </summary>
/// <param name="Path">
/// Ordered list of nodes from virtual root to the target node (inclusive).
/// The first element is always the virtual root ("Default permissions").
/// </param>
/// <param name="Entries">
/// All stored permission entries for the specified verb at any node in the path, across all roles.
/// </param>
public sealed record PathEntriesResponseModel(
    IReadOnlyList<PathNodeModel> Path,
    IReadOnlyList<PermissionEntryResponseModel> Entries);
