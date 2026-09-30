using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Community.AdvancedPermissions.Controllers.Models;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;
using Umbraco.Community.AdvancedPermissions.Services;

namespace Umbraco.Community.AdvancedPermissions.Controllers;

/// <summary>
/// Provides CRUD operations for raw library element permission entries. The element analogue of
/// <see cref="AdvancedPermissionsPermissionController"/>; entries are validated against the canonical
/// element verb set (<see cref="AdvancedPermissionsConstants.ElementVerbs"/>).
/// </summary>
/// <remarks>
/// Gated on Users-section access: it exposes the mutating (PUT/DELETE) element endpoints, which are a
/// privilege-escalation primitive if reachable by any authenticated backoffice user.
/// </remarks>
/// <param name="permissionService">The element advanced permission service.</param>
/// <param name="repository">The element permission repository for batch queries.</param>
/// <param name="entityService">The Umbraco entity service for path resolution.</param>
[ApiVersion("1.0")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessUsers)]
public sealed class ElementPermissionsPermissionController(
    IElementNodePermissionService permissionService,
    IElementPermissionRepository repository,
    IEntityService entityService)
    : AdvancedPermissionsControllerBase
{
    /// <summary>
    /// Gets all stored element permission entries for a specific node and role.
    /// </summary>
    /// <remarks>
    /// The response carries an <c>ETag</c> header holding the concurrency stamp of the returned
    /// entries, exactly as the content endpoint does. A client that later saves back what it loaded
    /// here sends that stamp with the write; the server refuses the save if the stored entries have
    /// moved since, rather than silently overwriting a change nobody has seen yet.
    /// </remarks>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <param name="nodeKey">The element/folder key, or the virtual-root key for defaults.</param>
    /// <param name="roleAlias">The role alias to filter by.</param>
    /// <returns>The stored entries for the given node and role.</returns>
    [HttpGet("element/permissions", Name = "GetElementPermissions")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<IReadOnlyList<PermissionEntryResponseModel>>(StatusCodes.Status200OK)]
    [EndpointSummary("Gets element permission entries for a node and role.")]
    public async Task<IActionResult> GetPermissions(
        CancellationToken cancellationToken,
        Guid nodeKey,
        string roleAlias)
    {
        var entries = await permissionService.GetEntriesAsync(nodeKey, roleAlias, cancellationToken);
        var models = entries.Select(MapEntry).ToList();

        // Computed from the mapped models — exactly what the body carries — so the stamp always
        // describes what the client actually received.
        Response.Headers.ETag = $"\"{models.ComputeFromResponse()}\"";
        return Ok(models);
    }

    /// <summary>
    /// Gets all stored element permission entries for a specific node across all roles.
    /// </summary>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <param name="nodeKey">The element/folder key, or the virtual-root key for defaults.</param>
    /// <returns>All stored entries for the given node.</returns>
    [HttpGet("element/permissions/by-node", Name = "GetElementPermissionsByNode")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<IReadOnlyList<PermissionEntryResponseModel>>(StatusCodes.Status200OK)]
    [EndpointSummary("Gets all element permission entries for a node (all roles).")]
    public async Task<IActionResult> GetPermissionsByNode(
        CancellationToken cancellationToken,
        Guid nodeKey)
    {
        var entries = await permissionService.GetEntriesByNodeAsync(nodeKey, cancellationToken);
        return Ok(entries.Select(MapEntry).ToList());
    }

    /// <summary>
    /// Saves (replaces) element permission entries for a node and role.
    /// Pass an empty <c>Entries</c> list to remove all entries and revert to inherited behavior.
    /// </summary>
    /// <param name="request">The entries to save.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>
    /// <see cref="StatusCodes.Status200OK"/> on success, or <see cref="StatusCodes.Status409Conflict"/>
    /// when <see cref="SavePermissionsRequestModel.ExpectedStamp"/> no longer matches what is stored.
    /// </returns>
    [HttpPut("element/permissions", Name = "PutElementPermissions")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<BatchSaveConflictResponseModel>(StatusCodes.Status409Conflict)]
    [EndpointSummary("Saves (replaces) element permission entries for a node and role.")]
    public Task<IActionResult> SavePermissions(
        [FromBody] SavePermissionsRequestModel request,
        CancellationToken cancellationToken) =>
        PermissionSaveRequests.SaveNodeAsync(
            permissionService,
            request,
            AdvancedPermissionsConstants.ElementVerbs,
            "element permission verb",
            MapEntry,
            cancellationToken);

    /// <summary>
    /// Replaces element permission entries for several nodes and user groups at once, refusing the
    /// whole batch if any pair has changed since the client read it.
    /// </summary>
    /// <remarks>
    /// All or nothing, deliberately. The editors change several nodes before saving, and a partial
    /// write would leave a state nothing afterwards could interpret - not the client's, not the
    /// server's, and not the next person's. The stamp check is made by the write itself, inside its
    /// transaction, never in this controller.
    /// </remarks>
    /// <param name="request">The pairs to write, each with the stamp the client read.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>
    /// <see cref="StatusCodes.Status200OK"/> with the new stamp per pair, or
    /// <see cref="StatusCodes.Status409Conflict"/> naming the pairs that moved.
    /// </returns>
    [HttpPut("element/permissions/batch", Name = "PutElementPermissionsBatch")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<IReadOnlyList<BatchSavedStamp>>(StatusCodes.Status200OK)]
    [ProducesResponseType<BatchSaveConflictResponseModel>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [EndpointSummary("Saves element permission entries for several nodes at once, all or nothing.")]
    public Task<IActionResult> BatchSavePermissions(
        [FromBody] BatchSavePermissionsRequestModel request,
        CancellationToken cancellationToken) =>
        PermissionSaveRequests.SaveNodeBatchAsync(
            permissionService,
            request,
            AdvancedPermissionsConstants.ElementVerbs,
            "element permission verb",
            MapEntry,
            cancellationToken);

    /// <summary>
    /// Gets the inheritance path from virtual root to a target node, along with all stored element
    /// permission entries for a specific verb at every node in the path (across all roles). Powers the
    /// access viewer's reasoning dialog.
    /// </summary>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <param name="nodeKey">The element/folder key of the target node.</param>
    /// <param name="verb">The canonical element verb to filter entries by.</param>
    /// <returns>The path and filtered entries.</returns>
    [HttpGet("element/permissions/for-path", Name = "GetElementPermissionsForPath")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<PathEntriesResponseModel>(StatusCodes.Status200OK)]
    [EndpointSummary("Gets the inheritance path and stored element entries for a verb along that path.")]
    public async Task<IActionResult> GetPermissionsForPath(
        CancellationToken cancellationToken,
        Guid nodeKey,
        string verb)
    {
        var path = ElementTreePathResolver.BuildPathFromRoot(entityService, nodeKey);

        var pathNodes = new List<PathNodeModel>
        {
            new(
                AdvancedPermissionsConstants.VirtualRootNodeKey,
                AdvancedPermissionsConstants.EveryoneRoleDisplayName,
                "icon-globe"),
        };

        if (path.Count > 0)
        {
            var keys = path.ToArray();
            // Resolve names/icons across both object types in one call. The multi-object-type overload
            // queries by object-type GUID; the single-type GetAll(ElementContainer, keys) overload throws
            // because element containers have no CLR entity type mapping.
            var entities = entityService
                .GetAll(new[] { UmbracoObjectTypes.Element, UmbracoObjectTypes.ElementContainer }, keys)
                .ToDictionary(e => e.Key);

            foreach (var key in path)
            {
                if (entities.TryGetValue(key, out var entity))
                {
                    var icon = entity is IContentEntitySlim contentSlim ? contentSlim.ContentTypeIcon : "icon-folder";
                    pathNodes.Add(new PathNodeModel(key, entity.Name ?? string.Empty, icon));
                }
            }
        }

        var allEntries = await repository.GetByNodesAsync(pathNodes.Select(p => p.Key), cancellationToken);
        var filteredEntries = allEntries
            .Where(e => string.Equals(e.Verb, verb, StringComparison.Ordinal))
            .Select(MapEntry)
            .ToList();

        return Ok(new PathEntriesResponseModel(pathNodes, filteredEntries));
    }

    /// <summary>
    /// Removes a specific element permission entry, reverting it to the inherited state.
    /// </summary>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <param name="nodeKey">The element/folder key, or the virtual-root key for defaults.</param>
    /// <param name="roleAlias">The role alias.</param>
    /// <param name="verb">The canonical element verb to remove.</param>
    /// <returns><see cref="StatusCodes.Status200OK"/> on success.</returns>
    [HttpDelete("element/permissions", Name = "DeleteElementPermission")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [EndpointSummary("Removes a specific element permission entry (reverts to inherit).")]
    public async Task<IActionResult> DeletePermission(
        CancellationToken cancellationToken,
        Guid nodeKey,
        string roleAlias,
        string verb)
    {
        await permissionService.DeleteEntryAsync(nodeKey, roleAlias, verb, cancellationToken);
        return Ok();
    }
}
