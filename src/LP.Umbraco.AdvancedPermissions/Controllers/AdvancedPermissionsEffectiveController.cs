using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Services;
using LP.Umbraco.AdvancedPermissions.Controllers.Models;
using LP.Umbraco.AdvancedPermissions.Core.Interfaces;

namespace LP.Umbraco.AdvancedPermissions.Controllers;

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
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <param name="userKey">The key of the user to resolve for.</param>
    /// <param name="nodeKey">The key of the content node to resolve at.</param>
    /// <returns>The effective permissions for all standard verbs, with full reasoning.</returns>
    [HttpGet("effective")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<EffectivePermissionsResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [EndpointSummary("Resolves effective permissions for a user at a content node.")]
    public async Task<IActionResult> GetEffectiveForUser(
        CancellationToken cancellationToken,
        Guid userKey,
        Guid nodeKey)
    {
        var pathFromRoot = BuildPathFromRoot(nodeKey, entityService);
        if (pathFromRoot.Count == 0)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Node not found",
                Detail = $"No content node was found with key '{nodeKey}'.",
                Status = StatusCodes.Status404NotFound,
            });
        }

        var resolved = await permissionService.ResolveAllAsync(userKey, nodeKey, pathFromRoot, cancellationToken: cancellationToken);
        var items = resolved.Values.Select(MapEffective).ToList();
        return Ok(new EffectivePermissionsResponseModel(nodeKey, items));
    }

    /// <summary>
    /// Resolves all effective permissions for a specific role at a content node.
    /// Resolves as if the user has exactly this role plus the implicit <c>$everyone</c> role.
    /// </summary>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <param name="roleAlias">The role alias to resolve for.</param>
    /// <param name="nodeKey">The key of the content node to resolve at.</param>
    /// <returns>The effective permissions for all standard verbs, with full reasoning.</returns>
    [HttpGet("effective/by-role")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<EffectivePermissionsResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [EndpointSummary("Resolves effective permissions for a role at a content node.")]
    public async Task<IActionResult> GetEffectiveForRole(
        CancellationToken cancellationToken,
        string roleAlias,
        Guid nodeKey)
    {
        var pathFromRoot = BuildPathFromRoot(nodeKey, entityService);
        if (pathFromRoot.Count == 0)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Node not found",
                Detail = $"No content node was found with key '{nodeKey}'.",
                Status = StatusCodes.Status404NotFound,
            });
        }

        var resolved = await permissionService.ResolveForRoleAsync(
            roleAlias, nodeKey, pathFromRoot, cancellationToken: cancellationToken);

        var items = resolved.Values.Select(MapEffective).ToList();
        return Ok(new EffectivePermissionsResponseModel(nodeKey, items));
    }
}
