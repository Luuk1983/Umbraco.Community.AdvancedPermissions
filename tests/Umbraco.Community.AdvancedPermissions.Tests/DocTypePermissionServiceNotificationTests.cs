using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.Caching;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Exceptions;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;
using Umbraco.Community.AdvancedPermissions.Notifications;
using Umbraco.Community.AdvancedPermissions.Services;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests the notification, ordering and isolation behaviour of <see cref="DocTypePermissionService"/>.
/// </summary>
/// <remarks>
/// Document-type permissions have a three-part key (node, user group, document type), so they do not
/// share the node-keyed service base and carry their own copy of the same rules. Those rules are the
/// same ones <see cref="NodePermissionServiceNotificationTestsBase{TNotification}"/> proves for the
/// other two families: publish strictly after cache invalidation, isolate a failing handler but let a
/// cancellation through, and do nothing at all for an empty batch.
/// It additionally proves the family discriminator: the notification says whether the change is to a
/// document type or an element type, decided server-side from the content type itself.
/// </remarks>
public sealed class DocTypePermissionServiceNotificationTests
{
    /// <summary>The event aggregator substitute the service publishes through.</summary>
    private readonly IEventAggregator _eventAggregator = Substitute.For<IEventAggregator>();

    /// <summary>The logger substitute a swallowed handler failure is written to.</summary>
    private readonly ILogger<DocTypePermissionService> _logger = Substitute.For<ILogger<DocTypePermissionService>>();

    /// <summary>The repository substitute the service writes through.</summary>
    private readonly IDocTypePermissionRepository _repository = Substitute.For<IDocTypePermissionRepository>();

    /// <summary>The content type service substitute the family discriminator is resolved through.</summary>
    private readonly IContentTypeService _contentTypeService = Substitute.For<IContentTypeService>();

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
            _contentTypeService,
            new DocTypePermissionCache(appCaches),
            _eventAggregator,
            _logger);

    /// <summary>
    /// Saving a triple must publish a <see cref="DocTypePermissionsChangedNotification"/> naming the
    /// node, user group and document type that were written.
    /// </summary>
    [Fact]
    public async Task SaveEditorEntriesAsync_Publishes_ChangedNotification()
    {
        var sut = CreateService(AppCaches.NoCache);
        var nodeKey = Guid.NewGuid();
        var typeKey = Guid.NewGuid();

        await sut.SaveEditorEntriesAsync(
            nodeKey,
            "editors",
            typeKey,
            [(AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)]);

        await _eventAggregator.Received(1).PublishAsync(
            Arg.Is<DocTypePermissionsChangedNotification>(n =>
                n.NodeKey == nodeKey &&
                n.RoleAlias == "editors" &&
                n.ContentTypeKey == typeKey),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Clearing a triple (an empty entry list) is still a change and must still be announced.
    /// </summary>
    [Fact]
    public async Task SaveEditorEntriesAsync_EmptyEntries_StillPublishes()
    {
        var sut = CreateService(AppCaches.NoCache);

        await sut.SaveEditorEntriesAsync(Guid.NewGuid(), "editors", Guid.NewGuid(), []);

        await _eventAggregator.Received(1).PublishAsync(
            Arg.Any<DocTypePermissionsChangedNotification>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The notification must be published strictly after the repository write AND the cache
    /// invalidation. Records write, then both invalidations, then the publish, and fails if the
    /// publish moves above the invalidation: a client that refetches on an event that overtook the
    /// invalidation reads the snapshot the change replaced and never asks again.
    /// </summary>
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

        await sut.SaveEditorEntriesAsync(
            Guid.NewGuid(),
            "editors",
            Guid.NewGuid(),
            [(AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)]);

        Assert.Equal(["write", "invalidate-role", "invalidate-all", "publish"], order);
    }

    /// <summary>
    /// A batch publishes one notification per triple, each naming only its own node, user group and
    /// document type, so a handler sees the same granularity it would from single saves.
    /// </summary>
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
            (node1, "editors", type1, [(AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], null),
            (node2, "writers", type2, [(AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Deny, PermissionScope.ThisNodeOnly, false)], null),
        ]);

        await _eventAggregator.Received(1).PublishAsync(
            Arg.Is<DocTypePermissionsChangedNotification>(n =>
                n.NodeKey == node1 && n.RoleAlias == "editors" && n.ContentTypeKey == type1),
            Arg.Any<CancellationToken>());
        await _eventAggregator.Received(1).PublishAsync(
            Arg.Is<DocTypePermissionsChangedNotification>(n =>
                n.NodeKey == node2 && n.RoleAlias == "writers" && n.ContentTypeKey == type2),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// For a batch, the write and every invalidation happen before the first notification goes out;
    /// the flush is once per distinct user group, then once for all resolved permissions.
    /// </summary>
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
            (Guid.NewGuid(), "editors", Guid.NewGuid(), [(AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], null),
            (Guid.NewGuid(), "writers", Guid.NewGuid(), [(AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Deny, PermissionScope.ThisNodeOnly, false)], null),
        ]);

        Assert.Equal(
            ["write", "invalidate-role", "invalidate-role", "invalidate-all", "publish", "publish"],
            order);
    }

    /// <summary>
    /// A handler that throws must not turn a committed write into a reported failure; the failure is
    /// logged at error level and the save completes.
    /// </summary>
    [Fact]
    public async Task SaveEditorEntriesAsync_HandlerThrows_CompletesAndLogsAnError()
    {
        _eventAggregator
            .PublishAsync(Arg.Any<DocTypePermissionsChangedNotification>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Simulated handler failure.")));
        var sut = CreateService(AppCaches.NoCache);

        await sut.SaveEditorEntriesAsync(Guid.NewGuid(), "editors", Guid.NewGuid(), []);

        Assert.True(LoggedAnError());
    }

    /// <summary>
    /// A handler that throws synchronously is isolated just the same.
    /// </summary>
    [Fact]
    public async Task SaveEditorEntriesAsync_HandlerThrowsSynchronously_CompletesWithoutThrowing()
    {
        _eventAggregator
            .PublishAsync(Arg.Any<DocTypePermissionsChangedNotification>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Simulated synchronous handler failure."));
        var sut = CreateService(AppCaches.NoCache);

        await sut.SaveEditorEntriesAsync(Guid.NewGuid(), "editors", Guid.NewGuid(), []);

        Assert.True(LoggedAnError());
    }

    /// <summary>
    /// A cancelled request is not a handler failure and must propagate rather than be swallowed.
    /// </summary>
    [Fact]
    public async Task SaveEditorEntriesAsync_PublishCancelled_RethrowsOperationCanceled()
    {
        _eventAggregator
            .PublishAsync(Arg.Any<DocTypePermissionsChangedNotification>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new OperationCanceledException()));
        var sut = CreateService(AppCaches.NoCache);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            sut.SaveEditorEntriesAsync(Guid.NewGuid(), "editors", Guid.NewGuid(), []));
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
            (Guid.NewGuid(), "editors", Guid.NewGuid(), [(AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], null),
        ]));
    }

    /// <summary>
    /// If the first triple's handler throws, the remaining triples are still announced.
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
            (node1, "editors", Guid.NewGuid(), [(AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], null),
            (node2, "writers", Guid.NewGuid(), [(AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Deny, PermissionScope.ThisNodeOnly, false)], null),
        ]);

        await _eventAggregator.Received(1).PublishAsync(
            Arg.Is<DocTypePermissionsChangedNotification>(n => n.NodeKey == node2),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// An empty batch changed nothing: it must not touch the repository, the cache or the aggregator.
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
    /// If the write fails, nothing changed: the cache is not flushed and nothing is published.
    /// </summary>
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
            (Guid.NewGuid(), "editors", Guid.NewGuid(), [(AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], null),
        ]));

        Assert.Empty(runtimeCache.ReceivedCalls());
        await _eventAggregator.DidNotReceiveWithAnyArgs().PublishAsync(
            Arg.Any<DocTypePermissionsChangedNotification>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A change to a document type is announced as <see cref="DocTypePermissionFamily.Document"/>, so
    /// only the surfaces that show document-type permissions are woken.
    /// </summary>
    [Fact]
    public async Task SaveEditorEntriesAsync_DocumentType_PublishesDocumentFamily()
    {
        var typeKey = Guid.NewGuid();
        GivenContentType(typeKey, isElement: false);
        var sut = CreateService(AppCaches.NoCache);

        await sut.SaveEditorEntriesAsync(
            Guid.NewGuid(),
            "editors",
            typeKey,
            [(AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)]);

        await AssertPublishedFamily(typeKey, DocTypePermissionFamily.Document);
    }

    /// <summary>
    /// A change to an element type is announced as <see cref="DocTypePermissionFamily.Element"/>.
    /// </summary>
    [Fact]
    public async Task SaveEditorEntriesAsync_ElementType_PublishesElementFamily()
    {
        var typeKey = Guid.NewGuid();
        GivenContentType(typeKey, isElement: true);
        var sut = CreateService(AppCaches.NoCache);

        await sut.SaveEditorEntriesAsync(
            Guid.NewGuid(),
            "editors",
            typeKey,
            [(AdvancedPermissionsConstants.VerbElementCreateOfType, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)]);

        await AssertPublishedFamily(typeKey, DocTypePermissionFamily.Element);
    }

    /// <summary>
    /// A content type that no longer exists (or cannot be found) is announced as
    /// <see cref="DocTypePermissionFamily.Unknown"/>, which consumers must treat as "wake both": a
    /// spurious wake costs one refetch that reconciles to no change, whereas a missed wake is a
    /// silently stale screen.
    /// </summary>
    [Fact]
    public async Task SaveEditorEntriesAsync_MissingContentType_PublishesUnknownFamily()
    {
        var typeKey = Guid.NewGuid();
        _contentTypeService.GetAsync(typeKey).Returns(Task.FromResult<IContentType?>(null));
        var sut = CreateService(AppCaches.NoCache);

        await sut.SaveEditorEntriesAsync(
            Guid.NewGuid(),
            "editors",
            typeKey,
            [(AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)]);

        await AssertPublishedFamily(typeKey, DocTypePermissionFamily.Unknown);
    }

    /// <summary>
    /// The family comes from the content type, never from the verb. The save endpoint accepts either
    /// verb family for any content type key, so an element type saved with the document-type verb is
    /// still an element-type change.
    /// </summary>
    [Fact]
    public async Task SaveEditorEntriesAsync_FamilyFollowsTheContentType_NotTheVerb()
    {
        var elementTypeKey = Guid.NewGuid();
        var documentTypeKey = Guid.NewGuid();
        GivenContentType(elementTypeKey, isElement: true);
        GivenContentType(documentTypeKey, isElement: false);
        var sut = CreateService(AppCaches.NoCache);

        await sut.SaveEditorEntriesAsync(
            Guid.NewGuid(),
            "editors",
            elementTypeKey,
            [(AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)]);
        await sut.SaveEditorEntriesAsync(
            Guid.NewGuid(),
            "editors",
            documentTypeKey,
            [(AdvancedPermissionsConstants.VerbElementCreateOfType, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)]);

        await AssertPublishedFamily(elementTypeKey, DocTypePermissionFamily.Element);
        await AssertPublishedFamily(documentTypeKey, DocTypePermissionFamily.Document);
    }

    /// <summary>
    /// A save that clears every entry carries no verbs at all, which is exactly the case where a
    /// verb-based guess would have nothing to go on. The family must still be resolved.
    /// </summary>
    [Fact]
    public async Task SaveEditorEntriesAsync_EmptyEntries_StillResolvesTheFamily()
    {
        var typeKey = Guid.NewGuid();
        GivenContentType(typeKey, isElement: true);
        var sut = CreateService(AppCaches.NoCache);

        await sut.SaveEditorEntriesAsync(Guid.NewGuid(), "editors", typeKey, []);

        await AssertPublishedFamily(typeKey, DocTypePermissionFamily.Element);
    }

    /// <summary>
    /// If the content type lookup itself throws, the write has already committed and must not be
    /// reported as failed: the change is announced as <see cref="DocTypePermissionFamily.Unknown"/>
    /// (wake both) and the save completes.
    /// </summary>
    [Fact]
    public async Task SaveEditorEntriesAsync_ContentTypeLookupThrows_PublishesUnknownAndCompletes()
    {
        var typeKey = Guid.NewGuid();
        _contentTypeService
            .GetAsync(typeKey)
            .Returns(Task.FromException<IContentType?>(new InvalidOperationException("Simulated lookup failure.")));
        var sut = CreateService(AppCaches.NoCache);

        await sut.SaveEditorEntriesAsync(Guid.NewGuid(), "editors", typeKey, []);

        await AssertPublishedFamily(typeKey, DocTypePermissionFamily.Unknown);
    }

    /// <summary>
    /// In a batch each triple gets the family of its own content type, so one save spanning a document
    /// type and an element type wakes each surface exactly once.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_MixedTypes_PublishesEachTriplesOwnFamily()
    {
        var documentTypeKey = Guid.NewGuid();
        var elementTypeKey = Guid.NewGuid();
        var missingTypeKey = Guid.NewGuid();
        GivenContentType(documentTypeKey, isElement: false);
        GivenContentType(elementTypeKey, isElement: true);
        _contentTypeService.GetAsync(missingTypeKey).Returns(Task.FromResult<IContentType?>(null));
        var sut = CreateService(AppCaches.NoCache);

        await sut.SaveManyAsync(
        [
            (Guid.NewGuid(), "editors", documentTypeKey, [(AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], null),
            (Guid.NewGuid(), "editors", elementTypeKey, [(AdvancedPermissionsConstants.VerbElementCreateOfType, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], null),
            (Guid.NewGuid(), "editors", missingTypeKey, [], null),
        ]);

        await AssertPublishedFamily(documentTypeKey, DocTypePermissionFamily.Document);
        await AssertPublishedFamily(elementTypeKey, DocTypePermissionFamily.Element);
        await AssertPublishedFamily(missingTypeKey, DocTypePermissionFamily.Unknown);
    }

    /// <summary>
    /// A batch touching one content type on several nodes looks that type up once, not once per
    /// triple.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_SameContentTypeOnSeveralNodes_LooksItUpOnce()
    {
        var typeKey = Guid.NewGuid();
        GivenContentType(typeKey, isElement: false);
        var sut = CreateService(AppCaches.NoCache);

        await sut.SaveManyAsync(
        [
            (Guid.NewGuid(), "editors", typeKey, [(AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], null),
            (Guid.NewGuid(), "editors", typeKey, [(AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Deny, PermissionScope.ThisNodeOnly, false)], null),
        ]);

        await _contentTypeService.Received(1).GetAsync(typeKey);
    }

    /// <summary>
    /// Two triples for the same user group (different nodes) invalidate that group's cached entries
    /// once, not once per triple: the flush is per distinct user group.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_InvalidatesEachDistinctUserGroupOnce()
    {
        var runtimeCache = Substitute.For<IAppPolicyCache>();
        var sut = CreateService(AppCachesOver(runtimeCache));

        await sut.SaveManyAsync(
        [
            (Guid.NewGuid(), "editors", Guid.NewGuid(), [(AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], null),
            (Guid.NewGuid(), "editors", Guid.NewGuid(), [(AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Deny, PermissionScope.ThisNodeOnly, false)], null),
            (Guid.NewGuid(), "writers", Guid.NewGuid(), [(AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Deny, PermissionScope.ThisNodeOnly, false)], null),
        ]);

        runtimeCache.Received(1).Clear(Arg.Is<string>(k => k.EndsWith(".L1.editors", StringComparison.Ordinal)));
        runtimeCache.Received(1).Clear(Arg.Is<string>(k => k.EndsWith(".L1.writers", StringComparison.Ordinal)));
    }

    /// <summary>
    /// The single-triple save has the same rule as the batch: a failed write invalidates nothing and
    /// publishes nothing.
    /// </summary>
    [Fact]
    public async Task SaveEditorEntries_RepositoryThrows_PublishesNothing()
    {
        var runtimeCache = Substitute.For<IAppPolicyCache>();
        _repository
            .SaveAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(), AnyEntries(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Simulated write failure.")));
        var sut = CreateService(AppCachesOver(runtimeCache));

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.SaveEditorEntriesAsync(
            Guid.NewGuid(),
            "editors",
            Guid.NewGuid(),
            [(AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)]));

        Assert.Empty(runtimeCache.ReceivedCalls());
        await _eventAggregator.DidNotReceiveWithAnyArgs().PublishAsync(
            Arg.Any<DocTypePermissionsChangedNotification>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The batch is handed to the repository in one call, so the repository's transaction covers every
    /// triple.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_HandsTheWholeBatchToTheRepositoryInOneCall()
    {
        var sut = CreateService(AppCaches.NoCache);
        var node1 = Guid.NewGuid();
        var node2 = Guid.NewGuid();

        await sut.SaveManyAsync(
        [
            (node1, "editors", Guid.NewGuid(), [(AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], null),
            (node2, "writers", Guid.NewGuid(), [(AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Deny, PermissionScope.ThisNodeOnly, false)], null),
        ]);

        await _repository.Received(1).SaveManyAsync(
            Arg.Is<IEnumerable<(Guid NodeKey, string RoleAlias, Guid ContentTypeKey, IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries, string? ExpectedStamp)>>(
                b => b.Count() == 2 && b.Any(t => t.NodeKey == node1) && b.Any(t => t.NodeKey == node2)),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Makes the content type service report a content type of the given kind.
    /// </summary>
    /// <param name="key">The content type key.</param>
    /// <param name="isElement">Whether the content type is an element type.</param>
    private void GivenContentType(Guid key, bool isElement)
    {
        var contentType = Substitute.For<IContentType>();
        contentType.IsElement.Returns(isElement);
        _contentTypeService.GetAsync(key).Returns(Task.FromResult<IContentType?>(contentType));
    }

    /// <summary>
    /// Asserts that a notification for the given content type was published carrying the given family.
    /// </summary>
    /// <param name="contentTypeKey">The content type the notification concerns.</param>
    /// <param name="family">The family it must carry.</param>
    /// <returns>A task that completes once the assertion has been made.</returns>
    private async Task AssertPublishedFamily(Guid contentTypeKey, DocTypePermissionFamily family) =>
        await _eventAggregator.Received(1).PublishAsync(
            Arg.Is<DocTypePermissionsChangedNotification>(n =>
                n.ContentTypeKey == contentTypeKey && n.Family == family),
            Arg.Any<CancellationToken>());

    /// <summary>
    /// Gets whether the logger substitute recorded a call at error level.
    /// </summary>
    /// <returns><see langword="true"/> if an error was logged.</returns>
    private bool LoggedAnError() =>
        _logger.ReceivedCalls().Any(c =>
            c.GetMethodInfo().Name == nameof(ILogger.Log) &&
            c.GetArguments()[0] is LogLevel.Error);

    /// <summary>
    /// Records a "publish" marker each time the notification goes out.
    /// </summary>
    /// <param name="order">The list the marker is appended to.</param>
    private void RecordPublishes(List<string> order) =>
        _eventAggregator
            .When(a => a.PublishAsync(Arg.Any<DocTypePermissionsChangedNotification>(), Arg.Any<CancellationToken>()))
            .Do(_ => order.Add("publish"));

    /// <summary>
    /// Builds caches whose runtime cache records each invalidation.
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
    private static IEnumerable<(Guid NodeKey, string RoleAlias, Guid ContentTypeKey, IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries, string? ExpectedStamp)> AnyBatch() =>
        Arg.Any<IEnumerable<(Guid NodeKey, string RoleAlias, Guid ContentTypeKey, IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries, string? ExpectedStamp)>>();

    /// <summary>
    /// A stale stamp makes the repository refuse the batch. The service must let that refusal reach the
    /// caller untouched and do nothing else: nothing was written, so no cache is flushed and no
    /// notification goes out.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_StaleStamp_PropagatesAndInvalidatesNothingAndPublishesNothing()
    {
        var runtimeCache = Substitute.For<IAppPolicyCache>();
        var conflict = new DocTypePermissionConflict(Guid.NewGuid(), "editors", Guid.NewGuid(), [], "current-stamp");
        _repository
            .SaveManyAsync(AnyBatch(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new DocTypePermissionConcurrencyException([conflict])));
        var sut = CreateService(AppCachesOver(runtimeCache));

        var thrown = await Assert.ThrowsAsync<DocTypePermissionConcurrencyException>(() => sut.SaveManyAsync(
        [
            (conflict.NodeKey, "editors", conflict.ContentTypeKey, [(AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], "stale"),
        ]));

        Assert.Same(conflict, Assert.Single(thrown.Conflicts));
        Assert.Empty(runtimeCache.ReceivedCalls());
        await _eventAggregator.DidNotReceiveWithAnyArgs().PublishAsync(
            Arg.Any<DocTypePermissionsChangedNotification>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The service does not check stamps itself - the repository does, inside its transaction - so each
    /// triple's expected stamp must reach the repository exactly as supplied, including a null one that
    /// tells it to skip the check for that triple.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_ForwardsEachTriplesExpectedStampToTheRepository()
    {
        var sut = CreateService(AppCaches.NoCache);
        var checkedNode = Guid.NewGuid();
        var uncheckedNode = Guid.NewGuid();

        await sut.SaveManyAsync(
        [
            (checkedNode, "editors", Guid.NewGuid(), [(AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], "the-stamp"),
            (uncheckedNode, "editors", Guid.NewGuid(), [(AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Deny, PermissionScope.ThisNodeOnly, false)], null),
        ]);

        await _repository.Received(1).SaveManyAsync(
            Arg.Is<IEnumerable<(Guid NodeKey, string RoleAlias, Guid ContentTypeKey, IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries, string? ExpectedStamp)>>(
                b => b.Single(t => t.NodeKey == checkedNode).ExpectedStamp == "the-stamp"
                     && b.Single(t => t.NodeKey == uncheckedNode).ExpectedStamp == null),
            Arg.Any<CancellationToken>());
    }
}
