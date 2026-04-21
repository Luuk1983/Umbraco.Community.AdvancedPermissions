using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.Controllers.Models;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;

namespace Umbraco.Community.AdvancedPermissions.Controllers;

/// <summary>
/// Provides endpoints for resolving effective (inherited + explicit) permissions for users and roles.
/// Used by the Access Viewer to explain why a permission resolved the way it did.
/// </summary>
/// <param name="permissionService">The advanced permission service.</param>
/// <param name="entityService">The Umbraco entity service (for path resolution).</param>
[ApiVersion("1.0")]
public sealed class AdvancedPermissionsEffectiveController(
    IAdvancedPermissionService permissionService,
    IEntityService entityService)
    : AdvancedPermissionsControllerBase
{
    /// <summary>
    /// Resolves all effective permissions for a specific user at a content node.
    /// Includes reasoning for each verb showing which role(s) contributed and at what scope.
    /// </summary>
    /// <remarks>
    /// When <paramref name="nodeKey"/> does not resolve to an existing content node, this
    /// endpoint returns <c>200 OK</c> with an empty <c>Permissions</c> list rather than
    /// <c>404 Not Found</c>. This lets the backoffice safely query permissions for
    /// transient keys (e.g. drafts that have been pre-assigned a Guid but not yet saved)
    /// without triggering error-handling code paths. Callers that need to distinguish
    /// "node exists but has no permissions" from "node does not exist" should verify the
    /// node via another means.
    /// </remarks>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <param name="userKey">The key of the user to resolve for.</param>
    /// <param name="nodeKey">The key of the content node to resolve at.</param>
    /// <returns>The effective permissions for all standard verbs, with full reasoning.</returns>
    [HttpGet("effective")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<EffectivePermissionsResponseModel>(StatusCodes.Status200OK)]
    [EndpointSummary("Resolves effective permissions for a user at a content node.")]
    public async Task<IActionResult> GetEffectiveForUser(
        CancellationToken cancellationToken,
        Guid userKey,
        Guid nodeKey)
    {
        var pathFromRoot = BuildPathFromRoot(nodeKey, entityService);
        if (pathFromRoot.Count == 0)
        {
            return Ok(new EffectivePermissionsResponseModel(nodeKey, []));
        }

        var resolved = await permissionService.ResolveAllAsync(userKey, nodeKey, pathFromRoot, cancellationToken: cancellationToken);
        var items = resolved.Values.Select(MapEffective).ToList();
        return Ok(new EffectivePermissionsResponseModel(nodeKey, items));
    }

    /// <summary>
    /// Resolves all effective permissions for a specific role at a content node.
    /// Only the role's own entries are considered — <c>$everyone</c> is excluded.
    /// </summary>
    /// <remarks>
    /// When <paramref name="nodeKey"/> does not resolve to an existing content node, this
    /// endpoint returns <c>200 OK</c> with an empty <c>Permissions</c> list rather than
    /// <c>404 Not Found</c>. See <see cref="GetEffectiveForUser"/> for rationale.
    /// </remarks>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <param name="roleAlias">The role alias to resolve for.</param>
    /// <param name="nodeKey">The key of the content node to resolve at.</param>
    /// <returns>The effective permissions for all standard verbs, with full reasoning.</returns>
    [HttpGet("effective/by-role")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<EffectivePermissionsResponseModel>(StatusCodes.Status200OK)]
    [EndpointSummary("Resolves effective permissions for a role at a content node.")]
    public async Task<IActionResult> GetEffectiveForRole(
        CancellationToken cancellationToken,
        string roleAlias,
        Guid nodeKey)
    {
        var pathFromRoot = BuildPathFromRoot(nodeKey, entityService);
        if (pathFromRoot.Count == 0)
        {
            return Ok(new EffectivePermissionsResponseModel(nodeKey, []));
        }

        var resolved = await permissionService.ResolveForRoleAsync(
            roleAlias, nodeKey, pathFromRoot, cancellationToken: cancellationToken);

        var items = resolved.Values.Select(MapEffective).ToList();
        return Ok(new EffectivePermissionsResponseModel(nodeKey, items));
    }
}
