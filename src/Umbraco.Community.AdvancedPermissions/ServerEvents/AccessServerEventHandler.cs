using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.ServerEvents;

namespace Umbraco.Community.AdvancedPermissions.ServerEvents;

/// <summary>
/// Puts the Umbraco-side changes that shift effective permissions onto the server-events hub.
/// </summary>
/// <remarks>
/// <para>
/// Effective permissions move without this package's own store being touched at all: a user
/// joining a group, a group being deleted, a node moving to a new parent and inheriting a
/// different branch. A viewer showing a user whose group membership just changed is exactly as
/// wrong as one showing a node whose entries changed, and until now nothing told it so.
/// </para>
/// <para>
/// <b>Ordering.</b> These notifications also drive <c>AdvancedPermissionCacheInvalidator</c> and
/// <c>DocTypePermissionCacheInvalidator</c>. This handler must run after them, so a client refetching
/// on the event cannot read the cached value the change invalidated. Umbraco's <c>EventAggregator</c>
/// runs every synchronous <c>INotificationHandler</c> for a notification before it runs any
/// asynchronous <c>INotificationAsyncHandler</c>. The invalidators are synchronous and this handler is
/// asynchronous, so that ordering holds whatever the registration order. Registration order still
/// decides the order among the asynchronous handlers (the cleanup and seeding handlers, for example),
/// which is why <c>AdvancedPermissionsComposer</c> registers this one last - see the comment there.
/// </para>
/// <para>
/// <b>Isolation.</b> Unlike the handler for this package's own notifications, which run after their
/// write committed, these are core notifications published <i>before</i> <c>scope.Complete()</c> with
/// no surrounding try/catch. An escaping exception would roll back the editor's save, move or delete.
/// Every route is therefore isolated: see <c>ServerEventRouting</c>.
/// </para>
/// </remarks>
/// <param name="router">Umbraco's server-event router, registered by the management API.</param>
/// <param name="logger">Used to record a route failure without letting it escape this handler.</param>
public sealed class AccessServerEventHandler(IServerEventRouter router, ILogger<AccessServerEventHandler> logger) :
    INotificationAsyncHandler<UserGroupSavedNotification>,
    INotificationAsyncHandler<UserGroupDeletedNotification>,
    INotificationAsyncHandler<UserSavedNotification>,
    INotificationAsyncHandler<ContentMovedNotification>,
    INotificationAsyncHandler<ContentMovedToRecycleBinNotification>,
    INotificationAsyncHandler<ContentDeletedNotification>
{
    /// <inheritdoc />
    public Task HandleAsync(UserGroupSavedNotification notification, CancellationToken cancellationToken) =>
        RouteAllAsync(notification.SavedEntities.Select(e => e.Key));

    /// <inheritdoc />
    public Task HandleAsync(UserGroupDeletedNotification notification, CancellationToken cancellationToken) =>
        RouteAllAsync(notification.DeletedEntities.Select(e => e.Key));

    /// <inheritdoc />
    public Task HandleAsync(UserSavedNotification notification, CancellationToken cancellationToken) =>
        RouteAllAsync(notification.SavedEntities.Select(e => e.Key));

    /// <inheritdoc />
    public Task HandleAsync(ContentMovedNotification notification, CancellationToken cancellationToken) =>
        RouteAllAsync(notification.MoveInfoCollection.Select(m => m.Entity.Key));

    /// <inheritdoc />
    public Task HandleAsync(ContentMovedToRecycleBinNotification notification, CancellationToken cancellationToken) =>
        RouteAllAsync(notification.MoveInfoCollection.Select(m => m.Entity.Key));

    /// <inheritdoc />
    public Task HandleAsync(ContentDeletedNotification notification, CancellationToken cancellationToken) =>
        RouteAllAsync(notification.DeletedEntities.Select(e => e.Key));

    /// <summary>
    /// Routes one <c>Access</c> event per key, concurrently and with each failure isolated.
    /// </summary>
    /// <remarks>
    /// One event per entity rather than one per operation, because a client matches on the key and a
    /// single summary event would match nothing. Bulk operations therefore produce bursts, which is why
    /// every client-side consumer coalesces. The events are routed together rather than one at a time
    /// because this handler runs inline, before the caller's scope completes: a bulk operation -
    /// deleting a user group that touches hundreds of nodes, emptying the recycle bin - would otherwise
    /// multiply per-call latency by the number of keys, directly on the path that keeps the transaction
    /// open. Concurrency and isolation are both provided by <c>ServerEventRouting</c>, shared with the
    /// handler for this package's own notifications.
    /// </remarks>
    /// <param name="keys">The entity keys that changed.</param>
    /// <returns>A task that completes when every event has been routed.</returns>
    private Task RouteAllAsync(IEnumerable<Guid> keys) =>
        ServerEventRouting.RouteAllAsync(router, logger, keys.Select(k => (AdvancedPermissionsServerEvents.AccessSource, k)));
}
