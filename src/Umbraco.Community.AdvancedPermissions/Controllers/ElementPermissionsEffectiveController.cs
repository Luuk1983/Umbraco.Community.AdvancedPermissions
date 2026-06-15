using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.Controllers.Models;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Services;

namespace Umbraco.Community.AdvancedPermissions.Controllers;

/// <summary>
/// Resolves effective (inherited + explicit) element permissions for users and roles. Powers the
/// Library access viewer and — critically — the <c>current-user</c> endpoint backs the package's custom
/// element/element-folder backoffice conditions.
/// </summary>
/// <remarks>
/// Umbraco 18 does not route the native element current-user permission path through
/// <see cref="IElementPermissionService"/> (the element form of issue #22351), so the package's element
/// conditions resolve UI action visibility via <see cref="GetEffectiveForCurrentUser"/> here rather than
/// the native <c>/user/current</c> set. All returned verbs are the canonical <c>Umb.Element.*</c> verbs.
/// </remarks>
/// <param name="permissionService">The element advanced permission service.</param>
/// <param name="entityService">The Umbraco entity service (for path resolution).</param>
/// <param name="backOfficeSecurityAccessor">Accessor for the current backoffice user.</param>
[ApiVersion("1.0")]
public sealed class ElementPermissionsEffectiveController(
    IElementNodePermissionService permissionService,
    IEntityService entityService,
    IBackOfficeSecurityAccessor backOfficeSecurityAccessor)
    : AdvancedPermissionsControllerBase
{
    /// <summary>
    /// Resolves all effective element permissions for a specific user at an element or folder.
    /// </summary>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <param name="userKey">The key of the user to resolve for.</param>
    /// <param name="nodeKey">The element/folder key to resolve at.</param>
    /// <returns>The effective permissions for all element verbs, with full reasoning.</returns>
    [HttpGet("element/effective", Name = "GetElementEffectiveForUser")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<EffectivePermissionsResponseModel>(StatusCodes.Status200OK)]
    [EndpointSummary("Resolves effective element permissions for a user at a node.")]
    public async Task<IActionResult> GetEffectiveForUser(
        CancellationToken cancellationToken,
        Guid userKey,
        Guid nodeKey)
    {
        var (pathFromRoot, targetKey) = ResolvePath(nodeKey);
        if (pathFromRoot.Count == 0)
        {
            return Ok(new EffectivePermissionsResponseModel(nodeKey, []));
        }

        var resolved = await permissionService.ResolveAllAsync(userKey, targetKey, pathFromRoot, cancellationToken: cancellationToken);
        return Ok(new EffectivePermissionsResponseModel(nodeKey, resolved.Values.Select(MapEffective).ToList()));
    }

    /// <summary>
    /// Resolves all effective element permissions for a specific role at an element or folder. Only the
    /// role's own entries are considered — <c>$everyone</c> is excluded.
    /// </summary>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <param name="roleAlias">The role alias to resolve for.</param>
    /// <param name="nodeKey">The element/folder key to resolve at.</param>
    /// <returns>The effective permissions for all element verbs, with full reasoning.</returns>
    [HttpGet("element/effective/by-role", Name = "GetElementEffectiveForRole")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<EffectivePermissionsResponseModel>(StatusCodes.Status200OK)]
    [EndpointSummary("Resolves effective element permissions for a role at a node.")]
    public async Task<IActionResult> GetEffectiveForRole(
        CancellationToken cancellationToken,
        string roleAlias,
        Guid nodeKey)
    {
        var (pathFromRoot, targetKey) = ResolvePath(nodeKey);
        if (pathFromRoot.Count == 0)
        {
            return Ok(new EffectivePermissionsResponseModel(nodeKey, []));
        }

        var resolved = await permissionService.ResolveForRoleAsync(roleAlias, targetKey, pathFromRoot, cancellationToken: cancellationToken);
        return Ok(new EffectivePermissionsResponseModel(nodeKey, resolved.Values.Select(MapEffective).ToList()));
    }

    /// <summary>
    /// Resolves all effective element permissions for the current backoffice user at an element or
    /// folder. This is the seam the package's custom element conditions call to gate UI action
    /// visibility (replacing the native, unrouted element current-user path).
    /// </summary>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <param name="nodeKey">
    /// The element/folder key to resolve at, or the virtual-root key for root-level defaults.
    /// </param>
    /// <returns>The current user's effective element permissions for all verbs.</returns>
    [HttpGet("element/effective/current-user", Name = "GetElementEffectiveForCurrentUser")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<EffectivePermissionsResponseModel>(StatusCodes.Status200OK)]
    [EndpointSummary("Resolves effective element permissions for the current user at a node.")]
    public async Task<IActionResult> GetEffectiveForCurrentUser(
        CancellationToken cancellationToken,
        Guid nodeKey)
    {
        var currentUser = backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser;
        if (currentUser is null)
        {
            return Ok(new EffectivePermissionsResponseModel(nodeKey, []));
        }

        var (pathFromRoot, targetKey) = ResolvePath(nodeKey);
        if (pathFromRoot.Count == 0)
        {
            return Ok(new EffectivePermissionsResponseModel(nodeKey, []));
        }

        var resolved = await permissionService.ResolveAllAsync(currentUser.Key, targetKey, pathFromRoot, cancellationToken: cancellationToken);
        return Ok(new EffectivePermissionsResponseModel(nodeKey, resolved.Values.Select(MapEffective).ToList()));
    }

    /// <summary>
    /// Resolves the path-from-root for a node key, treating the virtual-root key as a root-level
    /// resolution (path = [virtual root]) so root defaults can be queried.
    /// </summary>
    /// <param name="nodeKey">The requested node key.</param>
    /// <returns>The path from root and the target key to resolve at; an empty path when unresolvable.</returns>
    private (IReadOnlyList<Guid> PathFromRoot, Guid TargetKey) ResolvePath(Guid nodeKey)
    {
        if (nodeKey == AdvancedPermissionsConstants.VirtualRootNodeKey)
        {
            return ([AdvancedPermissionsConstants.VirtualRootNodeKey], AdvancedPermissionsConstants.VirtualRootNodeKey);
        }

        return (ElementTreePathResolver.BuildPathFromRoot(entityService, nodeKey), nodeKey);
    }
}
