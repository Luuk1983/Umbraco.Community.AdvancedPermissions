using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.Caching;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Notifications;

namespace Umbraco.Community.AdvancedPermissions.Services;

/// <summary>
/// Resolves and manages advanced security permissions for content nodes. Binds the shared
/// <see cref="NodePermissionServiceBase{TNotification}"/> orchestration to the content repository, the
/// content cache (<c>UAP</c> prefix), the content verb set
/// (<see cref="AdvancedPermissionsConstants.AllVerbs"/>) and the content change notification
/// (<see cref="AdvancedPermissionsChangedNotification"/>).
/// </summary>
/// <param name="repository">The content-node permission repository.</param>
/// <param name="resolver">The pure resolver that applies inheritance and priority rules.</param>
/// <param name="userService">The Umbraco user service used to look up user group memberships.</param>
/// <param name="cache">The content two-level permission cache.</param>
/// <param name="eventAggregator">
/// Used to publish <see cref="AdvancedPermissionsChangedNotification"/> after a write has been
/// persisted and the cache invalidated.
/// </param>
/// <param name="logger">Used to record a notification handler's failure without letting it propagate.</param>
public sealed class AdvancedPermissionService(
    IAdvancedPermissionRepository repository,
    IPermissionResolver resolver,
    IUserService userService,
    AdvancedPermissionCache cache,
    IEventAggregator eventAggregator,
    ILogger<AdvancedPermissionService> logger)
    : NodePermissionServiceBase<AdvancedPermissionsChangedNotification>(
            repository, resolver, userService, cache, AdvancedPermissionsConstants.AllVerbs, eventAggregator, logger),
        IAdvancedPermissionService
{
    /// <inheritdoc />
    protected override AdvancedPermissionsChangedNotification CreateChangedNotification(
        Guid nodeKey,
        string roleAlias,
        IReadOnlyList<string> verbs) =>
        new(nodeKey, roleAlias, verbs);
}
