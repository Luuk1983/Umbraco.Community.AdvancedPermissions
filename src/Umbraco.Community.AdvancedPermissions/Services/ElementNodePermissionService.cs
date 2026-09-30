using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.Caching;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Notifications;

namespace Umbraco.Community.AdvancedPermissions.Services;

/// <summary>
/// Resolves and manages advanced security permissions for library elements and folders. Binds the
/// shared <see cref="NodePermissionServiceBase{TNotification}"/> orchestration to the element
/// repository, the element cache (<c>UAP.Element</c> prefix), the canonical element verb set
/// (<see cref="AdvancedPermissionsConstants.ElementVerbs"/>) and the element change notification
/// (<see cref="ElementPermissionsChangedNotification"/>).
/// </summary>
/// <param name="repository">The element/folder permission repository.</param>
/// <param name="resolver">The pure resolver that applies inheritance and priority rules (default Deny).</param>
/// <param name="userService">The Umbraco user service used to look up user group memberships.</param>
/// <param name="cache">The element two-level permission cache.</param>
/// <param name="eventAggregator">
/// Used to publish <see cref="ElementPermissionsChangedNotification"/> after a write has been
/// persisted and the cache invalidated.
/// </param>
/// <param name="logger">Used to record a notification handler's failure without letting it propagate.</param>
public sealed class ElementNodePermissionService(
    IElementPermissionRepository repository,
    IPermissionResolver resolver,
    IUserService userService,
    ElementPermissionCache cache,
    IEventAggregator eventAggregator,
    ILogger<ElementNodePermissionService> logger)
    : NodePermissionServiceBase<ElementPermissionsChangedNotification>(
            repository, resolver, userService, cache, AdvancedPermissionsConstants.ElementVerbs, eventAggregator, logger),
        IElementNodePermissionService
{
    /// <inheritdoc />
    protected override ElementPermissionsChangedNotification CreateChangedNotification(
        Guid nodeKey,
        string roleAlias,
        IReadOnlyList<string> verbs) =>
        new(nodeKey, roleAlias, verbs);
}
