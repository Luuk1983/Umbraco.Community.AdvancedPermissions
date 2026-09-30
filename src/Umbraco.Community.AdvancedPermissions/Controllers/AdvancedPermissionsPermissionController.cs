using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Community.AdvancedPermissions.Controllers.Models;
using Umbraco.Community.AdvancedPermissions.Core.Concurrency;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Exceptions;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.Controllers;

/// <summary>
/// Provides CRUD operations for raw advanced security permission entries.
/// </summary>
/// <remarks>
/// Gated on Users-section access: it exposes the mutating (PUT/DELETE) endpoints, which are a
/// privilege-escalation primitive if reachable by any authenticated backoffice user.
/// </remarks>
/// <param name="permissionService">The advanced permission service.</param>
/// <param name="repository">The permission repository for batch queries.</param>
/// <param name="entityService">The Umbraco entity service for path resolution.</param>
[ApiVersion("1.0")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessUsers)]
public sealed class AdvancedPermissionsPermissionController(
    IAdvancedPermissionService permissionService,
    IAdvancedPermissionRepository repository,
    IEntityService entityService)
    : AdvancedPermissionsControllerBase
{
    /// <summary>
    /// Gets all stored permission entries for a specific node and role.
    /// </summary>
    /// <remarks>
    /// The response carries an <c>ETag</c> header holding the concurrency stamp of the returned
    /// entries. A client that later saves back what it loaded here sends that stamp with the
    /// write; the server refuses the save if the stored entries have moved since, rather than
    /// silently overwriting a change nobody has seen yet.
    /// </remarks>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <param name="nodeKey">
    /// The content node key. Use <c>ffffffff-ffff-ffff-ffff-ffffffffffff</c> for virtual-root entries.
    /// </param>
    /// <param name="roleAlias">The role alias to filter by.</param>
    /// <returns>The stored entries for the given node and role.</returns>
    [HttpGet("permissions")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<IReadOnlyList<PermissionEntryResponseModel>>(StatusCodes.Status200OK)]
    [EndpointSummary("Gets permission entries for a node and role.")]
    public async Task<IActionResult> GetPermissions(
        CancellationToken cancellationToken,
        Guid nodeKey,
        string roleAlias)
    {
        var entries = await permissionService.GetEntriesAsync(nodeKey, roleAlias, cancellationToken);
        var models = entries.Select(MapEntry).ToList();
        Response.Headers.ETag = $"\"{models.ComputeFromResponse()}\"";
        return Ok(models);
    }

    /// <summary>
    /// Gets all stored permission entries for a specific node across all roles.
    /// </summary>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <param name="nodeKey">
    /// The content node key. Use <c>ffffffff-ffff-ffff-ffff-ffffffffffff</c> for virtual-root entries.
    /// </param>
    /// <returns>All stored entries for the given node.</returns>
    [HttpGet("permissions/by-node")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<IReadOnlyList<PermissionEntryResponseModel>>(StatusCodes.Status200OK)]
    [EndpointSummary("Gets all permission entries for a node (all roles).")]
    public async Task<IActionResult> GetPermissionsByNode(
        CancellationToken cancellationToken,
        Guid nodeKey)
    {
        var entries = await permissionService.GetEntriesByNodeAsync(nodeKey, cancellationToken);
        return Ok(entries.Select(MapEntry).ToList());
    }

    /// <summary>
    /// Saves (replaces) permission entries for a node and role.
    /// Pass an empty <c>Entries</c> list to remove all entries and revert to inherited behavior.
    /// </summary>
    /// <param name="request">The entries to save.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>
    /// <see cref="StatusCodes.Status200OK"/> on success, or <see cref="StatusCodes.Status409Conflict"/>
    /// when <see cref="SavePermissionsRequestModel.ExpectedStamp"/> no longer matches what is stored.
    /// </returns>
    [HttpPut("permissions")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<BatchSaveConflictResponseModel>(StatusCodes.Status409Conflict)]
    [EndpointSummary("Saves (replaces) permission entries for a node and role.")]
    public async Task<IActionResult> SavePermissions(
        [FromBody] SavePermissionsRequestModel request,
        CancellationToken cancellationToken)
    {
        if (!TryMapEntries(request.Entries, out var mapped, out var problem))
        {
            return BadRequest(problem);
        }

        // The stamp is checked by the write itself, inside its transaction, not here. A check made
        // here and a write made afterwards are two operations, and two saves carrying the same
        // stamp could both pass the first and then both perform the second. Force withholds the
        // stamp, which is what tells the write to skip the check.
        try
        {
            await permissionService.SaveEntriesAsync(
                request.NodeKey,
                request.RoleAlias,
                mapped,
                request.Force ? null : request.ExpectedStamp,
                cancellationToken);
        }
        catch (PermissionConcurrencyException ex)
        {
            return ConflictResult(ex);
        }

        return Ok();
    }

    /// <summary>
    /// Replaces permission entries for several nodes and user groups at once, refusing the whole
    /// batch if any pair has changed since the client read it.
    /// </summary>
    /// <remarks>
    /// All or nothing, deliberately. The editors change several nodes before saving, and a partial
    /// write would leave a state nothing afterwards could interpret — not the client's, not the
    /// server's, and not the next person's.
    /// </remarks>
    /// <param name="request">The pairs to write, each with the stamp the client read.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>
    /// <see cref="StatusCodes.Status200OK"/> with the new stamp per pair, or
    /// <see cref="StatusCodes.Status409Conflict"/> naming the pairs that moved.
    /// </returns>
    [HttpPut("permissions/batch")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<IReadOnlyList<BatchSavedStamp>>(StatusCodes.Status200OK)]
    [ProducesResponseType<BatchSaveConflictResponseModel>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [EndpointSummary("Saves permission entries for several nodes at once, all or nothing.")]
    public async Task<IActionResult> BatchSavePermissions(
        [FromBody] BatchSavePermissionsRequestModel request,
        CancellationToken cancellationToken)
    {
        // Validate everything first. A batch that cannot be mapped is a client bug, not a
        // conflict, and reporting it as one would send the user to a dialog about somebody
        // else's changes when nobody else has changed anything.
        var pending = new List<(Guid NodeKey, string RoleAlias, IReadOnlyList<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries, string? ExpectedStamp)>();

        foreach (var node in request.Nodes)
        {
            if (!TryMapEntries(node.Entries, out var mapped, out var problem))
            {
                return BadRequest(problem);
            }

            pending.Add((node.NodeKey, node.RoleAlias, mapped, request.Force ? null : node.ExpectedStamp));
        }

        // A repeated (NodeKey, RoleAlias) pair is a malformed request, the same class of problem
        // as a bad verb, so it is checked here alongside the other request-shape validation.
        // IAdvancedPermissionService.SaveManyAsync (via the repository) also guards this, but that
        // guard is a last-resort invariant check deep in the write path, not a request-validation
        // mechanism — there is no global exception handler in this package, so letting its
        // ArgumentException escape from here would turn a client mistake into an unhandled 500.
        var seenPairs = new HashSet<(Guid NodeKey, string RoleAlias)>();
        foreach (var node in request.Nodes)
        {
            if (!seenPairs.Add((node.NodeKey, node.RoleAlias)))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Duplicate node and role pair",
                    Detail = $"Node '{node.NodeKey}' and role '{node.RoleAlias}' appear more than once in this batch. Each node+role pair must appear at most once.",
                    Status = StatusCodes.Status400BadRequest,
                });
            }
        }

        // As with the single save, the stamps are checked by the write, inside its transaction —
        // not by a pass over the stored entries here. The exception, when it comes, lists every
        // conflicted pair and guarantees nothing was written for any of them.
        try
        {
            await permissionService.SaveManyAsync(pending, cancellationToken);
        }
        catch (PermissionConcurrencyException ex)
        {
            return ConflictResult(ex);
        }

        // The new stamps, so the client can keep editing without a further read. Computed from
        // what was written rather than re-read, because the write just made them equal and a
        // second round trip per node would buy nothing.
        var saved = request.Nodes
            .Select(n => new BatchSavedStamp(
                n.NodeKey,
                n.RoleAlias,
                PermissionStamp.ComputeFromNames(
                    n.Entries.Select(e => (e.Verb, e.State, e.Scope, e.IsPriorityOverride)))))
            .ToList();

        return Ok(saved);
    }

    /// <summary>
    /// Turns the exception a stale write raises into the <c>409 Conflict</c> the client is written
    /// against: a <see cref="ProblemDetails"/> carrying the conflicts as its <c>conflicts</c>
    /// extension. The body's shape is a wire contract — see <see cref="ConflictProblemDetails"/>.
    /// </summary>
    /// <param name="exception">The exception listing every conflicted pair.</param>
    /// <returns>The 409 result.</returns>
    private ConflictObjectResult ConflictResult(PermissionConcurrencyException exception) =>
        Conflict(ConflictProblemDetails.Create<BatchSaveConflict>(
            exception.Conflicts
                .Select(c => new BatchSaveConflict(
                    c.NodeKey,
                    c.RoleAlias,
                    c.CurrentEntries.Select(MapEntry).ToList(),
                    c.CurrentStamp))
                .ToList()));

    /// <summary>
    /// Validates and maps the entries of one save, or produces the problem describing why it
    /// cannot be mapped.
    /// </summary>
    /// <param name="items">The raw entries from the request.</param>
    /// <param name="mapped">The mapped entries, when validation succeeds.</param>
    /// <param name="problem">The problem to return, when it does not.</param>
    /// <returns><see langword="true"/> when every entry is valid.</returns>
    private static bool TryMapEntries(
        IReadOnlyList<SavePermissionEntryItem> items,
        out List<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> mapped,
        out ProblemDetails? problem)
    {
        mapped = [];
        problem = null;

        foreach (var entry in items)
        {
            if (!Enum.TryParse<PermissionState>(entry.State, ignoreCase: true, out var state))
            {
                problem = new ProblemDetails
                {
                    Title = "Invalid state",
                    Detail = $"'{entry.State}' is not a valid permission state. Use 'Allow' or 'Deny'.",
                    Status = StatusCodes.Status400BadRequest,
                };
                return false;
            }

            if (!Enum.TryParse<PermissionScope>(entry.Scope, ignoreCase: true, out var scope))
            {
                problem = new ProblemDetails
                {
                    Title = "Invalid scope",
                    Detail = $"'{entry.Scope}' is not a valid permission scope. Use 'ThisNodeOnly', 'ThisNodeAndDescendants', or 'DescendantsOnly'.",
                    Status = StatusCodes.Status400BadRequest,
                };
                return false;
            }

            if (!AdvancedPermissionsConstants.AllVerbs.Contains(entry.Verb, StringComparer.Ordinal))
            {
                problem = new ProblemDetails
                {
                    Title = "Invalid verb",
                    Detail = $"'{entry.Verb}' is not a recognized permission verb.",
                    Status = StatusCodes.Status400BadRequest,
                };
                return false;
            }

            mapped.Add((entry.Verb, state, scope, entry.IsPriorityOverride));
        }

        return true;
    }

    /// <summary>
    /// Gets the inheritance path from virtual root to a target node, along with all stored
    /// permission entries for a specific verb at every node in the path (across all roles).
    /// Used by the Access Viewer reasoning dialog to show where permissions come from.
    /// </summary>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <param name="nodeKey">The content node key of the target node.</param>
    /// <param name="verb">The permission verb to filter entries by.</param>
    /// <returns>The path and filtered entries.</returns>
    [HttpGet("permissions/for-path")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<PathEntriesResponseModel>(StatusCodes.Status200OK)]
    [EndpointSummary("Gets the inheritance path and stored entries for a verb along that path.")]
    public async Task<IActionResult> GetPermissionsForPath(
        CancellationToken cancellationToken,
        Guid nodeKey,
        string verb)
    {
        // Build the content path from root to target
        var contentPath = BuildPathFromRoot(nodeKey, entityService);

        // Build path models with names and icons
        var pathNodes = new List<PathNodeModel>();

        // Virtual root is always first
        pathNodes.Add(new PathNodeModel(
            AdvancedPermissionsConstants.VirtualRootNodeKey,
            AdvancedPermissionsConstants.EveryoneRoleDisplayName,
            "icon-globe"));

        if (contentPath.Count > 0)
        {
            // Bulk-fetch entity details for names and icons
            var entities = entityService
                .GetAll(UmbracoObjectTypes.Document, contentPath.Select(k => k).ToArray())
                .ToDictionary(e => e.Key);

            foreach (var key in contentPath)
            {
                if (entities.TryGetValue(key, out var entity))
                {
                    var icon = entity is IContentEntitySlim contentSlim ? contentSlim.ContentTypeIcon : null;
                    pathNodes.Add(new PathNodeModel(key, entity.Name ?? string.Empty, icon));
                }
            }
        }

        // Fetch all entries for the path nodes in a single query
        var allNodeKeys = pathNodes.Select(p => p.Key);
        var allEntries = await repository.GetByNodesAsync(allNodeKeys, cancellationToken);

        // Filter to the requested verb only
        var filteredEntries = allEntries
            .Where(e => string.Equals(e.Verb, verb, StringComparison.Ordinal))
            .Select(MapEntry)
            .ToList();

        return Ok(new PathEntriesResponseModel(pathNodes, filteredEntries));
    }

    /// <summary>
    /// Removes a specific permission entry, reverting it to the inherited state.
    /// </summary>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <param name="nodeKey">
    /// The content node key. Use <c>ffffffff-ffff-ffff-ffff-ffffffffffff</c> for virtual-root entries.
    /// </param>
    /// <param name="roleAlias">The role alias.</param>
    /// <param name="verb">The permission verb to remove.</param>
    /// <returns><see cref="StatusCodes.Status200OK"/> on success.</returns>
    [HttpDelete("permissions")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [EndpointSummary("Removes a specific permission entry (reverts to inherit).")]
    public async Task<IActionResult> DeletePermission(
        CancellationToken cancellationToken,
        Guid nodeKey,
        string roleAlias,
        string verb)
    {
        await permissionService.DeleteEntryAsync(nodeKey, roleAlias, verb, cancellationToken);
        return Ok();
    }
}
