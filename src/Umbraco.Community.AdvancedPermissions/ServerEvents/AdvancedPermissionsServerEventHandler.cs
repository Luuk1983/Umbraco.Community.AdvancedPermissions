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
/// snapshot the write replaced and - having consumed its one notification - never ask again.
/// <c>AdvancedPermissionsComposer</c> also registers this handler after every cache invalidator, for
/// the notifications Umbraco raises that both of them handle.
/// </para>
/// <para>
/// A routing failure is logged and swallowed, and never propagates: see <c>ServerEventRouting</c>.
/// </para>
/// </remarks>
/// <param name="router">Umbraco's server-event router, registered by the management API.</param>
/// <param name="logger">Used to record a route failure without letting it escape this handler.</param>
public sealed class AdvancedPermissionsServerEventHandler(
    IServerEventRouter router,
    ILogger<AdvancedPermissionsServerEventHandler> logger) :
    INotificationAsyncHandler<AdvancedPermissionsChangedNotification>,
    INotificationAsyncHandler<ElementPermissionsChangedNotification>,
    INotificationAsyncHandler<DocTypePermissionsChangedNotification>
{
    /// <inheritdoc />
    public Task HandleAsync(AdvancedPermissionsChangedNotification notification, CancellationToken cancellationToken) =>
        ServerEventRouting.RouteAllAsync(
            router,
            logger,
            [(AdvancedPermissionsServerEvents.NodePermissionsSource, notification.NodeKey)]);

    /// <inheritdoc />
    public Task HandleAsync(ElementPermissionsChangedNotification notification, CancellationToken cancellationToken) =>
        ServerEventRouting.RouteAllAsync(
            router,
            logger,
            [(AdvancedPermissionsServerEvents.ElementPermissionsSource, notification.NodeKey)]);

    /// <summary>
    /// Routes a document-type change under the source of the family it concerns, so the Document Type
    /// editor and the Element Type editor do not wake for each other's changes.
    /// </summary>
    /// <remarks>
    /// <see cref="DocTypePermissionFamily.Unknown"/> is routed to <em>both</em> sources. A spurious wake
    /// costs one refetch that reconciles to no change; a missed wake is a screen that stays silently
    /// stale. The family is resolved on the server because <c>ServerEvent</c> carries only a single key,
    /// so a consumer on the wire cannot inspect the notification to work it out for itself.
    /// </remarks>
    /// <param name="notification">The change notification.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>A task that completes when the event has been routed.</returns>
    public Task HandleAsync(DocTypePermissionsChangedNotification notification, CancellationToken cancellationToken) =>
        ServerEventRouting.RouteAllAsync(router, logger, SourcesFor(notification.Family).Select(s => (s, notification.NodeKey)));

    /// <summary>
    /// Chooses the sources a document-type change is announced on.
    /// </summary>
    /// <param name="family">The family the change concerns.</param>
    /// <returns>
    /// The document-type source for <see cref="DocTypePermissionFamily.Document"/>, the element-type
    /// source for <see cref="DocTypePermissionFamily.Element"/>, and both for anything else.
    /// </returns>
    private static IEnumerable<string> SourcesFor(DocTypePermissionFamily family) =>
        family switch
        {
            DocTypePermissionFamily.Document => [AdvancedPermissionsServerEvents.DocTypePermissionsSource],
            DocTypePermissionFamily.Element => [AdvancedPermissionsServerEvents.ElementTypePermissionsSource],
            _ => [AdvancedPermissionsServerEvents.DocTypePermissionsSource, AdvancedPermissionsServerEvents.ElementTypePermissionsSource],
        };
}
