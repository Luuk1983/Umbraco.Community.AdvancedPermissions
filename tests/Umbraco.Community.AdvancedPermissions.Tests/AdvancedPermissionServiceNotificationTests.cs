using Umbraco.Community.AdvancedPermissions.Caching;
using Umbraco.Community.AdvancedPermissions.Core.Exceptions;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;
using Umbraco.Community.AdvancedPermissions.Core.Services;
using Umbraco.Community.AdvancedPermissions.Notifications;
using Umbraco.Community.AdvancedPermissions.Services;
using NSubstitute;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Services;
using static Umbraco.Community.AdvancedPermissions.Core.Constants.AdvancedPermissionsConstants;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Unit tests for the notifications <see cref="AdvancedPermissionService"/> publishes after a
/// permission write, for the ordering guarantee that the publish happens strictly after the
/// cache has been invalidated, and for the isolation guarantee that a handler's failure cannot
/// mask a successful write — the extensibility seam that Task 8's SignalR forwarder (and any
/// third-party consumer) relies on to never observe a stale cache, and never turn a real save
/// into a reported failure.
/// </summary>
public sealed class AdvancedPermissionServiceNotificationTests
{
    private readonly IAdvancedPermissionRepository _repository = Substitute.For<IAdvancedPermissionRepository>();
    private readonly IPermissionResolver _resolver = Substitute.For<IPermissionResolver>();
    private readonly IUserService _userService = Substitute.For<IUserService>();
    private readonly IEventAggregator _eventAggregator = Substitute.For<IEventAggregator>();
    private readonly ILogger<AdvancedPermissionService> _logger = Substitute.For<ILogger<AdvancedPermissionService>>();
    private readonly AdvancedPermissionService _sut;

    /// <summary>Initialises the system under test with no-op caches and substituted dependencies.</summary>
    public AdvancedPermissionServiceNotificationTests()
    {
        _sut = new AdvancedPermissionService(
            _repository, _resolver, _userService, new AdvancedPermissionCache(AppCaches.NoCache), _eventAggregator, _logger);
    }

    /// <summary>
    /// Saving entries must publish an <see cref="AdvancedPermissionsChangedNotification"/> naming the
    /// exact node, user group and verbs that were written, so a consumer can react to the change
    /// without depending on how the write happened.
    /// </summary>
    [Fact]
    public async Task SaveEntriesAsync_Publishes_ChangedNotification()
    {
        var nodeKey = Guid.NewGuid();
        const string role = "editors";
        var entries = new[]
        {
            (VerbRead, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants, false),
            (VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeOnly, false),
        };

        await _sut.SaveEntriesAsync(nodeKey, role, entries);

        await _eventAggregator.Received(1).PublishAsync(
            Arg.Is<AdvancedPermissionsChangedNotification>(n =>
                n.NodeKey == nodeKey &&
                n.RoleAlias == role &&
                n.Verbs.Count == 2 &&
                n.Verbs.Contains(VerbRead) &&
                n.Verbs.Contains(VerbDelete)),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Removing every entry for a node and role ("revert to inherit") is still a change: a consumer
    /// that never hears about it keeps showing permissions that no longer exist. The notification
    /// must still be published, carrying an empty verb list rather than being skipped.
    /// </summary>
    [Fact]
    public async Task SaveEntriesAsync_EmptyEntries_PublishesWithNoVerbs()
    {
        var nodeKey = Guid.NewGuid();
        const string role = "editors";

        await _sut.SaveEntriesAsync(nodeKey, role, []);

        await _eventAggregator.Received(1).PublishAsync(
            Arg.Is<AdvancedPermissionsChangedNotification>(n =>
                n.NodeKey == nodeKey &&
                n.RoleAlias == role &&
                n.Verbs.Count == 0),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The notification must be published strictly after the repository write AND the cache
    /// invalidation — never before, and never from a handler racing the invalidation. A client that
    /// refetches on an event which overtook the invalidation would read the very snapshot the change
    /// was meant to replace, and having consumed its one notification, would never ask again.
    /// This test substitutes the L1/L2 cache's underlying <see cref="IAppPolicyCache"/> (the one
    /// piece of <see cref="AdvancedPermissionCache"/> that is itself an interface) so the
    /// invalidation step, not just the write and the publish, is captured in the recorded order.
    /// </summary>
    [Fact]
    public async Task SaveEntriesAsync_PublishesAfterRepositoryWrite()
    {
        var order = new List<string>();

        _repository
            .When(r => r.SaveAsync(
                Arg.Any<Guid>(),
                Arg.Any<string>(),
                Arg.Any<IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)>>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>()))
            .Do(_ => order.Add("write"));

        var runtimeCache = Substitute.For<IAppPolicyCache>();
        runtimeCache.When(c => c.Clear(Arg.Any<string>())).Do(_ => order.Add("invalidate-role"));
        runtimeCache.When(c => c.ClearByKey(Arg.Any<string>())).Do(_ => order.Add("invalidate-all"));
        var cache = new AdvancedPermissionCache(
            new AppCaches(runtimeCache, NoAppCache.Instance, new IsolatedCaches(_ => NoAppCache.Instance)));

        _eventAggregator
            .When(a => a.PublishAsync(Arg.Any<AdvancedPermissionsChangedNotification>(), Arg.Any<CancellationToken>()))
            .Do(_ => order.Add("publish"));

        var sut = new AdvancedPermissionService(_repository, _resolver, _userService, cache, _eventAggregator, _logger);

        await sut.SaveEntriesAsync(
            Guid.NewGuid(), "editors", [(VerbRead, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)]);

        Assert.Equal(["write", "invalidate-role", "invalidate-all", "publish"], order);
    }

    /// <summary>
    /// Deleting a single entry publishes too, naming just that one verb — the same extensibility
    /// seam applies to deletes as it does to saves.
    /// </summary>
    [Fact]
    public async Task DeleteEntryAsync_Publishes_ChangedNotification()
    {
        var nodeKey = Guid.NewGuid();
        const string role = "editors";

        await _sut.DeleteEntryAsync(nodeKey, role, VerbRead);

        await _eventAggregator.Received(1).PublishAsync(
            Arg.Is<AdvancedPermissionsChangedNotification>(n =>
                n.NodeKey == nodeKey &&
                n.RoleAlias == role &&
                n.Verbs.Count == 1 &&
                n.Verbs[0] == VerbRead),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// SaveManyAsync writes a batch of node/user-group pairs in a single transaction, but a handler
    /// should still see the same granularity it would from N individual saves: one notification per
    /// pair, each naming only its own node and verbs, not a single opaque batch event.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_TwoPairs_PublishesTwoNotifications_OnePerPair()
    {
        var nodeKey1 = Guid.NewGuid();
        var nodeKey2 = Guid.NewGuid();
        const string role1 = "editors";
        const string role2 = "writers";

        var batch = new (Guid NodeKey, string RoleAlias, IReadOnlyList<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries, string? ExpectedStamp)[]
        {
            (nodeKey1, role1, [(VerbRead, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], null),
            (nodeKey2, role2, [(VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeOnly, false)], null),
        };

        await _sut.SaveManyAsync(batch);

        await _eventAggregator.Received(1).PublishAsync(
            Arg.Is<AdvancedPermissionsChangedNotification>(n =>
                n.NodeKey == nodeKey1 && n.RoleAlias == role1 && n.Verbs.Count == 1 && n.Verbs[0] == VerbRead),
            Arg.Any<CancellationToken>());
        await _eventAggregator.Received(1).PublishAsync(
            Arg.Is<AdvancedPermissionsChangedNotification>(n =>
                n.NodeKey == nodeKey2 && n.RoleAlias == role2 && n.Verbs.Count == 1 && n.Verbs[0] == VerbDelete),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// By the time the notification is published, the write has already committed and the cache has
    /// already been invalidated — the change is real. If a handler throws (a badly-behaved
    /// third-party subscriber, or our own Task 8 SignalR forwarder), that failure must not propagate
    /// out of SaveEntriesAsync: a controller catching it would return an error for a save that in
    /// fact succeeded, and the editor would retry or reload and lose work over nothing. Swallowing
    /// the handler's exception is the correct behaviour — a degraded (silent) live update, not a
    /// false-negative save.
    /// </summary>
    [Fact]
    public async Task SaveEntriesAsync_NotificationHandlerThrows_CompletesWithoutThrowing()
    {
        _eventAggregator
            .PublishAsync(Arg.Any<AdvancedPermissionsChangedNotification>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Simulated handler failure.")));

        // Must not throw — the write already succeeded.
        await _sut.SaveEntriesAsync(
            Guid.NewGuid(), "editors", [(VerbRead, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)]);
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
        var nodeKey1 = Guid.NewGuid();
        var nodeKey2 = Guid.NewGuid();
        const string role1 = "editors";
        const string role2 = "writers";

        _eventAggregator
            .PublishAsync(
                Arg.Is<AdvancedPermissionsChangedNotification>(n => n.NodeKey == nodeKey1),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Simulated handler failure.")));

        var batch = new (Guid NodeKey, string RoleAlias, IReadOnlyList<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries, string? ExpectedStamp)[]
        {
            (nodeKey1, role1, [(VerbRead, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], null),
            (nodeKey2, role2, [(VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeOnly, false)], null),
        };

        // Must not throw, and the second pair's notification must still have gone out.
        await _sut.SaveManyAsync(batch);

        await _eventAggregator.Received(1).PublishAsync(
            Arg.Is<AdvancedPermissionsChangedNotification>(n => n.NodeKey == nodeKey2 && n.RoleAlias == role2),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// An empty batch changed nothing, so SaveManyAsync should skip the repository transaction and
    /// the L2 cache flush entirely rather than pay for a no-op write and an unnecessary full-cache
    /// invalidation.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_EmptyBatch_TouchesNeitherRepositoryNorAggregator()
    {
        await _sut.SaveManyAsync([]);

        await _repository.DidNotReceiveWithAnyArgs().SaveManyAsync(default!, default);
        await _eventAggregator.DidNotReceiveWithAnyArgs().PublishAsync(
            Arg.Any<AdvancedPermissionsChangedNotification>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A write the repository refuses because the stored entries moved changed nothing, so nothing
    /// may be announced. Publishing a change that did not happen would send every other editor to
    /// refetch for nothing; invalidating the caches would throw away entries that are still correct.
    /// The exception must reach the caller untouched, because the controller turns it into the 409.
    /// </summary>
    [Fact]
    public async Task SaveEntriesAsync_RepositoryReportsConflict_PropagatesWithoutInvalidatingOrPublishing()
    {
        var nodeKey = Guid.NewGuid();
        var conflict = new PermissionConcurrencyException([new PermissionConflict(nodeKey, "editors", [], "current")]);
        _repository
            .SaveAsync(
                Arg.Any<Guid>(),
                Arg.Any<string>(),
                Arg.Any<IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)>>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(conflict));

        var thrown = await Assert.ThrowsAsync<PermissionConcurrencyException>(() =>
            _sut.SaveEntriesAsync(nodeKey, "editors", [], "stale"));

        Assert.Same(conflict, thrown);
        await _eventAggregator.DidNotReceiveWithAnyArgs().PublishAsync(
            Arg.Any<AdvancedPermissionsChangedNotification>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The expected stamp has to reach the repository, because that is where the check is made —
    /// atomically with the write. A service that dropped it would silently turn every checked save
    /// into an unchecked one.
    /// </summary>
    [Fact]
    public async Task SaveEntriesAsync_ForwardsExpectedStampToRepository()
    {
        var nodeKey = Guid.NewGuid();

        await _sut.SaveEntriesAsync(nodeKey, "editors", [], "the-stamp");

        await _repository.Received(1).SaveAsync(
            nodeKey,
            "editors",
            Arg.Any<IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)>>(),
            "the-stamp",
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A batch the repository refuses on a stale stamp likewise propagates the exception and
    /// announces nothing — not even for the pairs that would have written cleanly, because the
    /// batch is all or nothing and none of them were written.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_RepositoryReportsConflict_PropagatesWithoutPublishing()
    {
        var nodeKey = Guid.NewGuid();
        _repository
            .SaveManyAsync(
                Arg.Any<IEnumerable<(Guid NodeKey, string RoleAlias, IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries, string? ExpectedStamp)>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new PermissionConcurrencyException([new PermissionConflict(nodeKey, "editors", [], "current")])));

        var batch = new (Guid NodeKey, string RoleAlias, IReadOnlyList<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries, string? ExpectedStamp)[]
        {
            (nodeKey, "editors", [(VerbRead, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], "stale"),
            (Guid.NewGuid(), "writers", [(VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeOnly, false)], "fine"),
        };

        await Assert.ThrowsAsync<PermissionConcurrencyException>(() => _sut.SaveManyAsync(batch));

        await _eventAggregator.DidNotReceiveWithAnyArgs().PublishAsync(
            Arg.Any<AdvancedPermissionsChangedNotification>(), Arg.Any<CancellationToken>());
    }
}
