using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Community.AdvancedPermissions.Caching;

namespace Umbraco.Community.AdvancedPermissions.Notifications;

/// <summary>
/// Invalidates the <see cref="DocTypePermissionCache"/> on the same events that affect the
/// node-level cache, plus content-type save (default permissions may change) and content
/// move/recycle-bin (node paths shift).
/// </summary>
/// <param name="cache">The doc-type cache to invalidate.</param>
public sealed class DocTypePermissionCacheInvalidator(DocTypePermissionCache cache)
    : INotificationHandler<ContentMovedNotification>,
      INotificationHandler<ContentMovedToRecycleBinNotification>,
      INotificationHandler<UserGroupSavedNotification>,
      INotificationHandler<UserSavedNotification>,
      INotificationHandler<ContentTypeSavedNotification>
{
    /// <summary>Invalidates all resolved doc-type permissions when content is moved.</summary>
    /// <param name="notification">The notification.</param>
    public void Handle(ContentMovedNotification notification) =>
        cache.InvalidateAllResolved();

    /// <summary>Invalidates all resolved doc-type permissions when content is trashed.</summary>
    /// <param name="notification">The notification.</param>
    public void Handle(ContentMovedToRecycleBinNotification notification) =>
        cache.InvalidateAllResolved();

    /// <summary>Invalidates all resolved doc-type permissions when a user group is saved (its membership/defaults may change).</summary>
    /// <param name="notification">The notification.</param>
    public void Handle(UserGroupSavedNotification notification) =>
        cache.InvalidateAllResolved();

    /// <summary>Invalidates resolved doc-type permissions for each saved user (their group membership may have changed).</summary>
    /// <param name="notification">The notification.</param>
    public void Handle(UserSavedNotification notification)
    {
        foreach (var user in notification.SavedEntities)
        {
            cache.InvalidateUser(user.Key);
        }
    }

    /// <summary>Invalidates resolved doc-type permissions when a content type is saved (its allowed-children may change).</summary>
    /// <param name="notification">The notification.</param>
    public void Handle(ContentTypeSavedNotification notification) =>
        cache.InvalidateAllResolved();
}
