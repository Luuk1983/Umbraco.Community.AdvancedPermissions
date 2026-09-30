using Microsoft.Extensions.Logging;
using NSubstitute;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Models.ServerEvents;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.ServerEvents;
using Umbraco.Community.AdvancedPermissions.Notifications;
using Umbraco.Community.AdvancedPermissions.ServerEvents;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests that this package's own change notifications reach the server-event hub under the right
/// source and key, and that a failure to route one can never propagate back into the write that
/// raised it.
/// </summary>
public sealed class ServerEventHandlerTests
{
    /// <summary>The router the handler under test routes through.</summary>
    private readonly IServerEventRouter _router = Substitute.For<IServerEventRouter>();

    /// <summary>The logger the handler under test records a swallowed failure to.</summary>
    private readonly ILogger<AdvancedPermissionsServerEventHandler> _logger = Substitute.For<ILogger<AdvancedPermissionsServerEventHandler>>();

    /// <summary>Builds the handler under test.</summary>
    /// <returns>A handler wired to the substitute router and logger.</returns>
    private AdvancedPermissionsServerEventHandler CreateHandler() => new(_router, _logger);

    /// <summary>Gets whether the handler logged an error.</summary>
    /// <returns><see langword="true"/> if an error-level log entry was written.</returns>
    private bool LoggedAnError() =>
        _logger.ReceivedCalls().Any(c =>
            c.GetMethodInfo().Name == nameof(ILogger.Log) && c.GetArguments()[0] is LogLevel.Error);

    /// <summary>A permission change is routed under the node-permissions source, keyed by node.</summary>
    [Fact]
    public async Task PermissionChanged_RoutesNodePermissionsEvent()
    {
        var nodeKey = Guid.Parse("77777777-7777-7777-7777-777777777777");

        await CreateHandler().HandleAsync(
            new AdvancedPermissionsChangedNotification(nodeKey, "editors", ["Umb.Document.Read"]),
            CancellationToken.None);

        await _router.Received(1).RouteEventAsync(Arg.Is<ServerEvent>(e =>
            e.EventSource == AdvancedPermissionsServerEvents.NodePermissionsSource &&
            e.EventType == AdvancedPermissionsServerEvents.EventType.Updated &&
            e.Key == nodeKey));
    }

    /// <summary>A document-type change is routed under its own source.</summary>
    [Fact]
    public async Task DocTypePermissionChanged_RoutesDocTypePermissionsEvent()
    {
        var nodeKey = Guid.Parse("88888888-8888-8888-8888-888888888888");

        await CreateHandler().HandleAsync(
            new DocTypePermissionsChangedNotification(nodeKey, "editors", [Guid.NewGuid()]),
            CancellationToken.None);

        await _router.Received(1).RouteEventAsync(Arg.Is<ServerEvent>(e =>
            e.EventSource == AdvancedPermissionsServerEvents.DocTypePermissionsSource &&
            e.Key == nodeKey));
    }

    /// <summary>
    /// The router throwing must not propagate out of the handler: these notifications are published by
    /// the permission services after their write has committed, and an escaping exception would report
    /// a false failure for a save that succeeded. It is logged instead.
    /// </summary>
    /// <param name="notification">The notification to handle, of each kind the handler accepts.</param>
    [Theory]
    [MemberData(nameof(OwnNotifications))]
    public async Task RouterThrows_DoesNotPropagate_AndIsLogged(object notification)
    {
        _router
            .RouteEventAsync(Arg.Any<ServerEvent>())
            .Returns(Task.FromException(new InvalidOperationException("Simulated hub failure.")));

        await HandleAsync(CreateHandler(), notification, CancellationToken.None);

        Assert.True(LoggedAnError());
    }

    /// <summary>
    /// A router that throws synchronously, rather than returning a faulted task, is isolated too: the
    /// handler must not depend on which of the two ways a router happens to fail.
    /// </summary>
    /// <param name="notification">The notification to handle, of each kind the handler accepts.</param>
    [Theory]
    [MemberData(nameof(OwnNotifications))]
    public async Task RouterThrowsSynchronously_DoesNotPropagate_AndIsLogged(object notification)
    {
        _router
            .RouteEventAsync(Arg.Any<ServerEvent>())
            .Returns<Task>(_ => throw new InvalidOperationException("Simulated hub failure."));

        await HandleAsync(CreateHandler(), notification, CancellationToken.None);

        Assert.True(LoggedAnError());
    }

    /// <summary>
    /// A cancelled request is not a handler failure and must not be swallowed: hiding a genuine
    /// cancellation would leave the caller running on.
    /// </summary>
    /// <param name="notification">The notification to handle, of each kind the handler accepts.</param>
    [Theory]
    [MemberData(nameof(OwnNotifications))]
    public async Task RouterCancelled_RethrowsOperationCanceled(object notification)
    {
        _router
            .RouteEventAsync(Arg.Any<ServerEvent>())
            .Returns(Task.FromException(new OperationCanceledException()));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            HandleAsync(CreateHandler(), notification, CancellationToken.None));
    }

    /// <summary>One notification of each kind this handler accepts, for the theories above.</summary>
    /// <returns>The test cases.</returns>
    public static TheoryData<object> OwnNotifications() =>
    [
        new AdvancedPermissionsChangedNotification(Guid.NewGuid(), "editors", []),
        new DocTypePermissionsChangedNotification(Guid.NewGuid(), "editors", [Guid.NewGuid()]),
    ];

    /// <summary>Dispatches a notification to the handler method that accepts its type.</summary>
    /// <param name="handler">The handler.</param>
    /// <param name="notification">The notification.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The handler's task.</returns>
    private static Task HandleAsync(AdvancedPermissionsServerEventHandler handler, object notification, CancellationToken cancellationToken) =>
        notification switch
        {
            AdvancedPermissionsChangedNotification n => handler.HandleAsync(n, cancellationToken),
            DocTypePermissionsChangedNotification n => handler.HandleAsync(n, cancellationToken),
            _ => throw new ArgumentException($"Unexpected notification {notification.GetType().Name}.", nameof(notification)),
        };
}

/// <summary>
/// Tests that <see cref="AccessServerEventHandler"/> routes the six Umbraco-core notifications that
/// shift effective permissions without this package's own store being touched, and that a failure
/// routing one key cannot propagate out of the handler or block a sibling key.
/// </summary>
public sealed class AccessServerEventHandlerTests
{
    /// <summary>A saved user group is routed under the <c>Access</c> source, keyed by the group.</summary>
    [Fact]
    public async Task UserGroupSaved_RoutesAccessEvent()
    {
        var router = Substitute.For<IServerEventRouter>();
        var handler = CreateHandler(router);
        var groupKey = Guid.NewGuid();

        await handler.HandleAsync(
            new UserGroupSavedNotification(BuildGroup(groupKey), new EventMessages()),
            CancellationToken.None);

        await router.Received(1).RouteEventAsync(Arg.Is<ServerEvent>(e =>
            e.EventSource == AdvancedPermissionsServerEvents.AccessSource &&
            e.EventType == AdvancedPermissionsServerEvents.EventType.Updated &&
            e.Key == groupKey));
    }

    /// <summary>A deleted user group is routed under the <c>Access</c> source, keyed by the group.</summary>
    [Fact]
    public async Task UserGroupDeleted_RoutesAccessEvent()
    {
        var router = Substitute.For<IServerEventRouter>();
        var handler = CreateHandler(router);
        var groupKey = Guid.NewGuid();

        await handler.HandleAsync(
            new UserGroupDeletedNotification(BuildGroup(groupKey), new EventMessages()),
            CancellationToken.None);

        await router.Received(1).RouteEventAsync(Arg.Is<ServerEvent>(e =>
            e.EventSource == AdvancedPermissionsServerEvents.AccessSource &&
            e.Key == groupKey));
    }

    /// <summary>A saved user is routed under the <c>Access</c> source, keyed by the user.</summary>
    [Fact]
    public async Task UserSaved_RoutesAccessEvent()
    {
        var router = Substitute.For<IServerEventRouter>();
        var handler = CreateHandler(router);
        var userKey = Guid.NewGuid();

        await handler.HandleAsync(
            new UserSavedNotification(BuildUser(userKey), new EventMessages()),
            CancellationToken.None);

        await router.Received(1).RouteEventAsync(Arg.Is<ServerEvent>(e =>
            e.EventSource == AdvancedPermissionsServerEvents.AccessSource &&
            e.Key == userKey));
    }

    /// <summary>
    /// A single moved content item is routed under the <c>Access</c> source, keyed by the entity —
    /// not by anything on <see cref="MoveEventInfo{TEntity}"/> itself, since the key must come from
    /// <c>MoveInfoCollection[].Entity.Key</c>.
    /// </summary>
    [Fact]
    public async Task ContentMoved_RoutesAccessEvent()
    {
        var router = Substitute.For<IServerEventRouter>();
        var handler = CreateHandler(router);
        var contentKey = Guid.NewGuid();

        await handler.HandleAsync(
            new ContentMovedNotification(
                new MoveEventInfo<IContent>(BuildContent(contentKey), "-1,-20", -20),
                new EventMessages()),
            CancellationToken.None);

        await router.Received(1).RouteEventAsync(Arg.Is<ServerEvent>(e =>
            e.EventSource == AdvancedPermissionsServerEvents.AccessSource &&
            e.Key == contentKey));
    }

    /// <summary>
    /// A move affecting several content items — e.g. moving a branch — routes one event per entity,
    /// each keyed correctly. This is the extraction most likely to be wrong, since
    /// <c>MoveInfoCollection</c> holds move records rather than entities directly.
    /// </summary>
    [Fact]
    public async Task ContentMoved_MultipleEntities_RoutesOneEventPerEntity()
    {
        var router = Substitute.For<IServerEventRouter>();
        var handler = CreateHandler(router);
        var firstKey = Guid.NewGuid();
        var secondKey = Guid.NewGuid();

        await handler.HandleAsync(
            new ContentMovedNotification(
                [
                    new MoveEventInfo<IContent>(BuildContent(firstKey), "-1,-20", -20),
                    new MoveEventInfo<IContent>(BuildContent(secondKey), "-1,-21", -21),
                ],
                new EventMessages()),
            CancellationToken.None);

        await router.Received(1).RouteEventAsync(Arg.Is<ServerEvent>(e =>
            e.EventSource == AdvancedPermissionsServerEvents.AccessSource && e.Key == firstKey));
        await router.Received(1).RouteEventAsync(Arg.Is<ServerEvent>(e =>
            e.EventSource == AdvancedPermissionsServerEvents.AccessSource && e.Key == secondKey));
    }

    /// <summary>A content item moved to the recycle bin is routed under the <c>Access</c> source.</summary>
    [Fact]
    public async Task ContentMovedToRecycleBin_RoutesAccessEvent()
    {
        var router = Substitute.For<IServerEventRouter>();
        var handler = CreateHandler(router);
        var contentKey = Guid.NewGuid();

        await handler.HandleAsync(
            new ContentMovedToRecycleBinNotification(
                new MoveToRecycleBinEventInfo<IContent>(BuildContent(contentKey), "-1,-20"),
                new EventMessages()),
            CancellationToken.None);

        await router.Received(1).RouteEventAsync(Arg.Is<ServerEvent>(e =>
            e.EventSource == AdvancedPermissionsServerEvents.AccessSource &&
            e.Key == contentKey));
    }

    /// <summary>A permanently deleted content item is routed under the <c>Access</c> source.</summary>
    [Fact]
    public async Task ContentDeleted_RoutesAccessEvent()
    {
        var router = Substitute.For<IServerEventRouter>();
        var handler = CreateHandler(router);
        var contentKey = Guid.NewGuid();

        await handler.HandleAsync(
            new ContentDeletedNotification(BuildContent(contentKey), new EventMessages()),
            CancellationToken.None);

        await router.Received(1).RouteEventAsync(Arg.Is<ServerEvent>(e =>
            e.EventSource == AdvancedPermissionsServerEvents.AccessSource &&
            e.Key == contentKey));
    }

    /// <summary>
    /// The router throwing for one key must not propagate out of the handler, and must not stop the
    /// event for a sibling key from going out. Umbraco publishes the notifications this handler
    /// consumes before its own scope completes, with no surrounding try/catch — an exception
    /// escaping here would roll back the editor's content or user-group operation over nothing more
    /// than a failed live-update push.
    /// </summary>
    [Fact]
    public async Task RouterThrowsForOneKey_DoesNotPropagate_AndSiblingKeyStillRoutes()
    {
        var router = Substitute.For<IServerEventRouter>();
        var failingKey = Guid.NewGuid();
        var okKey = Guid.NewGuid();

        router
            .RouteEventAsync(Arg.Is<ServerEvent>(e => e.Key == failingKey))
            .Returns(Task.FromException(new InvalidOperationException("Simulated hub failure.")));

        var handler = CreateHandler(router);

        // Must not throw — the underlying content/user-group write already committed.
        await handler.HandleAsync(
            new ContentMovedNotification(
                [
                    new MoveEventInfo<IContent>(BuildContent(failingKey), "-1,-20", -20),
                    new MoveEventInfo<IContent>(BuildContent(okKey), "-1,-21", -21),
                ],
                new EventMessages()),
            CancellationToken.None);

        await router.Received(1).RouteEventAsync(Arg.Is<ServerEvent>(e => e.Key == okKey));
    }

    /// <summary>Builds an <see cref="AccessServerEventHandler"/> wired to the given router.</summary>
    /// <param name="router">The server-event router the handler routes through.</param>
    /// <returns>A handler ready to receive notifications.</returns>
    private static AccessServerEventHandler CreateHandler(IServerEventRouter router) =>
        new(router, Substitute.For<ILogger<AccessServerEventHandler>>());

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
}
