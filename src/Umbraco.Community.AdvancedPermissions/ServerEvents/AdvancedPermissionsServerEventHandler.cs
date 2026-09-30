using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models.ServerEvents;
using Umbraco.Cms.Core.ServerEvents;
using Umbraco.Community.AdvancedPermissions.Notifications;

namespace Umbraco.Community.AdvancedPermissions.ServerEvents;

/// <summary>
/// Puts this package's own permission changes onto Umbraco's built-in server-events hub, so open
/// editors and viewers hear about a write made from another window, by another user.
/// </summary>
/// <remarks>
/// <para>
/// Subscribes to this package's notifications rather than reaching into the services that raise
/// them, which keeps the transport a consumer of the same public seam anybody else would use.
/// </para>
/// <para>
/// The notifications are raised after the caches have been invalidated, and that ordering is what
/// makes this safe. A client refetching on an event that overtook the invalidation would read the
/// snapshot the write replaced and — having consumed its one notification — never ask again.
/// </para>
/// </remarks>
/// <param name="router">Umbraco's server-event router, registered by the management API.</param>
public sealed class AdvancedPermissionsServerEventHandler(IServerEventRouter router) :
    INotificationAsyncHandler<AdvancedPermissionsChangedNotification>,
    INotificationAsyncHandler<DocTypePermissionsChangedNotification>
{
    /// <inheritdoc />
    public Task HandleAsync(AdvancedPermissionsChangedNotification notification, CancellationToken cancellationToken) =>
        router.RouteEventAsync(new ServerEvent
        {
            EventType = AdvancedPermissionsServerEvents.EventType.Updated,
            EventSource = AdvancedPermissionsServerEvents.NodePermissionsSource,
            Key = notification.NodeKey,
        });

    /// <inheritdoc />
    public Task HandleAsync(DocTypePermissionsChangedNotification notification, CancellationToken cancellationToken) =>
        router.RouteEventAsync(new ServerEvent
        {
            EventType = AdvancedPermissionsServerEvents.EventType.Updated,
            EventSource = AdvancedPermissionsServerEvents.DocTypePermissionsSource,
            Key = notification.NodeKey,
        });
}
