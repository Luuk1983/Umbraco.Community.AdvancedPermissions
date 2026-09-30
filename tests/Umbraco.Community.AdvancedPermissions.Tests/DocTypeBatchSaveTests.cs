using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Umbraco.Community.AdvancedPermissions.Controllers;
using Umbraco.Community.AdvancedPermissions.Controllers.Models;
using Umbraco.Community.AdvancedPermissions.Core.Concurrency;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests the doc-type batch save endpoint: the concurrency check, the shape of the refusal, the
/// force bypass, and that a refused batch writes nothing at all.
/// </summary>
/// <remarks>
/// Mirrors <see cref="BatchSavePermissionsTests"/>, but every key is a triple — node, user group
/// AND document type — rather than a pair. Test case 3 exists specifically to catch a two-part key
/// used where the three-part triple belongs: two entries sharing a node and role but differing only
/// by content type must be treated as two distinct triples, never collapsed into one.
/// </remarks>
public sealed class DocTypeBatchSaveTests
{
    private static readonly Guid NodeA = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid ContentTypeA = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly Guid ContentTypeB = Guid.Parse("99999999-9999-9999-9999-999999999999");

    /// <summary>Builds a stored doc-type entry for the given node, content type and state.</summary>
    /// <param name="nodeKey">The node key.</param>
    /// <param name="contentTypeKey">The content-type key.</param>
    /// <param name="state">The state.</param>
    /// <returns>The entry.</returns>
    private static DocTypePermissionEntry Stored(
        Guid nodeKey,
        Guid contentTypeKey,
        PermissionState state = PermissionState.Allow) =>
        new(
            Id: Guid.NewGuid(),
            NodeKey: nodeKey,
            ContentTypeKey: contentTypeKey,
            RoleAlias: "editors",
            Verb: AdvancedPermissionsConstants.VerbCreateOfType,
            State: state,
            Scope: PermissionScope.ThisNodeOnly,
            IsPriorityOverride: false);

    /// <summary>Computes the stamp a stored set of doc-type entries would carry over the wire.</summary>
    /// <param name="entries">The entries to hash.</param>
    /// <returns>The stamp, as lowercase hex.</returns>
    private static string StampOf(IEnumerable<DocTypePermissionEntry> entries) =>
        PermissionStamp.ComputeFromNames(
            entries.Select(e => (e.Verb, e.State.ToString(), e.Scope.ToString(), e.IsPriorityOverride)));

    /// <summary>A matching stamp saves, and the write reaches the service as one batch.</summary>
    [Fact]
    public async Task BatchSave_MatchingStamp_Saves()
    {
        var service = Substitute.For<IDocTypePermissionService>();
        var stored = new[] { Stored(NodeA, ContentTypeA) };
        service.GetEditorEntriesAsync("editors", ContentTypeA, Arg.Any<CancellationToken>()).Returns(stored);

        var controller = BuildController(service);
        var request = new BatchSaveDocTypePermissionsRequestModel(
        [
            new BatchSaveDocTypePermissionsNode(
                NodeA,
                "editors",
                ContentTypeA,
                [new SavePermissionEntryItem(AdvancedPermissionsConstants.VerbCreateOfType, "Deny", "ThisNodeOnly")],
                StampOf(stored)),
        ]);

        var result = await controller.BatchSaveDocTypePermissions(request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        await service.Received(1).SaveManyAsync(
            Arg.Any<IReadOnlyList<(Guid, string, Guid, IReadOnlyList<(string, PermissionState, PermissionScope, bool)>)>>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>A stale stamp is refused with 409, and nothing is written.</summary>
    [Fact]
    public async Task BatchSave_StaleStamp_Returns409AndWritesNothing()
    {
        var service = Substitute.For<IDocTypePermissionService>();
        service.GetEditorEntriesAsync("editors", ContentTypeA, Arg.Any<CancellationToken>())
            .Returns(new[] { Stored(NodeA, ContentTypeA, PermissionState.Deny) });

        var controller = BuildController(service);
        var request = new BatchSaveDocTypePermissionsRequestModel(
        [
            new BatchSaveDocTypePermissionsNode(
                NodeA,
                "editors",
                ContentTypeA,
                [new SavePermissionEntryItem(AdvancedPermissionsConstants.VerbCreateOfType, "Allow", "ThisNodeOnly")],
                StampOf([Stored(NodeA, ContentTypeA)])),
        ]);

        var result = await controller.BatchSaveDocTypePermissions(request, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var body = Assert.IsType<BatchSaveDocTypeConflictResponseModel>(conflict.Value);
        Assert.Single(body.Conflicts);
        Assert.Equal(NodeA, body.Conflicts[0].NodeKey);
        Assert.Equal(ContentTypeA, body.Conflicts[0].ContentTypeKey);
        Assert.Single(body.Conflicts[0].CurrentEntries);
        Assert.Equal("Deny", body.Conflicts[0].CurrentEntries[0].State);

        await service.DidNotReceive().SaveManyAsync(
            Arg.Any<IReadOnlyList<(Guid, string, Guid, IReadOnlyList<(string, PermissionState, PermissionScope, bool)>)>>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Two triples sharing a node and role but differing only by content type: one fresh, one
    /// stale. The whole batch is refused and only the stale triple is listed. If the content type
    /// were dropped from any key comparison, these two triples would collapse into one and this
    /// test would fail — either by missing the conflict entirely or by reporting the wrong triple.
    /// </summary>
    [Fact]
    public async Task BatchSave_OneStalePair_RefusesWholeBatch()
    {
        var service = Substitute.For<IDocTypePermissionService>();
        var freshA = new[] { Stored(NodeA, ContentTypeA) };
        service.GetEditorEntriesAsync("editors", ContentTypeA, Arg.Any<CancellationToken>()).Returns(freshA);
        service.GetEditorEntriesAsync("editors", ContentTypeB, Arg.Any<CancellationToken>())
            .Returns(new[] { Stored(NodeA, ContentTypeB, PermissionState.Deny) });

        var controller = BuildController(service);
        var request = new BatchSaveDocTypePermissionsRequestModel(
        [
            new BatchSaveDocTypePermissionsNode(NodeA, "editors", ContentTypeA, [], StampOf(freshA)),
            new BatchSaveDocTypePermissionsNode(NodeA, "editors", ContentTypeB, [], StampOf([])),
        ]);

        var result = await controller.BatchSaveDocTypePermissions(request, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var body = Assert.IsType<BatchSaveDocTypeConflictResponseModel>(conflict.Value);
        Assert.Single(body.Conflicts);
        Assert.Equal(NodeA, body.Conflicts[0].NodeKey);
        Assert.Equal(ContentTypeB, body.Conflicts[0].ContentTypeKey);

        await service.DidNotReceive().SaveManyAsync(
            Arg.Any<IReadOnlyList<(Guid, string, Guid, IReadOnlyList<(string, PermissionState, PermissionScope, bool)>)>>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>Force writes through a stale stamp without asking.</summary>
    [Fact]
    public async Task BatchSave_Force_SavesDespiteStaleStamp()
    {
        var service = Substitute.For<IDocTypePermissionService>();
        service.GetEditorEntriesAsync("editors", ContentTypeA, Arg.Any<CancellationToken>())
            .Returns(new[] { Stored(NodeA, ContentTypeA, PermissionState.Deny) });

        var controller = BuildController(service);
        var request = new BatchSaveDocTypePermissionsRequestModel(
            [new BatchSaveDocTypePermissionsNode(NodeA, "editors", ContentTypeA, [], "a-stamp-that-does-not-match")],
            Force: true);

        var result = await controller.BatchSaveDocTypePermissions(request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        await service.Received(1).SaveManyAsync(
            Arg.Any<IReadOnlyList<(Guid, string, Guid, IReadOnlyList<(string, PermissionState, PermissionScope, bool)>)>>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>A null stamp means an older client, which keeps its previous behaviour.</summary>
    [Fact]
    public async Task BatchSave_NullStamp_SkipsCheck()
    {
        var service = Substitute.For<IDocTypePermissionService>();
        service.GetEditorEntriesAsync("editors", ContentTypeA, Arg.Any<CancellationToken>())
            .Returns(new[] { Stored(NodeA, ContentTypeA, PermissionState.Deny) });

        var controller = BuildController(service);
        var request = new BatchSaveDocTypePermissionsRequestModel(
            [new BatchSaveDocTypePermissionsNode(NodeA, "editors", ContentTypeA, [], ExpectedStamp: null)]);

        var result = await controller.BatchSaveDocTypePermissions(request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    /// <summary>An unrecognised verb is still rejected as a bad request, before any stamp work.</summary>
    [Fact]
    public async Task BatchSave_InvalidVerb_ReturnsBadRequest()
    {
        var service = Substitute.For<IDocTypePermissionService>();
        var controller = BuildController(service);
        var request = new BatchSaveDocTypePermissionsRequestModel(
        [
            new BatchSaveDocTypePermissionsNode(
                NodeA,
                "editors",
                ContentTypeA,
                [new SavePermissionEntryItem("Not.A.Real.Verb", "Allow", "ThisNodeOnly")]),
        ]);

        var result = await controller.BatchSaveDocTypePermissions(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    /// <summary>
    /// Builds the controller under test with substitutes for every collaborator besides the
    /// doc-type permission service, which each test configures directly.
    /// </summary>
    /// <param name="service">The doc-type permission service substitute.</param>
    /// <returns>The controller.</returns>
    private static DocTypePermissionsController BuildController(IDocTypePermissionService service) =>
        new(service,
            Substitute.For<IDocTypePermissionRepository>(),
            Substitute.For<global::Umbraco.Cms.Core.Services.IContentTypeService>(),
            Substitute.For<global::Umbraco.Cms.Core.Services.IEntityService>(),
            Substitute.For<global::Umbraco.Cms.Core.Services.IUserService>());
}
