using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.Core.Interfaces;

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
    /// <param name="nodeKey">
    /// The content node key. Use <c>AdvancedPermissionsConstants.VirtualRootNodeKey</c> for virtual-root entries.
    /// </param>
    /// <param name="roleAlias">The role alias.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The stored entries for the node and role.</returns>
    Task<IReadOnlyList<AdvancedPermissionEntry>> GetEntriesAsync(
        Guid nodeKey,
        string roleAlias,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves permission entries for a node and role, replacing any existing entries, <b>without any
    /// concurrency check</b>. Retained only for compatibility; implement the overload that takes an
    /// <c>expectedStamp</c> instead.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the member as released before concurrency checking existed. It survives so that an
    /// implementation written against an earlier release keeps compiling. Which of the two members
    /// an implementation supplies has a correctness consequence: an implementation that supplies
    /// only this one gets no concurrency protection, and a save through it can silently overwrite a
    /// change another user made after the caller read the entries.
    /// </para>
    /// <para>
    /// The package's own code never calls this member; it always calls the stamped overload. The
    /// stamped overload's default implementation forwards here and ignores the stamp, which is what
    /// keeps an older implementation working - and unchecked.
    /// </para>
    /// </remarks>
    /// <param name="nodeKey">
    /// The content node key. Use <c>AdvancedPermissionsConstants.VirtualRootNodeKey</c> for virtual-root entries.
    /// </param>
    /// <param name="roleAlias">The role alias.</param>
    /// <param name="entries">The new entries to store.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    [Obsolete(
        "This overload performs NO concurrency check: a save through it can silently overwrite a change " +
        "another user saved after the caller read the permissions. It exists only so implementations " +
        "written against earlier releases keep compiling. New implementations must implement the " +
        "SaveEntriesAsync overload that takes an 'expectedStamp' parameter and verify that stamp " +
        "atomically with the write - do not implement only this member and assume you are protected. " +
        "Callers should call the overload that takes 'expectedStamp'.")]
    Task SaveEntriesAsync(
        Guid nodeKey,
        string roleAlias,
        IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> entries,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves permission entries for a node and role, replacing any existing entries, refusing the
    /// write if the stored entries have changed since the caller read them. Invalidates the
    /// relevant cache entries. <b>This is the member to implement.</b>
    /// </summary>
    /// <remarks>
    /// This overload has a default implementation that forwards to the older, unstamped
    /// <c>SaveEntriesAsync</c> and ignores <paramref name="expectedStamp"/>. It exists so an
    /// implementation written before concurrency checking existed keeps compiling - and, because it
    /// ignores the stamp, an implementation that relies on it performs <b>no</b> concurrency check.
    /// Override it and make the check atomically with the write to get the protection.
    /// </remarks>
    /// <param name="nodeKey">
    /// The content node key. Use <c>AdvancedPermissionsConstants.VirtualRootNodeKey</c> for virtual-root entries.
    /// </param>
    /// <param name="roleAlias">The role alias.</param>
    /// <param name="entries">The new entries to store.</param>
    /// <param name="expectedStamp">
    /// The stamp the caller read these entries under, or <see langword="null"/> to skip the
    /// concurrency check. The check is made atomically with the write.
    /// </param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <exception cref="Exceptions.PermissionConcurrencyException">
    /// <paramref name="expectedStamp"/> was supplied and no longer matches the stored entries.
    /// Nothing was written, no cache was invalidated and no notification was published.
    /// </exception>
    Task SaveEntriesAsync(
        Guid nodeKey,
        string roleAlias,
        IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> entries,
        string? expectedStamp,
        CancellationToken cancellationToken = default) =>
        // Default implementation: forwards to the original member and ignores the stamp, so an
        // implementation written before concurrency checking existed keeps compiling. Suppressed
        // deliberately - calling the obsolete member from here is the whole point of this fallback.
#pragma warning disable CS0618
        SaveEntriesAsync(nodeKey, roleAlias, entries, cancellationToken);
#pragma warning restore CS0618

    /// <summary>
    /// Gets the raw permission entries for a set of nodes and a single role in one batch.
    /// Used by the tree controller to avoid N+1 queries when rendering children.
    /// </summary>
    /// <param name="nodeKeys">The content node keys to load entries for.</param>
    /// <param name="roleAlias">The role alias.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>All stored entries for the given nodes and role.</returns>
    Task<IReadOnlyList<AdvancedPermissionEntry>> GetEntriesByNodesAndRoleAsync(
        IEnumerable<Guid> nodeKeys,
        string roleAlias,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all raw permission entries for a specific node across all roles.
    /// Used by the Security Editor to show the full permission picture for a node.
    /// </summary>
    /// <param name="nodeKey">
    /// The content node key. Use <c>AdvancedPermissionsConstants.VirtualRootNodeKey</c> for virtual-root entries.
    /// </param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>All stored entries for the node.</returns>
    Task<IReadOnlyList<AdvancedPermissionEntry>> GetEntriesByNodeAsync(
        Guid nodeKey,
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
    /// <param name="verbs">The verbs to resolve. Resolves all standard verbs if <see langword="null"/>.</param>
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
    /// The content node key. Use <c>AdvancedPermissionsConstants.VirtualRootNodeKey</c> for virtual-root entries.
    /// </param>
    /// <param name="roleAlias">The role alias.</param>
    /// <param name="verb">The permission verb to remove.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    Task DeleteEntryAsync(
        Guid nodeKey,
        string roleAlias,
        string verb,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the entries for several node-and-user-group pairs in a single transaction,
    /// invalidating caches and publishing one notification per pair afterwards.
    /// </summary>
    /// <param name="batch">
    /// The node key, user group alias, replacement entries and expected stamp for each pair. A
    /// <see langword="null"/> expected stamp skips the concurrency check for that pair.
    /// </param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <exception cref="Exceptions.PermissionConcurrencyException">
    /// One or more pairs supplied an expected stamp that no longer matches the stored entries. The
    /// check is made atomically with the write; nothing was written, no cache was invalidated and no
    /// notification was published.
    /// </exception>
    Task SaveManyAsync(
        IReadOnlyList<(Guid NodeKey, string RoleAlias, IReadOnlyList<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries, string? ExpectedStamp)> batch,
        CancellationToken cancellationToken = default);
}
