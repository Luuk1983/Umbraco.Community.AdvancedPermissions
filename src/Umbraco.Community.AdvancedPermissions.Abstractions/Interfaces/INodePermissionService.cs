using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.Core.Interfaces;

/// <summary>
/// High-level operations for managing and resolving node-keyed advanced security permissions.
/// </summary>
/// <remarks>
/// The orchestration contract shared by every node-based permission target (content documents,
/// library elements/folders). It composes a repository, the pure resolver, the user service, and a
/// two-level cache. Concrete targets expose a thin marker (<see cref="IAdvancedPermissionService"/>,
/// <see cref="IElementNodePermissionService"/>) so each can be registered and consumed independently
/// while sharing one orchestration implementation.
/// </remarks>
public interface INodePermissionService
{
    /// <summary>
    /// Resolves the effective permission for a specific user, node, and verb.
    /// </summary>
    /// <param name="userKey">The key of the user to resolve permissions for.</param>
    /// <param name="nodeKey">The key of the node.</param>
    /// <param name="pathFromRoot">
    /// The ordered list of node keys from root to the target node (inclusive).
    /// </param>
    /// <param name="verb">The permission verb to resolve, e.g. <c>Umb.Document.Read</c>.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The effective permission including reasoning for the Access Viewer.</returns>
    Task<EffectivePermission> ResolveAsync(
        Guid userKey,
        Guid nodeKey,
        IReadOnlyList<Guid> pathFromRoot,
        string verb,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves effective permissions for all of the target's standard verbs for a user at a node.
    /// </summary>
    /// <param name="userKey">The key of the user to resolve permissions for.</param>
    /// <param name="nodeKey">The key of the node.</param>
    /// <param name="pathFromRoot">
    /// The ordered list of node keys from root to the target node (inclusive).
    /// </param>
    /// <param name="verbs">The verbs to resolve. Resolves all of the target's standard verbs if <see langword="null"/>.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>A dictionary mapping each verb to its effective permission.</returns>
    Task<IReadOnlyDictionary<string, EffectivePermission>> ResolveAllAsync(
        Guid userKey,
        Guid nodeKey,
        IReadOnlyList<Guid> pathFromRoot,
        IEnumerable<string>? verbs = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the raw permission entries for a specific node and role.
    /// </summary>
    /// <param name="nodeKey">
    /// The node key. Use <c>AdvancedPermissionsConstants.VirtualRootNodeKey</c> for virtual-root entries.
    /// </param>
    /// <param name="roleAlias">The role alias.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The stored entries for the node and role.</returns>
    Task<IReadOnlyList<AdvancedPermissionEntry>> GetEntriesAsync(
        Guid nodeKey,
        string roleAlias,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves permission entries for a node and role, replacing any existing entries.
    /// Invalidates the relevant cache entries.
    /// </summary>
    /// <param name="nodeKey">
    /// The node key. Use <c>AdvancedPermissionsConstants.VirtualRootNodeKey</c> for virtual-root entries.
    /// </param>
    /// <param name="roleAlias">The role alias.</param>
    /// <param name="entries">The new entries to store.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    Task SaveEntriesAsync(
        Guid nodeKey,
        string roleAlias,
        IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> entries,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the raw permission entries for a set of nodes and a single role in one batch.
    /// Used by the tree controller to avoid N+1 queries when rendering children.
    /// </summary>
    /// <param name="nodeKeys">The node keys to load entries for.</param>
    /// <param name="roleAlias">The role alias.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>All stored entries for the given nodes and role.</returns>
    Task<IReadOnlyList<AdvancedPermissionEntry>> GetEntriesByNodesAndRoleAsync(
        IEnumerable<Guid> nodeKeys,
        string roleAlias,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all raw permission entries for a specific node across all roles.
    /// Used by the editor to show the full permission picture for a node.
    /// </summary>
    /// <param name="nodeKey">
    /// The node key. Use <c>AdvancedPermissionsConstants.VirtualRootNodeKey</c> for virtual-root entries.
    /// </param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>All stored entries for the node.</returns>
    Task<IReadOnlyList<AdvancedPermissionEntry>> GetEntriesByNodeAsync(
        Guid nodeKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the effective permissions for a single role at a specific node.
    /// Unlike <see cref="ResolveAllAsync"/>, this does not require a user — it resolves
    /// using only the given role's own entries (the <c>$everyone</c> role is intentionally excluded).
    /// </summary>
    /// <param name="roleAlias">The role alias to resolve for.</param>
    /// <param name="nodeKey">The key of the node.</param>
    /// <param name="pathFromRoot">
    /// The ordered list of node keys from root to the target node (inclusive).
    /// </param>
    /// <param name="verbs">The verbs to resolve. Resolves all of the target's standard verbs if <see langword="null"/>.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>A dictionary mapping each verb to its effective permission.</returns>
    Task<IReadOnlyDictionary<string, EffectivePermission>> ResolveForRoleAsync(
        string roleAlias,
        Guid nodeKey,
        IReadOnlyList<Guid> pathFromRoot,
        IEnumerable<string>? verbs = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a specific permission entry (reverts it to inherit).
    /// Invalidates the relevant cache entries.
    /// </summary>
    /// <param name="nodeKey">
    /// The node key. Use <c>AdvancedPermissionsConstants.VirtualRootNodeKey</c> for virtual-root entries.
    /// </param>
    /// <param name="roleAlias">The role alias.</param>
    /// <param name="verb">The permission verb to remove.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    Task DeleteEntryAsync(
        Guid nodeKey,
        string roleAlias,
        string verb,
        CancellationToken cancellationToken = default);
}
