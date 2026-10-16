using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Community.AdvancedPermissions.Caching;

namespace Umbraco.Community.AdvancedPermissions.Notifications;

/// <summary>
/// Clears the <see cref="StartNodeScopeCache"/> on every event that can change a subject's start
/// nodes (users and user groups saved or deleted) or the paths of start nodes (content and Library
/// folders moved, trashed, restored or deleted). The cache is small and these events are rare, so it is
/// cleared as a whole rather than per subject.
/// </summary>
/// <remarks>
/// These handlers are synchronous and registered before the server-event handlers, so a viewer that
/// refetches on the matching live event never reads a scope the change replaced.
/// </remarks>
/// <param name="cache">The scope cache to clear.</param>
public sealed class StartNodeScopeCacheInvalidator(StartNodeScopeCache cache)
    : INotificationHandler<UserSavedNotification>,
      INotificationHandler<UserDeletedNotification>,
      INotificationHandler<UserGroupSavedNotification>,
      INotificationHandler<UserGroupWithUsersSavedNotification>,
      INotificationHandler<UserGroupDeletedNotification>,
      INotificationHandler<ContentMovedNotification>,
      INotificationHandler<ContentMovedToRecycleBinNotification>,
      INotificationHandler<ContentDeletedNotification>,
      INotificationHandler<EntityContainerMovedNotification>,
      INotificationHandler<EntityContainerMovedToRecycleBinNotification>,
      INotificationHandler<EntityContainerDeletedNotification>
{
    /// <summary>Clears the scopes when a user is saved: their own start nodes or groups may have changed.</summary>
    /// <param name="notification">The notification.</param>
    public void Handle(UserSavedNotification notification) => cache.Clear();

    /// <summary>Clears the scopes when a user is deleted.</summary>
    /// <param name="notification">The notification.</param>
    public void Handle(UserDeletedNotification notification) => cache.Clear();

    /// <summary>Clears the scopes when a user group is saved: its content or Library start node may have changed.</summary>
    /// <param name="notification">The notification.</param>
    public void Handle(UserGroupSavedNotification notification) => cache.Clear();

    /// <summary>
    /// Clears the scopes when a user group is saved with its users. Umbraco clears its own cached start
    /// nodes of the group's users on this notification, so clearing here as well keeps a scope rebuilt
    /// in between from outliving Umbraco's stale value.
    /// </summary>
    /// <param name="notification">The notification.</param>
    public void Handle(UserGroupWithUsersSavedNotification notification) => cache.Clear();

    /// <summary>Clears the scopes when a user group is deleted.</summary>
    /// <param name="notification">The notification.</param>
    public void Handle(UserGroupDeletedNotification notification) => cache.Clear();

    /// <summary>Clears the scopes when content is moved or restored: a start node's path may have changed.</summary>
    /// <param name="notification">The notification.</param>
    public void Handle(ContentMovedNotification notification) => cache.Clear();

    /// <summary>Clears the scopes when content is trashed: a trashed start node no longer counts.</summary>
    /// <param name="notification">The notification.</param>
    public void Handle(ContentMovedToRecycleBinNotification notification) => cache.Clear();

    /// <summary>Clears the scopes when content is deleted: a deleted start node no longer counts.</summary>
    /// <param name="notification">The notification.</param>
    public void Handle(ContentDeletedNotification notification) => cache.Clear();

    /// <summary>Clears the scopes when a Library folder is moved or restored: a Library start node's path may have changed.</summary>
    /// <param name="notification">The notification.</param>
    public void Handle(EntityContainerMovedNotification notification) => cache.Clear();

    /// <summary>Clears the scopes when a Library folder is trashed.</summary>
    /// <param name="notification">The notification.</param>
    public void Handle(EntityContainerMovedToRecycleBinNotification notification) => cache.Clear();

    /// <summary>Clears the scopes when a Library folder is deleted.</summary>
    /// <param name="notification">The notification.</param>
    public void Handle(EntityContainerDeletedNotification notification) => cache.Clear();
}
