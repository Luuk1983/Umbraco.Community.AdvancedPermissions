using Umbraco.Community.AdvancedPermissions.Caching;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Community.AdvancedPermissions.Services;

/// <summary>
/// Resolves and manages advanced security permissions for library elements and folders. Binds the
/// shared <see cref="NodePermissionServiceBase"/> orchestration to the element repository, the element
/// cache (<c>UAP.Element</c> prefix), and the canonical element verb set
/// (<see cref="AdvancedPermissionsConstants.ElementVerbs"/>).
/// </summary>
/// <param name="repository">The element/folder permission repository.</param>
/// <param name="resolver">The pure resolver that applies inheritance and priority rules (default Deny).</param>
/// <param name="userService">The Umbraco user service used to look up user group memberships.</param>
/// <param name="cache">The element two-level permission cache.</param>
public sealed class ElementNodePermissionService(
    IElementPermissionRepository repository,
    IPermissionResolver resolver,
    IUserService userService,
    ElementPermissionCache cache)
    : NodePermissionServiceBase(repository, resolver, userService, cache, AdvancedPermissionsConstants.ElementVerbs),
        IElementNodePermissionService;
