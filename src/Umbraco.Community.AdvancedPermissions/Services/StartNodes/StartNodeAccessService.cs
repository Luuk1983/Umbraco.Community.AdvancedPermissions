using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.Core.Constants;

namespace Umbraco.Community.AdvancedPermissions.Services.StartNodes;

/// <summary>
/// Default <see cref="IStartNodeAccessService"/>. Takes the effective start nodes from Umbraco
/// (<c>CalculateContentStartNodeIds</c>, cached per user exactly as enforcement reads it) and reads
/// node names and paths fresh, never from Umbraco's start-node path cache, which a move leaves stale.
/// </summary>
/// <param name="userService">Loads the user.</param>
/// <param name="userGroupService">Loads a user group by alias.</param>
/// <param name="entityService">Reads node keys, names and paths.</param>
/// <param name="appCaches">Umbraco's caches, passed to <c>CalculateContentStartNodeIds</c>.</param>
public sealed class StartNodeAccessService(
    IUserService userService,
    IUserGroupService userGroupService,
    IEntityService entityService,
    AppCaches appCaches) : IStartNodeAccessService
{
    /// <summary>The content root as a snapshot node.</summary>
    private static readonly StartNodeInfo RootNode =
        new(Constants.System.Root, Guid.Empty, string.Empty, Constants.System.RootString);

    /// <inheritdoc />
    public async Task<StartNodeSnapshot?> GetForUserAsync(Guid userKey)
    {
        var user = await userService.GetAsync(userKey);
        if (user is null)
        {
            return null;
        }

        var effective = user.CalculateContentStartNodeIds(entityService, appCaches) ?? [];

        var sources = user.Groups
            .Select(g => new StartNodeSource(StartNodeSourceKind.UserGroup, g.Alias, g.Name, g.StartContentId))
            .ToList();
        var userIds = user.StartContentIds ?? [];
        if (userIds.Length == 0)
        {
            sources.Add(new StartNodeSource(StartNodeSourceKind.User, null, null, null));
        }
        else
        {
            sources.AddRange(userIds.Select(id => new StartNodeSource(StartNodeSourceKind.User, null, null, id)));
        }

        return Build(effective, sources);
    }

    /// <inheritdoc />
    public async Task<StartNodeSnapshot?> GetForUserGroupAsync(string userGroupAlias)
    {
        if (userGroupAlias == AdvancedPermissionsConstants.EveryoneRoleAlias)
        {
            return StartNodeSnapshot.NotApplicable;
        }

        var group = await userGroupService.GetAsync(userGroupAlias);
        if (group is null)
        {
            return null;
        }

        var source = new StartNodeSource(StartNodeSourceKind.UserGroup, group.Alias, group.Name, group.StartContentId);
        var nodes = LoadNodes(group.StartContentId is { } startNodeId && startNodeId != Constants.System.Root ? [startNodeId] : []);

        // A group on its own: root, its one start node when it still exists outside the bin, or nothing.
        int[] effective = group.StartContentId switch
        {
            null => [],
            Constants.System.Root => [Constants.System.Root],
            int id when nodes.TryGetValue(id, out var node) && !StartNodeRules.IsInRecycleBin(node.Path) => [id],
            _ => [],
        };

        return Snapshot(effective, [source], nodes);
    }

    /// <inheritdoc />
    public StartNodeAccess Classify(StartNodeSnapshot snapshot, string nodePath)
    {
        if (snapshot.State == StartNodeSubjectState.NotApplicable)
        {
            return StartNodeAccess.Accessible;
        }

        var effectivePaths = snapshot.EffectiveStartNodeIds
            .Where(snapshot.Nodes.ContainsKey)
            .Select(id => snapshot.Nodes[id].Path);

        return StartNodeRules.Classify(nodePath, snapshot.EffectiveStartNodeIds, effectivePaths);
    }

    /// <summary>Loads every node a user snapshot references and builds the snapshot.</summary>
    /// <param name="effective">Umbraco's combined start node ids.</param>
    /// <param name="sources">The configured start nodes.</param>
    /// <returns>The snapshot.</returns>
    private StartNodeSnapshot Build(IReadOnlyList<int> effective, IReadOnlyList<StartNodeSource> sources)
    {
        var ids = effective
            .Concat(sources.Where(s => s.StartNodeId.HasValue).Select(s => s.StartNodeId!.Value))
            .Where(id => id != Constants.System.Root)
            .Distinct()
            .ToArray();

        return Snapshot(effective, sources, LoadNodes(ids));
    }

    /// <summary>Assembles a snapshot from its parts.</summary>
    /// <param name="effective">The effective start node ids.</param>
    /// <param name="sources">The configured start nodes.</param>
    /// <param name="nodes">Every referenced node, including the root.</param>
    /// <returns>The snapshot.</returns>
    private static StartNodeSnapshot Snapshot(
        IReadOnlyList<int> effective,
        IReadOnlyList<StartNodeSource> sources,
        IReadOnlyDictionary<int, StartNodeInfo> nodes)
    {
        var state = effective.Contains(Constants.System.Root)
            ? StartNodeSubjectState.RootAccess
            : effective.Count == 0
                ? StartNodeSubjectState.NoAccess
                : StartNodeSubjectState.Restricted;

        var paths = nodes.ToDictionary(n => n.Key, n => n.Value.Path);
        return new StartNodeSnapshot(state, effective, nodes, StartNodeRules.Attribute(sources, effective, paths));
    }

    /// <summary>Loads keys, names and paths for a set of node ids, plus the content root.</summary>
    /// <param name="ids">The node ids; must not contain the root.</param>
    /// <returns>The found nodes by id; deleted nodes are absent.</returns>
    private Dictionary<int, StartNodeInfo> LoadNodes(int[] ids)
    {
        var nodes = new Dictionary<int, StartNodeInfo> { [Constants.System.Root] = RootNode };

        // GetAll with no ids returns every document, so an empty set must never reach it.
        if (ids.Length == 0)
        {
            return nodes;
        }

        foreach (var entity in entityService.GetAll(UmbracoObjectTypes.Document, ids))
        {
            nodes[entity.Id] = new StartNodeInfo(entity.Id, entity.Key, entity.Name ?? string.Empty, entity.Path);
        }

        return nodes;
    }
}
