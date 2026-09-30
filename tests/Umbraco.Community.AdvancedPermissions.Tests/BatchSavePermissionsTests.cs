using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Umbraco.Community.AdvancedPermissions.Controllers;
using Umbraco.Community.AdvancedPermissions.Controllers.Models;
using Umbraco.Community.AdvancedPermissions.Core.Concurrency;
using Umbraco.Community.AdvancedPermissions.Core.Exceptions;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests the permission save endpoints' handling of the concurrency check: what they forward to
/// the service, and how the <see cref="PermissionConcurrencyException"/> the write raises becomes
/// the 409 the client is written against.
/// </summary>
/// <remarks>
/// The check itself no longer lives in the controller. It moved into the repository's write
/// transaction, because a check made in the controller and a write made afterwards are two
/// operations, and two saves carrying the same stamp could both pass the first. These tests
/// therefore stand a substitute service in for the write and drive the conflict by having it throw
/// — the repository's own tests prove it throws when it should. What is pinned here is the seam:
/// stamps travel to the service, <c>force</c> withholds them, the controller reads nothing itself,
/// and the exception is turned into a body Umbraco's interceptor keeps.
/// </remarks>
public sealed class BatchSavePermissionsTests
{
    private static readonly Guid NodeA = Guid.Parse("55555555-5555-5555-5555-555555555555");

    /// <summary>Builds a stored entry for the given verb.</summary>
    /// <param name="verb">The verb.</param>
    /// <param name="state">The state.</param>
    /// <param name="nodeKey">The node the entry is stored against; defaults to <see cref="NodeA"/>.</param>
    /// <returns>The entry.</returns>
    private static AdvancedPermissionEntry Stored(string verb, PermissionState state = PermissionState.Allow, Guid? nodeKey = null) =>
        new(
            Id: Guid.NewGuid(),
            NodeKey: nodeKey ?? NodeA,
            RoleAlias: "editors",
            Verb: verb,
            State: state,
            Scope: PermissionScope.ThisNodeOnly,
            IsPriorityOverride: false);

    /// <summary>Builds the conflict a repository would report for a pair whose entries moved.</summary>
    /// <param name="nodeKey">The conflicted node.</param>
    /// <param name="current">What is stored now.</param>
    /// <returns>The conflict, carrying the stamp of <paramref name="current"/>.</returns>
    private static PermissionConflict ConflictFor(Guid nodeKey, params AdvancedPermissionEntry[] current) =>
        new(nodeKey, "editors", current, PermissionStamp.Compute(current));

    /// <summary>
    /// Makes every batch write on the substitute fail the way the repository fails when a pair's
    /// stored entries no longer match its expected stamp.
    /// </summary>
    /// <param name="service">The substitute service.</param>
    /// <param name="conflicts">The conflicts to report.</param>
    private static void SaveManyConflictsWith(IAdvancedPermissionService service, params PermissionConflict[] conflicts) =>
        service
            .SaveManyAsync(Arg.Any<IReadOnlyList<(Guid, string, IReadOnlyList<(string, PermissionState, PermissionScope, bool)>, string?)>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new PermissionConcurrencyException(conflicts)));

    /// <summary>
    /// Reads back the expected stamps the controller forwarded on its one batch write, in batch order.
    /// </summary>
    /// <param name="service">The substitute service.</param>
    /// <returns>The forwarded stamps, one per pair.</returns>
    private static IReadOnlyList<string?> ForwardedBatchStamps(IAdvancedPermissionService service)
    {
        var call = Assert.Single(
            service.ReceivedCalls(),
            c => c.GetMethodInfo().Name == nameof(IAdvancedPermissionService.SaveManyAsync));
        var batch = Assert.IsAssignableFrom<IReadOnlyList<(Guid NodeKey, string RoleAlias, IReadOnlyList<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries, string? ExpectedStamp)>>(
            call.GetArguments()[0]);
        return batch.Select(b => b.ExpectedStamp).ToList();
    }

    /// <summary>A save with a stamp writes, and the stamp reaches the service with the batch.</summary>
    [Fact]
    public async Task BatchSave_ForwardsExpectedStampsToTheService()
    {
        var service = Substitute.For<IAdvancedPermissionService>();
        var nodeB = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var controller = BuildController(service);
        var request = new BatchSavePermissionsRequestModel(
        [
            new BatchSavePermissionsNode(NodeA, "editors", [new SavePermissionEntryItem("Umb.Document.Publish", "Allow", "ThisNodeOnly")], "stamp-a"),
            new BatchSavePermissionsNode(nodeB, "editors", [], ExpectedStamp: null),
        ]);

        var result = await controller.BatchSavePermissions(request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(["stamp-a", null], ForwardedBatchStamps(service));
    }

    /// <summary>
    /// The controller performs no check of its own. Reading the stored entries first would be the
    /// racy pre-check this design replaced, and leaving it in "as a first pass" would make it look
    /// as though the check still lived here.
    /// </summary>
    [Fact]
    public async Task BatchSave_DoesNotReadStoredEntriesItself()
    {
        var service = Substitute.For<IAdvancedPermissionService>();
        var controller = BuildController(service);
        var request = new BatchSavePermissionsRequestModel(
            [new BatchSavePermissionsNode(NodeA, "editors", [], "some-stamp")]);

        await controller.BatchSavePermissions(request, CancellationToken.None);

        await service.DidNotReceiveWithAnyArgs().GetEntriesAsync(default, default!, default);
    }

    /// <summary>A stale stamp — the write reports a conflict — is refused with 409 carrying what is stored now.</summary>
    [Fact]
    public async Task BatchSave_WriteReportsConflict_Returns409WithCurrentEntries()
    {
        var service = Substitute.For<IAdvancedPermissionService>();
        SaveManyConflictsWith(service, ConflictFor(NodeA, Stored("Umb.Document.Read", PermissionState.Deny)));
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
        var conflicts = ConflictsOf(conflict);
        Assert.Single(conflicts);
        Assert.Equal(NodeA, conflicts[0].NodeKey);
        Assert.Equal("editors", conflicts[0].RoleAlias);
        Assert.Single(conflicts[0].CurrentEntries);
        Assert.Equal("Deny", conflicts[0].CurrentEntries[0].State);
        Assert.Equal(PermissionStamp.Compute([Stored("Umb.Document.Read", PermissionState.Deny)]), conflicts[0].CurrentStamp);
    }

    /// <summary>
    /// Every conflicted pair the write reports reaches the client, in the order reported. The
    /// conflict dialog lists them all so the person resolves them in one pass, not one save at a time.
    /// </summary>
    [Fact]
    public async Task BatchSave_WriteReportsSeveralConflicts_ReturnsAllOfThem()
    {
        var nodeB = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var service = Substitute.For<IAdvancedPermissionService>();
        SaveManyConflictsWith(
            service,
            ConflictFor(NodeA, Stored("Umb.Document.Read", PermissionState.Deny)),
            ConflictFor(nodeB));
        var controller = BuildController(service);
        var request = new BatchSavePermissionsRequestModel(
        [
            new BatchSavePermissionsNode(NodeA, "editors", [], "stale"),
            new BatchSavePermissionsNode(nodeB, "editors", [], "stale"),
        ]);

        var result = await controller.BatchSavePermissions(request, CancellationToken.None);

        var conflicts = ConflictsOf(Assert.IsType<ConflictObjectResult>(result));
        Assert.Equal([NodeA, nodeB], conflicts.Select(c => c.NodeKey));
        Assert.Empty(conflicts[1].CurrentEntries);
    }

    /// <summary>Force withholds every stamp, so the write skips the check and overwrites whatever is stored.</summary>
    [Fact]
    public async Task BatchSave_Force_ForwardsNoStamps()
    {
        var service = Substitute.For<IAdvancedPermissionService>();
        var controller = BuildController(service);
        var request = new BatchSavePermissionsRequestModel(
            [new BatchSavePermissionsNode(NodeA, "editors", [], "a-stamp-that-does-not-match")],
            Force: true);

        var result = await controller.BatchSavePermissions(request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal<string?>([null], ForwardedBatchStamps(service));
    }

    /// <summary>A null stamp means an older client, which keeps its previous behaviour: no check, a plain write.</summary>
    [Fact]
    public async Task BatchSave_NullStamp_ForwardsNoStamp()
    {
        var service = Substitute.For<IAdvancedPermissionService>();
        var controller = BuildController(service);
        var request = new BatchSavePermissionsRequestModel(
            [new BatchSavePermissionsNode(NodeA, "editors", [], ExpectedStamp: null)]);

        var result = await controller.BatchSavePermissions(request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal<string?>([null], ForwardedBatchStamps(service));
    }

    /// <summary>An unrecognised verb is still rejected as a bad request, before any write is attempted.</summary>
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
        Assert.DoesNotContain(service.ReceivedCalls(), c => c.GetMethodInfo().Name == nameof(IAdvancedPermissionService.SaveManyAsync));
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
        Assert.DoesNotContain(service.ReceivedCalls(), c => c.GetMethodInfo().Name == nameof(IAdvancedPermissionService.SaveManyAsync));
    }

    /// <summary>
    /// The 409 body must be a <see cref="ProblemDetails"/> with <c>Type</c>, <c>Title</c> and
    /// <c>Status</c> all populated, carrying the conflicts as the <c>conflicts</c> extension.
    /// Those three fields are exactly what Umbraco's backoffice response interceptor checks
    /// (<c>isProblemDetailsLike</c>) before deciding whether to keep the body: without them it
    /// replaces the body with a generic error, the conflicts are lost, and the conflict dialog
    /// can never open. Building the body from an exception, rather than in the controller, is
    /// exactly the sort of change that could drop <c>conflicts</c> from <c>Extensions</c>.
    /// </summary>
    [Fact]
    public async Task BatchSave_Conflict_409BodySurvivesUmbracoInterceptor()
    {
        var service = Substitute.For<IAdvancedPermissionService>();
        SaveManyConflictsWith(service, ConflictFor(NodeA, Stored("Umb.Document.Read", PermissionState.Deny)));
        var controller = BuildController(service);
        var request = new BatchSavePermissionsRequestModel(
            [new BatchSavePermissionsNode(NodeA, "editors", [], PermissionStamp.Compute([]))]);

        var result = await controller.BatchSavePermissions(request, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(conflict.Value);
        Assert.False(string.IsNullOrWhiteSpace(problem.Type));
        Assert.False(string.IsNullOrWhiteSpace(problem.Title));
        Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        Assert.True(problem.Extensions.ContainsKey("conflicts"));
        Assert.Single(ConflictsOf(conflict));
    }

    /// <summary>
    /// Serialised the way the Management API writes it, the 409 body carries <c>type</c>,
    /// <c>title</c> and <c>status</c> at the top level alongside <c>conflicts</c>. That flat shape
    /// is what Umbraco's <c>isProblemDetailsLike</c> check inspects; if <c>conflicts</c> were
    /// nested or the three fields went missing, the interceptor would replace the body and the
    /// conflict dialog could never open. Each conflict keeps its <c>nodeKey</c>, <c>roleAlias</c>,
    /// <c>currentEntries</c> and <c>currentStamp</c> — the client is already written against them.
    /// </summary>
    [Fact]
    public async Task BatchSave_Conflict_409JsonIsFlatProblemDetailsWithConflicts()
    {
        var service = Substitute.For<IAdvancedPermissionService>();
        SaveManyConflictsWith(service, ConflictFor(NodeA, Stored("Umb.Document.Read", PermissionState.Deny)));
        var controller = BuildController(service);
        var request = new BatchSavePermissionsRequestModel(
            [new BatchSavePermissionsNode(NodeA, "editors", [], PermissionStamp.Compute([]))]);

        var result = await controller.BatchSavePermissions(request, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var json = JsonSerializer.SerializeToElement(conflict.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(JsonValueKind.String, json.GetProperty("type").ValueKind);
        Assert.Equal(JsonValueKind.String, json.GetProperty("title").ValueKind);
        Assert.Equal(StatusCodes.Status409Conflict, json.GetProperty("status").GetInt32());
        var conflicts = json.GetProperty("conflicts");
        Assert.Equal(JsonValueKind.Array, conflicts.ValueKind);
        Assert.Equal(NodeA, conflicts[0].GetProperty("nodeKey").GetGuid());
        Assert.Equal("editors", conflicts[0].GetProperty("roleAlias").GetString());
        Assert.Equal("Deny", conflicts[0].GetProperty("currentEntries")[0].GetProperty("state").GetString());
        Assert.Equal(
            PermissionStamp.Compute([Stored("Umb.Document.Read", PermissionState.Deny)]),
            conflicts[0].GetProperty("currentStamp").GetString());
    }

    /// <summary>The single-pair save forwards its stamp to the service instead of checking it itself.</summary>
    [Fact]
    public async Task SavePermissions_ForwardsExpectedStampAndReadsNothing()
    {
        var service = Substitute.For<IAdvancedPermissionService>();
        var controller = BuildController(service);
        var request = new SavePermissionsRequestModel(NodeA, "editors", [], "the-stamp");

        var result = await controller.SavePermissions(request, CancellationToken.None);

        Assert.IsType<OkResult>(result);
        await service.Received(1).SaveEntriesAsync(
            NodeA, "editors", Arg.Any<IEnumerable<(string, PermissionState, PermissionScope, bool)>>(), "the-stamp", Arg.Any<CancellationToken>());
        await service.DidNotReceiveWithAnyArgs().GetEntriesAsync(default, default!, default);
    }

    /// <summary>Force on the single-pair save withholds the stamp, so the write skips the check.</summary>
    [Fact]
    public async Task SavePermissions_Force_ForwardsNoStamp()
    {
        var service = Substitute.For<IAdvancedPermissionService>();
        var controller = BuildController(service);
        var request = new SavePermissionsRequestModel(NodeA, "editors", [], "the-stamp", Force: true);

        await controller.SavePermissions(request, CancellationToken.None);

        await service.Received(1).SaveEntriesAsync(
            NodeA, "editors", Arg.Any<IEnumerable<(string, PermissionState, PermissionScope, bool)>>(), null, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The single-pair save returns the same interceptor-safe ProblemDetails shape as the batch
    /// endpoint when the write reports a conflict, with <c>Type</c>, <c>Title</c> and <c>Status</c>
    /// populated, because otherwise Umbraco replaces the body and the conflict dialog can never open.
    /// </summary>
    [Fact]
    public async Task SavePermissions_Conflict_409BodySurvivesUmbracoInterceptor()
    {
        var service = Substitute.For<IAdvancedPermissionService>();
        service
            .SaveEntriesAsync(
                Arg.Any<Guid>(),
                Arg.Any<string>(),
                Arg.Any<IEnumerable<(string, PermissionState, PermissionScope, bool)>>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new PermissionConcurrencyException(
                [ConflictFor(NodeA, Stored("Umb.Document.Read", PermissionState.Deny))])));
        var controller = BuildController(service);
        var request = new SavePermissionsRequestModel(NodeA, "editors", [], PermissionStamp.Compute([]));

        var result = await controller.SavePermissions(request, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(conflict.Value);
        Assert.False(string.IsNullOrWhiteSpace(problem.Type));
        Assert.False(string.IsNullOrWhiteSpace(problem.Title));
        Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
        var conflicts = ConflictsOf(conflict);
        Assert.Single(conflicts);
        Assert.Equal(NodeA, conflicts[0].NodeKey);
        Assert.Equal("Deny", conflicts[0].CurrentEntries[0].State);
    }

    /// <summary>Extracts the conflict list from the <c>conflicts</c> extension of a 409 ProblemDetails.</summary>
    /// <param name="conflict">The 409 result.</param>
    /// <returns>The conflicts.</returns>
    private static IReadOnlyList<BatchSaveConflict> ConflictsOf(ConflictObjectResult conflict)
    {
        var problem = Assert.IsType<ProblemDetails>(conflict.Value);
        return Assert.IsAssignableFrom<IReadOnlyList<BatchSaveConflict>>(problem.Extensions["conflicts"]);
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
