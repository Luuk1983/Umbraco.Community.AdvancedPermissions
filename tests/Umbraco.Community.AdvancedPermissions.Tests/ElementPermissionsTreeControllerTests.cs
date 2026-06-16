using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Persistence.Querying;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.Controllers;
using Umbraco.Community.AdvancedPermissions.Controllers.Models;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests the tree-building contract of <see cref="ElementPermissionsTreeController"/>. The library tree
/// mixes element folders (<see cref="UmbracoObjectTypes.ElementContainer"/>) and elements
/// (<see cref="UmbracoObjectTypes.Element"/>); both kinds must appear in the editor. The single-object-type
/// <c>GetChildren(parentKey, objectType)</c> overload resolves the parent key <em>using the child object
/// type</em>, so requesting elements under a folder parent silently returns nothing — these tests pin the
/// controller to the multi-object-type paged overload Umbraco's own element tree uses.
/// </summary>
public sealed class ElementPermissionsTreeControllerTests
{
    private readonly IElementNodePermissionService _permissionService =
        Substitute.For<IElementNodePermissionService>();

    private readonly IEntityService _entityService = Substitute.For<IEntityService>();
    private readonly ElementPermissionsTreeController _sut;

    /// <summary>
    /// Initializes a new instance of the <see cref="ElementPermissionsTreeControllerTests"/> class.
    /// </summary>
    public ElementPermissionsTreeControllerTests()
    {
        _sut = new ElementPermissionsTreeController(_permissionService, _entityService);
        _permissionService
            .GetEntriesByNodesAndRoleAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<AdvancedPermissionEntry>());
    }

    /// <summary>
    /// A folder's children include nested elements, not just sub-folders. The element must be returned
    /// (this is the regression the single-type <c>GetChildren</c> overload could not satisfy).
    /// </summary>
    [Fact]
    public async Task GetChildren_IncludesNestedElementsAndFolders_FoldersFirst()
    {
        var folderKey = Guid.NewGuid();
        var elementKey = Guid.NewGuid();
        var folder = StubEntity(10, folderKey, "Sub folder", Constants.ObjectTypes.ElementContainer, hasChildren: true);
        var element = StubEntity(11, elementKey, "Element 1", Constants.ObjectTypes.Element, hasChildren: false);

        StubPagedChildren(folder, element);

        var result = await _sut.GetChildren(CancellationToken.None, Guid.NewGuid(), "$everyone");

        var nodes = AssertNodes(result);
        Assert.Equal(2, nodes.Count);
        // Folders are the inheritance backbone — they come first.
        Assert.True(nodes[0].IsFolder);
        Assert.Equal(folderKey, nodes[0].Key);
        // The element must be present and flagged as a leaf.
        Assert.False(nodes[1].IsFolder);
        Assert.Equal(elementKey, nodes[1].Key);
    }

    /// <summary>
    /// Root-level library nodes also mix folders and elements; both must be returned.
    /// </summary>
    [Fact]
    public async Task GetRoot_IncludesElementsAndFolders()
    {
        var folderKey = Guid.NewGuid();
        var elementKey = Guid.NewGuid();
        var folder = StubEntity(20, folderKey, "Root folder", Constants.ObjectTypes.ElementContainer, hasChildren: false);
        var element = StubEntity(21, elementKey, "Root element", Constants.ObjectTypes.Element, hasChildren: false);

        StubPagedChildren(folder, element);

        var result = await _sut.GetRoot(CancellationToken.None, "$everyone");

        var nodes = AssertNodes(result);
        Assert.Contains(nodes, n => n.Key == folderKey && n.IsFolder);
        Assert.Contains(nodes, n => n.Key == elementKey && !n.IsFolder);
    }

    /// <summary>
    /// Stubs the multi-object-type paged-children overload to return the supplied entities, mirroring
    /// what Umbraco's element tree relies on. The <c>out</c> total is set to the entity count.
    /// </summary>
    /// <param name="entities">The entities to return for any parent key.</param>
    private void StubPagedChildren(params IEntitySlim[] entities)
    {
        long total;
        _entityService
            .GetPagedChildren(
                Arg.Any<Guid?>(),
                Arg.Any<IEnumerable<UmbracoObjectTypes>>(),
                Arg.Any<IEnumerable<UmbracoObjectTypes>>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<bool>(),
                out total,
                Arg.Any<IQuery<IUmbracoEntity>?>(),
                Arg.Any<Ordering?>())
            .Returns(ci =>
            {
                ci[6] = (long)entities.Length;
                return entities;
            });
    }

    /// <summary>
    /// Asserts the action result is a 200 OK carrying a list of tree node models and returns it.
    /// </summary>
    /// <param name="result">The action result to unwrap.</param>
    /// <returns>The tree node models.</returns>
    private static IReadOnlyList<ElementTreeNodeResponseModel> AssertNodes(IActionResult result)
    {
        var ok = Assert.IsType<OkObjectResult>(result);
        return Assert.IsAssignableFrom<IReadOnlyList<ElementTreeNodeResponseModel>>(ok.Value);
    }

    /// <summary>
    /// Builds a stub entity with the supplied id, key, name, object type and child flag.
    /// </summary>
    /// <param name="id">The integer id.</param>
    /// <param name="key">The unique key.</param>
    /// <param name="name">The display name.</param>
    /// <param name="objectType">The node object-type GUID (element or element container).</param>
    /// <param name="hasChildren">Whether the entity reports having children.</param>
    /// <returns>The stub entity.</returns>
    private static IEntitySlim StubEntity(int id, Guid key, string name, Guid objectType, bool hasChildren)
    {
        var entity = Substitute.For<IEntitySlim>();
        entity.Id.Returns(id);
        entity.Key.Returns(key);
        entity.Name.Returns(name);
        entity.NodeObjectType.Returns(objectType);
        entity.HasChildren.Returns(hasChildren);
        return entity;
    }
}
