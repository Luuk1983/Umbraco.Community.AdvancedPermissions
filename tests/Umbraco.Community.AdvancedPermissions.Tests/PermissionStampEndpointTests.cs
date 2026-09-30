using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Persistence.Querying;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.Controllers;
using Umbraco.Community.AdvancedPermissions.Controllers.Models;
using Umbraco.Community.AdvancedPermissions.Core.Concurrency;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests that the stamp a read endpoint hands out is the stamp a save will be checked against, for
/// all three permission families — content, library elements and document types.
/// </summary>
/// <remarks>
/// The invariant is that a stamp is computed from exactly the entries being returned. If it is not,
/// a client that saves back precisely what it was given is told it conflicts with somebody else's
/// change that never happened, and learns to click through the conflict dialog.
/// </remarks>
public sealed class PermissionStampEndpointTests
{
    /// <summary>
    /// The two entry points into the stamp must never disagree: the domain-entry overload works on
    /// enum state and scope, while the response-model overload works on the strings the API carries.
    /// If the same logical entry set ever hashed differently through the two paths, the server would
    /// reject saves that are perfectly valid — a save that round-trips exactly what a read handed out.
    /// </summary>
    [Fact]
    public void ComputeFromResponse_AgreesWithCompute_ForTheSameLogicalEntries()
    {
        var nodeKey = Guid.Parse("44444444-4444-4444-4444-444444444444");

        var domainEntries = new List<AdvancedPermissionEntry>
        {
            new(Guid.NewGuid(), nodeKey, "editors", "Umb.Document.Read", PermissionState.Allow, PermissionScope.ThisNodeOnly, false),
            new(Guid.NewGuid(), nodeKey, "editors", "Umb.Document.Publish", PermissionState.Deny, PermissionScope.ThisNodeAndDescendants, true),
        };

        var responseEntries = new List<PermissionEntryResponseModel>
        {
            new(Guid.NewGuid(), nodeKey, "editors", "Umb.Document.Read", "Allow", "ThisNodeOnly", false),
            new(Guid.NewGuid(), nodeKey, "editors", "Umb.Document.Publish", "Deny", "ThisNodeAndDescendants", true),
        };

        Assert.Equal(PermissionStamp.Compute(domainEntries), responseEntries.ComputeFromResponse());
    }

    /// <summary>
    /// The document-type response model must hash to the same stamp as the equivalent node-keyed
    /// entries, because the same <see cref="PermissionStamp"/> canonical form serves both.
    /// </summary>
    [Fact]
    public void ComputeFromResponse_DocTypeEntries_AgreeWithNodeEntriesOfTheSameContent()
    {
        var nodeKey = Guid.NewGuid();
        var typeKey = Guid.NewGuid();

        var docTypeEntries = new List<DocTypePermissionEntryResponseModel>
        {
            new(Guid.NewGuid(), nodeKey, typeKey, "editors", "Umb.Document.CreateOfType", "Deny", "ThisNodeAndDescendants", true),
        };
        var nodeEntries = new List<PermissionEntryResponseModel>
        {
            new(Guid.NewGuid(), nodeKey, "editors", "Umb.Document.CreateOfType", "Deny", "ThisNodeAndDescendants", true),
        };

        Assert.Equal(nodeEntries.ComputeFromResponse(), docTypeEntries.ComputeFromResponse());
    }

    /// <summary>
    /// The content <c>GET permissions</c> endpoint returns a bare array, so the stamp travels as an
    /// <c>ETag</c> header. It must equal the stamp of the entries in the body, quoted.
    /// </summary>
    [Fact]
    public async Task ContentGetPermissions_SetsETagToTheStampOfTheReturnedEntries()
    {
        var nodeKey = Guid.NewGuid();
        var service = Substitute.For<IAdvancedPermissionService>();
        IReadOnlyList<AdvancedPermissionEntry> stored =
        [
            new(Guid.NewGuid(), nodeKey, "editors", "Umb.Document.Read", PermissionState.Allow, PermissionScope.ThisNodeOnly, false),
            new(Guid.NewGuid(), nodeKey, "editors", "Umb.Document.Delete", PermissionState.Deny, PermissionScope.DescendantsOnly, true),
        ];
        service.GetEntriesAsync(nodeKey, "editors", Arg.Any<CancellationToken>()).Returns(stored);
        var sut = new AdvancedPermissionsPermissionController(
            service, Substitute.For<IAdvancedPermissionRepository>(), Substitute.For<IEntityService>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        var result = await sut.GetPermissions(CancellationToken.None, nodeKey, "editors");

        var body = Assert.IsAssignableFrom<IReadOnlyList<PermissionEntryResponseModel>>(
            Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(2, body.Count);
        Assert.Equal($"\"{PermissionStamp.Compute(stored)}\"", sut.Response.Headers.ETag.ToString());
    }

    /// <summary>
    /// A node with nothing stored still carries an <c>ETag</c>: the well-known empty stamp, never
    /// absent, so the client can always send back what it read.
    /// </summary>
    [Fact]
    public async Task ContentGetPermissions_NothingStored_SetsETagToTheEmptyStamp()
    {
        var nodeKey = Guid.NewGuid();
        var service = Substitute.For<IAdvancedPermissionService>();
        service.GetEntriesAsync(nodeKey, "editors", Arg.Any<CancellationToken>())
            .Returns(Array.Empty<AdvancedPermissionEntry>());
        var sut = new AdvancedPermissionsPermissionController(
            service, Substitute.For<IAdvancedPermissionRepository>(), Substitute.For<IEntityService>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        await sut.GetPermissions(CancellationToken.None, nodeKey, "editors");

        Assert.Equal($"\"{PermissionStamp.Compute([])}\"", sut.Response.Headers.ETag.ToString());
    }

    /// <summary>
    /// The element <c>GET element/permissions</c> endpoint has the same contract as the content one:
    /// an <c>ETag</c> holding the stamp of the returned entries. Without it the Library editors would
    /// have no concurrency token to send back.
    /// </summary>
    [Fact]
    public async Task ElementGetPermissions_SetsETagToTheStampOfTheReturnedEntries()
    {
        var nodeKey = Guid.NewGuid();
        var service = Substitute.For<IElementNodePermissionService>();
        IReadOnlyList<AdvancedPermissionEntry> stored =
        [
            new(Guid.NewGuid(), nodeKey, "editors", "Umb.Element.Read", PermissionState.Allow, PermissionScope.ThisNodeAndDescendants, false),
        ];
        service.GetEntriesAsync(nodeKey, "editors", Arg.Any<CancellationToken>()).Returns(stored);
        var sut = new ElementPermissionsPermissionController(
            service, Substitute.For<IElementPermissionRepository>(), Substitute.For<IEntityService>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        var result = await sut.GetPermissions(CancellationToken.None, nodeKey, "editors");

        var body = Assert.IsAssignableFrom<IReadOnlyList<PermissionEntryResponseModel>>(
            Assert.IsType<OkObjectResult>(result).Value);
        Assert.Single(body);
        Assert.Equal($"\"{PermissionStamp.Compute(stored)}\"", sut.Response.Headers.ETag.ToString());
    }

    /// <summary>
    /// A library node with nothing stored still carries an <c>ETag</c>: the well-known empty stamp,
    /// never absent, exactly as the content endpoint does.
    /// </summary>
    [Fact]
    public async Task ElementGetPermissions_NothingStored_SetsETagToTheEmptyStamp()
    {
        var nodeKey = Guid.NewGuid();
        var service = Substitute.For<IElementNodePermissionService>();
        service.GetEntriesAsync(nodeKey, "editors", Arg.Any<CancellationToken>())
            .Returns(Array.Empty<AdvancedPermissionEntry>());
        var sut = new ElementPermissionsPermissionController(
            service, Substitute.For<IElementPermissionRepository>(), Substitute.For<IEntityService>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        await sut.GetPermissions(CancellationToken.None, nodeKey, "editors");

        Assert.Equal($"\"{PermissionStamp.Compute([])}\"", sut.Response.Headers.ETag.ToString());
    }

    /// <summary>
    /// The content tree endpoints carry a stamp per node, computed from that node's own entries; a
    /// node with none carries the empty stamp. This is the stamp the editor sends back on save.
    /// </summary>
    [Fact]
    public async Task ContentTreeRoot_EachNodeCarriesTheStampOfItsOwnEntries()
    {
        var withEntries = Guid.NewGuid();
        var withoutEntries = Guid.NewGuid();
        IReadOnlyList<AdvancedPermissionEntry> stored =
        [
            new(Guid.NewGuid(), withEntries, "editors", "Umb.Document.Read", PermissionState.Deny, PermissionScope.ThisNodeOnly, false),
            new(Guid.NewGuid(), withEntries, "editors", "Umb.Document.Update", PermissionState.Allow, PermissionScope.DescendantsOnly, true),
        ];
        var service = Substitute.For<IAdvancedPermissionService>();
        service.GetEntriesByNodesAndRoleAsync(Arg.Any<IEnumerable<Guid>>(), "editors", Arg.Any<CancellationToken>())
            .Returns(stored);
        var entityService = Substitute.For<IEntityService>();
        IEntitySlim[] roots = [StubEntity(withEntries, "Has entries"), StubEntity(withoutEntries, "Has none")];
        entityService.GetRootEntities(UmbracoObjectTypes.Document).Returns(roots);
        var sut = new AdvancedPermissionsTreeController(service, entityService);

        var result = await sut.GetRoot(CancellationToken.None, "editors");

        var nodes = Assert.IsAssignableFrom<IReadOnlyList<TreeNodeResponseModel>>(
            Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(PermissionStamp.Compute(stored), nodes.Single(n => n.Key == withEntries).Stamp);
        Assert.Equal(PermissionStamp.Compute([]), nodes.Single(n => n.Key == withoutEntries).Stamp);
    }

    /// <summary>
    /// The content <c>GetChildren</c> endpoint is the path the editor takes when it expands a node, so
    /// it must carry the same per-node stamp as the root: computed from that child's own entries, with
    /// the empty stamp for a child that has none.
    /// </summary>
    [Fact]
    public async Task ContentTreeChildren_EachNodeCarriesTheStampOfItsOwnEntries()
    {
        var parentKey = Guid.NewGuid();
        var withEntries = Guid.NewGuid();
        var withoutEntries = Guid.NewGuid();
        IReadOnlyList<AdvancedPermissionEntry> stored =
        [
            new(Guid.NewGuid(), withEntries, "editors", "Umb.Document.Read", PermissionState.Deny, PermissionScope.ThisNodeOnly, false),
            new(Guid.NewGuid(), withEntries, "editors", "Umb.Document.Update", PermissionState.Allow, PermissionScope.DescendantsOnly, true),
        ];
        var service = Substitute.For<IAdvancedPermissionService>();
        service.GetEntriesByNodesAndRoleAsync(Arg.Any<IEnumerable<Guid>>(), "editors", Arg.Any<CancellationToken>())
            .Returns(stored);
        var entityService = Substitute.For<IEntityService>();
        IEntitySlim[] children = [StubEntity(withEntries, "Has entries"), StubEntity(withoutEntries, "Has none")];
        entityService.GetChildren(parentKey, UmbracoObjectTypes.Document).Returns(children);
        var sut = new AdvancedPermissionsTreeController(service, entityService);

        var result = await sut.GetChildren(CancellationToken.None, parentKey, "editors");

        var nodes = Assert.IsAssignableFrom<IReadOnlyList<TreeNodeResponseModel>>(
            Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(PermissionStamp.Compute(stored), nodes.Single(n => n.Key == withEntries).Stamp);
        Assert.Equal(PermissionStamp.Compute([]), nodes.Single(n => n.Key == withoutEntries).Stamp);
    }

    /// <summary>
    /// The library tree endpoints carry a stamp per node too. Without it the element family would be
    /// the one place a concurrent edit was silently lost.
    /// </summary>
    [Fact]
    public async Task ElementTreeRoot_EachNodeCarriesTheStampOfItsOwnEntries()
    {
        var folderKey = Guid.NewGuid();
        var elementKey = Guid.NewGuid();
        IReadOnlyList<AdvancedPermissionEntry> stored =
        [
            new(Guid.NewGuid(), folderKey, "editors", "Umb.ElementContainer.Read", PermissionState.Allow, PermissionScope.ThisNodeAndDescendants, false),
        ];
        var service = Substitute.For<IElementNodePermissionService>();
        service.GetEntriesByNodesAndRoleAsync(Arg.Any<IEnumerable<Guid>>(), "editors", Arg.Any<CancellationToken>())
            .Returns(stored);
        var entityService = Substitute.For<IEntityService>();
        StubLibraryChildren(
            entityService,
            StubEntity(folderKey, "Folder", Constants.ObjectTypes.ElementContainer),
            StubEntity(elementKey, "Element", Constants.ObjectTypes.Element));
        var sut = new ElementPermissionsTreeController(service, entityService);

        var result = await sut.GetRoot(CancellationToken.None, "editors");

        var nodes = Assert.IsAssignableFrom<IReadOnlyList<ElementTreeNodeResponseModel>>(
            Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(PermissionStamp.Compute(stored), nodes.Single(n => n.Key == folderKey).Stamp);
        Assert.Equal(PermissionStamp.Compute([]), nodes.Single(n => n.Key == elementKey).Stamp);
    }

    /// <summary>
    /// The library <c>GetChildren</c> endpoint is the path the editor takes when it expands a folder,
    /// so it must carry the same per-node stamp as the root.
    /// </summary>
    [Fact]
    public async Task ElementTreeChildren_EachNodeCarriesTheStampOfItsOwnEntries()
    {
        var folderKey = Guid.NewGuid();
        var elementKey = Guid.NewGuid();
        IReadOnlyList<AdvancedPermissionEntry> stored =
        [
            new(Guid.NewGuid(), elementKey, "editors", "Umb.Element.Read", PermissionState.Deny, PermissionScope.ThisNodeOnly, false),
        ];
        var service = Substitute.For<IElementNodePermissionService>();
        service.GetEntriesByNodesAndRoleAsync(Arg.Any<IEnumerable<Guid>>(), "editors", Arg.Any<CancellationToken>())
            .Returns(stored);
        var entityService = Substitute.For<IEntityService>();
        StubLibraryChildren(
            entityService,
            StubEntity(folderKey, "Sub folder", Constants.ObjectTypes.ElementContainer),
            StubEntity(elementKey, "Element", Constants.ObjectTypes.Element));
        var sut = new ElementPermissionsTreeController(service, entityService);

        var result = await sut.GetChildren(CancellationToken.None, Guid.NewGuid(), "editors");

        var nodes = Assert.IsAssignableFrom<IReadOnlyList<ElementTreeNodeResponseModel>>(
            Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(PermissionStamp.Compute(stored), nodes.Single(n => n.Key == elementKey).Stamp);
        Assert.Equal(PermissionStamp.Compute([]), nodes.Single(n => n.Key == folderKey).Stamp);
    }

    /// <summary>
    /// The document-type editor read returns one row per node that has entries, each with the stamp of
    /// that node's own triple. The stamp has to describe each node's set, not the whole result together,
    /// or a save of one node could never tell whether its own entries had moved.
    /// </summary>
    [Fact]
    public async Task DocTypeGetForEditor_GroupsByNode_EachWithItsOwnStamp()
    {
        var nodeA = Guid.NewGuid();
        var nodeB = Guid.NewGuid();
        var typeKey = Guid.NewGuid();
        IReadOnlyList<DocTypePermissionEntry> stored =
        [
            new(Guid.NewGuid(), nodeA, typeKey, "editors", "Umb.Document.CreateOfType", PermissionState.Allow, PermissionScope.ThisNodeOnly),
            new(Guid.NewGuid(), nodeA, typeKey, "editors", "Umb.Document.CreateOfType", PermissionState.Deny, PermissionScope.DescendantsOnly, true),
            new(Guid.NewGuid(), nodeB, typeKey, "editors", "Umb.Document.CreateOfType", PermissionState.Deny, PermissionScope.ThisNodeAndDescendants),
        ];
        var service = Substitute.For<IDocTypePermissionService>();
        service.GetEditorEntriesAsync("editors", typeKey, Arg.Any<CancellationToken>()).Returns(stored);
        var sut = new DocTypePermissionsController(
            service,
            Substitute.For<IDocTypePermissionRepository>(),
            Substitute.For<IContentTypeService>(),
            Substitute.For<IEntityService>(),
            Substitute.For<IUserService>());

        var result = await sut.GetForEditor(CancellationToken.None, "editors", typeKey);

        var nodes = Assert.IsAssignableFrom<IReadOnlyList<DocTypeEditorNodeResponseModel>>(
            Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(2, nodes.Count);

        var a = nodes.Single(n => n.NodeKey == nodeA);
        Assert.Equal(2, a.Entries.Count);
        Assert.Equal(a.Entries.ComputeFromResponse(), a.Stamp);

        var b = nodes.Single(n => n.NodeKey == nodeB);
        Assert.Single(b.Entries);
        Assert.Equal(b.Entries.ComputeFromResponse(), b.Stamp);

        Assert.NotEqual(a.Stamp, b.Stamp);
    }

    /// <summary>
    /// Stubs the multi-object-type paged-children overload the library tree controller uses, so it
    /// returns the supplied entities for any parent.
    /// </summary>
    /// <param name="entityService">The entity service substitute to stub.</param>
    /// <param name="children">The entities to return.</param>
    private static void StubLibraryChildren(IEntityService entityService, params IEntitySlim[] children)
    {
        long total;
        entityService
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
                ci[6] = (long)children.Length;
                return children;
            });
    }

    /// <summary>
    /// Builds a stub entity with the supplied key, name and object type.
    /// </summary>
    /// <param name="key">The unique key.</param>
    /// <param name="name">The display name.</param>
    /// <param name="objectType">The node object-type GUID; defaults to a document.</param>
    /// <returns>The stub entity.</returns>
    private static IEntitySlim StubEntity(Guid key, string name, Guid? objectType = null)
    {
        var entity = Substitute.For<IEntitySlim>();
        entity.Key.Returns(key);
        entity.Name.Returns(name);
        entity.NodeObjectType.Returns(objectType ?? Constants.ObjectTypes.Document);
        entity.HasChildren.Returns(false);
        return entity;
    }
}
