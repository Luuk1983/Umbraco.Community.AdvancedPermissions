using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.Core.Interfaces;

/// <summary>
/// High-level operations for managing and resolving document-type permissions.
/// </summary>
public interface IDocTypePermissionService
{
    /// <summary>
    /// Resolves whether a user may create instances of a given doc-type under a given parent.
    /// Combines the user's group aliases with <c>$everyone</c>, builds the resolution context, and
    /// delegates to the resolver.
    /// </summary>
    /// <param name="userKey">The user whose effective permission to compute.</param>
    /// <param name="parentNodeKey">
    /// The parent node where a new instance would be created, or
    /// <c>AdvancedPermissionsConstants.VirtualRootNodeKey</c> for root-level creates.
    /// </param>
    /// <param name="parentPathFromRoot">The ordered path of node keys from root to the parent (inclusive).</param>
    /// <param name="contentTypeKey">The candidate doc-type's key.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The effective permission together with reasoning.</returns>
    Task<EffectivePermission> ResolveCreateAsync(
        Guid userKey,
        Guid parentNodeKey,
        IReadOnlyList<Guid> parentPathFromRoot,
        Guid contentTypeKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convenience method for direct role-alias resolution (no user lookup needed).
    /// Used by the <c>IContentTypeFilter</c> implementation when the current user's role aliases
    /// are already known.
    /// </summary>
    /// <param name="roleAliases">
    /// The role aliases to consider — the caller is responsible for including <c>$everyone</c>
    /// if appropriate.
    /// </param>
    /// <param name="parentPathFromRoot">The ordered path of node keys from root to the parent (inclusive).</param>
    /// <param name="contentTypeKey">The candidate doc-type's key.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The effective permission.</returns>
    Task<EffectivePermission> ResolveCreateForRolesAsync(
        IReadOnlyList<string> roleAliases,
        IReadOnlyList<Guid> parentPathFromRoot,
        Guid contentTypeKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the entries that the editor needs to render the tree state for a selected
    /// (role, doc-type) combination.
    /// </summary>
    /// <param name="roleAlias">The role alias selected in the editor.</param>
    /// <param name="contentTypeKey">The doc-type selected in the editor.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>All stored entries for the combination, across all nodes (including virtual root).</returns>
    Task<IReadOnlyList<DocTypePermissionEntry>> GetEditorEntriesAsync(
        string roleAlias,
        Guid contentTypeKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves (replaces) the editor's entries for a (node, role, content-type) triple, <b>without any
    /// concurrency check</b>. Empty list clears. Retained only for compatibility; implement the
    /// overload that takes an <c>expectedStamp</c> instead.
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
    /// <param name="nodeKey">The node the entries apply to (or virtual root).</param>
    /// <param name="roleAlias">The role alias.</param>
    /// <param name="contentTypeKey">The doc-type key.</param>
    /// <param name="entries">The verb+state+scope tuples to persist.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    [Obsolete(
        "This overload performs NO concurrency check: a save through it can silently overwrite a change " +
        "another user saved after the caller read the permissions. It exists only so implementations " +
        "written against earlier releases keep compiling. New implementations must implement the " +
        "SaveEditorEntriesAsync overload that takes an 'expectedStamp' parameter and verify that stamp " +
        "atomically with the write - do not implement only this member and assume you are protected. " +
        "Callers should call the overload that takes 'expectedStamp'.")]
    Task SaveEditorEntriesAsync(
        Guid nodeKey,
        string roleAlias,
        Guid contentTypeKey,
        IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> entries,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves (replaces) the editor's entries for a (node, role, content-type) triple, refusing the
    /// write if the stored entries have changed since the caller read them. Empty list clears.
    /// Invalidates the cache. <b>This is the member to implement.</b>
    /// </summary>
    /// <remarks>
    /// This overload has a default implementation that forwards to the older, unstamped
    /// <c>SaveEditorEntriesAsync</c> and ignores <paramref name="expectedStamp"/>. It exists so an
    /// implementation written before concurrency checking existed keeps compiling - and, because it
    /// ignores the stamp, an implementation that relies on it performs <b>no</b> concurrency check.
    /// Override it and make the check atomically with the write to get the protection.
    /// </remarks>
    /// <param name="nodeKey">The node the entries apply to (or virtual root).</param>
    /// <param name="roleAlias">The role alias.</param>
    /// <param name="contentTypeKey">The doc-type key.</param>
    /// <param name="entries">The verb+state+scope tuples to persist.</param>
    /// <param name="expectedStamp">
    /// The stamp the caller read these entries under, or <see langword="null"/> to skip the
    /// concurrency check. The check is made atomically with the write.
    /// </param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <exception cref="Exceptions.DocTypePermissionConcurrencyException">
    /// <paramref name="expectedStamp"/> was supplied and no longer matches the stored entries.
    /// Nothing was written, no cache was invalidated and no notification was published.
    /// </exception>
    Task SaveEditorEntriesAsync(
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
        SaveEditorEntriesAsync(nodeKey, roleAlias, contentTypeKey, entries, cancellationToken);
#pragma warning restore CS0618

    /// <summary>
    /// Replaces the entries for several node, user group and document type triples in a single
    /// transaction, invalidating the affected caches once for the whole batch and publishing one
    /// change notification per triple.
    /// </summary>
    /// <remarks>
    /// All or nothing, for the same reason as <see cref="IAdvancedPermissionService.SaveManyAsync"/>:
    /// the editor changes several at once, and a partial write leaves a state nothing afterwards
    /// can read.
    /// </remarks>
    /// <param name="batch">
    /// The triple, replacement entries and expected stamp for each. A <see langword="null"/>
    /// expected stamp skips the concurrency check for that triple.
    /// </param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <exception cref="Exceptions.DocTypePermissionConcurrencyException">
    /// One or more triples supplied an expected stamp that no longer matches the stored entries.
    /// The check is made atomically with the write; nothing was written, no cache was invalidated
    /// and no notification was published.
    /// </exception>
    Task SaveManyAsync(
        IReadOnlyList<(Guid NodeKey, string RoleAlias, Guid ContentTypeKey, IReadOnlyList<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries, string? ExpectedStamp)> batch,
        CancellationToken cancellationToken = default);
}
