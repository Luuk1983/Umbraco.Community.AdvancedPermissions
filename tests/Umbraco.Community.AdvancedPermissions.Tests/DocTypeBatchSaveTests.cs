using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Umbraco.Community.AdvancedPermissions.Controllers;
using Umbraco.Community.AdvancedPermissions.Controllers.Models;
using Umbraco.Community.AdvancedPermissions.Core.Concurrency;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Exceptions;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests the doc-type batch save endpoint's handling of the concurrency check: what it forwards to
/// the service, and how the <see cref="DocTypePermissionConcurrencyException"/> the write raises
/// becomes the 409 the client is written against.
/// </summary>
/// <remarks>
/// Mirrors <see cref="BatchSavePermissionsTests"/>, including its reason for existing in this shape:
/// the check moved into the repository's write transaction, so these tests drive the conflict by
/// having a substitute service throw. The one structural difference is that every key is a triple —
/// node, user group AND document type — rather than a pair. The multi-conflict test exists
/// specifically to catch a two-part key used where the three-part triple belongs: two conflicts
/// sharing a node and role but differing only by content type must both survive to the response,
/// each carrying its own <c>contentTypeKey</c>.
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

    /// <summary>Builds the conflict a repository would report for a triple whose entries moved.</summary>
    /// <param name="contentTypeKey">The conflicted content type.</param>
    /// <param name="current">What is stored now.</param>
    /// <returns>The conflict, carrying the stamp of <paramref name="current"/>.</returns>
    private static DocTypePermissionConflict ConflictFor(Guid contentTypeKey, params DocTypePermissionEntry[] current) =>
        new(NodeA, "editors", contentTypeKey, current, PermissionStamp.ComputeForDocType(current));

    /// <summary>
    /// Makes every batch write on the substitute fail the way the repository fails when a triple's
    /// stored entries no longer match its expected stamp.
    /// </summary>
    /// <param name="service">The substitute service.</param>
    /// <param name="conflicts">The conflicts to report.</param>
    private static void SaveManyConflictsWith(IDocTypePermissionService service, params DocTypePermissionConflict[] conflicts) =>
        service
            .SaveManyAsync(Arg.Any<IReadOnlyList<(Guid, string, Guid, IReadOnlyList<(string, PermissionState, PermissionScope, bool)>, string?)>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new DocTypePermissionConcurrencyException(conflicts)));

    /// <summary>
    /// Reads back the expected stamps the controller forwarded on its one batch write, in batch order.
    /// </summary>
    /// <param name="service">The substitute service.</param>
    /// <returns>The forwarded stamps, one per triple.</returns>
    private static IReadOnlyList<string?> ForwardedBatchStamps(IDocTypePermissionService service)
    {
        var call = Assert.Single(
            service.ReceivedCalls(),
            c => c.GetMethodInfo().Name == nameof(IDocTypePermissionService.SaveManyAsync));
        var batch = Assert.IsAssignableFrom<IReadOnlyList<(Guid NodeKey, string RoleAlias, Guid ContentTypeKey, IReadOnlyList<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries, string? ExpectedStamp)>>(
            call.GetArguments()[0]);
        return batch.Select(b => b.ExpectedStamp).ToList();
    }

    /// <summary>A save with stamps writes, and each triple's stamp reaches the service with the batch.</summary>
    [Fact]
    public async Task BatchSave_ForwardsExpectedStampsToTheService()
    {
        var service = Substitute.For<IDocTypePermissionService>();
        var controller = BuildController(service);
        var request = new BatchSaveDocTypePermissionsRequestModel(
        [
            new BatchSaveDocTypePermissionsNode(
                NodeA,
                "editors",
                ContentTypeA,
                [new SavePermissionEntryItem(AdvancedPermissionsConstants.VerbCreateOfType, "Deny", "ThisNodeOnly")],
                "stamp-a"),
            new BatchSaveDocTypePermissionsNode(NodeA, "editors", ContentTypeB, [], ExpectedStamp: null),
        ]);

        var result = await controller.BatchSaveDocTypePermissions(request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(["stamp-a", null], ForwardedBatchStamps(service));
    }

    /// <summary>
    /// The controller performs no check of its own. Reading the stored entries first would be the
    /// racy pre-check this design replaced.
    /// </summary>
    [Fact]
    public async Task BatchSave_DoesNotReadStoredEntriesItself()
    {
        var service = Substitute.For<IDocTypePermissionService>();
        var controller = BuildController(service);
        var request = new BatchSaveDocTypePermissionsRequestModel(
            [new BatchSaveDocTypePermissionsNode(NodeA, "editors", ContentTypeA, [], "some-stamp")]);

        await controller.BatchSaveDocTypePermissions(request, CancellationToken.None);

        await service.DidNotReceiveWithAnyArgs().GetEditorEntriesAsync(default!, default, default);
    }

    /// <summary>A stale stamp — the write reports a conflict — is refused with 409 carrying what is stored now.</summary>
    [Fact]
    public async Task BatchSave_WriteReportsConflict_Returns409WithCurrentEntries()
    {
        var service = Substitute.For<IDocTypePermissionService>();
        SaveManyConflictsWith(service, ConflictFor(ContentTypeA, Stored(NodeA, ContentTypeA, PermissionState.Deny)));
        var controller = BuildController(service);
        var request = new BatchSaveDocTypePermissionsRequestModel(
        [
            new BatchSaveDocTypePermissionsNode(
                NodeA,
                "editors",
                ContentTypeA,
                [new SavePermissionEntryItem(AdvancedPermissionsConstants.VerbCreateOfType, "Allow", "ThisNodeOnly")],
                PermissionStamp.ComputeForDocType([Stored(NodeA, ContentTypeA)])),
        ]);

        var result = await controller.BatchSaveDocTypePermissions(request, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var conflicts = ConflictsOf(conflict);
        Assert.Single(conflicts);
        Assert.Equal(NodeA, conflicts[0].NodeKey);
        Assert.Equal(ContentTypeA, conflicts[0].ContentTypeKey);
        Assert.Single(conflicts[0].CurrentEntries);
        Assert.Equal("Deny", conflicts[0].CurrentEntries[0].State);
        Assert.Equal(
            PermissionStamp.ComputeForDocType([Stored(NodeA, ContentTypeA, PermissionState.Deny)]),
            conflicts[0].CurrentStamp);
    }

    /// <summary>
    /// Two conflicted triples sharing a node and role but differing only by content type both reach
    /// the client, each under its own content type. If the content type were dropped from the
    /// exception-to-response mapping, both would read as the same triple.
    /// </summary>
    [Fact]
    public async Task BatchSave_WriteReportsConflictsForTwoContentTypes_ReturnsBothAsDistinctTriples()
    {
        var service = Substitute.For<IDocTypePermissionService>();
        SaveManyConflictsWith(
            service,
            ConflictFor(ContentTypeA, Stored(NodeA, ContentTypeA, PermissionState.Deny)),
            ConflictFor(ContentTypeB));
        var controller = BuildController(service);
        var request = new BatchSaveDocTypePermissionsRequestModel(
        [
            new BatchSaveDocTypePermissionsNode(NodeA, "editors", ContentTypeA, [], "stale"),
            new BatchSaveDocTypePermissionsNode(NodeA, "editors", ContentTypeB, [], "stale"),
        ]);

        var result = await controller.BatchSaveDocTypePermissions(request, CancellationToken.None);

        var conflicts = ConflictsOf(Assert.IsType<ConflictObjectResult>(result));
        Assert.Equal([ContentTypeA, ContentTypeB], conflicts.Select(c => c.ContentTypeKey));
        Assert.Single(conflicts[0].CurrentEntries);
        Assert.Empty(conflicts[1].CurrentEntries);
    }

    /// <summary>Force withholds every stamp, so the write skips the check and overwrites whatever is stored.</summary>
    [Fact]
    public async Task BatchSave_Force_ForwardsNoStamps()
    {
        var service = Substitute.For<IDocTypePermissionService>();
        var controller = BuildController(service);
        var request = new BatchSaveDocTypePermissionsRequestModel(
            [new BatchSaveDocTypePermissionsNode(NodeA, "editors", ContentTypeA, [], "a-stamp-that-does-not-match")],
            Force: true);

        var result = await controller.BatchSaveDocTypePermissions(request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal<string?>([null], ForwardedBatchStamps(service));
    }

    /// <summary>A null stamp means an older client, which keeps its previous behaviour: no check, a plain write.</summary>
    [Fact]
    public async Task BatchSave_NullStamp_ForwardsNoStamp()
    {
        var service = Substitute.For<IDocTypePermissionService>();
        var controller = BuildController(service);
        var request = new BatchSaveDocTypePermissionsRequestModel(
            [new BatchSaveDocTypePermissionsNode(NodeA, "editors", ContentTypeA, [], ExpectedStamp: null)]);

        var result = await controller.BatchSaveDocTypePermissions(request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal<string?>([null], ForwardedBatchStamps(service));
    }

    /// <summary>An unrecognised verb is still rejected as a bad request, before any write is attempted.</summary>
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
        Assert.DoesNotContain(service.ReceivedCalls(), c => c.GetMethodInfo().Name == nameof(IDocTypePermissionService.SaveManyAsync));
    }

    /// <summary>
    /// The 409 body must be a <see cref="ProblemDetails"/> with <c>Type</c>, <c>Title</c> and
    /// <c>Status</c> all populated, carrying the conflicts as the <c>conflicts</c> extension.
    /// Those three fields are exactly what Umbraco's backoffice response interceptor checks
    /// (<c>isProblemDetailsLike</c>) before deciding whether to keep the body: without them it
    /// replaces the body with a generic error, the conflicts are lost, and the conflict dialog
    /// can never open.
    /// </summary>
    [Fact]
    public async Task BatchSave_Conflict_409BodySurvivesUmbracoInterceptor()
    {
        var service = Substitute.For<IDocTypePermissionService>();
        SaveManyConflictsWith(service, ConflictFor(ContentTypeA, Stored(NodeA, ContentTypeA, PermissionState.Deny)));
        var controller = BuildController(service);
        var request = new BatchSaveDocTypePermissionsRequestModel(
        [
            new BatchSaveDocTypePermissionsNode(NodeA, "editors", ContentTypeA, [], PermissionStamp.ComputeForDocType([])),
        ]);

        var result = await controller.BatchSaveDocTypePermissions(request, CancellationToken.None);

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
    /// <c>title</c> and <c>status</c> at the top level alongside <c>conflicts</c>, and each
    /// conflict keeps the property names the client reads.
    /// </summary>
    [Fact]
    public async Task BatchSave_Conflict_409JsonIsFlatProblemDetailsWithConflicts()
    {
        var service = Substitute.For<IDocTypePermissionService>();
        SaveManyConflictsWith(service, ConflictFor(ContentTypeA, Stored(NodeA, ContentTypeA, PermissionState.Deny)));
        var controller = BuildController(service);
        var request = new BatchSaveDocTypePermissionsRequestModel(
            [new BatchSaveDocTypePermissionsNode(NodeA, "editors", ContentTypeA, [], PermissionStamp.ComputeForDocType([]))]);

        var result = await controller.BatchSaveDocTypePermissions(request, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var json = JsonSerializer.SerializeToElement(conflict.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(JsonValueKind.String, json.GetProperty("type").ValueKind);
        Assert.Equal(JsonValueKind.String, json.GetProperty("title").ValueKind);
        Assert.Equal(StatusCodes.Status409Conflict, json.GetProperty("status").GetInt32());
        var conflicts = json.GetProperty("conflicts");
        Assert.Equal(JsonValueKind.Array, conflicts.ValueKind);
        Assert.Equal(NodeA, conflicts[0].GetProperty("nodeKey").GetGuid());
        Assert.Equal("editors", conflicts[0].GetProperty("roleAlias").GetString());
        Assert.Equal(ContentTypeA, conflicts[0].GetProperty("contentTypeKey").GetGuid());
        Assert.Equal("Deny", conflicts[0].GetProperty("currentEntries")[0].GetProperty("state").GetString());
        Assert.Equal(
            PermissionStamp.ComputeForDocType([Stored(NodeA, ContentTypeA, PermissionState.Deny)]),
            conflicts[0].GetProperty("currentStamp").GetString());
    }

    /// <summary>The single-triple save forwards its stamp to the service instead of checking it itself.</summary>
    [Fact]
    public async Task Save_ForwardsExpectedStampAndReadsNothing()
    {
        var service = Substitute.For<IDocTypePermissionService>();
        var controller = BuildController(service);
        var request = new SaveDocTypePermissionsRequestModel(NodeA, "editors", ContentTypeA, [], "the-stamp");

        var result = await controller.Save(request, CancellationToken.None);

        Assert.IsType<OkResult>(result);
        await service.Received(1).SaveEditorEntriesAsync(
            NodeA, "editors", ContentTypeA, Arg.Any<IEnumerable<(string, PermissionState, PermissionScope, bool)>>(), "the-stamp", Arg.Any<CancellationToken>());
        await service.DidNotReceiveWithAnyArgs().GetEditorEntriesAsync(default!, default, default);
    }

    /// <summary>Force on the single-triple save withholds the stamp, so the write skips the check.</summary>
    [Fact]
    public async Task Save_Force_ForwardsNoStamp()
    {
        var service = Substitute.For<IDocTypePermissionService>();
        var controller = BuildController(service);
        var request = new SaveDocTypePermissionsRequestModel(NodeA, "editors", ContentTypeA, [], "the-stamp", Force: true);

        var result = await controller.Save(request, CancellationToken.None);

        Assert.IsType<OkResult>(result);
        await service.Received(1).SaveEditorEntriesAsync(
            NodeA, "editors", ContentTypeA, Arg.Any<IEnumerable<(string, PermissionState, PermissionScope, bool)>>(), null, Arg.Any<CancellationToken>());
    }

    /// <summary>A request that omits the stamp — a caller written before stamps existed — is written without a check.</summary>
    [Fact]
    public async Task Save_NullStamp_ForwardsNoStamp()
    {
        var service = Substitute.For<IDocTypePermissionService>();
        var controller = BuildController(service);
        var request = new SaveDocTypePermissionsRequestModel(NodeA, "editors", ContentTypeA, []);

        var result = await controller.Save(request, CancellationToken.None);

        Assert.IsType<OkResult>(result);
        await service.Received(1).SaveEditorEntriesAsync(
            NodeA, "editors", ContentTypeA, Arg.Any<IEnumerable<(string, PermissionState, PermissionScope, bool)>>(), null, Arg.Any<CancellationToken>());
    }

    /// <summary>An unrecognised verb is still rejected as a bad request, before any write is attempted.</summary>
    [Fact]
    public async Task Save_InvalidVerb_ReturnsBadRequestWithoutWriting()
    {
        var service = Substitute.For<IDocTypePermissionService>();
        var controller = BuildController(service);
        var request = new SaveDocTypePermissionsRequestModel(
            NodeA, "editors", ContentTypeA, [new SavePermissionEntryItem("Not.A.Real.Verb", "Allow", "ThisNodeOnly")], "the-stamp");

        var result = await controller.Save(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.DoesNotContain(service.ReceivedCalls(), c => c.GetMethodInfo().Name == nameof(IDocTypePermissionService.SaveEditorEntriesAsync));
    }

    /// <summary>
    /// The single-triple save returns the same interceptor-safe ProblemDetails shape as the batch
    /// endpoint when the write reports a conflict, with <c>Type</c>, <c>Title</c> and <c>Status</c>
    /// populated, because otherwise Umbraco replaces the body and the conflict dialog can never open.
    /// </summary>
    [Fact]
    public async Task Save_Conflict_409BodySurvivesUmbracoInterceptor()
    {
        var service = Substitute.For<IDocTypePermissionService>();
        service
            .SaveEditorEntriesAsync(
                Arg.Any<Guid>(),
                Arg.Any<string>(),
                Arg.Any<Guid>(),
                Arg.Any<IEnumerable<(string, PermissionState, PermissionScope, bool)>>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new DocTypePermissionConcurrencyException(
                [ConflictFor(ContentTypeA, Stored(NodeA, ContentTypeA, PermissionState.Deny))])));
        var controller = BuildController(service);
        var request = new SaveDocTypePermissionsRequestModel(NodeA, "editors", ContentTypeA, [], PermissionStamp.ComputeForDocType([]));

        var result = await controller.Save(request, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(conflict.Value);
        Assert.False(string.IsNullOrWhiteSpace(problem.Type));
        Assert.False(string.IsNullOrWhiteSpace(problem.Title));
        Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        var conflicts = ConflictsOf(conflict);
        Assert.Single(conflicts);
        Assert.Equal(NodeA, conflicts[0].NodeKey);
        Assert.Equal(ContentTypeA, conflicts[0].ContentTypeKey);
        Assert.Equal("Deny", conflicts[0].CurrentEntries[0].State);
    }

    /// <summary>
    /// The single-triple 409, serialised the way the Management API writes it, is the same flat
    /// ProblemDetails as the batch endpoint's: <c>type</c>, <c>title</c>, <c>status</c> and
    /// <c>conflicts</c> at the top level, each conflict carrying its <c>contentTypeKey</c>.
    /// </summary>
    [Fact]
    public async Task Save_Conflict_409JsonIsFlatProblemDetailsWithConflicts()
    {
        var service = Substitute.For<IDocTypePermissionService>();
        service
            .SaveEditorEntriesAsync(
                Arg.Any<Guid>(),
                Arg.Any<string>(),
                Arg.Any<Guid>(),
                Arg.Any<IEnumerable<(string, PermissionState, PermissionScope, bool)>>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new DocTypePermissionConcurrencyException(
                [ConflictFor(ContentTypeA, Stored(NodeA, ContentTypeA, PermissionState.Deny))])));
        var controller = BuildController(service);
        var request = new SaveDocTypePermissionsRequestModel(NodeA, "editors", ContentTypeA, [], PermissionStamp.ComputeForDocType([]));

        var result = await controller.Save(request, CancellationToken.None);

        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ConflictObjectResult>(result).Value);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var json = JsonSerializer.SerializeToElement(problem, options);
        Assert.Equal(JsonValueKind.String, json.GetProperty("type").ValueKind);
        Assert.Equal(JsonValueKind.String, json.GetProperty("title").ValueKind);
        Assert.Equal(StatusCodes.Status409Conflict, json.GetProperty("status").GetInt32());
        var conflicts = json.GetProperty("conflicts");
        Assert.Equal(JsonValueKind.Array, conflicts.ValueKind);
        Assert.Equal(NodeA, conflicts[0].GetProperty("nodeKey").GetGuid());
        Assert.Equal("editors", conflicts[0].GetProperty("roleAlias").GetString());
        Assert.Equal(ContentTypeA, conflicts[0].GetProperty("contentTypeKey").GetGuid());
        Assert.Equal("Deny", conflicts[0].GetProperty("currentEntries")[0].GetProperty("state").GetString());
    }

    /// <summary>
    /// Two entries in the same batch for the same node, user group AND document type are a malformed
    /// request - a 400, before any stamp work, never a conflict and never the repository's last-resort
    /// <see cref="ArgumentException"/> surfacing as a 500.
    /// </summary>
    [Fact]
    public async Task BatchSave_DuplicateTriple_ReturnsBadRequestWithoutWriting()
    {
        var service = Substitute.For<IDocTypePermissionService>();
        var controller = BuildController(service);
        var request = new BatchSaveDocTypePermissionsRequestModel(
        [
            new BatchSaveDocTypePermissionsNode(NodeA, "editors", ContentTypeA, [], "stamp"),
            new BatchSaveDocTypePermissionsNode(NodeA, "editors", ContentTypeA, [], "stamp"),
        ]);

        var result = await controller.BatchSaveDocTypePermissions(request, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.DoesNotContain(service.ReceivedCalls(), c => c.GetMethodInfo().Name == nameof(IDocTypePermissionService.SaveManyAsync));
    }

    /// <summary>
    /// The same node and user group with a different document type is two legitimate triples: only the
    /// triple as a whole must be unique, so a key of node and user group alone would wrongly refuse this.
    /// </summary>
    [Fact]
    public async Task BatchSave_SameNodeAndRoleDifferentContentType_IsNotADuplicate()
    {
        var service = Substitute.For<IDocTypePermissionService>();
        var controller = BuildController(service);
        var request = new BatchSaveDocTypePermissionsRequestModel(
        [
            new BatchSaveDocTypePermissionsNode(NodeA, "editors", ContentTypeA, []),
            new BatchSaveDocTypePermissionsNode(NodeA, "editors", ContentTypeB, []),
        ]);

        var result = await controller.BatchSaveDocTypePermissions(request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    /// <summary>
    /// The doc-type endpoints accept both the document-type and the element-type create verbs, because
    /// one editor and one table serve both families.
    /// </summary>
    [Fact]
    public async Task BatchSave_ElementTypeVerb_IsAccepted()
    {
        var service = Substitute.For<IDocTypePermissionService>();
        var controller = BuildController(service);
        var request = new BatchSaveDocTypePermissionsRequestModel(
            [new BatchSaveDocTypePermissionsNode(
                NodeA, "editors", ContentTypeA, [new SavePermissionEntryItem(AdvancedPermissionsConstants.VerbElementCreateOfType, "Allow", "ThisNodeOnly")])]);

        var result = await controller.BatchSaveDocTypePermissions(request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    /// <summary>Extracts the conflict list from the <c>conflicts</c> extension of a 409 ProblemDetails.</summary>
    /// <param name="conflict">The 409 result.</param>
    /// <returns>The conflicts.</returns>
    private static IReadOnlyList<BatchSaveDocTypeConflict> ConflictsOf(ConflictObjectResult conflict)
    {
        var problem = Assert.IsType<ProblemDetails>(conflict.Value);
        return Assert.IsAssignableFrom<IReadOnlyList<BatchSaveDocTypeConflict>>(problem.Extensions["conflicts"]);
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
