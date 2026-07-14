namespace Umbraco.Community.AdvancedPermissions.Core.Models;

/// <summary>
/// Provides all the input data needed by the permission resolver to resolve
/// effective permissions for a user at a specific content node.
/// </summary>
/// <param name="TargetNodeKey">The key of the content node to resolve permissions for.</param>
/// <param name="PathFromRoot">
/// The ordered list of node keys from the root down to (and including) the target node.
/// Example: [rootKey, sectionKey, parentKey, targetKey].
/// </param>
/// <param name="RoleAliases">
/// All role aliases belonging to the user, including <c>$everyone</c>.
/// </param>
/// <param name="StoredEntries">
/// All permission entries stored in the advanced security table for the roles in
/// <paramref name="RoleAliases"/> across the nodes in <paramref name="PathFromRoot"/>,
/// plus root-level entries (<c>NodeKey = null</c>) which act as the virtual-root defaults.
/// </param>
public sealed record PermissionResolutionContext(
    Guid TargetNodeKey,
    IReadOnlyList<Guid> PathFromRoot,
    IReadOnlyList<string> RoleAliases,
    IReadOnlyList<AdvancedPermissionEntry> StoredEntries);
