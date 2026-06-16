using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core;
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
    /// The object types that make up the library tree: element folders (the inheritance backbone) and
    /// the elements they contain. Used as both the parent and child object-type set when paging children.
    /// </summary>
    private static readonly UmbracoObjectTypes[] LibraryObjectTypes =
        [UmbracoObjectTypes.ElementContainer, UmbracoObjectTypes.Element];

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
        var nodes = GetLibraryChildren(Constants.System.RootKey);
        return Ok(await MapNodesWithEntriesAsync(nodes, roleAlias, cancellationToken));
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
        var nodes = GetLibraryChildren(parentKey);
        return Ok(await MapNodesWithEntriesAsync(nodes, roleAlias, cancellationToken));
    }

    /// <summary>
    /// Gets the immediate library children (folders and elements) of a parent, ordered folders-first.
    /// </summary>
    /// <remarks>
    /// Uses the multi-object-type paged overload rather than the single-type
    /// <c>GetChildren(parentKey, objectType)</c>: that overload resolves the parent key <em>using the
    /// child object type</em>, so asking for <see cref="UmbracoObjectTypes.Element"/> children under a
    /// folder parent (an <see cref="UmbracoObjectTypes.ElementContainer"/>) resolves nothing and returns
    /// an empty set. Querying both object types together — the same approach Umbraco's own element tree
    /// uses — returns folders and elements. Elements are leaves and folders are the only nesting level, so
    /// a single unbounded page mirrors the editor's load-everything behaviour.
    /// </remarks>
    /// <param name="parentKey">The parent folder key, or the system root key for the library root.</param>
    /// <returns>The child entities, element folders before elements and then ordered by name.</returns>
    private IReadOnlyList<IEntitySlim> GetLibraryChildren(Guid? parentKey)
    {
        var children = entityService.GetPagedChildren(
            parentKey,
            LibraryObjectTypes,
            LibraryObjectTypes,
            0,
            int.MaxValue,
            trashed: false,
            out _);

        return children
            .OrderByDescending(e => e.NodeObjectType == Constants.ObjectTypes.ElementContainer)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Maps library entities to tree node response models, loading their permission entries in a single
    /// batch query to avoid N+1 round trips. Each entity's folder/element kind is derived from its node
    /// object type.
    /// </summary>
    /// <param name="nodes">The library entities (folders and elements), pre-ordered for display.</param>
    /// <param name="roleAlias">The role alias to load entries for.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The mapped tree node models with entries.</returns>
    private async Task<IReadOnlyList<ElementTreeNodeResponseModel>> MapNodesWithEntriesAsync(
        IReadOnlyList<IEntitySlim> nodes,
        string roleAlias,
        CancellationToken cancellationToken)
    {
        var allEntries = await permissionService.GetEntriesByNodesAndRoleAsync(
            nodes.Select(n => n.Key), roleAlias, cancellationToken);
        var entriesByNode = allEntries
            .GroupBy(e => e.NodeKey)
            .ToDictionary(g => g.Key, g => g.Select(MapEntry).ToList());

        var result = new List<ElementTreeNodeResponseModel>(nodes.Count);
        foreach (var node in nodes)
        {
            var isFolder = node.NodeObjectType == Constants.ObjectTypes.ElementContainer;
            var icon = isFolder
                ? "icon-folder"
                : node is IContentEntitySlim contentSlim ? contentSlim.ContentTypeIcon : null;

            entriesByNode.TryGetValue(node.Key, out var entries);
            result.Add(new ElementTreeNodeResponseModel(
                node.Key,
                node.Name ?? string.Empty,
                icon,
                node.HasChildren,
                IsFolder: isFolder,
                entries ?? []));
        }

        return result;
    }
}
