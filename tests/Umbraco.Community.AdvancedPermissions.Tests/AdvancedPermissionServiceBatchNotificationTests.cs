using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.Caching;
using Umbraco.Community.AdvancedPermissions.Core.Exceptions;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;
using Umbraco.Community.AdvancedPermissions.Notifications;
using Umbraco.Community.AdvancedPermissions.Services;
using static Umbraco.Community.AdvancedPermissions.Core.Constants.AdvancedPermissionsConstants;
using static Umbraco.Community.AdvancedPermissions.Tests.PermissionServiceNotificationTestSupport;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests the write-then-invalidate-then-publish contract of <see cref="AdvancedPermissionService"/>
/// for the paths the permission editors actually use: the batch save, and the failure and cache
/// behaviour that the single-save tests never observed.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="AdvancedPermissionServiceNotificationTests"/> uses a no-op cache for everything but its
/// one single-save ordering test, so nothing in it can see whether the service invalidates its caches
/// at all. The batch path had no cache coverage of any kind: dropping either invalidation from
/// <c>SaveManyAsync</c> left the whole suite green, and the result is the failure these tests exist to
/// prevent - an editor saves, every other editor is told to refetch, and the refetch is served from
/// the cache the write should have emptied, so it returns the old permissions and, having consumed its
/// one notification, is never told again.
/// </para>
/// <para>
/// The caches are observed through the <see cref="IAppPolicyCache"/> underneath them, which is the
/// only seam <see cref="AdvancedPermissionCache"/> (a concrete class) exposes.
/// </para>
/// </remarks>
public sealed class AdvancedPermissionServiceBatchNotificationTests
{
    /// <summary>The user group most tests write for.</summary>
    private const string Editors = "editors";

    /// <summary>The repository substitute the service writes through.</summary>
    private readonly IAdvancedPermissionRepository _repository = Substitute.For<IAdvancedPermissionRepository>();

    /// <summary>The aggregator substitute the service publishes through.</summary>
    private readonly IEventAggregator _eventAggregator = Substitute.For<IEventAggregator>();

    /// <summary>The logger substitute a swallowed handler failure is written to.</summary>
    private readonly ILogger<AdvancedPermissionService> _logger = Substitute.For<ILogger<AdvancedPermissionService>>();

    /// <summary>
    /// Builds the service under test over the given caches.
    /// </summary>
    /// <param name="appCaches">The caches its two-level permission cache is built over.</param>
    /// <returns>The service under test.</returns>
    private AdvancedPermissionService CreateService(AppCaches appCaches) =>
        new(
            _repository,
            Substitute.For<IPermissionResolver>(),
            Substitute.For<IUserService>(),
            new AdvancedPermissionCache(appCaches),
            _eventAggregator,
            _logger);

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
    /// Records a <c>publish</c> marker each time the change notification goes out.
    /// </summary>
    /// <param name="order">The list the marker is appended to.</param>
    private void RecordPublishes(List<string> order) =>
        _eventAggregator
            .When(a => a.PublishAsync(Arg.Any<AdvancedPermissionsChangedNotification>(), Arg.Any<CancellationToken>()))
            .Do(_ => order.Add("publish"));

    /// <summary>
    /// A batch publishes only after the write and after every invalidation: the write, one flush per
    /// distinct user group, one flush of every resolved permission, and only then a notification per pair.
    /// </summary>
    /// <remarks>
    /// This is the batch counterpart of the single-save ordering test, and the batch is the path the
    /// editors use. It fails if either invalidation is dropped (the recorded sequence loses a marker) and
    /// if a publish moves above any invalidation. Before it existed, removing
    /// <c>cache.InvalidateAllResolved()</c> or the <c>InvalidateRoleEntries</c> loop from
    /// <c>SaveManyAsync</c> left all 131 tests green.
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
            (Guid.NewGuid(), Editors, [(VerbRead, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], null),
            (Guid.NewGuid(), "writers", [(VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeOnly, false)], null),
        ]);

        Assert.Equal(
            ["write", "invalidate-role", "invalidate-role", "invalidate-all", "publish", "publish"],
            order);
    }

    /// <summary>
    /// A batch flushes the resolved-permission cache - every user's, since any user's resolution may
    /// have changed - exactly once, however many pairs it writes.
    /// </summary>
    /// <remarks>
    /// The ordering test above would also notice a dropped flush, but only as a different sequence of
    /// markers. This names the failure: a stale resolved-permission cache is what makes a refetch return
    /// the permissions the write replaced. Removing <c>cache.InvalidateAllResolved()</c> from
    /// <c>SaveManyAsync</c> used to pass every test.
    /// </remarks>
    [Fact]
    public async Task SaveManyAsync_FlushesResolvedPermissionsOnce_ForTheWholeBatch()
    {
        var runtimeCache = Substitute.For<IAppPolicyCache>();
        var sut = CreateService(AppCachesOver(runtimeCache));

        await sut.SaveManyAsync(
        [
            (Guid.NewGuid(), Editors, [(VerbRead, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], null),
            (Guid.NewGuid(), "writers", [(VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeOnly, false)], null),
            (Guid.NewGuid(), "admins", [(VerbRead, PermissionState.Deny, PermissionScope.ThisNodeOnly, false)], null),
        ]);

        runtimeCache.Received(1).ClearByKey(Arg.Is<string>(k => k.EndsWith(".L2.", StringComparison.Ordinal)));
    }

    /// <summary>
    /// Pairs for the same user group (different nodes) flush that group's cached entries once, not once
    /// per pair, and every group in the batch is flushed.
    /// </summary>
    /// <remarks>
    /// Fails if the <c>InvalidateRoleEntries</c> loop is dropped (no group is flushed, so the next read
    /// serves the entries the write replaced) and if the loop stops de-duplicating (a group is flushed
    /// once per pair). Neither used to fail anything.
    /// </remarks>
    [Fact]
    public async Task SaveManyAsync_InvalidatesEachDistinctUserGroupOnce()
    {
        var runtimeCache = Substitute.For<IAppPolicyCache>();
        var sut = CreateService(AppCachesOver(runtimeCache));

        await sut.SaveManyAsync(
        [
            (Guid.NewGuid(), Editors, [(VerbRead, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], null),
            (Guid.NewGuid(), Editors, [(VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeOnly, false)], null),
            (Guid.NewGuid(), "writers", [(VerbRead, PermissionState.Deny, PermissionScope.ThisNodeOnly, false)], null),
        ]);

        runtimeCache.Received(1).Clear(Arg.Is<string>(k => k.EndsWith(".L1.editors", StringComparison.Ordinal)));
        runtimeCache.Received(1).Clear(Arg.Is<string>(k => k.EndsWith(".L1.writers", StringComparison.Ordinal)));
    }

    /// <summary>
    /// If the batch write itself fails, nothing changed: no cache is flushed and no notification goes
    /// out, because a notification is a claim that something changed.
    /// </summary>
    /// <remarks>
    /// Guards the position of the write relative to the invalidations. A service that invalidated first
    /// (or that swallowed the repository's failure) would empty caches that are still correct and tell
    /// every editor to refetch for a save that never happened.
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
            (Guid.NewGuid(), Editors, [(VerbRead, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], null),
        ]));

        Assert.Empty(runtimeCache.ReceivedCalls());
        await _eventAggregator.DidNotReceiveWithAnyArgs().PublishAsync(
            Arg.Any<AdvancedPermissionsChangedNotification>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The single save has the same rule: a failed write invalidates nothing and publishes nothing.
    /// </summary>
    [Fact]
    public async Task SaveEntriesAsync_RepositoryThrows_InvalidatesNothingAndPublishesNothing()
    {
        var runtimeCache = Substitute.For<IAppPolicyCache>();
        _repository
            .SaveAsync(Arg.Any<Guid>(), Arg.Any<string>(), AnyEntries(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Simulated write failure.")));
        var sut = CreateService(AppCachesOver(runtimeCache));

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.SaveEntriesAsync(
            Guid.NewGuid(), Editors, [(VerbRead, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], (string?)null));

        Assert.Empty(runtimeCache.ReceivedCalls());
        await _eventAggregator.DidNotReceiveWithAnyArgs().PublishAsync(
            Arg.Any<AdvancedPermissionsChangedNotification>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A batch the repository refuses on a stale stamp propagates the exception and invalidates and
    /// publishes nothing - not even for the pairs that would have written cleanly, because the batch is
    /// all or nothing and none of them were written.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_StaleStamp_InvalidatesNothing()
    {
        var runtimeCache = Substitute.For<IAppPolicyCache>();
        var conflict = new PermissionConflict(Guid.NewGuid(), Editors, [], "current-stamp");
        _repository
            .SaveManyAsync(AnyBatch(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new PermissionConcurrencyException([conflict])));
        var sut = CreateService(AppCachesOver(runtimeCache));

        await Assert.ThrowsAsync<PermissionConcurrencyException>(() => sut.SaveManyAsync(
        [
            (conflict.NodeKey, Editors, [(VerbRead, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], "stale"),
            (Guid.NewGuid(), "writers", [(VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeOnly, false)], "fine"),
        ]));

        Assert.Empty(runtimeCache.ReceivedCalls());
    }

    /// <summary>
    /// A caller is entitled to hand the service a lazy sequence. It is needed twice - once for the
    /// repository and once for the notification's verb list - so it must be materialised exactly once,
    /// or a one-shot sequence would reach the repository and be empty by the time the notification is
    /// built.
    /// </summary>
    /// <remarks>
    /// The repository substitute enumerates what it is handed, as the real one does. Without that, a
    /// service that passed the lazy sequence straight through would enumerate it only once (for the
    /// notification) and the test could not tell.
    /// </remarks>
    [Fact]
    public async Task SaveEntriesAsync_LazySequence_IsEnumeratedOnce()
    {
        var enumerations = 0;
        _repository
            .SaveAsync(Arg.Any<Guid>(), Arg.Any<string>(), AnyEntries(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                _ = call.ArgAt<IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)>>(2).ToList();
                return Task.CompletedTask;
            });
        var sut = CreateService(AppCaches.NoCache);

        IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Lazy()
        {
            enumerations++;
            yield return (VerbRead, PermissionState.Allow, PermissionScope.ThisNodeOnly, false);
        }

        await sut.SaveEntriesAsync(Guid.NewGuid(), Editors, Lazy(), (string?)null);

        Assert.Equal(1, enumerations);
        await _eventAggregator.Received(1).PublishAsync(
            Arg.Is<AdvancedPermissionsChangedNotification>(n => n.Verbs.Count == 1 && n.Verbs[0] == VerbRead),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A handler that throws synchronously, rather than returning a faulted task, is isolated just the
    /// same as an asynchronous failure, and the failure is logged at error level rather than lost.
    /// </summary>
    [Fact]
    public async Task SaveEntriesAsync_HandlerThrowsSynchronously_CompletesAndLogsAnError()
    {
        _eventAggregator
            .PublishAsync(Arg.Any<AdvancedPermissionsChangedNotification>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Simulated synchronous handler failure."));
        var sut = CreateService(AppCaches.NoCache);

        await sut.SaveEntriesAsync(
            Guid.NewGuid(), Editors, [(VerbRead, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], (string?)null);

        Assert.True(LoggedAnError(_logger), "A swallowed handler failure must still be logged at error level.");
    }

    /// <summary>
    /// A cancelled request is not a handler failure. Swallowing <see cref="OperationCanceledException"/>
    /// would hide a genuine cancellation, so it propagates from a single save even though other handler
    /// failures are contained.
    /// </summary>
    [Fact]
    public async Task SaveEntriesAsync_PublishCancelled_RethrowsOperationCanceled()
    {
        _eventAggregator
            .PublishAsync(Arg.Any<AdvancedPermissionsChangedNotification>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new OperationCanceledException()));
        var sut = CreateService(AppCaches.NoCache);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.SaveEntriesAsync(
            Guid.NewGuid(), Editors, [(VerbRead, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], (string?)null));
    }

    /// <summary>
    /// The same cancellation rule holds for a batch: it propagates rather than being swallowed.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_PublishCancelled_RethrowsOperationCanceled()
    {
        _eventAggregator
            .PublishAsync(Arg.Any<AdvancedPermissionsChangedNotification>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new OperationCanceledException()));
        var sut = CreateService(AppCaches.NoCache);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.SaveManyAsync(
        [
            (Guid.NewGuid(), Editors, [(VerbRead, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], null),
        ]));
    }

    /// <summary>
    /// The batch is handed to the repository in one call, so the repository's single transaction
    /// covers every pair, and each pair's expected stamp reaches it exactly as supplied - including a
    /// null one, which tells the repository to skip the check for that pair.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_HandsTheWholeBatchToTheRepositoryInOneCall_WithEachPairsStamp()
    {
        var sut = CreateService(AppCaches.NoCache);
        var checkedNode = Guid.NewGuid();
        var uncheckedNode = Guid.NewGuid();

        await sut.SaveManyAsync(
        [
            (checkedNode, Editors, [(VerbRead, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], "the-stamp"),
            (uncheckedNode, "writers", [(VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeOnly, false)], null),
        ]);

        await _repository.Received(1).SaveManyAsync(
            Arg.Is<IEnumerable<(Guid NodeKey, string RoleAlias, IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries, string? ExpectedStamp)>>(
                b => b.Count() == 2
                     && b.Single(p => p.NodeKey == checkedNode).ExpectedStamp == "the-stamp"
                     && b.Single(p => p.NodeKey == uncheckedNode).ExpectedStamp == null),
            Arg.Any<CancellationToken>());
    }
}
