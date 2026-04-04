namespace UmbracoAdvancedSecurity.Core.Models;

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
/// <param name="GroupDefaultVerbsByRole">
/// A mapping of role alias to the set of permission verbs granted by that role's
/// Umbraco group defaults. These are treated as virtual Allow entries at the root
/// with <see cref="PermissionScope.ThisNodeAndDescendants"/> scope.
/// </param>
/// <param name="StoredEntries">
/// All permission entries stored in the advanced security table for the roles in
/// <paramref name="RoleAliases"/> across the nodes in <paramref name="PathFromRoot"/>.
/// </param>
public sealed record PermissionResolutionContext(
    Guid TargetNodeKey,
    IReadOnlyList<Guid> PathFromRoot,
    IReadOnlyList<string> RoleAliases,
    IReadOnlyDictionary<string, IReadOnlySet<string>> GroupDefaultVerbsByRole,
    IReadOnlyList<AdvancedPermissionEntry> StoredEntries);
