using Microsoft.Extensions.Logging;
using NSubstitute;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Models.ServerEvents;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.ServerEvents;
using Umbraco.Community.AdvancedPermissions.ServerEvents;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests that <see cref="AccessServerEventHandler"/> routes the Umbraco-core notifications that shift
/// effective permissions without this package's own store being touched - user groups and users,
/// content, library elements and folders - and that a failure routing one key cannot propagate out of
/// the handler or block a sibling key.
/// </summary>
public sealed class AccessServerEventHandlerTests
{
    /// <summary>The router the handler under test routes through.</summary>
    private readonly IServerEventRouter _router = Substitute.For<IServerEventRouter>();

    /// <summary>The logger the handler under test records a swallowed failure to.</summary>
    private readonly ILogger<AccessServerEventHandler> _logger = Substitute.For<ILogger<AccessServerEventHandler>>();

    /// <summary>Builds the handler under test.</summary>
    /// <returns>A handler wired to the substitute router and logger.</returns>
    private AccessServerEventHandler CreateHandler() => new(_router, _logger);

    /// <summary>Asserts that one <c>Access</c> event was routed for the given key.</summary>
    /// <param name="key">The expected key.</param>
    private async Task AssertAccessRoutedAsync(Guid key) =>
        await _router.Received(1).RouteEventAsync(Arg.Is<ServerEvent>(e =>
            e.EventSource == AdvancedPermissionsServerEvents.AccessSource &&
            e.EventType == AdvancedPermissionsServerEvents.EventType.Updated &&
            e.Key == key));

    /// <summary>A saved user group is routed under the <c>Access</c> source, keyed by the group.</summary>
    [Fact]
    public async Task UserGroupSaved_RoutesAccessEvent()
    {
        var groupKey = Guid.NewGuid();

        await CreateHandler().HandleAsync(
            new UserGroupSavedNotification(BuildGroup(groupKey), new EventMessages()),
            CancellationToken.None);

        await AssertAccessRoutedAsync(groupKey);
    }

    /// <summary>A deleted user group is routed under the <c>Access</c> source, keyed by the group.</summary>
    [Fact]
    public async Task UserGroupDeleted_RoutesAccessEvent()
    {
        var groupKey = Guid.NewGuid();

        await CreateHandler().HandleAsync(
            new UserGroupDeletedNotification(BuildGroup(groupKey), new EventMessages()),
            CancellationToken.None);

        await AssertAccessRoutedAsync(groupKey);
    }

    /// <summary>A saved user is routed under the <c>Access</c> source, keyed by the user.</summary>
    [Fact]
    public async Task UserSaved_RoutesAccessEvent()
    {
        var userKey = Guid.NewGuid();

        await CreateHandler().HandleAsync(
            new UserSavedNotification(BuildUser(userKey), new EventMessages()),
            CancellationToken.None);

        await AssertAccessRoutedAsync(userKey);
    }

    /// <summary>
    /// A single moved content item is routed under the <c>Access</c> source, keyed by the entity - not
    /// by anything on <see cref="MoveEventInfo{TEntity}"/> itself, since the key must come from
    /// <c>MoveInfoCollection[].Entity.Key</c>.
    /// </summary>
    [Fact]
    public async Task ContentMoved_RoutesAccessEvent()
    {
        var contentKey = Guid.NewGuid();

        await CreateHandler().HandleAsync(
            new ContentMovedNotification(
                new MoveEventInfo<IContent>(BuildContent(contentKey), "-1,-20", -20),
                new EventMessages()),
            CancellationToken.None);

        await AssertAccessRoutedAsync(contentKey);
    }

    /// <summary>
    /// A move affecting several content items - e.g. moving a branch - routes one event per entity,
    /// each keyed correctly. This is the extraction most likely to be wrong, since
    /// <c>MoveInfoCollection</c> holds move records rather than entities directly.
    /// </summary>
    [Fact]
    public async Task ContentMoved_MultipleEntities_RoutesOneEventPerEntity()
    {
        var firstKey = Guid.NewGuid();
        var secondKey = Guid.NewGuid();

        await CreateHandler().HandleAsync(
            new ContentMovedNotification(
                [
                    new MoveEventInfo<IContent>(BuildContent(firstKey), "-1,-20", -20),
                    new MoveEventInfo<IContent>(BuildContent(secondKey), "-1,-21", -21),
                ],
                new EventMessages()),
            CancellationToken.None);

        await AssertAccessRoutedAsync(firstKey);
        await AssertAccessRoutedAsync(secondKey);
    }

    /// <summary>A content item moved to the recycle bin is routed under the <c>Access</c> source.</summary>
    [Fact]
    public async Task ContentMovedToRecycleBin_RoutesAccessEvent()
    {
        var contentKey = Guid.NewGuid();

        await CreateHandler().HandleAsync(
            new ContentMovedToRecycleBinNotification(
                new MoveToRecycleBinEventInfo<IContent>(BuildContent(contentKey), "-1,-20"),
                new EventMessages()),
            CancellationToken.None);

        await AssertAccessRoutedAsync(contentKey);
    }

    /// <summary>A permanently deleted content item is routed under the <c>Access</c> source.</summary>
    [Fact]
    public async Task ContentDeleted_RoutesAccessEvent()
    {
        var contentKey = Guid.NewGuid();

        await CreateHandler().HandleAsync(
            new ContentDeletedNotification(BuildContent(contentKey), new EventMessages()),
            CancellationToken.None);

        await AssertAccessRoutedAsync(contentKey);
    }

    /// <summary>
    /// A moved library element is routed under the <c>Access</c> source: its position in the element
    /// tree decides which folder's entries it inherits, so its effective permissions moved although no
    /// entry was written.
    /// </summary>
    [Fact]
    public async Task ElementMoved_RoutesAccessEvent()
    {
        var elementKey = Guid.NewGuid();

        await CreateHandler().HandleAsync(
            new ElementMovedNotification(
                new MoveEventInfo<IElement>(BuildElement(elementKey), "-1,-20", -20),
                new EventMessages()),
            CancellationToken.None);

        await AssertAccessRoutedAsync(elementKey);
    }

    /// <summary>A library element moved to the recycle bin is routed under the <c>Access</c> source.</summary>
    [Fact]
    public async Task ElementMovedToRecycleBin_RoutesAccessEvent()
    {
        var elementKey = Guid.NewGuid();

        await CreateHandler().HandleAsync(
            new ElementMovedToRecycleBinNotification(
                new MoveToRecycleBinEventInfo<IElement>(BuildElement(elementKey), "-1,-20"),
                new EventMessages()),
            CancellationToken.None);

        await AssertAccessRoutedAsync(elementKey);
    }

    /// <summary>A permanently deleted library element is routed under the <c>Access</c> source.</summary>
    [Fact]
    public async Task ElementDeleted_RoutesAccessEvent()
    {
        var elementKey = Guid.NewGuid();

        await CreateHandler().HandleAsync(
            new ElementDeletedNotification(BuildElement(elementKey), new EventMessages()),
            CancellationToken.None);

        await AssertAccessRoutedAsync(elementKey);
    }

    /// <summary>
    /// A moved folder is routed under the <c>Access</c> source, keyed by the folder: everything beneath
    /// it inherits from a new place. The notification is generic across container types, so a folder of
    /// another kind also wakes the viewers - a spurious refetch, never a missed one.
    /// </summary>
    [Fact]
    public async Task EntityContainerMoved_RoutesAccessEvent()
    {
        var containerKey = Guid.NewGuid();

        await CreateHandler().HandleAsync(
            new EntityContainerMovedNotification(
                new MoveEventInfo<EntityContainer>(BuildContainer(containerKey), "-1,-20", -20),
                new EventMessages()),
            CancellationToken.None);

        await AssertAccessRoutedAsync(containerKey);
    }

    /// <summary>A folder moved to the recycle bin is routed under the <c>Access</c> source.</summary>
    [Fact]
    public async Task EntityContainerMovedToRecycleBin_RoutesAccessEvent()
    {
        var containerKey = Guid.NewGuid();

        await CreateHandler().HandleAsync(
            new EntityContainerMovedToRecycleBinNotification(
                new MoveToRecycleBinEventInfo<EntityContainer>(BuildContainer(containerKey), "-1,-20"),
                new EventMessages()),
            CancellationToken.None);

        await AssertAccessRoutedAsync(containerKey);
    }

    /// <summary>A permanently deleted folder is routed under the <c>Access</c> source.</summary>
    [Fact]
    public async Task EntityContainerDeleted_RoutesAccessEvent()
    {
        var containerKey = Guid.NewGuid();

        await CreateHandler().HandleAsync(
            new EntityContainerDeletedNotification(BuildContainer(containerKey), new EventMessages()),
            CancellationToken.None);

        await AssertAccessRoutedAsync(containerKey);
    }

    /// <summary>
    /// The router throwing for one key must not propagate out of the handler, and must not stop the
    /// event for a sibling key from going out. Umbraco publishes the notifications this handler consumes
    /// before its own scope completes, with no surrounding try/catch - an exception escaping here would
    /// roll back the editor's content or user-group operation over nothing more than a failed
    /// live-update push.
    /// </summary>
    [Fact]
    public async Task RouterThrowsForOneKey_DoesNotPropagate_AndSiblingKeyStillRoutes()
    {
        var failingKey = Guid.NewGuid();
        var okKey = Guid.NewGuid();

        _router
            .RouteEventAsync(Arg.Is<ServerEvent>(e => e.Key == failingKey))
            .Returns(Task.FromException(new InvalidOperationException("Simulated hub failure.")));

        // Must not throw - the underlying content/user-group write already committed.
        await CreateHandler().HandleAsync(
            new ContentMovedNotification(
                [
                    new MoveEventInfo<IContent>(BuildContent(failingKey), "-1,-20", -20),
                    new MoveEventInfo<IContent>(BuildContent(okKey), "-1,-21", -21),
                ],
                new EventMessages()),
            CancellationToken.None);

        await AssertAccessRoutedAsync(okKey);
        Assert.Contains(
            _logger.ReceivedCalls(),
            c => c.GetMethodInfo().Name == nameof(ILogger.Log) && c.GetArguments()[0] is LogLevel.Error);
    }

    /// <summary>
    /// The same isolation holds for the library notifications: a failure routing an element or a folder
    /// is logged and swallowed, never thrown into the scope that raised it.
    /// </summary>
    [Fact]
    public async Task RouterThrowsForLibraryNotifications_DoesNotPropagate()
    {
        _router
            .RouteEventAsync(Arg.Any<ServerEvent>())
            .Returns(Task.FromException(new InvalidOperationException("Simulated hub failure.")));
        var handler = CreateHandler();

        await handler.HandleAsync(
            new ElementDeletedNotification(BuildElement(Guid.NewGuid()), new EventMessages()),
            CancellationToken.None);
        await handler.HandleAsync(
            new EntityContainerDeletedNotification(BuildContainer(Guid.NewGuid()), new EventMessages()),
            CancellationToken.None);
    }

    /// <summary>
    /// A cancelled request is not a handler failure and must not be swallowed: hiding a genuine
    /// cancellation would leave the caller running on.
    /// </summary>
    [Fact]
    public async Task RouterCancelled_RethrowsOperationCanceled()
    {
        _router
            .RouteEventAsync(Arg.Any<ServerEvent>())
            .Returns(Task.FromException(new OperationCanceledException()));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateHandler().HandleAsync(
            new ContentDeletedNotification(BuildContent(Guid.NewGuid()), new EventMessages()),
            CancellationToken.None));
    }

    /// <summary>
    /// The events for a burst are routed concurrently, not one after another: the handler runs inside
    /// the caller's scope, so a serial loop would multiply per-call latency by the number of keys on
    /// the path that keeps the transaction open. The first route is held open, and the second must
    /// start regardless.
    /// </summary>
    [Fact]
    public async Task MultipleKeys_AreRoutedConcurrently()
    {
        var firstKey = Guid.NewGuid();
        var secondKey = Guid.NewGuid();
        var gate = new TaskCompletionSource();
        _router
            .RouteEventAsync(Arg.Is<ServerEvent>(e => e.Key == firstKey))
            .Returns(gate.Task);

        var handling = CreateHandler().HandleAsync(
            new ContentMovedNotification(
                [
                    new MoveEventInfo<IContent>(BuildContent(firstKey), "-1,-20", -20),
                    new MoveEventInfo<IContent>(BuildContent(secondKey), "-1,-21", -21),
                ],
                new EventMessages()),
            CancellationToken.None);

        await AssertAccessRoutedAsync(secondKey);
        Assert.False(handling.IsCompleted);

        gate.SetResult();
        await handling;
    }

    /// <summary>Builds a substitute <see cref="IUserGroup"/> with the given key.</summary>
    /// <param name="key">The group's key.</param>
    /// <returns>The substitute user group.</returns>
    private static IUserGroup BuildGroup(Guid key)
    {
        var group = Substitute.For<IUserGroup>();
        group.Key.Returns(key);
        return group;
    }

    /// <summary>Builds a substitute <see cref="IUser"/> with the given key.</summary>
    /// <param name="key">The user's key.</param>
    /// <returns>The substitute user.</returns>
    private static IUser BuildUser(Guid key)
    {
        var user = Substitute.For<IUser>();
        user.Key.Returns(key);
        return user;
    }

    /// <summary>Builds a substitute <see cref="IContent"/> with the given key.</summary>
    /// <param name="key">The content item's key.</param>
    /// <returns>The substitute content item.</returns>
    private static IContent BuildContent(Guid key)
    {
        var content = Substitute.For<IContent>();
        content.Key.Returns(key);
        return content;
    }

    /// <summary>Builds a substitute <see cref="IElement"/> with the given key.</summary>
    /// <param name="key">The element's key.</param>
    /// <returns>The substitute element.</returns>
    private static IElement BuildElement(Guid key)
    {
        var element = Substitute.For<IElement>();
        element.Key.Returns(key);
        return element;
    }

    /// <summary>Builds an element folder with the given key.</summary>
    /// <param name="key">The folder's key.</param>
    /// <returns>The folder.</returns>
    private static EntityContainer BuildContainer(Guid key) =>
        new(global::Umbraco.Cms.Core.Constants.ObjectTypes.Element) { Key = key };
}
