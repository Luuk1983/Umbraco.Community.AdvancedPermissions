using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models.ServerEvents;
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
/// <b>Ordering.</b> These notifications also drive <c>AdvancedPermissionCacheInvalidator</c>. This
/// handler must run after it, so a client refetching on the event cannot read the cached value the
/// change invalidated. Umbraco runs handlers for one notification in registration order, so
/// <c>AdvancedPermissionsComposer</c> registers the invalidator first — see the comment there.
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
    /// Routes one <c>Access</c> event per key, concurrently.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One event per entity rather than one per operation, because a client matches on the key and
    /// a single summary event would match nothing. Bulk operations therefore produce bursts, which
    /// is why every client-side consumer coalesces.
    /// </para>
    /// <para>
    /// Routed concurrently via <c>Task.WhenAll</c> rather than one at a time,
    /// because this handler runs inline in the notification pipeline, before the caller's scope
    /// completes: every millisecond spent here extends how long that transaction stays open. A
    /// bulk operation — deleting a user group that touches hundreds of nodes, emptying the recycle
    /// bin — would otherwise multiply per-call latency by the number of keys, directly on that
    /// critical path. The events are independent, ordering does not matter given the coalescing
    /// above, and <c>IServerEventRouter</c>'s underlying hub context is built for concurrent
    /// invocation.
    /// </para>
    /// </remarks>
    /// <param name="keys">The entity keys that changed.</param>
    /// <returns>A task that completes when every event has been routed.</returns>
    private Task RouteAllAsync(IEnumerable<Guid> keys) =>
        Task.WhenAll(keys.Select(RouteOneAsync));

    /// <summary>
    /// Routes a single <c>Access</c> event for one key, isolating any failure to this one event.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unlike <c>AdvancedPermissionsServerEventHandler</c>, which consumes this package's own
    /// notifications, this handler consumes Umbraco's core notifications directly — notifications
    /// that core publishes via <c>scope.Notifications.Publish(...)</c> <i>before</i>
    /// <c>scope.Complete()</c>, with no surrounding try/catch of its own. An exception escaping a
    /// handler here therefore does not just fail to notify: it prevents the scope from completing,
    /// rolling back the content editor's save, move or delete over nothing more than a failed
    /// live-update push.
    /// </para>
    /// <para>
    /// Today, <c>ServerEventRouter.RouteEventAsync</c> happens to swallow its own SignalR failures
    /// and never rethrow — so nothing currently escapes here. But that is an implementation detail
    /// of a dependency this package does not control; nothing in the <c>IServerEventRouter</c>
    /// contract promises it. This try/catch is what makes that promise ours to keep, the same way
    /// <c>AdvancedPermissionService.PublishSafelyAsync</c> already keeps it for our own
    /// notifications. <see cref="OperationCanceledException"/> is deliberately not caught: a
    /// cancelled request is not a handler failure, and swallowing it would hide a genuine
    /// cancellation. Catching it here, per key, also keeps one key's failure from cancelling its
    /// siblings or surfacing as an <see cref="AggregateException"/> out of the
    /// <c>Task.WhenAll</c> call in <see cref="RouteAllAsync"/>.
    /// </para>
    /// </remarks>
    /// <param name="key">The entity key that changed.</param>
    /// <returns>A task that completes whether or not the route succeeded.</returns>
    private async Task RouteOneAsync(Guid key)
    {
        try
        {
            await router.RouteEventAsync(new ServerEvent
            {
                EventType = AdvancedPermissionsServerEvents.EventType.Updated,
                EventSource = AdvancedPermissionsServerEvents.AccessSource,
                Key = key,
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to route an {EventSource} server event for key {Key}. The underlying " +
                "content or user-group operation already committed — only the live-update " +
                "notification was lost.",
                AdvancedPermissionsServerEvents.AccessSource,
                key);
        }
    }
}
