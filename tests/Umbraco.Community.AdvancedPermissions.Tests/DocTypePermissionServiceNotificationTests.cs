using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.Caching;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Exceptions;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;
using Umbraco.Community.AdvancedPermissions.Notifications;
using Umbraco.Community.AdvancedPermissions.Services;
using static Umbraco.Community.AdvancedPermissions.Tests.PermissionServiceNotificationTestSupport;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests the write-then-invalidate-then-publish contract of <see cref="DocTypePermissionService"/>,
/// which publishes the notification the live document-type permission editor listens for.
/// </summary>
/// <remarks>
/// <para>
/// Before this class the doc-type service had no notification coverage at all. Every one of these
/// mutations left the whole suite green: dropping <c>InvalidateAllResolved</c> from the single save,
/// dropping the publish from the single save, dropping both invalidations from the batch save, and
/// dropping the batch's publish loop. The last two are the ones that matter, because the batch save is
/// what the editor actually calls - the live update on that screen could be deleted outright and
/// nothing would notice.
/// </para>
/// <para>
/// The rules are the same ones proved for the node-keyed service in
/// <see cref="AdvancedPermissionServiceBatchNotificationTests"/>: publish strictly after the write and
/// every invalidation, isolate a failing handler but let a cancellation through, and do nothing at all
/// for an empty batch or a failed write. The key here has three parts - node, user group and document
/// type - so a notification names the document type as well.
/// </para>
/// </remarks>
public sealed class DocTypePermissionServiceNotificationTests
{
    /// <summary>The user group most tests write for.</summary>
    private const string Editors = "editors";

    /// <summary>The event aggregator substitute the service publishes through.</summary>
    private readonly IEventAggregator _eventAggregator = Substitute.For<IEventAggregator>();

    /// <summary>The logger substitute a swallowed handler failure is written to.</summary>
    private readonly ILogger<DocTypePermissionService> _logger = Substitute.For<ILogger<DocTypePermissionService>>();

    /// <summary>The repository substitute the service writes through.</summary>
    private readonly IDocTypePermissionRepository _repository = Substitute.For<IDocTypePermissionRepository>();

    /// <summary>
    /// Builds the service under test over the given caches.
    /// </summary>
    /// <param name="appCaches">The caches its two-level permission cache is built over.</param>
    /// <returns>The service under test.</returns>
    private DocTypePermissionService CreateService(AppCaches appCaches) =>
        new(
            _repository,
            Substitute.For<IDocTypePermissionResolver>(),
            Substitute.For<IUserService>(),
            new DocTypePermissionCache(appCaches),
            _eventAggregator,
            _logger);

    /// <summary>
    /// Builds one create-of-type entry.
    /// </summary>
    /// <param name="state">The state of the entry.</param>
    /// <returns>The entry tuple in the shape the service accepts.</returns>
    private static (string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride) Entry(PermissionState state) =>
        (AdvancedPermissionsConstants.VerbCreateOfType, state, PermissionScope.ThisNodeOnly, false);

    /// <summary>
    /// An argument matcher for any entry sequence.
    /// </summary>
    /// <returns>A placeholder matching any entries.</returns>
    private static IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> AnyEntries() =>
        Arg.Any<IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)>>();

    /// <summary>
    /// An argument matcher for any repository batch.
    /// </summary>
    /// <returns>A placeholder matching any batch.</returns>
    private static IEnumerable<(Guid NodeKey, string RoleAlias, Guid ContentTypeKey, IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries, string? ExpectedStamp)> AnyBatch() =>
        Arg.Any<IEnumerable<(Guid NodeKey, string RoleAlias, Guid ContentTypeKey, IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries, string? ExpectedStamp)>>();

    /// <summary>
    /// Records a <c>publish</c> marker each time the change notification goes out.
    /// </summary>
    /// <param name="order">The list the marker is appended to.</param>
    private void RecordPublishes(List<string> order) =>
        _eventAggregator
            .When(a => a.PublishAsync(Arg.Any<DocTypePermissionsChangedNotification>(), Arg.Any<CancellationToken>()))
            .Do(_ => order.Add("publish"));

    /// <summary>
    /// Saving a triple publishes a <see cref="DocTypePermissionsChangedNotification"/> naming the node,
    /// user group and document type that were written.
    /// </summary>
    /// <remarks>
    /// Removing the publish from <c>SaveEditorEntriesAsync</c> used to leave the suite green.
    /// </remarks>
    [Fact]
    public async Task SaveEditorEntriesAsync_Publishes_ChangedNotification()
    {
        var sut = CreateService(AppCaches.NoCache);
        var nodeKey = Guid.NewGuid();
        var typeKey = Guid.NewGuid();

        await sut.SaveEditorEntriesAsync(nodeKey, Editors, typeKey, [Entry(PermissionState.Allow)]);

        await _eventAggregator.Received(1).PublishAsync(
            Arg.Is<DocTypePermissionsChangedNotification>(n =>
                n.NodeKey == nodeKey &&
                n.RoleAlias == Editors &&
                n.ContentTypeKeys.Count == 1 &&
                n.ContentTypeKeys[0] == typeKey),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Clearing a triple (an empty entry list) is still a change and must still be announced: a
    /// consumer that never hears about it keeps showing permissions that no longer exist.
    /// </summary>
    [Fact]
    public async Task SaveEditorEntriesAsync_EmptyEntries_StillPublishes()
    {
        var sut = CreateService(AppCaches.NoCache);

        await sut.SaveEditorEntriesAsync(Guid.NewGuid(), Editors, Guid.NewGuid(), []);

        await _eventAggregator.Received(1).PublishAsync(
            Arg.Any<DocTypePermissionsChangedNotification>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The notification is published strictly after the repository write and after both cache
    /// invalidations: the write, the user group's entries, every user's resolved permissions, and only
    /// then the publish.
    /// </summary>
    /// <remarks>
    /// A client that refetches on an event which overtook the invalidation reads the snapshot the
    /// change replaced and, having consumed its one notification, never asks again. Dropping
    /// <c>InvalidateAllResolved</c> from <c>SaveEditorEntriesAsync</c> used to pass every test; it now
    /// removes the <c>invalidate-all</c> marker from the recorded sequence.
    /// </remarks>
    [Fact]
    public async Task SaveEditorEntriesAsync_PublishesAfterWriteAndCacheInvalidation()
    {
        var order = new List<string>();
        _repository
            .When(r => r.SaveAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(), AnyEntries(), Arg.Any<string?>(), Arg.Any<CancellationToken>()))
            .Do(_ => order.Add("write"));
        RecordPublishes(order);
        var sut = CreateService(RecordingCaches(order));

        await sut.SaveEditorEntriesAsync(Guid.NewGuid(), Editors, Guid.NewGuid(), [Entry(PermissionState.Allow)]);

        Assert.Equal(["write", "invalidate-role", "invalidate-all", "publish"], order);
    }

    /// <summary>
    /// A batch publishes one notification per triple, each naming only its own node, user group and
    /// document type, so a handler sees the same granularity it would from single saves.
    /// </summary>
    /// <remarks>
    /// Removing the publish loop from <c>SaveManyAsync</c> - the path the editor actually uses - used to
    /// leave the suite green, silently ending live updates on that screen.
    /// </remarks>
    [Fact]
    public async Task SaveManyAsync_TwoTriples_PublishesTwoNotifications_OnePerTriple()
    {
        var sut = CreateService(AppCaches.NoCache);
        var node1 = Guid.NewGuid();
        var node2 = Guid.NewGuid();
        var type1 = Guid.NewGuid();
        var type2 = Guid.NewGuid();

        await sut.SaveManyAsync(
        [
            (node1, Editors, type1, [Entry(PermissionState.Allow)], null),
            (node2, "writers", type2, [Entry(PermissionState.Deny)], null),
        ]);

        await _eventAggregator.Received(1).PublishAsync(
            Arg.Is<DocTypePermissionsChangedNotification>(n =>
                n.NodeKey == node1 && n.RoleAlias == Editors && n.ContentTypeKeys.Count == 1 && n.ContentTypeKeys[0] == type1),
            Arg.Any<CancellationToken>());
        await _eventAggregator.Received(1).PublishAsync(
            Arg.Is<DocTypePermissionsChangedNotification>(n =>
                n.NodeKey == node2 && n.RoleAlias == "writers" && n.ContentTypeKeys.Count == 1 && n.ContentTypeKeys[0] == type2),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// For a batch, the write and every invalidation happen before the first notification goes out:
    /// the write, one flush per distinct user group, one flush of every resolved permission, and only
    /// then a notification per triple.
    /// </summary>
    /// <remarks>
    /// Fails if either invalidation is dropped (the sequence loses a marker) and if a publish moves
    /// above any invalidation. Dropping both invalidations from <c>SaveManyAsync</c> used to pass.
    /// </remarks>
    [Fact]
    public async Task SaveManyAsync_PublishesOnlyAfterWriteAndEveryInvalidation()
    {
        var order = new List<string>();
        _repository
            .When(r => r.SaveManyAsync(AnyBatch(), Arg.Any<CancellationToken>()))
            .Do(_ => order.Add("write"));
        RecordPublishes(order);
        var sut = CreateService(RecordingCaches(order));

        await sut.SaveManyAsync(
        [
            (Guid.NewGuid(), Editors, Guid.NewGuid(), [Entry(PermissionState.Allow)], null),
            (Guid.NewGuid(), "writers", Guid.NewGuid(), [Entry(PermissionState.Deny)], null),
        ]);

        Assert.Equal(
            ["write", "invalidate-role", "invalidate-role", "invalidate-all", "publish", "publish"],
            order);
    }

    /// <summary>
    /// A batch flushes the resolved-permission cache - every user's - exactly once, however many
    /// triples it writes.
    /// </summary>
    /// <remarks>
    /// Names the failure the ordering test only shows as a different sequence: a stale resolved cache
    /// is what makes a refetch return the permissions the write replaced. Removing
    /// <c>cache.InvalidateAllResolved()</c> from <c>SaveManyAsync</c> used to pass every test.
    /// </remarks>
    [Fact]
    public async Task SaveManyAsync_FlushesResolvedPermissionsOnce_ForTheWholeBatch()
    {
        var runtimeCache = Substitute.For<IAppPolicyCache>();
        var sut = CreateService(AppCachesOver(runtimeCache));

        await sut.SaveManyAsync(
        [
            (Guid.NewGuid(), Editors, Guid.NewGuid(), [Entry(PermissionState.Allow)], null),
            (Guid.NewGuid(), "writers", Guid.NewGuid(), [Entry(PermissionState.Deny)], null),
            (Guid.NewGuid(), "admins", Guid.NewGuid(), [Entry(PermissionState.Deny)], null),
        ]);

        runtimeCache.Received(1).ClearByKey(Arg.Is<string>(k => k.EndsWith(".L2.", StringComparison.Ordinal)));
    }

    /// <summary>
    /// Triples for the same user group flush that group's cached entries once, not once per triple, and
    /// every group in the batch is flushed.
    /// </summary>
    /// <remarks>
    /// Fails if the <c>InvalidateRoleEntries</c> loop is dropped (the next read serves the entries the
    /// write replaced) and if it stops de-duplicating (a group is flushed once per triple).
    /// </remarks>
    [Fact]
    public async Task SaveManyAsync_InvalidatesEachDistinctUserGroupOnce()
    {
        var runtimeCache = Substitute.For<IAppPolicyCache>();
        var sut = CreateService(AppCachesOver(runtimeCache));

        await sut.SaveManyAsync(
        [
            (Guid.NewGuid(), Editors, Guid.NewGuid(), [Entry(PermissionState.Allow)], null),
            (Guid.NewGuid(), Editors, Guid.NewGuid(), [Entry(PermissionState.Deny)], null),
            (Guid.NewGuid(), "writers", Guid.NewGuid(), [Entry(PermissionState.Deny)], null),
        ]);

        runtimeCache.Received(1).Clear(Arg.Is<string>(k => k.EndsWith(".L1.editors", StringComparison.Ordinal)));
        runtimeCache.Received(1).Clear(Arg.Is<string>(k => k.EndsWith(".L1.writers", StringComparison.Ordinal)));
    }

    /// <summary>
    /// By the time the notification is published the write has committed and the caches are
    /// invalidated. A handler that throws must not turn that committed write into a reported failure;
    /// the failure is logged at error level and the save completes.
    /// </summary>
    [Fact]
    public async Task SaveEditorEntriesAsync_HandlerThrows_CompletesAndLogsAnError()
    {
        _eventAggregator
            .PublishAsync(Arg.Any<DocTypePermissionsChangedNotification>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Simulated handler failure.")));
        var sut = CreateService(AppCaches.NoCache);

        await sut.SaveEditorEntriesAsync(Guid.NewGuid(), Editors, Guid.NewGuid(), []);

        Assert.True(LoggedAnError(_logger), "A swallowed handler failure must still be logged at error level.");
    }

    /// <summary>
    /// A handler that throws synchronously, rather than returning a faulted task, is isolated just the
    /// same.
    /// </summary>
    [Fact]
    public async Task SaveEditorEntriesAsync_HandlerThrowsSynchronously_CompletesAndLogsAnError()
    {
        _eventAggregator
            .PublishAsync(Arg.Any<DocTypePermissionsChangedNotification>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Simulated synchronous handler failure."));
        var sut = CreateService(AppCaches.NoCache);

        await sut.SaveEditorEntriesAsync(Guid.NewGuid(), Editors, Guid.NewGuid(), []);

        Assert.True(LoggedAnError(_logger));
    }

    /// <summary>
    /// A cancelled request is not a handler failure and must propagate from a single save rather than
    /// be swallowed.
    /// </summary>
    [Fact]
    public async Task SaveEditorEntriesAsync_PublishCancelled_RethrowsOperationCanceled()
    {
        _eventAggregator
            .PublishAsync(Arg.Any<DocTypePermissionsChangedNotification>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new OperationCanceledException()));
        var sut = CreateService(AppCaches.NoCache);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            sut.SaveEditorEntriesAsync(Guid.NewGuid(), Editors, Guid.NewGuid(), []));
    }

    /// <summary>
    /// The same cancellation rule holds for a batch.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_PublishCancelled_RethrowsOperationCanceled()
    {
        _eventAggregator
            .PublishAsync(Arg.Any<DocTypePermissionsChangedNotification>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new OperationCanceledException()));
        var sut = CreateService(AppCaches.NoCache);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.SaveManyAsync(
        [
            (Guid.NewGuid(), Editors, Guid.NewGuid(), [Entry(PermissionState.Allow)], null),
        ]));
    }

    /// <summary>
    /// A batch publishes one notification per triple. If the first triple's handler throws, the
    /// exception is contained to that one publish so the remaining triples are still announced: one bad
    /// subscriber for one node must not silence the live update for every other node in the batch.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_FirstTripleHandlerThrows_SecondTripleStillPublished()
    {
        var sut = CreateService(AppCaches.NoCache);
        var node1 = Guid.NewGuid();
        var node2 = Guid.NewGuid();

        _eventAggregator
            .PublishAsync(
                Arg.Is<DocTypePermissionsChangedNotification>(n => n.NodeKey == node1),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Simulated handler failure.")));

        await sut.SaveManyAsync(
        [
            (node1, Editors, Guid.NewGuid(), [Entry(PermissionState.Allow)], null),
            (node2, "writers", Guid.NewGuid(), [Entry(PermissionState.Deny)], null),
        ]);

        await _eventAggregator.Received(1).PublishAsync(
            Arg.Is<DocTypePermissionsChangedNotification>(n => n.NodeKey == node2),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// An empty batch changed nothing, so nothing is stale: it must not touch the repository, the
    /// cache or the aggregator, rather than pay for a no-op write and a full flush of every user's
    /// resolved permissions.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_EmptyBatch_TouchesNeitherRepositoryNorCacheNorAggregator()
    {
        var runtimeCache = Substitute.For<IAppPolicyCache>();
        var sut = CreateService(AppCachesOver(runtimeCache));

        await sut.SaveManyAsync([]);

        await _repository.DidNotReceiveWithAnyArgs().SaveManyAsync(default!, default);
        Assert.Empty(runtimeCache.ReceivedCalls());
        await _eventAggregator.DidNotReceiveWithAnyArgs().PublishAsync(
            Arg.Any<DocTypePermissionsChangedNotification>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// If the batch write fails, nothing changed: no cache is flushed and nothing is published, because
    /// a notification is a claim that something changed.
    /// </summary>
    /// <remarks>
    /// Guards the position of the write relative to the invalidations, and that the failure is not
    /// swallowed on the way out.
    /// </remarks>
    [Fact]
    public async Task SaveManyAsync_RepositoryThrows_InvalidatesNothingAndPublishesNothing()
    {
        var runtimeCache = Substitute.For<IAppPolicyCache>();
        _repository
            .SaveManyAsync(AnyBatch(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Simulated write failure.")));
        var sut = CreateService(AppCachesOver(runtimeCache));

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.SaveManyAsync(
        [
            (Guid.NewGuid(), Editors, Guid.NewGuid(), [Entry(PermissionState.Allow)], null),
        ]));

        Assert.Empty(runtimeCache.ReceivedCalls());
        await _eventAggregator.DidNotReceiveWithAnyArgs().PublishAsync(
            Arg.Any<DocTypePermissionsChangedNotification>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The single save has the same rule: a failed write invalidates nothing and publishes nothing.
    /// </summary>
    [Fact]
    public async Task SaveEditorEntriesAsync_RepositoryThrows_InvalidatesNothingAndPublishesNothing()
    {
        var runtimeCache = Substitute.For<IAppPolicyCache>();
        _repository
            .SaveAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(), AnyEntries(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Simulated write failure.")));
        var sut = CreateService(AppCachesOver(runtimeCache));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.SaveEditorEntriesAsync(Guid.NewGuid(), Editors, Guid.NewGuid(), [Entry(PermissionState.Allow)]));

        Assert.Empty(runtimeCache.ReceivedCalls());
        await _eventAggregator.DidNotReceiveWithAnyArgs().PublishAsync(
            Arg.Any<DocTypePermissionsChangedNotification>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A batch the repository refuses on a stale stamp propagates the exception and invalidates and
    /// publishes nothing - not even for the triples that would have written cleanly, because the batch
    /// is all or nothing and none of them were written.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_StaleStamp_PropagatesAndInvalidatesNothingAndPublishesNothing()
    {
        var runtimeCache = Substitute.For<IAppPolicyCache>();
        var conflict = new DocTypePermissionConflict(Guid.NewGuid(), Editors, Guid.NewGuid(), [], "current-stamp");
        _repository
            .SaveManyAsync(AnyBatch(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new DocTypePermissionConcurrencyException([conflict])));
        var sut = CreateService(AppCachesOver(runtimeCache));

        var thrown = await Assert.ThrowsAsync<DocTypePermissionConcurrencyException>(() => sut.SaveManyAsync(
        [
            (conflict.NodeKey, Editors, conflict.ContentTypeKey, [Entry(PermissionState.Allow)], "stale"),
            (Guid.NewGuid(), "writers", Guid.NewGuid(), [Entry(PermissionState.Deny)], "fine"),
        ]));

        Assert.Same(conflict, Assert.Single(thrown.Conflicts));
        Assert.Empty(runtimeCache.ReceivedCalls());
        await _eventAggregator.DidNotReceiveWithAnyArgs().PublishAsync(
            Arg.Any<DocTypePermissionsChangedNotification>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The batch is handed to the repository in one call, so the repository's single transaction covers
    /// every triple, and each triple's expected stamp reaches it exactly as supplied - including a null
    /// one, which tells the repository to skip the check for that triple.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_HandsTheWholeBatchToTheRepositoryInOneCall_WithEachTriplesStamp()
    {
        var sut = CreateService(AppCaches.NoCache);
        var checkedNode = Guid.NewGuid();
        var uncheckedNode = Guid.NewGuid();

        await sut.SaveManyAsync(
        [
            (checkedNode, Editors, Guid.NewGuid(), [Entry(PermissionState.Allow)], "the-stamp"),
            (uncheckedNode, "writers", Guid.NewGuid(), [Entry(PermissionState.Deny)], null),
        ]);

        await _repository.Received(1).SaveManyAsync(
            Arg.Is<IEnumerable<(Guid NodeKey, string RoleAlias, Guid ContentTypeKey, IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries, string? ExpectedStamp)>>(
                b => b.Count() == 2
                     && b.Single(p => p.NodeKey == checkedNode).ExpectedStamp == "the-stamp"
                     && b.Single(p => p.NodeKey == uncheckedNode).ExpectedStamp == null),
            Arg.Any<CancellationToken>());
    }
}
