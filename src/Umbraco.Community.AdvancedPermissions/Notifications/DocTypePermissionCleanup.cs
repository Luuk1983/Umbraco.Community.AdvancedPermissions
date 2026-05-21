using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Community.AdvancedPermissions.Caching;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;

namespace Umbraco.Community.AdvancedPermissions.Notifications;

/// <summary>
/// Deletes orphaned <c>DocTypePermission</c> rows when the referenced node, content type, or
/// user group is permanently removed.
/// </summary>
/// <param name="repository">The doc-type permission repository.</param>
/// <param name="cache">The doc-type cache to invalidate after cleanup.</param>
/// <param name="logger">Logger for cleanup status messages.</param>
public sealed class DocTypePermissionCleanup(
    IDocTypePermissionRepository repository,
    DocTypePermissionCache cache,
    ILogger<DocTypePermissionCleanup> logger)
    : INotificationAsyncHandler<ContentDeletedNotification>,
      INotificationAsyncHandler<ContentTypeDeletedNotification>,
      INotificationAsyncHandler<UserGroupDeletedNotification>
{
    /// <summary>Cleans up doc-type entries referencing the deleted node.</summary>
    /// <param name="notification">The notification.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
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
                    "Advanced Permissions: failed to clean up doc-type entries for deleted node {NodeKey}",
                    entity.Key);
            }
        }

        if (count > 0)
        {
            cache.InvalidateAllRoleEntries();
            cache.InvalidateAllResolved();
        }
    }

    /// <summary>Cleans up doc-type entries referencing the deleted node.</summary>
    /// <param name="notification">The notification.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task HandleAsync(ContentTypeDeletedNotification notification, CancellationToken cancellationToken)
    {
        var count = 0;
        foreach (var entity in notification.DeletedEntities)
        {
            try
            {
                await repository.DeleteAllForContentTypeAsync(entity.Key, cancellationToken);
                count++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Advanced Permissions: failed to clean up doc-type entries for deleted content type {ContentTypeKey}",
                    entity.Key);
            }
        }

        if (count > 0)
        {
            cache.InvalidateAllRoleEntries();
            cache.InvalidateAllResolved();
        }
    }

    /// <summary>Cleans up doc-type entries referencing the deleted node.</summary>
    /// <param name="notification">The notification.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
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
                    "Advanced Permissions: failed to clean up doc-type entries for deleted user group {RoleAlias}",
                    group.Alias);
            }
        }

        if (count > 0)
        {
            cache.InvalidateAllRoleEntries();
            cache.InvalidateAllResolved();
        }
    }
}
