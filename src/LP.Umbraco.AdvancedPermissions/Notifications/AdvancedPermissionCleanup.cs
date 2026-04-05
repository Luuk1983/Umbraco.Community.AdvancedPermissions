using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Models;
using LP.Umbraco.AdvancedPermissions.Caching;
using LP.Umbraco.AdvancedPermissions.Core.Interfaces;

namespace LP.Umbraco.AdvancedPermissions.Notifications;

/// <summary>
/// Deletes orphaned permission entries when content is permanently removed from the system.
/// </summary>
/// <remarks>
/// Only <see cref="ContentDeletedNotification"/> is handled because Umbraco fires it for
/// every individual item — including each item deleted during an "empty recycle bin" operation
/// (via <c>ContentService.DeleteLocked</c>). A separate
/// <see cref="ContentEmptiedRecycleBinNotification"/> handler is therefore not required.
/// </remarks>
/// <param name="repository">The permission repository used to delete entries.</param>
/// <param name="cache">The permission cache to invalidate after cleanup.</param>
/// <param name="logger">Logger for cleanup status messages.</param>
public sealed class AdvancedPermissionCleanup(
    IAdvancedPermissionRepository repository,
    AdvancedPermissionCache cache,
    ILogger<AdvancedPermissionCleanup> logger)
    : INotificationAsyncHandler<ContentDeletedNotification>
{
    /// <summary>
    /// Handles the <see cref="ContentDeletedNotification"/> by removing all permission entries
    /// for each permanently deleted content node.
    /// </summary>
    public async Task HandleAsync(ContentDeletedNotification notification, CancellationToken cancellationToken)
    {
        await CleanupDeletedEntitiesAsync(notification.DeletedEntities, cancellationToken);
    }

    private async Task CleanupDeletedEntitiesAsync(
        IEnumerable<IContent> deletedEntities,
        CancellationToken cancellationToken)
    {
        var count = 0;

        foreach (var entity in deletedEntities)
        {
            try
            {
                await repository.DeleteAllForNodeAsync(entity.Key, cancellationToken);
                count++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Advanced Permissions: Failed to clean up permissions for deleted node {NodeKey}",
                    entity.Key);
            }
        }

        if (count > 0)
        {
            // Invalidate all caches — any role may have had entries for the deleted nodes
            cache.InvalidateAllRoleEntries();
            cache.InvalidateAllResolved();

            logger.LogDebug(
                "Advanced Permissions: Cleaned up permissions for {Count} deleted content node(s)",
                count);
        }
    }
}
