using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.Core.Interfaces;

/// <summary>
/// Provides data access for advanced security permission entries.
/// </summary>
public interface IAdvancedPermissionRepository
{
    /// <summary>
    /// Gets all permission entries for a specific node and role.
    /// </summary>
    /// <param name="nodeKey">
    /// The content node key. Use <c>AdvancedPermissionsConstants.VirtualRootNodeKey</c> for virtual-root entries.
    /// </param>
    /// <param name="roleAlias">The role alias to filter by.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>All entries matching the node and role.</returns>
    Task<IReadOnlyList<AdvancedPermissionEntry>> GetByNodeAndRoleAsync(
        Guid nodeKey,
        string roleAlias,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all permission entries for a specific node across all roles.
    /// </summary>
    /// <param name="nodeKey">
    /// The content node key. Use <c>AdvancedPermissionsConstants.VirtualRootNodeKey</c> for virtual-root entries.
    /// </param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>All entries for the given node.</returns>
    Task<IReadOnlyList<AdvancedPermissionEntry>> GetByNodeAsync(
        Guid nodeKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all permission entries for a specific role across all nodes.
    /// Used for eager-loading the entry cache when resolving permissions.
    /// </summary>
    /// <param name="roleAlias">The role alias to filter by.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>All entries for the given role.</returns>
    Task<IReadOnlyList<AdvancedPermissionEntry>> GetByRoleAsync(
        string roleAlias,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all permission entries for a set of node keys across all roles.
    /// Used by the path-entries endpoint to show raw entries along an inheritance path.
    /// </summary>
    /// <param name="nodeKeys">
    /// The node keys to include. Include <c>AdvancedPermissionsConstants.VirtualRootNodeKey</c> to include virtual-root entries.
    /// </param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>All entries for the given nodes across all roles.</returns>
    Task<IReadOnlyList<AdvancedPermissionEntry>> GetByNodesAsync(
        IEnumerable<Guid> nodeKeys,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all permission entries for a set of role aliases across a set of node keys.
    /// Used for batch resolution (e.g., checking permissions for many nodes in one query).
    /// </summary>
    /// <param name="roleAliases">The role aliases to include.</param>
    /// <param name="nodeKeys">
    /// The node keys to include. Include <c>AdvancedPermissionsConstants.VirtualRootNodeKey</c> to include virtual-root entries.
    /// </param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>All matching entries.</returns>
    Task<IReadOnlyList<AdvancedPermissionEntry>> GetByRolesAndNodesAsync(
        IEnumerable<string> roleAliases,
        IEnumerable<Guid> nodeKeys,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all permission entries for a set of node keys and a single role.
    /// Used to batch-load entries when rendering tree nodes (avoids N+1 queries).
    /// </summary>
    /// <param name="nodeKeys">The content node keys to load entries for.</param>
    /// <param name="roleAlias">The role alias to filter by.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>All entries matching any of the given nodes and the role.</returns>
    Task<IReadOnlyList<AdvancedPermissionEntry>> GetByNodesAndRoleAsync(
        IEnumerable<Guid> nodeKeys,
        string roleAlias,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves permission entries for a node and role, replacing any existing entries for that combination.
    /// </summary>
    /// <param name="nodeKey">
    /// The content node key. Use <c>AdvancedPermissionsConstants.VirtualRootNodeKey</c> for virtual-root entries.
    /// </param>
    /// <param name="roleAlias">The role alias.</param>
    /// <param name="entries">
    /// The new entries to store. Pass an empty collection to remove all entries (revert to inherit).
    /// </param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    Task SaveAsync(
        Guid nodeKey,
        string roleAlias,
        IEnumerable<(string Verb, PermissionState State, PermissionScope Scope)> entries,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a specific permission entry, reverting it to the inherited/default state.
    /// </summary>
    /// <param name="nodeKey">
    /// The content node key. Use <c>AdvancedPermissionsConstants.VirtualRootNodeKey</c> for virtual-root entries.
    /// </param>
    /// <param name="roleAlias">The role alias.</param>
    /// <param name="verb">The permission verb to remove.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    Task DeleteAsync(
        Guid nodeKey,
        string roleAlias,
        string verb,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all permission entries for a specific node across all roles.
    /// Used when a content node is permanently deleted.
    /// </summary>
    /// <param name="nodeKey">The content node key to remove all entries for.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    Task DeleteAllForNodeAsync(
        Guid nodeKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all permission entries for a specific role across all nodes.
    /// Used when a user group is permanently deleted.
    /// </summary>
    /// <param name="roleAlias">The role alias to remove all entries for.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    Task DeleteAllForRoleAsync(
        string roleAlias,
        CancellationToken cancellationToken = default);
}
