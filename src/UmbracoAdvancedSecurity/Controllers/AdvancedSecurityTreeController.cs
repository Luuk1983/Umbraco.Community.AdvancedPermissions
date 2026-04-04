using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Services;
using UmbracoAdvancedSecurity.Controllers.Models;
using UmbracoAdvancedSecurity.Core.Interfaces;

namespace UmbracoAdvancedSecurity.Controllers;

/// <summary>
/// Provides content tree data with per-node permission summaries for a given role.
/// Used by the Security Editor to render the permission grid.
/// </summary>
/// <param name="permissionService">The advanced permission service.</param>
/// <param name="entityService">The Umbraco entity service.</param>
[ApiVersion("1.0")]
public sealed class AdvancedSecurityTreeController(
    IAdvancedPermissionService permissionService,
    IEntityService entityService)
    : AdvancedSecurityControllerBase
{
    /// <summary>
    /// Gets the root content nodes with stored permission entries for the given role.
    /// </summary>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <param name="roleAlias">The role alias to load entries for.</param>
    /// <returns>Root content nodes, each with the stored permission entries for the role.</returns>
    [HttpGet("tree/root")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<IReadOnlyList<TreeNodeResponseModel>>(StatusCodes.Status200OK)]
    [EndpointSummary("Gets root content nodes with permission entries for a role.")]
    public async Task<IActionResult> GetRoot(
        CancellationToken cancellationToken,
        string roleAlias)
    {
        var roots = entityService.GetRootEntities(UmbracoObjectTypes.Document);
        return Ok(await MapNodesWithEntriesAsync(roots, roleAlias, cancellationToken));
    }

    /// <summary>
    /// Gets the child content nodes of a given parent with stored permission entries for the given role.
    /// </summary>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <param name="parentKey">The key of the parent content node.</param>
    /// <param name="roleAlias">The role alias to load entries for.</param>
    /// <returns>Child content nodes, each with the stored permission entries for the role.</returns>
    [HttpGet("tree/children")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<IReadOnlyList<TreeNodeResponseModel>>(StatusCodes.Status200OK)]
    [EndpointSummary("Gets child content nodes with permission entries for a role.")]
    public async Task<IActionResult> GetChildren(
        CancellationToken cancellationToken,
        Guid parentKey,
        string roleAlias)
    {
        var children = entityService.GetChildren(parentKey, UmbracoObjectTypes.Document);
        return Ok(await MapNodesWithEntriesAsync(children, roleAlias, cancellationToken));
    }

    /// <summary>
    /// Maps a collection of entity slims to tree node response models, loading their permission entries.
    /// </summary>
    /// <param name="nodes">The content node entities to map.</param>
    /// <param name="roleAlias">The role alias to load entries for.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The mapped tree node models with entries.</returns>
    private async Task<IReadOnlyList<TreeNodeResponseModel>> MapNodesWithEntriesAsync(
        IEnumerable<IEntitySlim> nodes,
        string roleAlias,
        CancellationToken cancellationToken)
    {
        var result = new List<TreeNodeResponseModel>();

        foreach (var node in nodes)
        {
            var entries = await permissionService.GetEntriesAsync(node.Key, roleAlias, cancellationToken);
            var icon = node is IContentEntitySlim contentSlim ? contentSlim.ContentTypeIcon : null;
            result.Add(new TreeNodeResponseModel(
                node.Key,
                node.Name ?? string.Empty,
                icon,
                node.HasChildren,
                entries.Select(MapEntry).ToList()));
        }

        return result;
    }
}
