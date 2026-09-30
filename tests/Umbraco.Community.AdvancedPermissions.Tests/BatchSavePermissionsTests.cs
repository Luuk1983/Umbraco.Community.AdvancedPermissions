using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Umbraco.Community.AdvancedPermissions.Controllers;
using Umbraco.Community.AdvancedPermissions.Controllers.Models;
using Umbraco.Community.AdvancedPermissions.Core.Concurrency;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests the batch save endpoint: the concurrency check, the shape of the refusal, the force
/// bypass, and that a refused batch writes nothing at all.
/// </summary>
public sealed class BatchSavePermissionsTests
{
    private static readonly Guid NodeA = Guid.Parse("55555555-5555-5555-5555-555555555555");

    /// <summary>Builds a stored entry for the given verb.</summary>
    /// <param name="verb">The verb.</param>
    /// <param name="state">The state.</param>
    /// <returns>The entry.</returns>
    private static AdvancedPermissionEntry Stored(string verb, PermissionState state = PermissionState.Allow) =>
        new(
            Id: Guid.NewGuid(),
            NodeKey: NodeA,
            RoleAlias: "editors",
            Verb: verb,
            State: state,
            Scope: PermissionScope.ThisNodeOnly,
            IsPriorityOverride: false);

    /// <summary>A matching stamp saves, and the write reaches the service as one batch.</summary>
    [Fact]
    public async Task BatchSave_MatchingStamp_Saves()
    {
        var service = Substitute.For<IAdvancedPermissionService>();
        var stored = new[] { Stored("Umb.Document.Read") };
        service.GetEntriesAsync(NodeA, "editors", Arg.Any<CancellationToken>()).Returns(stored);

        var controller = BuildController(service);
        var request = new BatchSavePermissionsRequestModel(
        [
            new BatchSavePermissionsNode(
                NodeA,
                "editors",
                [new SavePermissionEntryItem("Umb.Document.Publish", "Allow", "ThisNodeOnly")],
                PermissionStamp.Compute(stored)),
        ]);

        var result = await controller.BatchSavePermissions(request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        await service.Received(1).SaveManyAsync(
            Arg.Any<IReadOnlyList<(Guid, string, IReadOnlyList<(string, PermissionState, PermissionScope, bool)>)>>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>A stale stamp is refused with 409, and nothing is written.</summary>
    [Fact]
    public async Task BatchSave_StaleStamp_Returns409AndWritesNothing()
    {
        var service = Substitute.For<IAdvancedPermissionService>();
        service.GetEntriesAsync(NodeA, "editors", Arg.Any<CancellationToken>())
            .Returns(new[] { Stored("Umb.Document.Read", PermissionState.Deny) });

        var controller = BuildController(service);
        var request = new BatchSavePermissionsRequestModel(
        [
            new BatchSavePermissionsNode(
                NodeA,
                "editors",
                [new SavePermissionEntryItem("Umb.Document.Publish", "Allow", "ThisNodeOnly")],
                PermissionStamp.Compute([Stored("Umb.Document.Read")])),
        ]);

        var result = await controller.BatchSavePermissions(request, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var body = Assert.IsType<BatchSaveConflictResponseModel>(conflict.Value);
        Assert.Single(body.Conflicts);
        Assert.Equal(NodeA, body.Conflicts[0].NodeKey);
        Assert.Single(body.Conflicts[0].CurrentEntries);
        Assert.Equal("Deny", body.Conflicts[0].CurrentEntries[0].State);

        await service.DidNotReceive().SaveManyAsync(
            Arg.Any<IReadOnlyList<(Guid, string, IReadOnlyList<(string, PermissionState, PermissionScope, bool)>)>>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>One stale pair in a batch refuses the whole batch, not just that pair.</summary>
    [Fact]
    public async Task BatchSave_OneStalePair_RefusesWholeBatch()
    {
        var nodeB = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var service = Substitute.For<IAdvancedPermissionService>();
        var freshA = new[] { Stored("Umb.Document.Read") };
        service.GetEntriesAsync(NodeA, "editors", Arg.Any<CancellationToken>()).Returns(freshA);
        service.GetEntriesAsync(nodeB, "editors", Arg.Any<CancellationToken>())
            .Returns(new[] { Stored("Umb.Document.Read", PermissionState.Deny) });

        var controller = BuildController(service);
        var request = new BatchSavePermissionsRequestModel(
        [
            new BatchSavePermissionsNode(NodeA, "editors", [], PermissionStamp.Compute(freshA)),
            new BatchSavePermissionsNode(nodeB, "editors", [], PermissionStamp.Compute([])),
        ]);

        var result = await controller.BatchSavePermissions(request, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var body = Assert.IsType<BatchSaveConflictResponseModel>(conflict.Value);
        Assert.Single(body.Conflicts);
        Assert.Equal(nodeB, body.Conflicts[0].NodeKey);

        await service.DidNotReceive().SaveManyAsync(
            Arg.Any<IReadOnlyList<(Guid, string, IReadOnlyList<(string, PermissionState, PermissionScope, bool)>)>>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>Force writes through a stale stamp without asking.</summary>
    [Fact]
    public async Task BatchSave_Force_SavesDespiteStaleStamp()
    {
        var service = Substitute.For<IAdvancedPermissionService>();
        service.GetEntriesAsync(NodeA, "editors", Arg.Any<CancellationToken>())
            .Returns(new[] { Stored("Umb.Document.Read", PermissionState.Deny) });

        var controller = BuildController(service);
        var request = new BatchSavePermissionsRequestModel(
            [new BatchSavePermissionsNode(NodeA, "editors", [], "a-stamp-that-does-not-match")],
            Force: true);

        var result = await controller.BatchSavePermissions(request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        await service.Received(1).SaveManyAsync(
            Arg.Any<IReadOnlyList<(Guid, string, IReadOnlyList<(string, PermissionState, PermissionScope, bool)>)>>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>A null stamp means an older client, which keeps its previous behaviour.</summary>
    [Fact]
    public async Task BatchSave_NullStamp_SkipsCheck()
    {
        var service = Substitute.For<IAdvancedPermissionService>();
        service.GetEntriesAsync(NodeA, "editors", Arg.Any<CancellationToken>())
            .Returns(new[] { Stored("Umb.Document.Read", PermissionState.Deny) });

        var controller = BuildController(service);
        var request = new BatchSavePermissionsRequestModel(
            [new BatchSavePermissionsNode(NodeA, "editors", [], ExpectedStamp: null)]);

        var result = await controller.BatchSavePermissions(request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    /// <summary>An unrecognised verb is still rejected as a bad request, before any stamp work.</summary>
    [Fact]
    public async Task BatchSave_InvalidVerb_ReturnsBadRequest()
    {
        var service = Substitute.For<IAdvancedPermissionService>();
        var controller = BuildController(service);
        var request = new BatchSavePermissionsRequestModel(
        [
            new BatchSavePermissionsNode(
                NodeA,
                "editors",
                [new SavePermissionEntryItem("Not.A.Real.Verb", "Allow", "ThisNodeOnly")]),
        ]);

        var result = await controller.BatchSavePermissions(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    /// <summary>
    /// Two entries in the same batch for the same node and role are a malformed request — not a
    /// conflict, and not something that should reach the repository's last-resort
    /// <see cref="ArgumentException"/> guard and surface as an unhandled 500.
    /// </summary>
    [Fact]
    public async Task BatchSave_DuplicateNodeAndRolePair_ReturnsBadRequest()
    {
        var service = Substitute.For<IAdvancedPermissionService>();
        var controller = BuildController(service);
        var request = new BatchSavePermissionsRequestModel(
        [
            new BatchSavePermissionsNode(NodeA, "editors", []),
            new BatchSavePermissionsNode(NodeA, "editors", []),
        ]);

        var result = await controller.BatchSavePermissions(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        await service.DidNotReceive().SaveManyAsync(
            Arg.Any<IReadOnlyList<(Guid, string, IReadOnlyList<(string, PermissionState, PermissionScope, bool)>)>>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Builds the controller under test. Mirror the substitute set-up used by
    /// <c>AdvancedPermissionsEffectiveControllerTests</c> for the repository and entity service.
    /// </summary>
    /// <param name="service">The permission service substitute.</param>
    /// <returns>The controller.</returns>
    private static AdvancedPermissionsPermissionController BuildController(IAdvancedPermissionService service) =>
        new(service,
            Substitute.For<IAdvancedPermissionRepository>(),
            Substitute.For<global::Umbraco.Cms.Core.Services.IEntityService>());
}
