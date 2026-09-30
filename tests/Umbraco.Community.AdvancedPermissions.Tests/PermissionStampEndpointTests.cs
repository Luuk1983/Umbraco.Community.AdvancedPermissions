using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.Controllers;
using Umbraco.Community.AdvancedPermissions.Controllers.Models;
using Umbraco.Community.AdvancedPermissions.Core.Concurrency;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests that the stamp a read endpoint hands out is the stamp a save will be checked against.
/// </summary>
/// <remarks>
/// The invariant is that a stamp is computed from exactly the entries being returned. If it is not,
/// a client that saves back precisely what it was given is told it conflicts with somebody else's
/// change that never happened, and learns to click through the conflict dialog. The endpoint tests
/// here call the real controllers. An earlier version of this file only built response records by
/// hand and compared their stamps, which exercised <see cref="PermissionStamp"/> and would have
/// passed for a controller that never computed a stamp or never set the <c>ETag</c>.
/// </remarks>
public sealed class PermissionStampEndpointTests
{
    /// <summary>
    /// The two entry points into the stamp must never disagree: <see cref="PermissionStamp.Compute(IEnumerable{AdvancedPermissionEntry})"/>
    /// operates on domain entries with enum State/Scope, while <see cref="PermissionStampExtensions.ComputeFromResponse(IEnumerable{PermissionEntryResponseModel})"/>
    /// operates on API response models whose State/Scope are strings. If the same logical entry
    /// set ever hashed differently through the two paths, the server would reject saves that are
    /// perfectly valid — a save that round-trips exactly what a read handed out — and nothing else
    /// in the suite would catch it.
    /// </summary>
    [Fact]
    public void ComputeFromNames_AgreesWithCompute_ForTheSameLogicalEntries()
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
    /// entries, because the same <see cref="PermissionStamp"/> canonical form serves both. If the
    /// two ever drift, the doc-type editor's stamp would stop matching what its save is checked
    /// against.
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
    /// <c>GET permissions</c> returns a bare array, so the stamp can only travel as an <c>ETag</c>
    /// header. It must hold the stamp of the entries in the body, quoted. A controller that forgot
    /// to set the header would leave every client with no concurrency token to send back; the old
    /// hand-built tests passed regardless because they never called the controller.
    /// </summary>
    [Fact]
    public async Task GetPermissions_SetsETagToTheStampOfTheReturnedEntries()
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
    /// absent, so the client can always send back what it read and a first-ever save is not
    /// mistaken for a conflict.
    /// </summary>
    [Fact]
    public async Task GetPermissions_NothingStored_SetsETagToTheEmptyStamp()
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
    /// <c>GET tree/root</c> carries a stamp per node, computed from that node's own entries; a node
    /// with none carries the empty stamp. This is the stamp the editor sends back on save, so a
    /// controller that left it blank, or hashed the whole result together, would make every save
    /// look like a conflict. The old hand-built test constructed the record directly and could not
    /// see that.
    /// </summary>
    [Fact]
    public async Task TreeGetRoot_EachNodeCarriesTheStampOfItsOwnEntries()
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
    /// <c>GET tree/children</c> is a separate endpoint from <c>tree/root</c> and both feed the same
    /// editor, so a child expanded lazily must carry its stamp too. Otherwise the second level of the
    /// tree would be the one place a concurrent edit was silently lost.
    /// </summary>
    [Fact]
    public async Task TreeGetChildren_EachNodeCarriesTheStampOfItsOwnEntries()
    {
        var parentKey = Guid.NewGuid();
        var withEntries = Guid.NewGuid();
        var withoutEntries = Guid.NewGuid();
        IReadOnlyList<AdvancedPermissionEntry> stored =
        [
            new(Guid.NewGuid(), withEntries, "editors", "Umb.Document.Publish", PermissionState.Allow, PermissionScope.ThisNodeAndDescendants, false),
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
        Assert.Equal(2, nodes.Count);
        Assert.Equal(PermissionStamp.Compute(stored), nodes.Single(n => n.Key == withEntries).Stamp);
        Assert.Equal(PermissionStamp.Compute([]), nodes.Single(n => n.Key == withoutEntries).Stamp);
    }

    /// <summary>
    /// <c>GET doc-type-permissions</c> returns one row per node that has entries, each with the stamp
    /// of that node's own triple. The stamp has to describe each node's set, not the whole result
    /// together, or a save of one node could never tell whether its own entries had moved.
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
    /// Builds a stub document entity with the supplied key and name.
    /// </summary>
    /// <param name="key">The unique key.</param>
    /// <param name="name">The display name.</param>
    /// <returns>The stub entity.</returns>
    private static IEntitySlim StubEntity(Guid key, string name)
    {
        var entity = Substitute.For<IEntitySlim>();
        entity.Key.Returns(key);
        entity.Name.Returns(name);
        entity.HasChildren.Returns(false);
        return entity;
    }
}
