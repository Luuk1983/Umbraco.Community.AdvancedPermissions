using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Umbraco.Community.AdvancedPermissions.Controllers.Models;
using Umbraco.Community.AdvancedPermissions.Core.Concurrency;
using Umbraco.Community.AdvancedPermissions.Core.Exceptions;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// The concurrency contract of the node-keyed save endpoints, written once and run against every
/// controller that exposes it - content and library element permissions today.
/// </summary>
/// <remarks>
/// <para>
/// The check itself does not live in the controller. It lives in the repository's write transaction,
/// because a check made in a controller and a write made afterwards are two operations, and two saves
/// carrying the same stamp could both pass the first. These tests therefore stand a substitute service
/// in for the write and drive the conflict by having it throw - the repository's own tests prove it
/// throws when it should. What is pinned here is the seam: stamps travel to the service, <c>force</c>
/// withholds them, the controller reads nothing itself, and the exception becomes a body Umbraco's
/// interceptor keeps.
/// </para>
/// <para>
/// An abstract base rather than a copy per controller because the two controllers share one
/// implementation (<c>PermissionSaveRequests</c>): a test that ran against only one could pass while
/// the other silently lost its protection.
/// </para>
/// </remarks>
/// <typeparam name="TService">The service contract the controller under test writes through.</typeparam>
public abstract class NodeBatchSaveTestsBase<TService>
    where TService : class, INodePermissionService
{
    /// <summary>The node most tests save against.</summary>
    private static readonly Guid NodeA = Guid.Parse("55555555-5555-5555-5555-555555555555");

    /// <summary>A second node, for tests that need two pairs.</summary>
    private static readonly Guid NodeB = Guid.Parse("66666666-6666-6666-6666-666666666666");

    /// <summary>Gets a verb the controller under test accepts.</summary>
    protected abstract string ValidVerb { get; }

    /// <summary>Gets a second, different verb the controller under test accepts.</summary>
    protected abstract string OtherValidVerb { get; }

    /// <summary>Gets a verb that belongs to another family and must be refused.</summary>
    protected abstract string ForeignVerb { get; }

    /// <summary>
    /// Invokes the single-pair save endpoint of the controller under test.
    /// </summary>
    /// <param name="service">The service substitute the controller writes through.</param>
    /// <param name="request">The request to send.</param>
    /// <returns>The action result.</returns>
    protected abstract Task<IActionResult> SaveAsync(TService service, SavePermissionsRequestModel request);

    /// <summary>
    /// Invokes the batch save endpoint of the controller under test.
    /// </summary>
    /// <param name="service">The service substitute the controller writes through.</param>
    /// <param name="request">The request to send.</param>
    /// <returns>The action result.</returns>
    protected abstract Task<IActionResult> BatchSaveAsync(TService service, BatchSavePermissionsRequestModel request);

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
    private static void SaveManyConflictsWith(TService service, params PermissionConflict[] conflicts) =>
        service
            .SaveManyAsync(Arg.Any<IReadOnlyList<(Guid, string, IReadOnlyList<(string, PermissionState, PermissionScope, bool)>, string?)>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new PermissionConcurrencyException(conflicts)));

    /// <summary>
    /// Reads back the expected stamps the controller forwarded on its one batch write, in batch order.
    /// </summary>
    /// <param name="service">The substitute service.</param>
    /// <returns>The forwarded stamps, one per pair.</returns>
    private static IReadOnlyList<string?> ForwardedBatchStamps(TService service)
    {
        var call = Assert.Single(
            service.ReceivedCalls(),
            c => c.GetMethodInfo().Name == nameof(INodePermissionService.SaveManyAsync));
        var batch = Assert.IsAssignableFrom<IReadOnlyList<(Guid NodeKey, string RoleAlias, IReadOnlyList<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries, string? ExpectedStamp)>>(
            call.GetArguments()[0]);
        return batch.Select(b => b.ExpectedStamp).ToList();
    }

    /// <summary>Asserts that no batch write reached the service.</summary>
    /// <param name="service">The substitute service.</param>
    private static void AssertNoBatchWrite(TService service) =>
        Assert.DoesNotContain(service.ReceivedCalls(), c => c.GetMethodInfo().Name == nameof(INodePermissionService.SaveManyAsync));

    /// <summary>Extracts the conflict list from the <c>conflicts</c> extension of a 409 ProblemDetails.</summary>
    /// <param name="conflict">The 409 result.</param>
    /// <returns>The conflicts.</returns>
    private static IReadOnlyList<BatchSaveConflict> ConflictsOf(ConflictObjectResult conflict)
    {
        var problem = Assert.IsType<ProblemDetails>(conflict.Value);
        return Assert.IsAssignableFrom<IReadOnlyList<BatchSaveConflict>>(problem.Extensions["conflicts"]);
    }

    /// <summary>A save with a stamp writes, and the stamp reaches the service with the batch.</summary>
    [Fact]
    public async Task BatchSave_ForwardsExpectedStampsToTheService()
    {
        var service = Substitute.For<TService>();
        var request = new BatchSavePermissionsRequestModel(
        [
            new BatchSavePermissionsNode(NodeA, "editors", [new SavePermissionEntryItem(ValidVerb, "Allow", "ThisNodeOnly")], "stamp-a"),
            new BatchSavePermissionsNode(NodeB, "editors", [], ExpectedStamp: null),
        ]);

        var result = await BatchSaveAsync(service, request);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(["stamp-a", null], ForwardedBatchStamps(service));
    }

    /// <summary>
    /// The controller performs no check of its own. Reading the stored entries first would be the racy
    /// pre-check this design replaced, and leaving it in "as a first pass" would make it look as though
    /// the check still lived here.
    /// </summary>
    [Fact]
    public async Task BatchSave_DoesNotReadStoredEntriesItself()
    {
        var service = Substitute.For<TService>();
        var request = new BatchSavePermissionsRequestModel(
            [new BatchSavePermissionsNode(NodeA, "editors", [], "some-stamp")]);

        await BatchSaveAsync(service, request);

        await service.DidNotReceiveWithAnyArgs().GetEntriesAsync(default, default!, default);
    }

    /// <summary>The success response carries each pair's new stamp, computed from what was written.</summary>
    [Fact]
    public async Task BatchSave_Success_ReturnsTheNewStampPerPair()
    {
        var service = Substitute.For<TService>();
        var request = new BatchSavePermissionsRequestModel(
            [new BatchSavePermissionsNode(NodeA, "editors", [new SavePermissionEntryItem(ValidVerb, "Deny", "ThisNodeOnly")], "old")]);

        var result = await BatchSaveAsync(service, request);

        var saved = Assert.IsAssignableFrom<IReadOnlyList<BatchSavedStamp>>(Assert.IsType<OkObjectResult>(result).Value);
        var only = Assert.Single(saved);
        Assert.Equal(NodeA, only.NodeKey);
        Assert.Equal(PermissionStamp.Compute([Stored(ValidVerb, PermissionState.Deny)]), only.Stamp);
    }

    /// <summary>A stale stamp - the write reports a conflict - is refused with 409 carrying what is stored now.</summary>
    [Fact]
    public async Task BatchSave_WriteReportsConflict_Returns409WithCurrentEntries()
    {
        var service = Substitute.For<TService>();
        SaveManyConflictsWith(service, ConflictFor(NodeA, Stored(ValidVerb, PermissionState.Deny)));
        var request = new BatchSavePermissionsRequestModel(
        [
            new BatchSavePermissionsNode(
                NodeA,
                "editors",
                [new SavePermissionEntryItem(OtherValidVerb, "Allow", "ThisNodeOnly")],
                PermissionStamp.Compute([Stored(ValidVerb)])),
        ]);

        var result = await BatchSaveAsync(service, request);

        var conflicts = ConflictsOf(Assert.IsType<ConflictObjectResult>(result));
        Assert.Single(conflicts);
        Assert.Equal(NodeA, conflicts[0].NodeKey);
        Assert.Equal("editors", conflicts[0].RoleAlias);
        Assert.Single(conflicts[0].CurrentEntries);
        Assert.Equal("Deny", conflicts[0].CurrentEntries[0].State);
        Assert.Equal(PermissionStamp.Compute([Stored(ValidVerb, PermissionState.Deny)]), conflicts[0].CurrentStamp);
    }

    /// <summary>
    /// Every conflicted pair the write reports reaches the client, in the order reported. The conflict
    /// dialog lists them all so the person resolves them in one pass, not one save at a time.
    /// </summary>
    [Fact]
    public async Task BatchSave_WriteReportsSeveralConflicts_ReturnsAllOfThem()
    {
        var service = Substitute.For<TService>();
        SaveManyConflictsWith(
            service,
            ConflictFor(NodeA, Stored(ValidVerb, PermissionState.Deny)),
            ConflictFor(NodeB));
        var request = new BatchSavePermissionsRequestModel(
        [
            new BatchSavePermissionsNode(NodeA, "editors", [], "stale"),
            new BatchSavePermissionsNode(NodeB, "editors", [], "stale"),
        ]);

        var result = await BatchSaveAsync(service, request);

        var conflicts = ConflictsOf(Assert.IsType<ConflictObjectResult>(result));
        Assert.Equal([NodeA, NodeB], conflicts.Select(c => c.NodeKey));
        Assert.Empty(conflicts[1].CurrentEntries);
    }

    /// <summary>Force withholds every stamp, so the write skips the check and overwrites whatever is stored.</summary>
    [Fact]
    public async Task BatchSave_Force_ForwardsNoStamps()
    {
        var service = Substitute.For<TService>();
        var request = new BatchSavePermissionsRequestModel(
            [new BatchSavePermissionsNode(NodeA, "editors", [], "a-stamp-that-does-not-match")],
            Force: true);

        var result = await BatchSaveAsync(service, request);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal<string?>([null], ForwardedBatchStamps(service));
    }

    /// <summary>A null stamp means an older client, which keeps its previous behaviour: no check, a plain write.</summary>
    [Fact]
    public async Task BatchSave_NullStamp_ForwardsNoStamp()
    {
        var service = Substitute.For<TService>();
        var request = new BatchSavePermissionsRequestModel(
            [new BatchSavePermissionsNode(NodeA, "editors", [], ExpectedStamp: null)]);

        var result = await BatchSaveAsync(service, request);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal<string?>([null], ForwardedBatchStamps(service));
    }

    /// <summary>An unrecognised verb is rejected as a bad request, before any write is attempted.</summary>
    [Fact]
    public async Task BatchSave_InvalidVerb_ReturnsBadRequest()
    {
        var service = Substitute.For<TService>();
        var request = new BatchSavePermissionsRequestModel(
            [new BatchSavePermissionsNode(NodeA, "editors", [new SavePermissionEntryItem("Not.A.Real.Verb", "Allow", "ThisNodeOnly")])]);

        var result = await BatchSaveAsync(service, request);

        Assert.IsType<BadRequestObjectResult>(result);
        AssertNoBatchWrite(service);
    }

    /// <summary>
    /// A verb that is valid for the other family is still invalid here: each controller accepts only
    /// its own family's verbs, so the shared validation must be handed the right set.
    /// </summary>
    [Fact]
    public async Task BatchSave_VerbFromAnotherFamily_ReturnsBadRequest()
    {
        var service = Substitute.For<TService>();
        var request = new BatchSavePermissionsRequestModel(
            [new BatchSavePermissionsNode(NodeA, "editors", [new SavePermissionEntryItem(ForeignVerb, "Allow", "ThisNodeOnly")])]);

        var result = await BatchSaveAsync(service, request);

        Assert.IsType<BadRequestObjectResult>(result);
        AssertNoBatchWrite(service);
    }

    /// <summary>An unrecognised state is a bad request, not a conflict.</summary>
    [Fact]
    public async Task BatchSave_InvalidState_ReturnsBadRequest()
    {
        var service = Substitute.For<TService>();
        var request = new BatchSavePermissionsRequestModel(
            [new BatchSavePermissionsNode(NodeA, "editors", [new SavePermissionEntryItem(ValidVerb, "Maybe", "ThisNodeOnly")])]);

        var result = await BatchSaveAsync(service, request);

        Assert.IsType<BadRequestObjectResult>(result);
        AssertNoBatchWrite(service);
    }

    /// <summary>
    /// Two entries in the same batch for the same node and role are a malformed request - not a
    /// conflict, and not something that should reach the repository's last-resort
    /// <see cref="ArgumentException"/> guard and surface as an unhandled 500. Refused before any write,
    /// and so before any stamp work.
    /// </summary>
    [Fact]
    public async Task BatchSave_DuplicateNodeAndRolePair_ReturnsBadRequest()
    {
        var service = Substitute.For<TService>();
        var request = new BatchSavePermissionsRequestModel(
        [
            new BatchSavePermissionsNode(NodeA, "editors", [], "stamp"),
            new BatchSavePermissionsNode(NodeA, "editors", [], "stamp"),
        ]);

        var result = await BatchSaveAsync(service, request);

        Assert.IsType<BadRequestObjectResult>(result);
        AssertNoBatchWrite(service);
    }

    /// <summary>
    /// The same node with a different user group is two legitimate pairs, and the same user group on a
    /// different node likewise: only the pair as a whole must be unique.
    /// </summary>
    [Fact]
    public async Task BatchSave_SameNodeDifferentRole_IsNotADuplicate()
    {
        var service = Substitute.For<TService>();
        var request = new BatchSavePermissionsRequestModel(
        [
            new BatchSavePermissionsNode(NodeA, "editors", []),
            new BatchSavePermissionsNode(NodeA, "writers", []),
        ]);

        var result = await BatchSaveAsync(service, request);

        Assert.IsType<OkObjectResult>(result);
    }

    /// <summary>
    /// The 409 body must be a <see cref="ProblemDetails"/> with <c>Type</c>, <c>Title</c> and
    /// <c>Status</c> all populated, carrying the conflicts as the <c>conflicts</c> extension. Those
    /// three fields are exactly what Umbraco's backoffice response interceptor checks
    /// (<c>isProblemDetailsLike</c>) before deciding whether to keep the body: without them it replaces
    /// the body with a generic error, the conflicts are lost, and the conflict dialog can never open.
    /// </summary>
    [Fact]
    public async Task BatchSave_Conflict_409BodySurvivesUmbracoInterceptor()
    {
        var service = Substitute.For<TService>();
        SaveManyConflictsWith(service, ConflictFor(NodeA, Stored(ValidVerb, PermissionState.Deny)));
        var request = new BatchSavePermissionsRequestModel(
            [new BatchSavePermissionsNode(NodeA, "editors", [], PermissionStamp.Compute([]))]);

        var result = await BatchSaveAsync(service, request);

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
    /// Serialised the way the Management API writes it, the 409 body carries <c>type</c>, <c>title</c>
    /// and <c>status</c> at the top level alongside <c>conflicts</c>. That flat shape is what Umbraco's
    /// <c>isProblemDetailsLike</c> check inspects; if <c>conflicts</c> were nested or the three fields
    /// went missing, the interceptor would replace the body and the conflict dialog could never open.
    /// Each conflict keeps its <c>nodeKey</c>, <c>roleAlias</c>, <c>currentEntries</c> and
    /// <c>currentStamp</c> - the client is written against them.
    /// </summary>
    [Fact]
    public async Task BatchSave_Conflict_409JsonIsFlatProblemDetailsWithConflicts()
    {
        var service = Substitute.For<TService>();
        SaveManyConflictsWith(service, ConflictFor(NodeA, Stored(ValidVerb, PermissionState.Deny)));
        var request = new BatchSavePermissionsRequestModel(
            [new BatchSavePermissionsNode(NodeA, "editors", [], PermissionStamp.Compute([]))]);

        var result = await BatchSaveAsync(service, request);

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
            PermissionStamp.Compute([Stored(ValidVerb, PermissionState.Deny)]),
            conflicts[0].GetProperty("currentStamp").GetString());
    }

    /// <summary>The single-pair save forwards its stamp to the service instead of checking it itself.</summary>
    [Fact]
    public async Task Save_ForwardsExpectedStampAndReadsNothing()
    {
        var service = Substitute.For<TService>();
        var request = new SavePermissionsRequestModel(NodeA, "editors", [], "the-stamp");

        var result = await SaveAsync(service, request);

        Assert.IsType<OkResult>(result);
        await service.Received(1).SaveEntriesAsync(
            NodeA, "editors", Arg.Any<IEnumerable<(string, PermissionState, PermissionScope, bool)>>(), "the-stamp", Arg.Any<CancellationToken>());
        await service.DidNotReceiveWithAnyArgs().GetEntriesAsync(default, default!, default);
    }

    /// <summary>Force on the single-pair save withholds the stamp, so the write skips the check.</summary>
    [Fact]
    public async Task Save_Force_ForwardsNoStamp()
    {
        var service = Substitute.For<TService>();
        var request = new SavePermissionsRequestModel(NodeA, "editors", [], "the-stamp", Force: true);

        await SaveAsync(service, request);

        await service.Received(1).SaveEntriesAsync(
            NodeA, "editors", Arg.Any<IEnumerable<(string, PermissionState, PermissionScope, bool)>>(), null, Arg.Any<CancellationToken>());
    }

    /// <summary>A request that omits the stamp - a caller written before stamps existed - is written without a check.</summary>
    [Fact]
    public async Task Save_NullStamp_ForwardsNoStamp()
    {
        var service = Substitute.For<TService>();
        var request = new SavePermissionsRequestModel(NodeA, "editors", []);

        await SaveAsync(service, request);

        await service.Received(1).SaveEntriesAsync(
            NodeA, "editors", Arg.Any<IEnumerable<(string, PermissionState, PermissionScope, bool)>>(), null, Arg.Any<CancellationToken>());
    }

    /// <summary>An unrecognised verb on the single save is a bad request, and nothing is written.</summary>
    [Fact]
    public async Task Save_InvalidVerb_ReturnsBadRequestWithoutWriting()
    {
        var service = Substitute.For<TService>();
        var request = new SavePermissionsRequestModel(
            NodeA, "editors", [new SavePermissionEntryItem(ForeignVerb, "Allow", "ThisNodeOnly")], "the-stamp");

        var result = await SaveAsync(service, request);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.DoesNotContain(service.ReceivedCalls(), c => c.GetMethodInfo().Name == nameof(INodePermissionService.SaveEntriesAsync));
    }

    /// <summary>
    /// The single-pair save returns the same interceptor-safe ProblemDetails shape as the batch endpoint
    /// when the write reports a conflict, with <c>Type</c>, <c>Title</c> and <c>Status</c> populated,
    /// because otherwise Umbraco replaces the body and the conflict dialog can never open.
    /// </summary>
    [Fact]
    public async Task Save_Conflict_409BodySurvivesUmbracoInterceptor()
    {
        var service = Substitute.For<TService>();
        service
            .SaveEntriesAsync(
                Arg.Any<Guid>(),
                Arg.Any<string>(),
                Arg.Any<IEnumerable<(string, PermissionState, PermissionScope, bool)>>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new PermissionConcurrencyException(
                [ConflictFor(NodeA, Stored(ValidVerb, PermissionState.Deny))])));
        var request = new SavePermissionsRequestModel(NodeA, "editors", [], PermissionStamp.Compute([]));

        var result = await SaveAsync(service, request);

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
}
