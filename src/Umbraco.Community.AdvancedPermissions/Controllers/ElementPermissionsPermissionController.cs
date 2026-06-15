using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Services;
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
/// <param name="permissionService">The element advanced permission service.</param>
/// <param name="repository">The element permission repository for batch queries.</param>
/// <param name="entityService">The Umbraco entity service for path resolution.</param>
[ApiVersion("1.0")]
public sealed class ElementPermissionsPermissionController(
    IElementNodePermissionService permissionService,
    IElementPermissionRepository repository,
    IEntityService entityService)
    : AdvancedPermissionsControllerBase
{
    /// <summary>
    /// Gets all stored element permission entries for a specific node and role.
    /// </summary>
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
        return Ok(entries.Select(MapEntry).ToList());
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
    /// Saves (replaces) element permission entries for a node and role. Pass an empty <c>Entries</c> list
    /// to remove all entries and revert to inherited behaviour.
    /// </summary>
    /// <param name="request">The entries to save.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns><see cref="StatusCodes.Status200OK"/> on success.</returns>
    [HttpPut("element/permissions", Name = "PutElementPermissions")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [EndpointSummary("Saves (replaces) element permission entries for a node and role.")]
    public async Task<IActionResult> SavePermissions(
        [FromBody] SavePermissionsRequestModel request,
        CancellationToken cancellationToken)
    {
        var mapped = new List<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)>();

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

            if (!AdvancedPermissionsConstants.ElementVerbs.Contains(entry.Verb, StringComparer.Ordinal))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid verb",
                    Detail = $"'{entry.Verb}' is not a recognized element permission verb.",
                    Status = StatusCodes.Status400BadRequest,
                });
            }

            mapped.Add((entry.Verb, state, scope, entry.IsPriorityOverride));
        }

        await permissionService.SaveEntriesAsync(request.NodeKey, request.RoleAlias, mapped, cancellationToken);
        return Ok();
    }

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
            // Resolve names/icons across both object types (path may include folders and elements).
            var entities = entityService.GetAll(UmbracoObjectTypes.Element, keys)
                .Concat(entityService.GetAll(UmbracoObjectTypes.ElementContainer, keys))
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
