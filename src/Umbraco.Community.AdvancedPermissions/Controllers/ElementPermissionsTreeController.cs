using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.Controllers.Models;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;

namespace Umbraco.Community.AdvancedPermissions.Controllers;

/// <summary>
/// Provides library tree data (elements and folders) with per-node permission summaries for a given
/// role. Used by the Library permissions editor to render the permission grid. The element analogue of
/// <see cref="AdvancedPermissionsTreeController"/>.
/// </summary>
/// <param name="permissionService">The element advanced permission service.</param>
/// <param name="entityService">The Umbraco entity service.</param>
[ApiVersion("1.0")]
public sealed class ElementPermissionsTreeController(
    IElementNodePermissionService permissionService,
    IEntityService entityService)
    : AdvancedPermissionsControllerBase
{
    /// <summary>
    /// Gets the root library nodes (folders and root-level elements) with stored permission entries for the role.
    /// </summary>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <param name="roleAlias">The role alias to load entries for.</param>
    /// <returns>Root library nodes, each with the stored permission entries for the role.</returns>
    [HttpGet("element/tree/root", Name = "GetElementRoot")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<IReadOnlyList<ElementTreeNodeResponseModel>>(StatusCodes.Status200OK)]
    [EndpointSummary("Gets root library nodes with permission entries for a role.")]
    public async Task<IActionResult> GetRoot(
        CancellationToken cancellationToken,
        string roleAlias)
    {
        var folders = entityService.GetRootEntities(UmbracoObjectTypes.ElementContainer);
        var elements = entityService.GetRootEntities(UmbracoObjectTypes.Element);
        return Ok(await MapNodesWithEntriesAsync(folders, elements, roleAlias, cancellationToken));
    }

    /// <summary>
    /// Gets the child library nodes of a given folder with stored permission entries for the role.
    /// </summary>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <param name="parentKey">The key of the parent folder.</param>
    /// <param name="roleAlias">The role alias to load entries for.</param>
    /// <returns>Child library nodes, each with the stored permission entries for the role.</returns>
    [HttpGet("element/tree/children", Name = "GetElementChildren")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<IReadOnlyList<ElementTreeNodeResponseModel>>(StatusCodes.Status200OK)]
    [EndpointSummary("Gets child library nodes with permission entries for a role.")]
    public async Task<IActionResult> GetChildren(
        CancellationToken cancellationToken,
        Guid parentKey,
        string roleAlias)
    {
        var folders = entityService.GetChildren(parentKey, UmbracoObjectTypes.ElementContainer);
        var elements = entityService.GetChildren(parentKey, UmbracoObjectTypes.Element);
        return Ok(await MapNodesWithEntriesAsync(folders, elements, roleAlias, cancellationToken));
    }

    /// <summary>
    /// Maps folder and element entities to tree node response models, loading their permission entries in
    /// a single batch query to avoid N+1 round trips. Folders are listed before elements.
    /// </summary>
    /// <param name="folders">The folder (container) entities.</param>
    /// <param name="elements">The element entities.</param>
    /// <param name="roleAlias">The role alias to load entries for.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The mapped tree node models with entries, folders first.</returns>
    private async Task<IReadOnlyList<ElementTreeNodeResponseModel>> MapNodesWithEntriesAsync(
        IEnumerable<IEntitySlim> folders,
        IEnumerable<IEntitySlim> elements,
        string roleAlias,
        CancellationToken cancellationToken)
    {
        var folderList = folders.ToList();
        var elementList = elements.ToList();

        var allKeys = folderList.Select(n => n.Key).Concat(elementList.Select(n => n.Key));
        var allEntries = await permissionService.GetEntriesByNodesAndRoleAsync(allKeys, roleAlias, cancellationToken);
        var entriesByNode = allEntries
            .GroupBy(e => e.NodeKey)
            .ToDictionary(g => g.Key, g => g.Select(MapEntry).ToList());

        var result = new List<ElementTreeNodeResponseModel>(folderList.Count + elementList.Count);

        // Folders first (the inheritance backbone), then leaf elements.
        foreach (var folder in folderList)
        {
            entriesByNode.TryGetValue(folder.Key, out var entries);
            result.Add(new ElementTreeNodeResponseModel(
                folder.Key,
                folder.Name ?? string.Empty,
                "icon-folder",
                folder.HasChildren,
                IsFolder: true,
                entries ?? []));
        }

        foreach (var element in elementList)
        {
            var icon = element is IContentEntitySlim contentSlim ? contentSlim.ContentTypeIcon : null;
            entriesByNode.TryGetValue(element.Key, out var entries);
            result.Add(new ElementTreeNodeResponseModel(
                element.Key,
                element.Name ?? string.Empty,
                icon,
                element.HasChildren,
                IsFolder: false,
                entries ?? []));
        }

        return result;
    }
}
