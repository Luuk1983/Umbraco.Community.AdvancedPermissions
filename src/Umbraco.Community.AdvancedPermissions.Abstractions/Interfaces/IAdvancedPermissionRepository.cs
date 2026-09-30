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
    /// Saves permission entries for a node and role, replacing any existing entries for that
    /// combination - <b>without any concurrency check</b>. Retained only for compatibility;
    /// implement the overload that takes an <c>expectedStamp</c> instead.
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
    /// <param name="entries">
    /// The new entries to store. Pass an empty collection to remove all entries (revert to inherit).
    /// </param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    [Obsolete(
        "This overload performs NO concurrency check: a save through it can silently overwrite a change " +
        "another user saved after the caller read the permissions. It exists only so implementations " +
        "written against earlier releases keep compiling. New implementations must implement the " +
        "SaveAsync overload that takes an 'expectedStamp' parameter and verify that stamp atomically " +
        "with the write - do not implement only this member and assume you are protected. Callers " +
        "should call the overload that takes 'expectedStamp'.")]
    Task SaveAsync(
        Guid nodeKey,
        string roleAlias,
        IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> entries,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves permission entries for a node and role, replacing any existing entries for that
    /// combination, refusing the write if the stored entries have changed since the caller read them.
    /// <b>This is the member to implement.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// Atomic: implemented as a single-pair <see cref="SaveManyAsync"/>, so a failure while writing
    /// the replacement rows leaves the previously stored entries intact rather than wiped.
    /// </para>
    /// <para>
    /// This overload has a default implementation that forwards to the older, unstamped
    /// <c>SaveAsync</c> and ignores <paramref name="expectedStamp"/>. It exists so an implementation
    /// written before concurrency checking existed keeps compiling - and, because it ignores the
    /// stamp, an implementation that relies on it performs <b>no</b> concurrency check. Override it
    /// and check the stamp inside the write transaction to get the protection.
    /// </para>
    /// </remarks>
    /// <param name="nodeKey">
    /// The content node key. Use <c>AdvancedPermissionsConstants.VirtualRootNodeKey</c> for virtual-root entries.
    /// </param>
    /// <param name="roleAlias">The role alias.</param>
    /// <param name="entries">
    /// The new entries to store. Pass an empty collection to remove all entries (revert to inherit).
    /// </param>
    /// <param name="expectedStamp">
    /// The stamp the writer read these entries under, or <see langword="null"/> to skip the
    /// concurrency check. Checked inside the write transaction - see <see cref="SaveManyAsync"/>.
    /// </param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <exception cref="Exceptions.PermissionConcurrencyException">
    /// <paramref name="expectedStamp"/> was supplied and no longer matches the stored entries.
    /// Nothing was written.
    /// </exception>
    Task SaveAsync(
        Guid nodeKey,
        string roleAlias,
        IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> entries,
        string? expectedStamp,
        CancellationToken cancellationToken = default) =>
        // Default implementation: forwards to the original member and ignores the stamp, so an
        // implementation written before concurrency checking existed keeps compiling. Suppressed
        // deliberately - calling the obsolete member from here is the whole point of this fallback.
#pragma warning disable CS0618
        SaveAsync(nodeKey, roleAlias, entries, cancellationToken);
#pragma warning restore CS0618

    /// <summary>
    /// Replaces the entries for several node-and-user-group pairs in a single transaction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// All or nothing. The editors save many nodes at once, and a partial write — some nodes
    /// updated, some not — is a worse outcome than either completing or refusing, because nothing
    /// afterwards can tell which half landed.
    /// </para>
    /// <para>
    /// The concurrency check lives here, inside the transaction, rather than in the caller. A check
    /// made before the write is a separate operation: two writers carrying the same expected stamp
    /// can both pass it and then both write, silently overwriting each other — the exact failure
    /// the stamp exists to prevent. Here the stored entries are read and compared under serializable
    /// isolation, in the same transaction that then deletes and inserts, so a concurrent writer
    /// cannot slip in between the read and the write. Implementations must preserve that: they may
    /// not check first and write in a separate transaction.
    /// </para>
    /// </remarks>
    /// <param name="batch">
    /// The node key, user group alias, replacement entries and expected stamp for each pair. A
    /// <see langword="null"/> expected stamp skips the check for that pair, which is how a forced
    /// save and an older client behave.
    /// </param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <exception cref="Exceptions.PermissionConcurrencyException">
    /// One or more pairs supplied an expected stamp that no longer matches the stored entries. The
    /// exception lists every such pair, and nothing was written for any pair in the batch.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown before any database work if <paramref name="batch"/> contains more than one entry for
    /// the same node key and user group alias. A well-behaved caller groups by node first, so this
    /// should never fire in practice; it exists so a caller that fails to do so gets a loud, specific
    /// error rather than a silent merge of the two entry sets (or a confusing unique-index violation).
    /// </exception>
    Task SaveManyAsync(
        IEnumerable<(Guid NodeKey, string RoleAlias, IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries, string? ExpectedStamp)> batch,
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
