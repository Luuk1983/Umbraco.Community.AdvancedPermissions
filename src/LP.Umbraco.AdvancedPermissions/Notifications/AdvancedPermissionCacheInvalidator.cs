using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using LP.Umbraco.AdvancedPermissions.Caching;

namespace LP.Umbraco.AdvancedPermissions.Notifications;

/// <summary>
/// Handles Umbraco notifications that may make cached permission data stale, and invalidates
/// the appropriate cache levels.
/// </summary>
/// <remarks>
/// <para>
/// <b>Invalidation strategy:</b>
/// <list type="bullet">
///   <item>Permission entry saved/deleted → invalidate L1 for affected role + ALL L2 (done in <see cref="Services.AdvancedPermissionService"/>).</item>
///   <item>Content moved or trashed → invalidate ALL L2 (node paths have changed; all path-based resolutions are stale).</item>
///   <item>User group saved → invalidate ALL L2 (group defaults may have changed, affecting all users in the group).</item>
///   <item>User saved (group membership changed) → invalidate L2 for that specific user only.</item>
/// </list>
/// </para>
/// </remarks>
/// <param name="cache">The advanced permission cache to invalidate.</param>
public sealed class AdvancedPermissionCacheInvalidator(AdvancedPermissionCache cache)
    : INotificationHandler<ContentMovedNotification>,
      INotificationHandler<ContentMovedToRecycleBinNotification>,
      INotificationHandler<UserGroupSavedNotification>,
      INotificationHandler<UserSavedNotification>
{
    /// <summary>
    /// Handles the <see cref="ContentMovedNotification"/> by clearing all L2 resolved permissions.
    /// Node paths have changed, so all cached path-based resolutions are stale.
    /// </summary>
    /// <param name="notification">The notification containing the moved content entities.</param>
    public void Handle(ContentMovedNotification notification) =>
        cache.InvalidateAllResolved();

    /// <summary>
    /// Handles the <see cref="ContentMovedToRecycleBinNotification"/> by clearing all L2 resolved permissions.
    /// </summary>
    /// <param name="notification">The notification containing the trashed content entities.</param>
    public void Handle(ContentMovedToRecycleBinNotification notification) =>
        cache.InvalidateAllResolved();

    /// <summary>
    /// Handles the <see cref="UserGroupSavedNotification"/> by clearing all L2 resolved permissions.
    /// Group defaults may have changed, affecting all users who belong to the modified group.
    /// </summary>
    /// <param name="notification">The notification containing the saved user groups.</param>
    public void Handle(UserGroupSavedNotification notification) =>
        cache.InvalidateAllResolved();

    /// <summary>
    /// Handles the <see cref="UserSavedNotification"/> by clearing L2 cached permissions for
    /// each saved user. This covers group membership changes that affect what roles the user has.
    /// </summary>
    /// <param name="notification">The notification containing the saved users.</param>
    public void Handle(UserSavedNotification notification)
    {
        foreach (var user in notification.SavedEntities)
        {
            cache.InvalidateUser(user.Key);
        }
    }
}
