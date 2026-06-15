using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Community.AdvancedPermissions.Caching;

namespace Umbraco.Community.AdvancedPermissions.Notifications;

/// <summary>
/// Handles Umbraco notifications that may make cached element permission data stale, and invalidates
/// the appropriate cache levels. The element analogue of <see cref="AdvancedPermissionCacheInvalidator"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Invalidation strategy:</b>
/// <list type="bullet">
///   <item>Permission entry saved/deleted → invalidate L1 for affected role + ALL L2 (done in <see cref="Services.ElementNodePermissionService"/>).</item>
///   <item>Element or folder moved/trashed → invalidate ALL L2 (paths changed; all path-based resolutions are stale).</item>
///   <item>User group saved → invalidate ALL L2 (group defaults may have changed).</item>
///   <item>User saved (group membership changed) → invalidate L2 for that specific user only.</item>
/// </list>
/// </para>
/// <para>
/// <see cref="EntityContainerMovedNotification"/>/<see cref="EntityContainerMovedToRecycleBinNotification"/>
/// are generic across all container types; invalidating the (cheap) L2 cache on any container move is
/// harmless and keeps element-folder path changes correct without inspecting the container object type.
/// </para>
/// </remarks>
/// <param name="cache">The element permission cache to invalidate.</param>
public sealed class ElementPermissionCacheInvalidator(ElementPermissionCache cache)
    : INotificationHandler<ElementMovedNotification>,
      INotificationHandler<ElementMovedToRecycleBinNotification>,
      INotificationHandler<EntityContainerMovedNotification>,
      INotificationHandler<EntityContainerMovedToRecycleBinNotification>,
      INotificationHandler<UserGroupSavedNotification>,
      INotificationHandler<UserSavedNotification>
{
    /// <summary>Clears all L2 resolved permissions when an element is moved (paths changed).</summary>
    /// <param name="notification">The notification containing the moved elements.</param>
    public void Handle(ElementMovedNotification notification) =>
        cache.InvalidateAllResolved();

    /// <summary>Clears all L2 resolved permissions when an element is trashed.</summary>
    /// <param name="notification">The notification containing the trashed elements.</param>
    public void Handle(ElementMovedToRecycleBinNotification notification) =>
        cache.InvalidateAllResolved();

    /// <summary>Clears all L2 resolved permissions when a folder (container) is moved.</summary>
    /// <param name="notification">The notification containing the moved containers.</param>
    public void Handle(EntityContainerMovedNotification notification) =>
        cache.InvalidateAllResolved();

    /// <summary>Clears all L2 resolved permissions when a folder (container) is trashed.</summary>
    /// <param name="notification">The notification containing the trashed containers.</param>
    public void Handle(EntityContainerMovedToRecycleBinNotification notification) =>
        cache.InvalidateAllResolved();

    /// <summary>Clears all L2 resolved permissions when a user group is saved (defaults may have changed).</summary>
    /// <param name="notification">The notification containing the saved user groups.</param>
    public void Handle(UserGroupSavedNotification notification) =>
        cache.InvalidateAllResolved();

    /// <summary>Clears L2 cached permissions for each saved user (group membership may have changed).</summary>
    /// <param name="notification">The notification containing the saved users.</param>
    public void Handle(UserSavedNotification notification)
    {
        foreach (var user in notification.SavedEntities)
        {
            cache.InvalidateUser(user.Key);
        }
    }
}
