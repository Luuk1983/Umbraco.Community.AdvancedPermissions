using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.Controllers.Models;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.Controllers;

/// <summary>
/// Management API for the document-type permission editor and the Create Audit view.
/// </summary>
/// <param name="docTypeService">The doc-type permission service.</param>
/// <param name="contentTypeService">The Umbraco content-type service used to list doc-types.</param>
/// <param name="entityService">Used to resolve content node paths for the audit.</param>
/// <param name="userService">Used to look up the audited user's groups.</param>
[ApiVersion("1.0")]
public sealed class DocTypePermissionsController(
    IDocTypePermissionService docTypeService,
    IContentTypeService contentTypeService,
    IEntityService entityService,
    IUserService userService)
    : AdvancedPermissionsControllerBase
{
    /// <summary>
    /// Lists every non-element document type. Used by the editor's doc-type picker.
    /// </summary>
    /// <returns>The list of non-element doc-types.</returns>
    [HttpGet("doc-type-permissions/doc-types")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<IReadOnlyList<DocTypeListItemModel>>(StatusCodes.Status200OK)]
    [EndpointSummary("Lists non-element document types.")]
    public IActionResult GetDocTypes()
    {
        var allTypes = contentTypeService.GetAll();
        var items = allTypes
            .Where(ct => !ct.IsElement)
            .OrderBy(ct => ct.Name, StringComparer.OrdinalIgnoreCase)
            .Select(ct => new DocTypeListItemModel(
                ct.Key,
                ct.Alias,
                ct.Name ?? ct.Alias,
                ct.Icon))
            .ToList();

        return Ok(items);
    }

    /// <summary>
    /// Gets all stored entries for a selected (role, doc-type) combination. The editor uses these
    /// to render the tree with the current state per node.
    /// </summary>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <param name="roleAlias">The role alias selected in the editor.</param>
    /// <param name="contentTypeKey">The doc-type selected in the editor.</param>
    /// <returns>All stored entries for the combination.</returns>
    [HttpGet("doc-type-permissions")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<IReadOnlyList<DocTypePermissionEntryResponseModel>>(StatusCodes.Status200OK)]
    [EndpointSummary("Gets all doc-type permission entries for a (role, content-type) combination.")]
    public async Task<IActionResult> GetForEditor(
        CancellationToken cancellationToken,
        string roleAlias,
        Guid contentTypeKey)
    {
        var entries = await docTypeService.GetEditorEntriesAsync(roleAlias, contentTypeKey, cancellationToken);
        return Ok(entries.Select(MapDocTypeEntry).ToList());
    }

    /// <summary>
    /// Saves (replaces) the entries for a (node, role, content-type) triple. Empty list clears.
    /// </summary>
    /// <param name="request">The entries to save.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns><see cref="StatusCodes.Status200OK"/> on success.</returns>
    [HttpPut("doc-type-permissions")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [EndpointSummary("Saves (replaces) doc-type permission entries for a node+role+content-type triple.")]
    public async Task<IActionResult> Save(
        [FromBody] SaveDocTypePermissionsRequestModel request,
        CancellationToken cancellationToken)
    {
        var mapped = new List<(string Verb, PermissionState State, PermissionScope Scope)>();

        foreach (var entry in request.Entries)
        {
            if (!Enum.TryParse<PermissionState>(entry.State, ignoreCase: true, out var state))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid state",
                    Detail = $"'{entry.State}' is not a valid permission state. Use 'Allow' or 'Deny'.",
                    Status = StatusCodes.Status400BadRequest,
                });
            }

            if (!Enum.TryParse<PermissionScope>(entry.Scope, ignoreCase: true, out var scope))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid scope",
                    Detail = $"'{entry.Scope}' is not a valid permission scope.",
                    Status = StatusCodes.Status400BadRequest,
                });
            }

            if (!AdvancedPermissionsConstants.DocTypeVerbs.Contains(entry.Verb, StringComparer.Ordinal))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid verb",
                    Detail = $"'{entry.Verb}' is not a recognized doc-type permission verb.",
                    Status = StatusCodes.Status400BadRequest,
                });
            }

            mapped.Add((entry.Verb, state, scope));
        }

        await docTypeService.SaveEditorEntriesAsync(
            request.NodeKey,
            request.RoleAlias,
            request.ContentTypeKey,
            mapped,
            cancellationToken);

        return Ok();
    }

    /// <summary>
    /// Returns the Create Audit listing for a given user under a given parent: every non-element
    /// doc-type with whether the user may create it, plus reasoning.
    /// </summary>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <param name="userKey">The audited user.</param>
    /// <param name="parentKey">
    /// The parent node, or <c>AdvancedPermissionsConstants.VirtualRootNodeKey</c> for root-level creates.
    /// </param>
    /// <returns>One row per candidate doc-type.</returns>
    [HttpGet("doc-type-permissions/audit")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<IReadOnlyList<DocTypeCreateAuditItemResponseModel>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [EndpointSummary("Audits which doc-types a user may create under a given parent.")]
    public async Task<IActionResult> Audit(
        CancellationToken cancellationToken,
        Guid userKey,
        Guid parentKey)
    {
        var user = await userService.GetAsync(userKey);
        if (user is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "User not found",
                Detail = $"No user with key {userKey}.",
                Status = StatusCodes.Status404NotFound,
            });
        }

        var path = parentKey == AdvancedPermissionsConstants.VirtualRootNodeKey
            ? [AdvancedPermissionsConstants.VirtualRootNodeKey]
            : BuildPathFromRoot(parentKey, entityService);

        if (path.Count == 0)
        {
            // Treat unresolvable parent as root-level
            path = [AdvancedPermissionsConstants.VirtualRootNodeKey];
        }

        var roleAliases = new List<string>(user.Groups.Count() + 1);
        roleAliases.AddRange(user.Groups.Select(g => g.Alias));
        roleAliases.Add(AdvancedPermissionsConstants.EveryoneRoleAlias);

        var candidates = contentTypeService.GetAll()
            .Where(ct => !ct.IsElement)
            .OrderBy(ct => ct.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rows = new List<DocTypeCreateAuditItemResponseModel>(candidates.Count);
        foreach (var ct in candidates)
        {
            var effective = await docTypeService.ResolveCreateForRolesAsync(
                roleAliases,
                path,
                ct.Key,
                cancellationToken);

            rows.Add(new DocTypeCreateAuditItemResponseModel(
                ContentTypeKey: ct.Key,
                ContentTypeAlias: ct.Alias,
                ContentTypeName: ct.Name ?? ct.Alias,
                ContentTypeIcon: ct.Icon,
                IsAllowed: effective.IsAllowed,
                IsExplicit: effective.IsExplicit,
                Reasoning: effective.Reasoning.Select(r => new ReasoningItem(
                    r.ContributingRole,
                    r.State.ToString(),
                    r.IsExplicit,
                    r.SourceNodeKey,
                    r.SourceScope?.ToString(),
                    r.IsFromGroupDefault)).ToList()));
        }

        return Ok(rows);
    }

    /// <summary>
    /// Maps a domain entry to the API response shape.
    /// </summary>
    /// <param name="entry">The domain entry.</param>
    /// <returns>The response model.</returns>
    private static DocTypePermissionEntryResponseModel MapDocTypeEntry(DocTypePermissionEntry entry) =>
        new(
            entry.Id,
            entry.NodeKey,
            entry.ContentTypeKey,
            entry.RoleAlias,
            entry.Verb,
            entry.State.ToString(),
            entry.Scope.ToString());
}
