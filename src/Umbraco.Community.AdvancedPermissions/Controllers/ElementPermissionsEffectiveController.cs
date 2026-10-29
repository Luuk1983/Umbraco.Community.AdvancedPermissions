using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Community.AdvancedPermissions.Controllers.Models;
using Umbraco.Community.AdvancedPermissions.Abstractions.Constants;
using Umbraco.Community.AdvancedPermissions.Abstractions.Interfaces;
using Umbraco.Community.AdvancedPermissions.Services;
using Umbraco.Community.AdvancedPermissions.Services.StartNodes;

namespace Umbraco.Community.AdvancedPermissions.Controllers;

/// <summary>
/// Resolves effective (inherited + explicit) element permissions for users and roles, with full reasoning.
/// Powers the Library access viewer.
/// </summary>
/// <remarks>
/// The package's element and element-folder backoffice conditions do not use this controller: they call
/// Umbraco's own <c>GET /user/current/permissions/element</c>, which Umbraco routes through
/// <see cref="IElementPermissionService"/> (issue #53). All returned verbs are the canonical
/// <c>Umb.Element.*</c> verbs.
/// </remarks>
/// <param name="permissionService">The element advanced permission service.</param>
/// <param name="entityService">The Umbraco entity service (for path resolution).</param>
/// <param name="startNodeAccessService">Classifies the node against the subject's Library start nodes.</param>
[ApiVersion("1.0")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessUsers)]
public sealed class ElementPermissionsEffectiveController(
    IElementNodePermissionService permissionService,
    IEntityService entityService,
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
