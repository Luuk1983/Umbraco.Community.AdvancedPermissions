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
/// Tests that this package's notifications reach the server-event hub under the right source and key.
/// </summary>
public sealed class ServerEventHandlerTests
{
    /// <summary>A permission change is routed under the node-permissions source, keyed by node.</summary>
    [Fact]
    public async Task PermissionChanged_RoutesNodePermissionsEvent()
    {
        var router = Substitute.For<IServerEventRouter>();
        var handler = new AdvancedPermissionsServerEventHandler(router);
        var nodeKey = Guid.Parse("77777777-7777-7777-7777-777777777777");

        await handler.HandleAsync(
            new AdvancedPermissionsChangedNotification(nodeKey, "editors", ["Umb.Document.Read"]),
            CancellationToken.None);

        await router.Received(1).RouteEventAsync(Arg.Is<ServerEvent>(e =>
            e.EventSource == AdvancedPermissionsServerEvents.NodePermissionsSource &&
            e.EventType == AdvancedPermissionsServerEvents.EventType.Updated &&
            e.Key == nodeKey));
    }

    /// <summary>A document-type change is routed under its own source.</summary>
    [Fact]
    public async Task DocTypePermissionChanged_RoutesDocTypePermissionsEvent()
    {
        var router = Substitute.For<IServerEventRouter>();
        var handler = new AdvancedPermissionsServerEventHandler(router);
        var nodeKey = Guid.Parse("88888888-8888-8888-8888-888888888888");

        await handler.HandleAsync(
            new DocTypePermissionsChangedNotification(nodeKey, "editors", [Guid.NewGuid()]),
            CancellationToken.None);

        await router.Received(1).RouteEventAsync(Arg.Is<ServerEvent>(e =>
            e.EventSource == AdvancedPermissionsServerEvents.DocTypePermissionsSource &&
            e.Key == nodeKey));
    }
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
