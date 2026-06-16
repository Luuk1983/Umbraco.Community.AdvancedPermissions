namespace Umbraco.Community.AdvancedPermissions.Controllers.Models;

/// <summary>
/// Represents a library tree node (element or element folder) with its stored permission entries for a
/// specific role. Used by the Library permissions editor to render the permission grid.
/// </summary>
/// <remarks>
/// Mirrors <see cref="TreeNodeResponseModel"/> but adds <paramref name="IsFolder"/> so the frontend can
/// apply the correct per-node-kind verb applicability: folders carry inheritance and the five container
/// verbs at the node level, while elements are leaves carrying the full element verb set.
/// </remarks>
/// <param name="Key">The unique key of the element or folder.</param>
/// <param name="Name">The display name.</param>
/// <param name="Icon">The icon (the element's content-type icon, or a folder icon).</param>
/// <param name="HasChildren">Whether this node has children (always <see langword="false"/> for elements).</param>
/// <param name="IsFolder">Whether this node is an element folder (container) rather than an element.</param>
/// <param name="Entries">The stored permission entries for the requested role on this node.</param>
public sealed record ElementTreeNodeResponseModel(
    Guid Key,
    string Name,
    string? Icon,
    bool HasChildren,
    bool IsFolder,
    IReadOnlyList<PermissionEntryResponseModel> Entries);
