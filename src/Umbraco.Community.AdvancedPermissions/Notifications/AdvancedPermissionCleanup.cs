using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Community.AdvancedPermissions.Caching;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;

namespace Umbraco.Community.AdvancedPermissions.Notifications;

/// <summary>
/// Deletes orphaned permission entries when content nodes or user groups are permanently removed
/// from the system.
/// </summary>
/// <remarks>
/// <para>
/// Only <see cref="ContentDeletedNotification"/> is handled for content because Umbraco fires it for
/// every individual item — including each item deleted during an "empty recycle bin" operation
/// (via <c>ContentService.DeleteLocked</c>). A separate
/// <see cref="ContentEmptiedRecycleBinNotification"/> handler is therefore not required.
/// </para>
/// <para>
/// <see cref="UserGroupDeletedNotification"/> is handled to remove all permission entries that
/// reference the deleted group's alias, preventing orphaned rows in the database.
/// </para>
/// </remarks>
/// <param name="repository">The permission repository used to delete entries.</param>
/// <param name="cache">The permission cache to invalidate after cleanup.</param>
/// <param name="logger">Logger for cleanup status messages.</param>
public sealed class AdvancedPermissionCleanup(
    IAdvancedPermissionRepository repository,
    AdvancedPermissionCache cache,
    ILogger<AdvancedPermissionCleanup> logger)
    : INotificationAsyncHandler<ContentDeletedNotification>,
      INotificationAsyncHandler<UserGroupDeletedNotification>
{
    /// <summary>
    /// Handles the <see cref="ContentDeletedNotification"/> by removing all permission entries
    /// for each permanently deleted content node.
    /// </summary>
    public async Task HandleAsync(ContentDeletedNotification notification, CancellationToken cancellationToken)
    {
        var count = 0;

        foreach (var entity in notification.DeletedEntities)
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
            cache.InvalidateAllRoleEntries();
            cache.InvalidateAllResolved();

            logger.LogDebug(
                "Advanced Permissions: Cleaned up permissions for {Count} deleted content node(s)",
                count);
        }
    }

    /// <summary>
    /// Handles the <see cref="UserGroupDeletedNotification"/> by removing all permission entries
    /// for each deleted user group's alias.
    /// </summary>
    public async Task HandleAsync(UserGroupDeletedNotification notification, CancellationToken cancellationToken)
    {
        var count = 0;

        foreach (var group in notification.DeletedEntities)
        {
            try
            {
                await repository.DeleteAllForRoleAsync(group.Alias, cancellationToken);
                count++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Advanced Permissions: Failed to clean up permissions for deleted user group {RoleAlias}",
                    group.Alias);
            }
        }

        if (count > 0)
        {
            cache.InvalidateAllRoleEntries();
            cache.InvalidateAllResolved();

            logger.LogDebug(
                "Advanced Permissions: Cleaned up permissions for {Count} deleted user group(s)",
                count);
        }
    }
}
