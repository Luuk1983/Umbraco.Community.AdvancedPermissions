using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Models.ServerEvents;
using Umbraco.Cms.Core.ServerEvents;

namespace Umbraco.Community.AdvancedPermissions.ServerEvents;

/// <summary>
/// Routes this package's events onto Umbraco's server-event hub without letting a routing failure
/// escape, and without serialising the routes.
/// </summary>
/// <remarks>
/// <para>
/// One implementation, shared by both server-event handlers, because the two rules it enforces are
/// exactly the ones that are easy to get right in one place and wrong in the other.
/// </para>
/// <para>
/// <b>Isolation.</b> The handlers run inline in Umbraco's notification pipeline, before the caller's
/// scope completes and with no surrounding try/catch. An exception escaping a handler does not merely
/// fail to notify: it prevents the scope from completing, rolling back the editor's content save, move
/// or delete over nothing more than a failed live-update push. Today <c>ServerEventRouter</c> happens
/// to swallow its own SignalR failures - but that is an implementation detail of a dependency this
/// package does not control, and nothing in the <c>IServerEventRouter</c> contract promises it. This
/// is what makes the promise ours to keep. <see cref="OperationCanceledException"/> is deliberately
/// not caught: a cancelled request is not a handler failure, and swallowing it would hide a genuine
/// cancellation.
/// </para>
/// <para>
/// <b>Concurrency.</b> The routes are issued together and awaited with <c>Task.WhenAll</c>, because
/// every millisecond spent here extends how long the caller's transaction stays open. The events are
/// independent, ordering does not matter given that every consumer coalesces, and the hub context
/// underneath is built for concurrent use. Each route is isolated individually, so one failure neither
/// cancels its siblings nor surfaces as an <see cref="AggregateException"/> out of the
/// <c>Task.WhenAll</c>.
/// </para>
/// </remarks>
internal static class ServerEventRouting
{
    /// <summary>
    /// Routes one <c>Updated</c> event per pair, concurrently, isolating each pair's failure.
    /// </summary>
    /// <param name="router">Umbraco's server-event router.</param>
    /// <param name="logger">Where a swallowed failure is recorded, at error level.</param>
    /// <param name="events">The source and key of each event to route.</param>
    /// <returns>A task that completes when every event has been routed or its failure logged.</returns>
    /// <exception cref="OperationCanceledException">A route was cancelled.</exception>
    internal static Task RouteAllAsync(
        IServerEventRouter router,
        ILogger logger,
        IEnumerable<(string Source, Guid Key)> events) =>
        // Materialised before WhenAll so every route has been started by the time it is awaited.
        Task.WhenAll(events.Select(e => RouteOneAsync(router, logger, e.Source, e.Key)).ToList());

    /// <summary>
    /// Routes a single <c>Updated</c> event, isolating any failure to this one event.
    /// </summary>
    /// <param name="router">Umbraco's server-event router.</param>
    /// <param name="logger">Where a swallowed failure is recorded, at error level.</param>
    /// <param name="source">The event source.</param>
    /// <param name="key">The entity key the event names.</param>
    /// <returns>A task that completes whether or not the route succeeded.</returns>
    /// <exception cref="OperationCanceledException">The route was cancelled.</exception>
    private static async Task RouteOneAsync(IServerEventRouter router, ILogger logger, string source, Guid key)
    {
        try
        {
            await router.RouteEventAsync(new ServerEvent
            {
                EventType = AdvancedPermissionsServerEvents.EventType.Updated,
                EventSource = source,
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
                "operation is unaffected - only the live-update notification was lost.",
                source,
                key);
        }
    }
}
