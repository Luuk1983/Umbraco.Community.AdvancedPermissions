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
    /// Replaces all entries for a (node, role, content-type) triple with the supplied entries.
    /// Passing an empty collection removes any existing entries for that triple.
    /// </summary>
    /// <param name="nodeKey">
    /// The node key. Use <c>AdvancedPermissionsConstants.VirtualRootNodeKey</c> for the virtual root row.
    /// </param>
    /// <param name="roleAlias">The role alias.</param>
    /// <param name="contentTypeKey">The doc-type key.</param>
    /// <param name="entries">The verb+state+scope tuples to persist.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    Task SaveAsync(
        Guid nodeKey,
        string roleAlias,
        Guid contentTypeKey,
        IEnumerable<(string Verb, PermissionState State, PermissionScope Scope)> entries,
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
}
