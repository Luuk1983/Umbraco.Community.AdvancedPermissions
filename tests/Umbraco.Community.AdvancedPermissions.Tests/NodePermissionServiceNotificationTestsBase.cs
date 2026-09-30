using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Community.AdvancedPermissions.Core.Exceptions;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// The write-then-invalidate-then-publish contract of <c>NodePermissionServiceBase</c>, written once
/// and run against every node-keyed family that inherits it — content and library elements today.
/// </summary>
/// <remarks>
/// <para>
/// The behaviour under test lives in the shared base class, so a test that ran for only one family
/// could pass while the other silently lost its notifications or its ordering guarantee. Each
/// concrete subclass supplies its own service, cache and notification type and inherits every test
/// below, which is what proves the inheritance actually holds — and, through
/// <see cref="Describe"/>, that each family publishes <em>its own</em> notification type.
/// </para>
/// <para>
/// The ordering tests are the ones that matter most. A client that refetches on an event which
/// overtook the cache invalidation reads the very snapshot the change replaced and, having consumed
/// its one notification, never asks again.
/// </para>
/// </remarks>
/// <typeparam name="TNotification">The notification type the family under test publishes.</typeparam>
/// <param name="logger">The logger substitute the concrete subclass builds its service with.</param>
public abstract class NodePermissionServiceNotificationTestsBase<TNotification>(ILogger logger)
    where TNotification : class, INotification
{
    /// <summary>The user group alias most tests write for.</summary>
    private const string Editors = "editors";

    /// <summary>
    /// Gets the event aggregator substitute the service publishes through.
    /// </summary>
    protected IEventAggregator EventAggregator { get; } = Substitute.For<IEventAggregator>();

    /// <summary>
    /// Gets the logger substitute the service logs a swallowed handler failure to.
    /// </summary>
    protected ILogger Logger { get; } = logger;

    /// <summary>
    /// Gets the repository substitute the service under test writes through.
    /// </summary>
    protected abstract INodePermissionRepository Repository { get; }

    /// <summary>
    /// Gets a verb valid for the family under test.
    /// </summary>
    protected abstract string VerbA { get; }

    /// <summary>
    /// Gets a second, different verb valid for the family under test.
    /// </summary>
    protected abstract string VerbB { get; }

    /// <summary>
    /// Builds the service under test over the given caches.
    /// </summary>
    /// <param name="appCaches">The caches its two-level permission cache is built over.</param>
    /// <returns>The service under test.</returns>
    protected abstract INodePermissionService CreateService(AppCaches appCaches);

    /// <summary>
    /// Reads the node key, user group alias and verbs out of the family's notification record.
    /// </summary>
    /// <param name="notification">The notification that was published.</param>
    /// <returns>Its node key, user group alias and verbs.</returns>
    protected abstract (Guid NodeKey, string RoleAlias, IReadOnlyList<string> Verbs) Describe(TNotification notification);

    /// <summary>
    /// Saving entries must publish the family's notification naming the exact node, user group and
    /// verbs that were written, so a consumer can react without depending on how the write happened.
    /// </summary>
    [Fact]
    public async Task SaveEntriesAsync_Publishes_ChangedNotification()
    {
        var sut = CreateService(AppCaches.NoCache);
        var nodeKey = Guid.NewGuid();

        await sut.SaveEntriesAsync(
            nodeKey,
            Editors,
            [
                (VerbA, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants, false),
                (VerbB, PermissionState.Deny, PermissionScope.ThisNodeOnly, false),
            ], (string?)null);

        await EventAggregator.Received(1).PublishAsync(
            Arg.Is<TNotification>(n => Matches(n, nodeKey, Editors, VerbA, VerbB)),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Removing every entry for a node and user group ("revert to inherit") is still a change: a
    /// consumer that never hears about it keeps showing permissions that no longer exist. The
    /// notification must still be published, carrying an empty verb list rather than being skipped.
    /// </summary>
    [Fact]
    public async Task SaveEntriesAsync_EmptyEntries_PublishesWithNoVerbs()
    {
        var sut = CreateService(AppCaches.NoCache);
        var nodeKey = Guid.NewGuid();

        await sut.SaveEntriesAsync(nodeKey, Editors, [], (string?)null);

        await EventAggregator.Received(1).PublishAsync(
            Arg.Is<TNotification>(n => Matches(n, nodeKey, Editors)),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A caller is entitled to hand the service a lazy sequence. It is needed twice — once for the
    /// repository and once for the notification's verb list — so it must be materialised exactly once,
    /// or a one-shot sequence would reach the repository and be empty by the time the notification is
    /// built.
    /// </summary>
    [Fact]
    public async Task SaveEntriesAsync_LazySequence_IsEnumeratedOnce()
    {
        var sut = CreateService(AppCaches.NoCache);
        var enumerations = 0;

        IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Lazy()
        {
            enumerations++;
            yield return (VerbA, PermissionState.Allow, PermissionScope.ThisNodeOnly, false);
        }

        await sut.SaveEntriesAsync(Guid.NewGuid(), Editors, Lazy(), (string?)null);

        Assert.Equal(1, enumerations);
        await EventAggregator.Received(1).PublishAsync(
            Arg.Is<TNotification>(n => Describe(n).Verbs.Count == 1 && Describe(n).Verbs[0] == VerbA),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The notification must be published strictly after the repository write AND the cache
    /// invalidation — never before. Records write, then both invalidations, then the publish, and
    /// fails if the publish moves above the invalidation.
    /// </summary>
    [Fact]
    public async Task SaveEntriesAsync_PublishesAfterWriteAndCacheInvalidation()
    {
        var order = new List<string>();
        Repository
            .When(r => r.SaveAsync(Arg.Any<Guid>(), Arg.Any<string>(), AnyEntries(), Arg.Any<string?>(), Arg.Any<CancellationToken>()))
            .Do(_ => order.Add("write"));
        RecordPublishes(order);
        var sut = CreateService(RecordingCaches(order));

        await sut.SaveEntriesAsync(
            Guid.NewGuid(), Editors, [(VerbA, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], (string?)null);

        Assert.Equal(["write", "invalidate-role", "invalidate-all", "publish"], order);
    }

    /// <summary>
    /// Deleting a single entry publishes too, naming just that one verb, and — as with a save — only
    /// after the write and the cache invalidation.
    /// </summary>
    [Fact]
    public async Task DeleteEntryAsync_PublishesAfterWriteAndCacheInvalidation_NamingTheVerb()
    {
        var order = new List<string>();
        Repository
            .When(r => r.DeleteAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()))
            .Do(_ => order.Add("write"));
        RecordPublishes(order);
        var sut = CreateService(RecordingCaches(order));
        var nodeKey = Guid.NewGuid();

        await sut.DeleteEntryAsync(nodeKey, Editors, VerbA);

        Assert.Equal(["write", "invalidate-role", "invalidate-all", "publish"], order);
        await EventAggregator.Received(1).PublishAsync(
            Arg.Is<TNotification>(n => Matches(n, nodeKey, Editors, VerbA)),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// SaveManyAsync writes a batch in a single transaction, but a handler should still see the same
    /// granularity it would from N individual saves: one notification per pair, each naming only its
    /// own node and verbs, not a single opaque batch event.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_TwoPairs_PublishesTwoNotifications_OnePerPair()
    {
        var sut = CreateService(AppCaches.NoCache);
        var node1 = Guid.NewGuid();
        var node2 = Guid.NewGuid();

        await sut.SaveManyAsync(
        [
            (node1, "editors", [(VerbA, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], null),
            (node2, "writers", [(VerbB, PermissionState.Deny, PermissionScope.ThisNodeOnly, false)], null),
        ]);

        await EventAggregator.Received(1).PublishAsync(
            Arg.Is<TNotification>(n => Matches(n, node1, "editors", VerbA)),
            Arg.Any<CancellationToken>());
        await EventAggregator.Received(1).PublishAsync(
            Arg.Is<TNotification>(n => Matches(n, node2, "writers", VerbB)),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// For a batch, every write and every invalidation must have happened before the first notification
    /// goes out. Records the whole sequence and fails if any publish is moved above any invalidation.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_PublishesOnlyAfterWriteAndEveryInvalidation()
    {
        var order = new List<string>();
        Repository
            .When(r => r.SaveManyAsync(AnyBatch(), Arg.Any<CancellationToken>()))
            .Do(_ => order.Add("write"));
        RecordPublishes(order);
        var sut = CreateService(RecordingCaches(order));

        await sut.SaveManyAsync(
        [
            (Guid.NewGuid(), "editors", [(VerbA, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], null),
            (Guid.NewGuid(), "writers", [(VerbB, PermissionState.Deny, PermissionScope.ThisNodeOnly, false)], null),
        ]);

        Assert.Equal(
            ["write", "invalidate-role", "invalidate-role", "invalidate-all", "publish", "publish"],
            order);
    }

    /// <summary>
    /// Two pairs for the same user group (different nodes) invalidate that group's cached entries
    /// once, not once per pair: the flush is per distinct user group.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_InvalidatesEachDistinctUserGroupOnce()
    {
        var runtimeCache = Substitute.For<IAppPolicyCache>();
        var sut = CreateService(AppCachesOver(runtimeCache));

        await sut.SaveManyAsync(
        [
            (Guid.NewGuid(), "editors", [(VerbA, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], null),
            (Guid.NewGuid(), "editors", [(VerbB, PermissionState.Deny, PermissionScope.ThisNodeOnly, false)], null),
            (Guid.NewGuid(), "writers", [(VerbA, PermissionState.Deny, PermissionScope.ThisNodeOnly, false)], null),
        ]);

        runtimeCache.Received(1).Clear(Arg.Is<string>(k => k.EndsWith(".L1.editors", StringComparison.Ordinal)));
        runtimeCache.Received(1).Clear(Arg.Is<string>(k => k.EndsWith(".L1.writers", StringComparison.Ordinal)));
    }

    /// <summary>
    /// By the time the notification is published, the write has already committed and the cache has
    /// been invalidated — the change is real. If a handler throws, that failure must not propagate: a
    /// controller catching it would report an error for a save that in fact succeeded, and the editor
    /// would retry or reload and lose work over nothing. The failure is logged at error level instead.
    /// </summary>
    [Fact]
    public async Task SaveEntriesAsync_NotificationHandlerThrows_CompletesAndLogsAnError()
    {
        EventAggregator
            .PublishAsync(Arg.Any<TNotification>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Simulated handler failure.")));
        var sut = CreateService(AppCaches.NoCache);

        // Must not throw — the write already succeeded.
        await sut.SaveEntriesAsync(
            Guid.NewGuid(), Editors, [(VerbA, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], (string?)null);

        Assert.True(LoggedAnError(), "A swallowed handler failure must still be logged at error level.");
    }

    /// <summary>
    /// A handler that throws synchronously, rather than returning a faulted task, is isolated just
    /// the same.
    /// </summary>
    [Fact]
    public async Task SaveEntriesAsync_NotificationHandlerThrowsSynchronously_CompletesWithoutThrowing()
    {
        EventAggregator
            .PublishAsync(Arg.Any<TNotification>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Simulated synchronous handler failure."));
        var sut = CreateService(AppCaches.NoCache);

        await sut.SaveEntriesAsync(
            Guid.NewGuid(), Editors, [(VerbA, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], (string?)null);

        Assert.True(LoggedAnError());
    }

    /// <summary>
    /// A cancelled request is not a handler failure. Swallowing <see cref="OperationCanceledException"/>
    /// would hide a genuine cancellation, so it must propagate even though other handler failures are
    /// contained.
    /// </summary>
    [Fact]
    public async Task SaveEntriesAsync_PublishCancelled_RethrowsOperationCanceled()
    {
        EventAggregator
            .PublishAsync(Arg.Any<TNotification>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new OperationCanceledException()));
        var sut = CreateService(AppCaches.NoCache);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.SaveEntriesAsync(
            Guid.NewGuid(), Editors, [(VerbA, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], (string?)null));
    }

    /// <summary>
    /// The same cancellation rule holds for a batch: it propagates rather than being swallowed.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_PublishCancelled_RethrowsOperationCanceled()
    {
        EventAggregator
            .PublishAsync(Arg.Any<TNotification>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new OperationCanceledException()));
        var sut = CreateService(AppCaches.NoCache);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.SaveManyAsync(
        [
            (Guid.NewGuid(), Editors, [(VerbA, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], null),
        ]));
    }

    /// <summary>
    /// SaveManyAsync publishes one notification per pair. If the first pair's handler throws, the
    /// exception must be contained to that one publish so the remaining pairs are still announced —
    /// one bad subscriber for one node must not silence the live update for every other node in the
    /// same batch.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_FirstPairHandlerThrows_SecondPairStillPublished()
    {
        var sut = CreateService(AppCaches.NoCache);
        var node1 = Guid.NewGuid();
        var node2 = Guid.NewGuid();

        EventAggregator
            .PublishAsync(
                Arg.Is<TNotification>(n => Describe(n).NodeKey == node1),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Simulated handler failure.")));

        await sut.SaveManyAsync(
        [
            (node1, "editors", [(VerbA, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], null),
            (node2, "writers", [(VerbB, PermissionState.Deny, PermissionScope.ThisNodeOnly, false)], null),
        ]);

        await EventAggregator.Received(1).PublishAsync(
            Arg.Is<TNotification>(n => Describe(n).NodeKey == node2 && Describe(n).RoleAlias == "writers"),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// An empty batch changed nothing, so nothing is stale: it must skip the repository transaction,
    /// the cache flush and the publish entirely rather than pay for a no-op write and a full
    /// invalidation of every user's resolved permissions.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_EmptyBatch_TouchesNeitherRepositoryNorCacheNorAggregator()
    {
        var runtimeCache = Substitute.For<IAppPolicyCache>();
        var sut = CreateService(AppCachesOver(runtimeCache));

        await sut.SaveManyAsync([]);

        await Repository.DidNotReceiveWithAnyArgs().SaveManyAsync(default!, default);
        Assert.Empty(runtimeCache.ReceivedCalls());
        await EventAggregator.DidNotReceiveWithAnyArgs().PublishAsync(Arg.Any<TNotification>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// If the write itself fails, nothing changed: the cache must not be flushed and no notification
    /// may go out, because a notification is a claim that something changed.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_RepositoryThrows_InvalidatesNothingAndPublishesNothing()
    {
        var runtimeCache = Substitute.For<IAppPolicyCache>();
        Repository
            .SaveManyAsync(AnyBatch(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Simulated write failure.")));
        var sut = CreateService(AppCachesOver(runtimeCache));

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.SaveManyAsync(
        [
            (Guid.NewGuid(), Editors, [(VerbA, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], null),
        ]));

        Assert.Empty(runtimeCache.ReceivedCalls());
        await EventAggregator.DidNotReceiveWithAnyArgs().PublishAsync(Arg.Any<TNotification>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The single-entry save has the same rule: a failed write publishes nothing.
    /// </summary>
    [Fact]
    public async Task SaveEntriesAsync_RepositoryThrows_PublishesNothing()
    {
        Repository
            .SaveAsync(Arg.Any<Guid>(), Arg.Any<string>(), AnyEntries(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Simulated write failure.")));
        var sut = CreateService(AppCaches.NoCache);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.SaveEntriesAsync(
            Guid.NewGuid(), Editors, [(VerbA, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], (string?)null));

        await EventAggregator.DidNotReceiveWithAnyArgs().PublishAsync(Arg.Any<TNotification>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The batch is handed to the repository in one call, unchanged, so the repository's transaction
    /// covers every pair.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_HandsTheWholeBatchToTheRepositoryInOneCall()
    {
        var sut = CreateService(AppCaches.NoCache);
        var node1 = Guid.NewGuid();
        var node2 = Guid.NewGuid();

        await sut.SaveManyAsync(
        [
            (node1, "editors", [(VerbA, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], null),
            (node2, "writers", [(VerbB, PermissionState.Deny, PermissionScope.ThisNodeOnly, false)], null),
        ]);

        await Repository.Received(1).SaveManyAsync(
            Arg.Is<IEnumerable<(Guid NodeKey, string RoleAlias, IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries, string? ExpectedStamp)>>(
                b => b.Count() == 2 && b.Any(p => p.NodeKey == node1) && b.Any(p => p.NodeKey == node2)),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Gets whether the logger substitute recorded a call at error level.
    /// </summary>
    /// <returns><see langword="true"/> if an error was logged.</returns>
    protected bool LoggedAnError() =>
        Logger.ReceivedCalls().Any(c =>
            c.GetMethodInfo().Name == nameof(ILogger.Log) &&
            c.GetArguments()[0] is LogLevel.Error);

    /// <summary>
    /// Gets whether a published notification carries exactly the given node, user group and verbs.
    /// </summary>
    /// <param name="notification">The published notification.</param>
    /// <param name="nodeKey">The expected node key.</param>
    /// <param name="roleAlias">The expected user group alias.</param>
    /// <param name="verbs">The expected verbs, in any order.</param>
    /// <returns><see langword="true"/> if every part matches.</returns>
    private bool Matches(TNotification notification, Guid nodeKey, string roleAlias, params string[] verbs)
    {
        var described = Describe(notification);
        return described.NodeKey == nodeKey
               && described.RoleAlias == roleAlias
               && described.Verbs.Count == verbs.Length
               && verbs.All(v => described.Verbs.Contains(v));
    }

    /// <summary>
    /// Records a "publish" marker each time the family's notification goes out.
    /// </summary>
    /// <param name="order">The list the marker is appended to.</param>
    private void RecordPublishes(List<string> order) =>
        EventAggregator
            .When(a => a.PublishAsync(Arg.Any<TNotification>(), Arg.Any<CancellationToken>()))
            .Do(_ => order.Add("publish"));

    /// <summary>
    /// Builds caches whose runtime cache records each invalidation, so a test can see whether the
    /// invalidation happened before or after another step.
    /// </summary>
    /// <param name="order">The list the "invalidate-role" and "invalidate-all" markers are appended to.</param>
    /// <returns>Caches wired to the recording runtime cache.</returns>
    private static AppCaches RecordingCaches(List<string> order)
    {
        var runtimeCache = Substitute.For<IAppPolicyCache>();
        runtimeCache.When(c => c.Clear(Arg.Any<string>())).Do(_ => order.Add("invalidate-role"));
        runtimeCache.When(c => c.ClearByKey(Arg.Any<string>())).Do(_ => order.Add("invalidate-all"));
        return AppCachesOver(runtimeCache);
    }

    /// <summary>
    /// Wraps a runtime cache substitute in an <see cref="AppCaches"/>.
    /// </summary>
    /// <param name="runtimeCache">The runtime cache to expose.</param>
    /// <returns>The caches.</returns>
    private static AppCaches AppCachesOver(IAppPolicyCache runtimeCache) =>
        new(runtimeCache, NoAppCache.Instance, new IsolatedCaches(_ => NoAppCache.Instance));

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
    private static IEnumerable<(Guid NodeKey, string RoleAlias, IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries, string? ExpectedStamp)> AnyBatch() =>
        Arg.Any<IEnumerable<(Guid NodeKey, string RoleAlias, IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries, string? ExpectedStamp)>>();

    /// <summary>
    /// A stale stamp makes the repository refuse the batch. The service must let that refusal reach the
    /// caller untouched and do nothing else: nothing was written, so no cache is flushed and no
    /// notification goes out - a notification is a claim that something changed.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_StaleStamp_PropagatesAndInvalidatesNothingAndPublishesNothing()
    {
        var runtimeCache = Substitute.For<IAppPolicyCache>();
        var conflict = new PermissionConflict(Guid.NewGuid(), Editors, [], "current-stamp");
        Repository
            .SaveManyAsync(AnyBatch(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new PermissionConcurrencyException([conflict])));
        var sut = CreateService(AppCachesOver(runtimeCache));

        var thrown = await Assert.ThrowsAsync<PermissionConcurrencyException>(() => sut.SaveManyAsync(
        [
            (conflict.NodeKey, Editors, [(VerbA, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], "stale"),
        ]));

        Assert.Same(conflict, Assert.Single(thrown.Conflicts));
        Assert.Empty(runtimeCache.ReceivedCalls());
        await EventAggregator.DidNotReceiveWithAnyArgs().PublishAsync(Arg.Any<TNotification>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The single save has the same rule: a stale stamp reaches the caller, and nothing is invalidated
    /// or published.
    /// </summary>
    [Fact]
    public async Task SaveEntriesAsync_StaleStamp_PropagatesAndInvalidatesNothingAndPublishesNothing()
    {
        var runtimeCache = Substitute.For<IAppPolicyCache>();
        var conflict = new PermissionConflict(Guid.NewGuid(), Editors, [], "current-stamp");
        Repository
            .SaveAsync(Arg.Any<Guid>(), Arg.Any<string>(), AnyEntries(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new PermissionConcurrencyException([conflict])));
        var sut = CreateService(AppCachesOver(runtimeCache));

        await Assert.ThrowsAsync<PermissionConcurrencyException>(() => sut.SaveEntriesAsync(
            conflict.NodeKey, Editors, [(VerbA, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], "stale"));

        Assert.Empty(runtimeCache.ReceivedCalls());
        await EventAggregator.DidNotReceiveWithAnyArgs().PublishAsync(Arg.Any<TNotification>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The service does not check stamps itself - the repository does, inside its transaction - so each
    /// pair's expected stamp must reach the repository exactly as supplied, including a null one that
    /// tells it to skip the check for that pair.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_ForwardsEachPairsExpectedStampToTheRepository()
    {
        var sut = CreateService(AppCaches.NoCache);
        var checkedNode = Guid.NewGuid();
        var uncheckedNode = Guid.NewGuid();

        await sut.SaveManyAsync(
        [
            (checkedNode, Editors, [(VerbA, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], "the-stamp"),
            (uncheckedNode, Editors, [(VerbB, PermissionState.Deny, PermissionScope.ThisNodeOnly, false)], null),
        ]);

        await Repository.Received(1).SaveManyAsync(
            Arg.Is<IEnumerable<(Guid NodeKey, string RoleAlias, IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries, string? ExpectedStamp)>>(
                b => b.Single(p => p.NodeKey == checkedNode).ExpectedStamp == "the-stamp"
                     && b.Single(p => p.NodeKey == uncheckedNode).ExpectedStamp == null),
            Arg.Any<CancellationToken>());
    }

    /// <summary>The single save forwards its expected stamp to the repository, for the same reason.</summary>
    [Fact]
    public async Task SaveEntriesAsync_ForwardsTheExpectedStampToTheRepository()
    {
        var sut = CreateService(AppCaches.NoCache);
        var nodeKey = Guid.NewGuid();

        await sut.SaveEntriesAsync(
            nodeKey, Editors, [(VerbA, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], "the-stamp");

        await Repository.Received(1).SaveAsync(
            nodeKey, Editors, AnyEntries(), "the-stamp", Arg.Any<CancellationToken>());
    }
}
