using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
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
/// <para>
/// A routing failure is logged and swallowed, and never propagates: see <c>ServerEventRouting</c>.
/// These handlers run inline in Umbraco's notification pipeline, so this class does not rely on the
/// publisher of the notification having wrapped the publish itself.
/// </para>
/// </remarks>
/// <param name="router">Umbraco's server-event router, registered by the management API.</param>
/// <param name="logger">Used to record a route failure without letting it escape this handler.</param>
public sealed class AdvancedPermissionsServerEventHandler(
    IServerEventRouter router,
    ILogger<AdvancedPermissionsServerEventHandler> logger) :
    INotificationAsyncHandler<AdvancedPermissionsChangedNotification>,
    INotificationAsyncHandler<DocTypePermissionsChangedNotification>
{
    /// <inheritdoc />
    public Task HandleAsync(AdvancedPermissionsChangedNotification notification, CancellationToken cancellationToken) =>
        ServerEventRouting.RouteAllAsync(
            router,
            logger,
            [(AdvancedPermissionsServerEvents.NodePermissionsSource, notification.NodeKey)]);

    /// <inheritdoc />
    public Task HandleAsync(DocTypePermissionsChangedNotification notification, CancellationToken cancellationToken) =>
        ServerEventRouting.RouteAllAsync(
            router,
            logger,
            [(AdvancedPermissionsServerEvents.DocTypePermissionsSource, notification.NodeKey)]);
}
