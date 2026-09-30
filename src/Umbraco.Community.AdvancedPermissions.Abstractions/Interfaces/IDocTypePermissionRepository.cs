using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.Core.Interfaces;

/// <summary>
/// Provides data access for document-type permission entries (the new table introduced
/// alongside <see cref="IAdvancedPermissionRepository"/>).
/// </summary>
public interface IDocTypePermissionRepository
{
    /// <summary>
    /// Gets all entries for a specific role across all nodes and content types.
    /// Used by the L1 cache to eager-load entries for the user's roles before resolution.
    /// </summary>
    /// <param name="roleAlias">The role alias to filter by.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>All entries for the given role.</returns>
    Task<IReadOnlyList<DocTypePermissionEntry>> GetByRoleAsync(
        string roleAlias,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all entries for a specific (role, content-type) pair across all nodes.
    /// Used by the editor when loading the tree state for a selected (role, doc-type).
    /// </summary>
    /// <param name="roleAlias">The role alias.</param>
    /// <param name="contentTypeKey">The doc-type key.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>All matching entries.</returns>
    Task<IReadOnlyList<DocTypePermissionEntry>> GetByRoleAndContentTypeAsync(
        string roleAlias,
        Guid contentTypeKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces all entries for a (node, role, content-type) triple with the supplied entries -
    /// <b>without any concurrency check</b>. Retained only for compatibility; implement the overload
    /// that takes an <c>expectedStamp</c> instead.
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
    /// The node key. Use <c>AdvancedPermissionsConstants.VirtualRootNodeKey</c> for the virtual root row.
    /// </param>
    /// <param name="roleAlias">The role alias.</param>
    /// <param name="contentTypeKey">The doc-type key.</param>
    /// <param name="entries">The verb+state+scope tuples to persist.</param>
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
        Guid contentTypeKey,
        IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> entries,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces all entries for a (node, role, content-type) triple with the supplied entries,
    /// refusing the write if the stored entries have changed since the caller read them.
    /// Passing an empty collection removes any existing entries for that triple.
    /// <b>This is the member to implement.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// Atomic: implemented as a single-triple <see cref="SaveManyAsync"/>, so a failure while
    /// writing the replacement rows leaves the previously stored entries intact rather than wiped.
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
    /// The node key. Use <c>AdvancedPermissionsConstants.VirtualRootNodeKey</c> for the virtual root row.
    /// </param>
    /// <param name="roleAlias">The role alias.</param>
    /// <param name="contentTypeKey">The doc-type key.</param>
    /// <param name="entries">The verb+state+scope tuples to persist.</param>
    /// <param name="expectedStamp">
    /// The stamp the writer read these entries under, or <see langword="null"/> to skip the
    /// concurrency check. Checked inside the write transaction - see <see cref="SaveManyAsync"/>.
    /// </param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <exception cref="Exceptions.DocTypePermissionConcurrencyException">
    /// <paramref name="expectedStamp"/> was supplied and no longer matches the stored entries.
    /// Nothing was written.
    /// </exception>
    Task SaveAsync(
        Guid nodeKey,
        string roleAlias,
        Guid contentTypeKey,
        IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> entries,
        string? expectedStamp,
        CancellationToken cancellationToken = default) =>
        // Default implementation: forwards to the original member and ignores the stamp, so an
        // implementation written before concurrency checking existed keeps compiling. Suppressed
        // deliberately - calling the obsolete member from here is the whole point of this fallback.
#pragma warning disable CS0618
        SaveAsync(nodeKey, roleAlias, contentTypeKey, entries, cancellationToken);
#pragma warning restore CS0618

    /// <summary>
    /// Replaces the entries for several node, user group and document type triples in a single
    /// transaction.
    /// </summary>
    /// <remarks>
    /// All or nothing, for the same reason as <see cref="INodePermissionRepository.SaveManyAsync"/>:
    /// the editor changes several at once, and a partial write leaves a state nothing afterwards
    /// can read. The concurrency check is made inside the write transaction, under serializable
    /// isolation, for the reason set out there.
    /// </remarks>
    /// <param name="batch">
    /// The triple, replacement entries and expected stamp for each. A <see langword="null"/>
    /// expected stamp skips the check for that triple.
    /// </param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <exception cref="Exceptions.DocTypePermissionConcurrencyException">
    /// One or more triples supplied an expected stamp that no longer matches the stored entries.
    /// The exception lists every such triple, and nothing was written for any triple in the batch.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown before any database work if <paramref name="batch"/> contains more than one entry for
    /// the same node key, user group alias and document type key.
    /// </exception>
    Task SaveManyAsync(
        IEnumerable<(Guid NodeKey, string RoleAlias, Guid ContentTypeKey, IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries, string? ExpectedStamp)> batch,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all entries that reference the given node, across all roles, content types, and verbs.
    /// Called from the content-deletion cleanup handler.
    /// </summary>
    /// <param name="nodeKey">The node whose entries to remove.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    Task DeleteAllForNodeAsync(
        Guid nodeKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all entries that reference the given content type, across all roles, nodes, and verbs.
    /// Called from the content-type-deletion cleanup handler.
    /// </summary>
    /// <param name="contentTypeKey">The doc-type whose entries to remove.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    Task DeleteAllForContentTypeAsync(
        Guid contentTypeKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all entries that reference the given role alias, across all nodes, content types, and verbs.
    /// Called from the user-group-deletion cleanup handler.
    /// </summary>
    /// <param name="roleAlias">The role alias whose entries to remove.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    Task DeleteAllForRoleAsync(
        string roleAlias,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all entries for a specific content-type-key across a set of node keys, across all
    /// roles and verbs. Used by the tree-style audit's reasoning dialog to show which entries
    /// exist along the inheritance path for the audited doc type.
    /// </summary>
    /// <param name="contentTypeKey">The doc-type to filter by.</param>
    /// <param name="nodeKeys">
    /// The nodes to include. Pass <c>AdvancedPermissionsConstants.VirtualRootNodeKey</c> to
    /// include virtual-root entries.
    /// </param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>All matching entries across all roles.</returns>
    Task<IReadOnlyList<DocTypePermissionEntry>> GetByContentTypeAndNodesAsync(
        Guid contentTypeKey,
        IEnumerable<Guid> nodeKeys,
        CancellationToken cancellationToken = default);
}
