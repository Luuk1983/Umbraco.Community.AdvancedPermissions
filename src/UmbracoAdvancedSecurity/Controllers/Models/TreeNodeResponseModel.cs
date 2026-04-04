namespace UmbracoAdvancedSecurity.Controllers.Models;

/// <summary>
/// Represents a content tree node with its stored permission entries for a specific role.
/// Used by the Security Editor to render the permission grid.
/// </summary>
/// <param name="Key">The unique key of the content node.</param>
/// <param name="Name">The display name of the content node.</param>
/// <param name="Icon">The content type icon, if available.</param>
/// <param name="HasChildren">Whether this node has child nodes.</param>
/// <param name="Entries">The stored permission entries for the requested role on this node.</param>
public sealed record TreeNodeResponseModel(
    Guid Key,
    string Name,
    string? Icon,
    bool HasChildren,
    IReadOnlyList<PermissionEntryResponseModel> Entries);
