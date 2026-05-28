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
    /// Saves (replaces) the editor's entries for a (node, role, content-type) triple. Empty list clears.
    /// Invalidates the cache.
    /// </summary>
    /// <param name="nodeKey">The node the entries apply to (or virtual root).</param>
    /// <param name="roleAlias">The role alias.</param>
    /// <param name="contentTypeKey">The doc-type key.</param>
    /// <param name="entries">The verb+state+scope tuples to persist.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    Task SaveEditorEntriesAsync(
        Guid nodeKey,
        string roleAlias,
        Guid contentTypeKey,
        IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> entries,
        CancellationToken cancellationToken = default);
}
