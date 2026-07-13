namespace Umbraco.Community.AdvancedPermissions.Core.Models;

/// <summary>
/// Input data for <see cref="Interfaces.IDocTypePermissionResolver"/>.
/// </summary>
/// <param name="ContentTypeKey">The doc-type the resolution is about (filter dimension).</param>
/// <param name="ParentNodeKey">The parent node a new instance would be created under.</param>
/// <param name="PathFromRoot">
/// The ordered list of node keys from root down to (and including) the parent node.
/// The "target" of the path walk is the parent — entries on or above the parent apply.
/// </param>
/// <param name="RoleAliases">
/// All role aliases belonging to the user, including <c>$everyone</c>.
/// </param>
/// <param name="StoredEntries">
/// All doc-type entries relevant to this resolution — typically pre-filtered to the user's
/// roles, the path nodes (plus virtual root), and the target content-type-key.
/// </param>
public sealed record DocTypePermissionResolutionContext(
    Guid ContentTypeKey,
    Guid ParentNodeKey,
    IReadOnlyList<Guid> PathFromRoot,
    IReadOnlyList<string> RoleAliases,
    IReadOnlyList<DocTypePermissionEntry> StoredEntries);
