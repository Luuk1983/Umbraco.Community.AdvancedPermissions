using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.Caching;
using Umbraco.Community.AdvancedPermissions.Core.Constants;

namespace Umbraco.Community.AdvancedPermissions.Services.StartNodes;

/// <summary>
/// Default <see cref="IStartNodeAccessService"/>. Takes the effective start nodes from Umbraco
/// (<c>CalculateContentStartNodeIds</c> or <c>CalculateElementStartNodeIds</c>, cached per user exactly
/// as enforcement reads them) and reads node names and paths fresh, never from Umbraco's start-node
/// path cache, which a move leaves stale.
/// </summary>
/// <param name="userService">Loads the user.</param>
/// <param name="userGroupService">Loads a user group by alias.</param>
/// <param name="entityService">Reads node keys, names and paths.</param>
/// <param name="appCaches">Umbraco's caches, passed to Umbraco's start-node calculation.</param>
/// <param name="scopeCache">Keeps each subject's classification scope, so a viewer's rows share one load.</param>
public sealed class StartNodeAccessService(
    IUserService userService,
    IUserGroupService userGroupService,
    IEntityService entityService,
    AppCaches appCaches,
    StartNodeScopeCache scopeCache) : IStartNodeAccessService
{
    /// <summary>The tree root as a snapshot node.</summary>
    private static readonly StartNodeInfo RootNode =
        new(Constants.System.Root, Guid.Empty, string.Empty, Constants.System.RootString);

    /// <inheritdoc />
    public async Task<StartNodeSnapshot?> GetForUserAsync(Guid userKey, StartNodeTree tree)
    {
        var user = await userService.GetAsync(userKey);
        if (user is null)
        {
            return null;
        }

        var definition = StartNodeTreeDefinition.For(tree);
        var effective = definition.CalculateEffective(user, entityService, appCaches) ?? [];

        var sources = user.Groups
            .Select(g => new StartNodeSource(StartNodeSourceKind.UserGroup, g.Alias, g.Name, definition.UserGroupStartNodeId(g)))
            .ToList();
        var userIds = definition.UserStartNodeIds(user) ?? [];
        if (userIds.Length == 0)
        {
            sources.Add(new StartNodeSource(StartNodeSourceKind.User, null, null, null));
        }
        else
        {
            sources.AddRange(userIds.Select(id => new StartNodeSource(StartNodeSourceKind.User, null, null, id)));
        }

        return Build(definition, effective, sources);
    }

    /// <inheritdoc />
    public async Task<StartNodeSnapshot?> GetForUserGroupAsync(string userGroupAlias, StartNodeTree tree)
    {
        if (userGroupAlias == AdvancedPermissionsConstants.EveryoneRoleAlias)
        {
            return StartNodeSnapshot.NotApplicableFor(tree);
        }

        var group = await userGroupService.GetAsync(userGroupAlias);
        if (group is null)
        {
            return null;
        }

        var definition = StartNodeTreeDefinition.For(tree);
        var startNodeId = definition.StoredUserGroupStartNodeId(group);
        var source = new StartNodeSource(StartNodeSourceKind.UserGroup, group.Alias, group.Name, startNodeId);
        var nodes = LoadNodes(definition, startNodeId is { } id && id != Constants.System.Root ? [id] : []);

        // A group on its own: root, its one start node when it still exists outside the bin, or nothing.
        int[] effective = startNodeId switch
        {
            null => [],
            Constants.System.Root => [Constants.System.Root],
            int nodeId when nodes.TryGetValue(nodeId, out var node)
                && !StartNodeRules.IsInRecycleBin(node.Path, definition.RecycleBinId) => [nodeId],
            _ => [],
        };

        return Snapshot(definition, effective, [source], nodes);
    }

    /// <inheritdoc />
    public async Task<StartNodeScope?> GetScopeForUserAsync(Guid userKey, StartNodeTree tree)
    {
        if (scopeCache.GetForUser(userKey, tree) is { } cached)
        {
            return cached;
        }

        var scope = ToScope(await GetForUserAsync(userKey, tree));
        if (scope is not null)
        {
            scopeCache.SetForUser(userKey, scope);
        }

        return scope;
    }

    /// <inheritdoc />
    public async Task<StartNodeScope?> GetScopeForUserGroupAsync(string userGroupAlias, StartNodeTree tree)
    {
        if (scopeCache.GetForUserGroup(userGroupAlias, tree) is { } cached)
        {
            return cached;
        }

        var scope = ToScope(await GetForUserGroupAsync(userGroupAlias, tree));
        if (scope is not null)
        {
            scopeCache.SetForUserGroup(userGroupAlias, scope);
        }

        return scope;
    }

    /// <inheritdoc />
    public StartNodeAccess Classify(StartNodeScope scope, string nodePath) =>
        scope.State == StartNodeSubjectState.NotApplicable
            ? StartNodeAccess.Accessible
            : StartNodeRules.Classify(
                nodePath,
                scope.EffectiveStartNodeIds,
                scope.EffectiveStartNodePaths,
                StartNodeTreeDefinition.For(scope.Tree).RecycleBinId);

    /// <summary>Reduces a snapshot to what classifying a node needs.</summary>
    /// <param name="snapshot">The snapshot, or <see langword="null"/> for an unknown subject.</param>
    /// <returns>The scope, or <see langword="null"/>.</returns>
    private static StartNodeScope? ToScope(StartNodeSnapshot? snapshot) =>
        snapshot is null
            ? null
            : new StartNodeScope(
                snapshot.Tree,
                snapshot.State,
                snapshot.EffectiveStartNodeIds,
                snapshot.EffectiveStartNodeIds
                    .Where(snapshot.Nodes.ContainsKey)
                    .Select(id => snapshot.Nodes[id].Path)
                    .ToList());

    /// <summary>Loads every node a user snapshot references and builds the snapshot.</summary>
    /// <param name="definition">The tree's definition.</param>
    /// <param name="effective">Umbraco's combined start node ids.</param>
    /// <param name="sources">The configured start nodes.</param>
    /// <returns>The snapshot.</returns>
    private StartNodeSnapshot Build(
        StartNodeTreeDefinition definition,
        IReadOnlyList<int> effective,
        IReadOnlyList<StartNodeSource> sources)
    {
        var ids = effective
            .Concat(sources.Where(s => s.StartNodeId.HasValue).Select(s => s.StartNodeId!.Value))
            .Where(id => id != Constants.System.Root)
            .Distinct()
            .ToArray();

        return Snapshot(definition, effective, sources, LoadNodes(definition, ids));
    }

    /// <summary>Assembles a snapshot from its parts.</summary>
    /// <param name="definition">The tree's definition.</param>
    /// <param name="effective">The effective start node ids.</param>
    /// <param name="sources">The configured start nodes.</param>
    /// <param name="nodes">Every referenced node, including the root.</param>
    /// <returns>The snapshot.</returns>
    private static StartNodeSnapshot Snapshot(
        StartNodeTreeDefinition definition,
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
        return new StartNodeSnapshot(
            definition.Tree,
            state,
            effective,
            nodes,
            StartNodeRules.Attribute(sources, effective, paths, definition.RecycleBinId));
    }

    /// <summary>Loads keys, names and paths for a set of start node ids, plus the tree root.</summary>
    /// <param name="definition">The tree's definition (which object type to read).</param>
    /// <param name="ids">The node ids; must not contain the root.</param>
    /// <returns>The found nodes by id; deleted nodes are absent.</returns>
    private Dictionary<int, StartNodeInfo> LoadNodes(StartNodeTreeDefinition definition, int[] ids)
    {
        var nodes = new Dictionary<int, StartNodeInfo> { [Constants.System.Root] = RootNode };

        // GetAll with no ids returns every entity, so an empty set must never reach it.
        if (ids.Length == 0)
        {
            return nodes;
        }

        // The multi-type overload: the single-type GetAll throws for element folders in Umbraco 18,
        // because they have no CLR entity type.
        foreach (var entity in entityService.GetAll([definition.NodeObjectType], ids))
        {
            nodes[entity.Id] = new StartNodeInfo(entity.Id, entity.Key, entity.Name ?? string.Empty, entity.Path);
        }

        return nodes;
    }
}
