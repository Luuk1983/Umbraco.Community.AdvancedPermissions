using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Services;
using LP.Umbraco.AdvancedPermissions.Controllers.Models;
using LP.Umbraco.AdvancedPermissions.Core.Constants;
using LP.Umbraco.AdvancedPermissions.Core.Interfaces;
using LP.Umbraco.AdvancedPermissions.Core.Models;

namespace LP.Umbraco.AdvancedPermissions.Controllers;

/// <summary>
/// Provides CRUD operations for raw advanced security permission entries.
/// </summary>
/// <param name="permissionService">The advanced permission service.</param>
/// <param name="repository">The permission repository for batch queries.</param>
/// <param name="entityService">The Umbraco entity service for path resolution.</param>
[ApiVersion("1.0")]
public sealed class AdvancedPermissionsPermissionController(
    IAdvancedPermissionService permissionService,
    IAdvancedPermissionRepository repository,
    IEntityService entityService)
    : AdvancedPermissionsControllerBase
{
    /// <summary>
    /// Gets all stored permission entries for a specific node and role.
    /// </summary>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <param name="nodeKey">
    /// The content node key. Use <c>ffffffff-ffff-ffff-ffff-ffffffffffff</c> for virtual-root entries.
    /// </param>
    /// <param name="roleAlias">The role alias to filter by.</param>
    /// <returns>The stored entries for the given node and role.</returns>
    [HttpGet("permissions")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<IReadOnlyList<PermissionEntryResponseModel>>(StatusCodes.Status200OK)]
    [EndpointSummary("Gets permission entries for a node and role.")]
    public async Task<IActionResult> GetPermissions(
        CancellationToken cancellationToken,
        Guid nodeKey,
        string roleAlias)
    {
        var entries = await permissionService.GetEntriesAsync(nodeKey, roleAlias, cancellationToken);
        return Ok(entries.Select(MapEntry).ToList());
    }

    /// <summary>
    /// Gets all stored permission entries for a specific node across all roles.
    /// </summary>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <param name="nodeKey">
    /// The content node key. Use <c>ffffffff-ffff-ffff-ffff-ffffffffffff</c> for virtual-root entries.
    /// </param>
    /// <returns>All stored entries for the given node.</returns>
    [HttpGet("permissions/by-node")]
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
    /// <returns><see cref="StatusCodes.Status200OK"/> on success.</returns>
    [HttpPut("permissions")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [EndpointSummary("Saves (replaces) permission entries for a node and role.")]
    public async Task<IActionResult> SavePermissions(
        [FromBody] SavePermissionsRequestModel request,
        CancellationToken cancellationToken)
    {
        var mapped = new List<(string Verb, PermissionState State, PermissionScope Scope)>();

        foreach (var entry in request.Entries)
        {
            if (!Enum.TryParse<PermissionState>(entry.State, ignoreCase: true, out var state))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid state",
                    Detail = $"'{entry.State}' is not a valid permission state. Use 'Allow' or 'Deny'.",
                    Status = StatusCodes.Status400BadRequest,
                });
            }

            if (!Enum.TryParse<PermissionScope>(entry.Scope, ignoreCase: true, out var scope))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid scope",
                    Detail = $"'{entry.Scope}' is not a valid permission scope. Use 'ThisNodeOnly', 'ThisNodeAndDescendants', or 'DescendantsOnly'.",
                    Status = StatusCodes.Status400BadRequest,
                });
            }

            if (!AdvancedPermissionsConstants.AllVerbs.Contains(entry.Verb, StringComparer.Ordinal))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid verb",
                    Detail = $"'{entry.Verb}' is not a recognized permission verb.",
                    Status = StatusCodes.Status400BadRequest,
                });
            }

            mapped.Add((entry.Verb, state, scope));
        }

        await permissionService.SaveEntriesAsync(request.NodeKey, request.RoleAlias, mapped, cancellationToken);
        return Ok();
    }

    /// <summary>
    /// Gets the inheritance path from virtual root to a target node, along with all stored
    /// permission entries for a specific verb at every node in the path (across all roles).
    /// Used by the Access Viewer reasoning dialog to show where permissions come from.
    /// </summary>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <param name="nodeKey">The content node key of the target node.</param>
    /// <param name="verb">The permission verb to filter entries by.</param>
    /// <returns>The path and filtered entries.</returns>
    [HttpGet("permissions/for-path")]
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
    [HttpDelete("permissions")]
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
