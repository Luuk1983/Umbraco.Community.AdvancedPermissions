using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Community.AdvancedPermissions.Controllers.Models;
using Umbraco.Community.AdvancedPermissions.Services.StartNodes;

namespace Umbraco.Community.AdvancedPermissions.Controllers;

/// <summary>
/// Provides the content and Library start nodes of a user or user group, with where each one comes
/// from. Feeds the start-node card in the four viewers.
/// </summary>
/// <param name="startNodeAccessService">Builds start-node snapshots.</param>
[ApiVersion("1.0")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessUsers)]
public sealed class AdvancedPermissionsStartNodesController(IStartNodeAccessService startNodeAccessService)
    : AdvancedPermissionsControllerBase
{
    /// <summary>Gets a user's effective content start nodes and their sources.</summary>
    /// <param name="userKey">The user's key.</param>
    /// <returns>The start nodes, or 404 when the user does not exist.</returns>
    [HttpGet("start-nodes", Name = "GetStartNodesForUser")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<StartNodesResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [EndpointSummary("Gets a user's effective content start nodes and where each comes from.")]
    public Task<IActionResult> GetStartNodesForUser(Guid userKey) => ForUserAsync(userKey, StartNodeTree.Content);

    /// <summary>Gets one user group's own content start node.</summary>
    /// <param name="roleAlias">The user group alias, or <c>$everyone</c>.</param>
    /// <returns>The start nodes, or 404 when the group does not exist.</returns>
    [HttpGet("start-nodes/by-role", Name = "GetStartNodesForRole")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<StartNodesResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [EndpointSummary("Gets one user group's own content start node.")]
    public Task<IActionResult> GetStartNodesForRole(string roleAlias) => ForUserGroupAsync(roleAlias, StartNodeTree.Content);

    /// <summary>Gets a user's effective Library start nodes and their sources.</summary>
    /// <param name="userKey">The user's key.</param>
    /// <returns>The start nodes, or 404 when the user does not exist.</returns>
    [HttpGet("element/start-nodes", Name = "GetElementStartNodesForUser")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<StartNodesResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [EndpointSummary("Gets a user's effective Library start nodes and where each comes from.")]
    public Task<IActionResult> GetElementStartNodesForUser(Guid userKey) => ForUserAsync(userKey, StartNodeTree.Library);

    /// <summary>Gets one user group's own Library start node.</summary>
    /// <param name="roleAlias">The user group alias, or <c>$everyone</c>.</param>
    /// <returns>The start nodes, or 404 when the group does not exist.</returns>
    [HttpGet("element/start-nodes/by-role", Name = "GetElementStartNodesForRole")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<StartNodesResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [EndpointSummary("Gets one user group's own Library start node.")]
    public Task<IActionResult> GetElementStartNodesForRole(string roleAlias) => ForUserGroupAsync(roleAlias, StartNodeTree.Library);

    /// <summary>Loads and maps a user's snapshot.</summary>
    /// <param name="userKey">The user's key.</param>
    /// <param name="tree">The tree.</param>
    /// <returns>200 with the start nodes, or 404.</returns>
    private async Task<IActionResult> ForUserAsync(Guid userKey, StartNodeTree tree)
    {
        var snapshot = await startNodeAccessService.GetForUserAsync(userKey, tree);
        return snapshot is null
            ? NotFound(new ProblemDetails { Title = "User not found", Detail = $"No user with key {userKey}.", Status = StatusCodes.Status404NotFound })
            : Ok(Map(snapshot));
    }

    /// <summary>Loads and maps a user group's snapshot.</summary>
    /// <param name="roleAlias">The user group alias.</param>
    /// <param name="tree">The tree.</param>
    /// <returns>200 with the start nodes, or 404.</returns>
    private async Task<IActionResult> ForUserGroupAsync(string roleAlias, StartNodeTree tree)
    {
        var snapshot = await startNodeAccessService.GetForUserGroupAsync(roleAlias, tree);
        return snapshot is null
            ? NotFound(new ProblemDetails { Title = "User group not found", Detail = $"No user group with alias {roleAlias}.", Status = StatusCodes.Status404NotFound })
            : Ok(Map(snapshot));
    }

    /// <summary>Maps a snapshot to the API model.</summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <returns>The response model.</returns>
    private static StartNodesResponseModel Map(StartNodeSnapshot snapshot)
    {
        var startNodes = snapshot.EffectiveStartNodeIds
            .Where(id => id != Constants.System.Root && snapshot.Nodes.ContainsKey(id))
            .Select(id => new StartNodeItemResponseModel(snapshot.Nodes[id].Key, snapshot.Nodes[id].Name))
            .ToList();

        var sources = snapshot.Sources
            .Select(r =>
            {
                var startNode = NodeOf(snapshot, r.Source.StartNodeId);
                var relatedNode = NodeOf(snapshot, r.RelatedStartNodeId);
                return new StartNodeSourceResponseModel(
                    r.Source.Kind.ToString(),
                    r.Source.UserGroupAlias,
                    r.Source.UserGroupName,
                    startNode?.Key,
                    startNode?.Name,
                    r.Source.StartNodeId == Constants.System.Root,
                    r.Status.ToString(),
                    relatedNode?.Key,
                    relatedNode?.Name,
                    r.RelatedStartNodeId == Constants.System.Root);
            })
            .ToList();

        return new StartNodesResponseModel(snapshot.State.ToString(), startNodes, sources);
    }

    /// <summary>Looks up a real (non-root) node in a snapshot.</summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <param name="id">The node id, or <see langword="null"/>.</param>
    /// <returns>The node, or <see langword="null"/> for none, root, or a deleted node.</returns>
    private static StartNodeInfo? NodeOf(StartNodeSnapshot snapshot, int? id) =>
        id is { } value && value != Constants.System.Root && snapshot.Nodes.TryGetValue(value, out var node) ? node : null;
}
