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
/// Effective permissions move without this package's own store being touched at all: a user joining a
/// group, a group being deleted, a content item, library element or folder moving to a new parent and
/// inheriting a different branch, or being deleted. A viewer showing a user whose group membership
/// just changed is exactly as wrong as one showing a node whose entries changed, and nothing else
/// tells it so.
/// </para>
/// <para>
/// The library notifications cover both elements and folders. The container notifications
/// (<see cref="EntityContainerMovedNotification"/> and friends) are generic across every container
/// type, so a folder of another kind wakes the viewers too - a spurious refetch that reconciles to no
/// change, which is the cheap direction to be wrong in.
/// </para>
/// <para>
/// <b>Ordering.</b> These notifications also drive the package's cache invalidators. This handler must
/// run after them, so a client refetching on the event cannot read the cached value the change
/// invalidated. Umbraco runs handlers for one notification in registration order, so
/// <c>AdvancedPermissionsComposer</c> registers the invalidators first and this handler last - see the
/// comment there.
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
    INotificationAsyncHandler<ContentDeletedNotification>,
    INotificationAsyncHandler<ElementMovedNotification>,
    INotificationAsyncHandler<ElementMovedToRecycleBinNotification>,
    INotificationAsyncHandler<ElementDeletedNotification>,
    INotificationAsyncHandler<EntityContainerMovedNotification>,
    INotificationAsyncHandler<EntityContainerMovedToRecycleBinNotification>,
    INotificationAsyncHandler<EntityContainerDeletedNotification>
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

    /// <inheritdoc />
    public Task HandleAsync(ElementMovedNotification notification, CancellationToken cancellationToken) =>
        RouteAllAsync(notification.MoveInfoCollection.Select(m => m.Entity.Key));

    /// <inheritdoc />
    public Task HandleAsync(ElementMovedToRecycleBinNotification notification, CancellationToken cancellationToken) =>
        RouteAllAsync(notification.MoveInfoCollection.Select(m => m.Entity.Key));

    /// <inheritdoc />
    public Task HandleAsync(ElementDeletedNotification notification, CancellationToken cancellationToken) =>
        RouteAllAsync(notification.DeletedEntities.Select(e => e.Key));

    /// <inheritdoc />
    public Task HandleAsync(EntityContainerMovedNotification notification, CancellationToken cancellationToken) =>
        RouteAllAsync(notification.MoveInfoCollection.Select(m => m.Entity.Key));

    /// <inheritdoc />
    public Task HandleAsync(EntityContainerMovedToRecycleBinNotification notification, CancellationToken cancellationToken) =>
        RouteAllAsync(notification.MoveInfoCollection.Select(m => m.Entity.Key));

    /// <inheritdoc />
    public Task HandleAsync(EntityContainerDeletedNotification notification, CancellationToken cancellationToken) =>
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
    /// open.
    /// </remarks>
    /// <param name="keys">The entity keys that changed.</param>
    /// <returns>A task that completes when every event has been routed.</returns>
    private Task RouteAllAsync(IEnumerable<Guid> keys) =>
        ServerEventRouting.RouteAllAsync(router, logger, keys.Select(k => (AdvancedPermissionsServerEvents.AccessSource, k)));
}
