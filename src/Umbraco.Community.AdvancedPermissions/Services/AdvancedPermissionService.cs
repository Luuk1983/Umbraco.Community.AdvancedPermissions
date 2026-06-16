using Umbraco.Community.AdvancedPermissions.Caching;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Community.AdvancedPermissions.Services;

/// <summary>
/// Resolves and manages advanced security permissions for content nodes. Binds the shared
/// <see cref="NodePermissionServiceBase"/> orchestration to the content repository, the content cache
/// (<c>UAP</c> prefix), and the content verb set (<see cref="AdvancedPermissionsConstants.AllVerbs"/>).
/// </summary>
/// <param name="repository">The content-node permission repository.</param>
/// <param name="resolver">The pure resolver that applies inheritance and priority rules.</param>
/// <param name="userService">The Umbraco user service used to look up user group memberships.</param>
/// <param name="cache">The content two-level permission cache.</param>
public sealed class AdvancedPermissionService(
    IAdvancedPermissionRepository repository,
    IPermissionResolver resolver,
    IUserService userService,
    AdvancedPermissionCache cache)
    : NodePermissionServiceBase(repository, resolver, userService, cache, AdvancedPermissionsConstants.AllVerbs),
        IAdvancedPermissionService;
