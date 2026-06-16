using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Community.AdvancedPermissions.Controllers.Models;
using Umbraco.Community.AdvancedPermissions.Core.Constants;

namespace Umbraco.Community.AdvancedPermissions.Controllers;

/// <summary>
/// Provides element-specific metadata for the Library permissions editor: the canonical element verbs.
/// Roles are shared with content and served by <see cref="AdvancedPermissionsMetaController"/>'s
/// <c>roles</c> endpoint.
/// </summary>
[ApiVersion("1.0")]
public sealed class ElementPermissionsMetaController : AdvancedPermissionsControllerBase
{
    /// <summary>
    /// Gets all canonical element permission verbs with human-readable display names. Verb grouping and
    /// per-node-kind applicability are applied by the frontend permission-target descriptor.
    /// </summary>
    /// <returns>The list of element verbs with display names.</returns>
    [HttpGet("element/verbs", Name = "GetElementVerbs")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<IReadOnlyList<VerbResponseModel>>(StatusCodes.Status200OK)]
    [EndpointSummary("Gets all canonical element permission verbs.")]
    public IActionResult GetVerbs() =>
        Ok(AdvancedPermissionsConstants.ElementVerbs
            .Select(v => new VerbResponseModel(v, GetVerbDisplayName(v)))
            .ToList());

    /// <summary>
    /// Converts a full element verb string to a human-readable display name
    /// (e.g. <c>Umb.Element.Read</c> becomes <c>Read</c>).
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
