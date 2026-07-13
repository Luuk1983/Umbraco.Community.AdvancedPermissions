using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.Controllers.Models;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.Controllers;

/// <summary>
/// Base controller for all Advanced Security management API endpoints.
/// Provides shared utilities for building paths from root and mapping domain models to view models.
/// </summary>
/// <remarks>
/// The base only inherits the backoffice-access gate from <see cref="ManagementApiControllerBase"/>
/// (every endpoint still requires an authenticated backoffice user). The Users-section gate is applied
/// per management controller/action instead of here, because a base-class authorization attribute
/// inherits to every endpoint and cannot be removed by a derived controller — which previously forced
/// even the content editor's effective-permission endpoint behind the Users section and left content
/// read-only for users without that section. See the individual controllers for where the section gate
/// is applied.
/// </remarks>
[VersionedApiBackOfficeRoute("advanced-permissions")]
[ApiExplorerSettings(GroupName = "Advanced Permissions")]
public abstract class AdvancedPermissionsControllerBase : ManagementApiControllerBase
{
    /// <summary>
    /// Builds an ordered list of content node keys from the tree root to (and including) the given node.
    /// Returns an empty list if the node is not found.
    /// </summary>
    /// <param name="nodeKey">The key of the target content node.</param>
    /// <param name="entityService">The Umbraco entity service.</param>
    /// <returns>An ordered list of node keys from root to the target node (inclusive).</returns>
    protected static IReadOnlyList<Guid> BuildPathFromRoot(Guid nodeKey, IEntityService entityService)
    {
        var paths = entityService.GetAllPaths(UmbracoObjectTypes.Document, nodeKey);
        var pathEntry = paths.FirstOrDefault();
        if (pathEntry is null)
        {
            return [];
        }

        var pathIds = pathEntry.Path
            .Split(',')
            .Select(s => int.TryParse(s, out var id) ? id : 0)
            .Where(id => id > 0)
            .ToArray();

        if (pathIds.Length == 0)
        {
            return [];
        }

        var idToKey = entityService
            .GetAll(UmbracoObjectTypes.Document, pathIds)
            .ToDictionary(e => e.Id, e => e.Key);

        var result = new List<Guid>(pathIds.Length);
        foreach (var id in pathIds)
        {
            if (idToKey.TryGetValue(id, out var key))
            {
                result.Add(key);
            }
        }

        return result;
    }

    /// <summary>
    /// Maps a domain <see cref="AdvancedPermissionEntry"/> to an API <see cref="PermissionEntryResponseModel"/>.
    /// </summary>
    /// <param name="entry">The domain entry to map.</param>
    /// <returns>The mapped response model.</returns>
    protected static PermissionEntryResponseModel MapEntry(AdvancedPermissionEntry entry) =>
        new(
            entry.Id,
            entry.NodeKey,
            entry.RoleAlias,
            entry.Verb,
            entry.State.ToString(),
            entry.Scope.ToString(),
            entry.IsPriorityOverride);

    /// <summary>
    /// Maps a domain <see cref="EffectivePermission"/> to an API <see cref="EffectivePermissionItem"/>.
    /// </summary>
    /// <param name="ep">The effective permission to map.</param>
    /// <returns>The mapped item.</returns>
    protected static EffectivePermissionItem MapEffective(EffectivePermission ep) =>
        new(
            ep.Verb,
            ep.IsAllowed,
            ep.IsExplicit,
            ep.Reasoning.Select(MapReasoning).ToList(),
            ep.WasPriorityOverrideActive,
            (ep.SuppressedReasoning ?? []).Select(MapReasoning).ToList());

    /// <summary>
    /// Maps a single <see cref="PermissionReasoning"/> entry to the API <see cref="ReasoningItem"/>.
    /// </summary>
    /// <param name="r">The domain reasoning entry.</param>
    /// <returns>The mapped item.</returns>
    private static ReasoningItem MapReasoning(PermissionReasoning r) =>
        new(
            r.ContributingRole,
            r.State.ToString(),
            r.IsExplicit,
            r.SourceNodeKey,
            r.SourceScope?.ToString(),
            r.IsFromGroupDefault,
            r.IsPriorityOverride);
}
