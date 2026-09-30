namespace Umbraco.Community.AdvancedPermissions.Controllers.Models;

/// <summary>
/// Represents a content tree node with its stored permission entries for a specific role.
/// Used by the Security Editor to render the permission grid.
/// </summary>
/// <param name="Key">The unique key of the content node.</param>
/// <param name="Name">The display name of the content node.</param>
/// <param name="Icon">The content type icon, if available.</param>
/// <param name="HasChildren">Whether this node has child nodes.</param>
/// <param name="Entries">The stored permission entries for the requested user group on this node.</param>
/// <param name="Stamp">
/// The concurrency stamp of <paramref name="Entries"/>. A client sends this back when saving; the
/// server refuses the save if the stored entries have moved since. A node with no entries carries
/// the well-known empty-set stamp, never null.
/// </param>
/// <remarks>
/// <para>
/// The <paramref name="Stamp"/> here is a <em>content</em> stamp: it is computed from the node's content
/// permission entries. The Doc Type editor loads its nodes from the content tree endpoints, so the
/// stamps on those nodes describe content permissions and are meaningless for a document-type save.
/// That editor must send back the stamp it got from the doc-type <c>GetForEditor</c> read (one per
/// node, computed from that node's document-type entries), never the one on the tree node.
/// </para>
/// </remarks>
public sealed record TreeNodeResponseModel(
    Guid Key,
    string Name,
    string? Icon,
    bool HasChildren,
    IReadOnlyList<PermissionEntryResponseModel> Entries,
    string Stamp);
