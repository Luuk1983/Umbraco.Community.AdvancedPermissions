using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Community.AdvancedPermissions.Services;

/// <summary>
/// Publishes a permission-change notification on behalf of a permission service, swallowing any
/// exception thrown by a handler so it cannot mask a successful write.
/// </summary>
/// <remarks>
/// <para>
/// One implementation of the isolation rule, shared by the node-keyed services (content and library
/// elements) and the document-type service, so the three families cannot drift apart on it.
/// </para>
/// <para>
/// By the time this runs, the write has already committed and the cache has already been invalidated —
/// the change is real and already visible to every other reader. Letting a handler's failure propagate
/// from here would turn a successful save into a reported failure: the caller (a controller) would
/// return an error, and the editor would see "save failed" for a save that in fact succeeded — then
/// retry, or reload and lose work, over nothing. A degraded live update (silently not going out) is
/// the correct failure mode here; a false negative on the save itself is not.
/// </para>
/// <para>
/// <see cref="OperationCanceledException"/> is deliberately not caught: a cancelled request is not a
/// handler failure, and swallowing it would hide a genuine cancellation.
/// </para>
/// </remarks>
internal static class PermissionChangePublisher
{
    /// <summary>
    /// Publishes <paramref name="notification"/>, logging and swallowing any handler failure other
    /// than a cancellation.
    /// </summary>
    /// <typeparam name="TNotification">
    /// The concrete notification type. It is a type parameter rather than <see cref="INotification"/>
    /// because <see cref="IEventAggregator.PublishAsync{TNotification}"/> resolves its handlers from the
    /// compile-time type: publishing through the interface would find no handler for the concrete
    /// notification and would fail silently.
    /// </typeparam>
    /// <param name="eventAggregator">The aggregator to publish through.</param>
    /// <param name="logger">The logger a swallowed failure is written to, at error level.</param>
    /// <param name="notification">The notification to publish.</param>
    /// <param name="context">
    /// A preformatted description of what the notification concerns, for the log message. Preformatted
    /// by the caller rather than passed as separate keys because what identifies a change differs per
    /// family: a document-type change is identified by a content type as well as a node and user group,
    /// and a failure that lost the content type would be far harder to trace.
    /// </param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>A task that completes once the notification has been published or the failure logged.</returns>
    /// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
    public static async Task PublishSafelyAsync<TNotification>(
        IEventAggregator eventAggregator,
        ILogger logger,
        TNotification notification,
        string context,
        CancellationToken cancellationToken)
        where TNotification : INotification
    {
        try
        {
            await eventAggregator.PublishAsync(notification, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to publish {Notification} for {Context}. " +
                "The write already committed and the cache was already invalidated — only the live-update notification was lost.",
                typeof(TNotification).Name,
                context);
        }
    }
}
