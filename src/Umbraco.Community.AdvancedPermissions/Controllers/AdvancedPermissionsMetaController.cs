using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.Controllers.Models;
using Umbraco.Community.AdvancedPermissions.Core.Constants;

namespace Umbraco.Community.AdvancedPermissions.Controllers;

/// <summary>
/// Provides metadata endpoints for the Advanced Security system:
/// available permission verbs and assignable roles.
/// </summary>
/// <param name="userGroupService">The Umbraco user group service.</param>
[ApiVersion("1.0")]
public sealed class AdvancedPermissionsMetaController(IUserGroupService userGroupService)
    : AdvancedPermissionsControllerBase
{
    /// <summary>
    /// Gets all permission verbs available in the Advanced Security system.
    /// </summary>
    /// <returns>The list of permission verbs with display names.</returns>
    [HttpGet("verbs")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<IReadOnlyList<VerbResponseModel>>(StatusCodes.Status200OK)]
    [EndpointSummary("Gets all available permission verbs.")]
    public IActionResult GetVerbs() =>
        Ok(AdvancedPermissionsConstants.AllVerbs
            // Umb.Document.Permissions is temporarily disabled in Umbraco v17 (PR #20584).
            // The "Set Permissions" entity action no longer exists, so this verb has no effect.
            // Re-enable here when Umbraco restores the action.
            .Where(v => v != AdvancedPermissionsConstants.VerbManagePermissions)
            .Select(v => new VerbResponseModel(v, GetVerbDisplayName(v)))
            .ToList());

    /// <summary>
    /// Gets all assignable roles: every Umbraco user group plus the virtual <c>$everyone</c> role.
    /// </summary>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The list of roles with their display names.</returns>
    [HttpGet("roles")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<IReadOnlyList<RoleResponseModel>>(StatusCodes.Status200OK)]
    [EndpointSummary("Gets all assignable roles (user groups + $everyone).")]
    public async Task<IActionResult> GetRoles(CancellationToken cancellationToken)
    {
        var result = new List<RoleResponseModel>
        {
            // $everyone is always first — it's the global default role
            new(
                AdvancedPermissionsConstants.EveryoneRoleAlias,
                AdvancedPermissionsConstants.EveryoneRoleDisplayName,
                IsEveryone: true),
        };

        var skip = 0;
        const int take = 100;

        while (true)
        {
            var page = await userGroupService.GetAllAsync(skip, take);
            foreach (var group in page.Items)
            {
                result.Add(new RoleResponseModel(
                    group.Alias,
                    group.Name ?? group.Alias,
                    IsEveryone: false));
            }

            skip += take;
            if (skip >= page.Total)
            {
                break;
            }
        }

        return Ok(result);
    }

    /// <summary>
    /// Converts a full permission verb string to a human-readable display name.
    /// For example, <c>Umb.Document.Read</c> becomes <c>Read</c>.
    /// </summary>
    /// <param name="verb">The full verb string.</param>
    /// <returns>The display name portion of the verb.</returns>
    private static string GetVerbDisplayName(string verb)
    {
        var lastDot = verb.LastIndexOf('.');
        return lastDot >= 0 && lastDot < verb.Length - 1
            ? verb[(lastDot + 1)..]
            : verb;
    }
}
