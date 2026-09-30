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

namespace Umbraco.Community.AdvancedPermissions.Controllers;

/// <summary>
/// Provides CRUD operations for raw advanced security permission entries.
/// </summary>
/// <remarks>
/// Gated on Users-section access: it exposes the mutating (PUT/DELETE) endpoints, which are a
/// privilege-escalation primitive if reachable by any authenticated backoffice user.
/// </remarks>
/// <param name="permissionService">The advanced permission service.</param>
/// <param name="repository">The permission repository for batch queries.</param>
/// <param name="entityService">The Umbraco entity service for path resolution.</param>
[ApiVersion("1.0")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessUsers)]
public sealed class AdvancedPermissionsPermissionController(
    IAdvancedPermissionService permissionService,
    IAdvancedPermissionRepository repository,
    IEntityService entityService)
    : AdvancedPermissionsControllerBase
{
    /// <summary>
    /// Gets all stored permission entries for a specific node and role.
    /// </summary>
    /// <remarks>
    /// The response carries an <c>ETag</c> header holding the concurrency stamp of the returned
    /// entries. A client that later saves back what it loaded here sends that stamp with the
    /// write; the server refuses the save if the stored entries have moved since, rather than
    /// silently overwriting a change nobody has seen yet.
    /// </remarks>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <param name="nodeKey">
    /// The content node key. Use <c>ffffffff-ffff-ffff-ffff-ffffffffffff</c> for virtual-root entries.
    /// </param>
    /// <param name="roleAlias">The role alias to filter by.</param>
    /// <returns>The stored entries for the given node and role.</returns>
    [HttpGet("permissions", Name = "GetPermissions")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<IReadOnlyList<PermissionEntryResponseModel>>(StatusCodes.Status200OK)]
    [EndpointSummary("Gets permission entries for a node and role.")]
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
    /// Gets all stored permission entries for a specific node across all roles.
    /// </summary>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <param name="nodeKey">
    /// The content node key. Use <c>ffffffff-ffff-ffff-ffff-ffffffffffff</c> for virtual-root entries.
    /// </param>
    /// <returns>All stored entries for the given node.</returns>
    [HttpGet("permissions/by-node", Name = "GetPermissionsByNode")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<IReadOnlyList<PermissionEntryResponseModel>>(StatusCodes.Status200OK)]
    [EndpointSummary("Gets all permission entries for a node (all roles).")]
    public async Task<IActionResult> GetPermissionsByNode(
        CancellationToken cancellationToken,
        Guid nodeKey)
    {
        var entries = await permissionService.GetEntriesByNodeAsync(nodeKey, cancellationToken);
        return Ok(entries.Select(MapEntry).ToList());
    }

    /// <summary>
    /// Saves (replaces) permission entries for a node and role.
    /// Pass an empty <c>Entries</c> list to remove all entries and revert to inherited behavior.
    /// </summary>
    /// <param name="request">The entries to save.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>
    /// <see cref="StatusCodes.Status200OK"/> on success, or <see cref="StatusCodes.Status409Conflict"/>
    /// when <see cref="SavePermissionsRequestModel.ExpectedStamp"/> no longer matches what is stored.
    /// </returns>
    [HttpPut("permissions", Name = "PutPermissions")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<BatchSaveConflictResponseModel>(StatusCodes.Status409Conflict)]
    [EndpointSummary("Saves (replaces) permission entries for a node and role.")]
    public Task<IActionResult> SavePermissions(
        [FromBody] SavePermissionsRequestModel request,
        CancellationToken cancellationToken) =>
        PermissionSaveRequests.SaveNodeAsync(
            permissionService,
            request,
            AdvancedPermissionsConstants.AllVerbs,
            "permission verb",
            MapEntry,
            cancellationToken);

    /// <summary>
    /// Replaces permission entries for several nodes and user groups at once, refusing the
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
    [HttpPut("permissions/batch", Name = "PutPermissionsBatch")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<IReadOnlyList<BatchSavedStamp>>(StatusCodes.Status200OK)]
    [ProducesResponseType<BatchSaveConflictResponseModel>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [EndpointSummary("Saves permission entries for several nodes at once, all or nothing.")]
    public Task<IActionResult> BatchSavePermissions(
        [FromBody] BatchSavePermissionsRequestModel request,
        CancellationToken cancellationToken) =>
        PermissionSaveRequests.SaveNodeBatchAsync(
            permissionService,
            request,
            AdvancedPermissionsConstants.AllVerbs,
            "permission verb",
            MapEntry,
            cancellationToken);

    /// <summary>
    /// Gets the inheritance path from virtual root to a target node, along with all stored
    /// permission entries for a specific verb at every node in the path (across all roles).
    /// Used by the Access Viewer reasoning dialog to show where permissions come from.
    /// </summary>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <param name="nodeKey">The content node key of the target node.</param>
    /// <param name="verb">The permission verb to filter entries by.</param>
    /// <returns>The path and filtered entries.</returns>
    [HttpGet("permissions/for-path", Name = "GetPermissionsForPath")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<PathEntriesResponseModel>(StatusCodes.Status200OK)]
    [EndpointSummary("Gets the inheritance path and stored entries for a verb along that path.")]
    public async Task<IActionResult> GetPermissionsForPath(
        CancellationToken cancellationToken,
        Guid nodeKey,
        string verb)
    {
        // Build the content path from root to target
        var contentPath = BuildPathFromRoot(nodeKey, entityService);

        // Build path models with names and icons
        var pathNodes = new List<PathNodeModel>();

        // Virtual root is always first
        pathNodes.Add(new PathNodeModel(
            AdvancedPermissionsConstants.VirtualRootNodeKey,
            AdvancedPermissionsConstants.EveryoneRoleDisplayName,
            "icon-globe"));

        if (contentPath.Count > 0)
        {
            // Bulk-fetch entity details for names and icons
            var entities = entityService
                .GetAll(UmbracoObjectTypes.Document, contentPath.Select(k => k).ToArray())
                .ToDictionary(e => e.Key);

            foreach (var key in contentPath)
            {
                if (entities.TryGetValue(key, out var entity))
                {
                    var icon = entity is IContentEntitySlim contentSlim ? contentSlim.ContentTypeIcon : null;
                    pathNodes.Add(new PathNodeModel(key, entity.Name ?? string.Empty, icon));
                }
            }
        }

        // Fetch all entries for the path nodes in a single query
        var allNodeKeys = pathNodes.Select(p => p.Key);
        var allEntries = await repository.GetByNodesAsync(allNodeKeys, cancellationToken);

        // Filter to the requested verb only
        var filteredEntries = allEntries
            .Where(e => string.Equals(e.Verb, verb, StringComparison.Ordinal))
            .Select(MapEntry)
            .ToList();

        return Ok(new PathEntriesResponseModel(pathNodes, filteredEntries));
    }

    /// <summary>
    /// Removes a specific permission entry, reverting it to the inherited state.
    /// </summary>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <param name="nodeKey">
    /// The content node key. Use <c>ffffffff-ffff-ffff-ffff-ffffffffffff</c> for virtual-root entries.
    /// </param>
    /// <param name="roleAlias">The role alias.</param>
    /// <param name="verb">The permission verb to remove.</param>
    /// <returns><see cref="StatusCodes.Status200OK"/> on success.</returns>
    [HttpDelete("permissions", Name = "DeletePermission")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [EndpointSummary("Removes a specific permission entry (reverts to inherit).")]
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
