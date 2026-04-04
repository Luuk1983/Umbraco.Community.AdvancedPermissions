using UmbracoAdvancedSecurity.Core.Models;

namespace UmbracoAdvancedSecurity.Core.Interfaces;

/// <summary>
/// Provides high-level operations for managing and resolving advanced security permissions.
/// This service is the main entry point for all permission-related logic in the package.
/// </summary>
public interface IAdvancedPermissionService
{
    /// <summary>
    /// Resolves the effective permission for a specific user, node, and verb.
    /// </summary>
    /// <param name="userKey">The key of the user to resolve permissions for.</param>
    /// <param name="nodeKey">The key of the content node.</param>
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
    /// Resolves effective permissions for all standard verbs for a user at a specific node.
    /// </summary>
    /// <param name="userKey">The key of the user to resolve permissions for.</param>
    /// <param name="nodeKey">The key of the content node.</param>
    /// <param name="pathFromRoot">
    /// The ordered list of node keys from root to the target node (inclusive).
    /// </param>
    /// <param name="verbs">The verbs to resolve. Resolves all standard verbs if <see langword="null"/>.</param>
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
    /// <param name="nodeKey">The content node key, or <see langword="null"/> for root-level entries.</param>
    /// <param name="roleAlias">The role alias.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The stored entries for the node and role.</returns>
    Task<IReadOnlyList<AdvancedPermissionEntry>> GetEntriesAsync(
        Guid? nodeKey,
        string roleAlias,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves permission entries for a node and role, replacing any existing entries.
    /// Invalidates the relevant cache entries.
    /// </summary>
    /// <param name="nodeKey">The content node key, or <see langword="null"/> for root-level entries.</param>
    /// <param name="roleAlias">The role alias.</param>
    /// <param name="entries">The new entries to store.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    Task SaveEntriesAsync(
        Guid? nodeKey,
        string roleAlias,
        IEnumerable<(string Verb, PermissionState State, PermissionScope Scope)> entries,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all raw permission entries for a specific node across all roles.
    /// Used by the Security Editor to show the full permission picture for a node.
    /// </summary>
    /// <param name="nodeKey">The content node key, or <see langword="null"/> for root-level entries.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>All stored entries for the node.</returns>
    Task<IReadOnlyList<AdvancedPermissionEntry>> GetEntriesByNodeAsync(
        Guid? nodeKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the effective permissions for a single role at a specific node.
    /// Unlike <see cref="ResolveAllAsync"/>, this does not require a user — it resolves
    /// as if the user has exactly the given role plus the implicit <c>$everyone</c> role.
    /// </summary>
    /// <param name="roleAlias">The role alias to resolve for.</param>
    /// <param name="nodeKey">The key of the content node.</param>
    /// <param name="pathFromRoot">
    /// The ordered list of node keys from root to the target node (inclusive).
    /// </param>
    /// <param name="roleDefaultVerbs">
    /// The default permission verbs for the given role (from Umbraco group defaults).
    /// Pass an empty set for the <c>$everyone</c> role or custom/virtual roles.
    /// </param>
    /// <param name="verbs">The verbs to resolve. Resolves all standard verbs if <see langword="null"/>.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>A dictionary mapping each verb to its effective permission.</returns>
    Task<IReadOnlyDictionary<string, EffectivePermission>> ResolveForRoleAsync(
        string roleAlias,
        Guid nodeKey,
        IReadOnlyList<Guid> pathFromRoot,
        IReadOnlySet<string> roleDefaultVerbs,
        IEnumerable<string>? verbs = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a specific permission entry (reverts it to inherit).
    /// Invalidates the relevant cache entries.
    /// </summary>
    /// <param name="nodeKey">The content node key, or <see langword="null"/> for root-level entries.</param>
    /// <param name="roleAlias">The role alias.</param>
    /// <param name="verb">The permission verb to remove.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    Task DeleteEntryAsync(
        Guid? nodeKey,
        string roleAlias,
        string verb,
        CancellationToken cancellationToken = default);
}
