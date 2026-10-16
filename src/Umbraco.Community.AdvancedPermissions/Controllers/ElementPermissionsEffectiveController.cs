using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Community.AdvancedPermissions.Controllers.Models;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Services;
using Umbraco.Community.AdvancedPermissions.Services.StartNodes;

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
/// <param name="startNodeAccessService">Classifies the node against the subject's Library start nodes.</param>
[ApiVersion("1.0")]
public sealed class ElementPermissionsEffectiveController(
    IElementNodePermissionService permissionService,
    IEntityService entityService,
    IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
    IStartNodeAccessService startNodeAccessService)
    : AdvancedPermissionsControllerBase
{
    /// <summary>
    /// Resolves all effective element permissions for a specific user at an element or folder.
    /// </summary>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <param name="userKey">The key of the user to resolve for.</param>
    /// <param name="nodeKey">The element/folder key to resolve at.</param>
    /// <returns>The effective permissions for all element verbs, with full reasoning, plus the node's Library start-node access.</returns>
    [HttpGet("element/effective", Name = "GetElementEffectiveForUser")]
    [Authorize(Policy = AuthorizationPolicies.SectionAccessUsers)]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<EffectivePermissionsResponseModel>(StatusCodes.Status200OK)]
    [EndpointSummary("Resolves effective element permissions for a user at a node.")]
    public async Task<IActionResult> GetEffectiveForUser(
        CancellationToken cancellationToken,
        Guid userKey,
        Guid nodeKey)
    {
        var (pathFromRoot, targetKey, path) = ResolvePath(nodeKey);
        if (pathFromRoot.Count == 0 || path is null)
        {
            return Ok(new EffectivePermissionsResponseModel(nodeKey, [], nameof(StartNodeAccess.NotVisible)));
        }

        var resolved = await permissionService.ResolveAllAsync(userKey, targetKey, pathFromRoot, cancellationToken: cancellationToken);
        var access = Classify(await startNodeAccessService.GetScopeForUserAsync(userKey, StartNodeTree.Library), path);
        return Ok(new EffectivePermissionsResponseModel(nodeKey, resolved.Values.Select(MapEffective).ToList(), access.ToString()));
    }

    /// <summary>
    /// Resolves all effective element permissions for a specific role at an element or folder. Only the
    /// role's own entries are considered — <c>$everyone</c> is excluded.
    /// </summary>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <param name="roleAlias">The role alias to resolve for.</param>
    /// <param name="nodeKey">The element/folder key to resolve at.</param>
    /// <returns>The effective permissions for all element verbs, with full reasoning, plus the node's Library start-node access.</returns>
    [HttpGet("element/effective/by-role", Name = "GetElementEffectiveForRole")]
    [Authorize(Policy = AuthorizationPolicies.SectionAccessUsers)]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<EffectivePermissionsResponseModel>(StatusCodes.Status200OK)]
    [EndpointSummary("Resolves effective element permissions for a role at a node.")]
    public async Task<IActionResult> GetEffectiveForRole(
        CancellationToken cancellationToken,
        string roleAlias,
        Guid nodeKey)
    {
        var (pathFromRoot, targetKey, path) = ResolvePath(nodeKey);
        if (pathFromRoot.Count == 0 || path is null)
        {
            return Ok(new EffectivePermissionsResponseModel(nodeKey, [], nameof(StartNodeAccess.NotVisible)));
        }

        var resolved = await permissionService.ResolveForRoleAsync(roleAlias, targetKey, pathFromRoot, cancellationToken: cancellationToken);
        var access = Classify(await startNodeAccessService.GetScopeForUserGroupAsync(roleAlias, StartNodeTree.Library), path);
        return Ok(new EffectivePermissionsResponseModel(nodeKey, resolved.Values.Select(MapEffective).ToList(), access.ToString()));
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
    /// <remarks>
    /// Intentionally NOT gated on <see cref="AuthorizationPolicies.SectionAccessUsers"/>: the element and
    /// element-folder permission conditions call this to gate library-editing actions for the current
    /// user, so a content editor without Users-section access must still be able to reach it. Adding a
    /// section gate here re-introduces the read-only-content regression. It remains backoffice-authenticated
    /// via <c>ManagementApiControllerBase</c>, and resolves only for the current user — it takes no user
    /// or role parameter — so it discloses nothing about other users.
    /// Start nodes are not classified here: this endpoint gates action visibility for the current user,
    /// and enforcement already refuses unreachable nodes. <c>StartNodeAccess</c> is always <c>Accessible</c>.
    /// </remarks>
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
            return Ok(new EffectivePermissionsResponseModel(nodeKey, [], nameof(StartNodeAccess.Accessible)));
        }

        var (pathFromRoot, targetKey, _) = ResolvePath(nodeKey);
        if (pathFromRoot.Count == 0)
        {
            return Ok(new EffectivePermissionsResponseModel(nodeKey, [], nameof(StartNodeAccess.Accessible)));
        }

        var resolved = await permissionService.ResolveAllAsync(currentUser.Key, targetKey, pathFromRoot, cancellationToken: cancellationToken);
        return Ok(new EffectivePermissionsResponseModel(nodeKey, resolved.Values.Select(MapEffective).ToList(), nameof(StartNodeAccess.Accessible)));
    }

    /// <summary>
    /// Resolves the path-from-root for a node key, treating the virtual-root key as a root-level
    /// resolution (path = [virtual root]) so root defaults can be queried.
    /// </summary>
    /// <param name="nodeKey">The requested node key.</param>
    /// <returns>
    /// The path from root, the target key to resolve at, and the node's materialised path (the Library
    /// root <c>-1</c> for the virtual root; <see langword="null"/> when the node is not in the tree);
    /// an empty path from root when unresolvable.
    /// </returns>
    private (IReadOnlyList<Guid> PathFromRoot, Guid TargetKey, string? Path) ResolvePath(Guid nodeKey)
    {
        if (nodeKey == AdvancedPermissionsConstants.VirtualRootNodeKey)
        {
            return ([AdvancedPermissionsConstants.VirtualRootNodeKey], AdvancedPermissionsConstants.VirtualRootNodeKey, Constants.System.RootString);
        }

        var path = ElementTreePathResolver.GetPath(entityService, nodeKey);
        return (path is null ? [] : ElementTreePathResolver.BuildPathFromRoot(entityService, path), nodeKey, path);
    }

    /// <summary>
    /// Classifies a Library node for a subject. A subject that cannot be found reports
    /// <c>Accessible</c>, so the viewer never shows a restriction it could not verify.
    /// </summary>
    /// <param name="scope">The subject's Library start-node scope, or <see langword="null"/> when unknown.</param>
    /// <param name="path">The node's path, as already read to resolve its permissions.</param>
    /// <returns>The node's start-node access.</returns>
    private StartNodeAccess Classify(StartNodeScope? scope, string path) =>
        scope is null ? StartNodeAccess.Accessible : startNodeAccessService.Classify(scope, path);
}
