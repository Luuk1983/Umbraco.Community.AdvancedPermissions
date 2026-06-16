using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Community.AdvancedPermissions.Caching;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;

namespace Umbraco.Community.AdvancedPermissions.Notifications;

/// <summary>
/// Deletes orphaned element permission entries when library elements, element folders, or user groups
/// are permanently removed. The element analogue of <see cref="AdvancedPermissionCleanup"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ElementDeletedNotification"/> fires per element on permanent deletion (including each
/// element removed while emptying the element recycle bin), and
/// <see cref="EntityContainerDeletedNotification"/> fires when a folder is removed. The latter is
/// generic across all container types; calling <c>DeleteAllForNodeAsync</c> with a non-element
/// container key is a harmless no-op (no rows match), so no object-type filtering is required.
/// </para>
/// </remarks>
/// <param name="repository">The element permission repository used to delete entries.</param>
/// <param name="cache">The element permission cache to invalidate after cleanup.</param>
/// <param name="logger">Logger for cleanup status messages.</param>
public sealed class ElementPermissionCleanup(
    IElementPermissionRepository repository,
    ElementPermissionCache cache,
    ILogger<ElementPermissionCleanup> logger)
    : INotificationAsyncHandler<ElementDeletedNotification>,
      INotificationAsyncHandler<EntityContainerDeletedNotification>,
      INotificationAsyncHandler<UserGroupDeletedNotification>
{
    /// <summary>
    /// Removes all element permission entries for each permanently deleted element.
    /// </summary>
    /// <param name="notification">The notification containing the deleted elements.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    public async Task HandleAsync(ElementDeletedNotification notification, CancellationToken cancellationToken)
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
                    "Advanced Permissions: Failed to clean up element permissions for deleted element {NodeKey}",
                    entity.Key);
            }
        }

        InvalidateIfAny(count, "deleted element(s)");
    }

    /// <summary>
    /// Removes all element permission entries for each permanently deleted folder (element container).
    /// Non-element containers match no rows and are silently skipped.
    /// </summary>
    /// <param name="notification">The notification containing the deleted containers.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    public async Task HandleAsync(EntityContainerDeletedNotification notification, CancellationToken cancellationToken)
    {
        var count = 0;

        foreach (var container in notification.DeletedEntities)
        {
            try
            {
                await repository.DeleteAllForNodeAsync(container.Key, cancellationToken);
                count++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Advanced Permissions: Failed to clean up element permissions for deleted folder {NodeKey}",
                    container.Key);
            }
        }

        InvalidateIfAny(count, "deleted folder(s)");
    }

    /// <summary>
    /// Removes all element permission entries for each deleted user group's alias.
    /// </summary>
    /// <param name="notification">The notification containing the deleted user groups.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
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
                    "Advanced Permissions: Failed to clean up element permissions for deleted user group {RoleAlias}",
                    group.Alias);
            }
        }

        InvalidateIfAny(count, "deleted user group(s)");
    }

    /// <summary>
    /// Invalidates all element caches when at least one cleanup occurred, and logs the outcome.
    /// </summary>
    /// <param name="count">The number of entities cleaned up.</param>
    /// <param name="what">A short description of what was cleaned, for the log message.</param>
    private void InvalidateIfAny(int count, string what)
    {
        if (count <= 0)
        {
            return;
        }

        cache.InvalidateAllRoleEntries();
        cache.InvalidateAllResolved();

        logger.LogDebug("Advanced Permissions: Cleaned up element permissions for {Count} {What}", count, what);
    }
}
