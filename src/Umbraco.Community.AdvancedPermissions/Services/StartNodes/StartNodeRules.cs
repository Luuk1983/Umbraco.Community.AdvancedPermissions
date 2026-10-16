using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Security;

namespace Umbraco.Community.AdvancedPermissions.Services.StartNodes;

/// <summary>
/// Pure start-node rules: classifying a node against a subject's effective start nodes, and
/// explaining what became of each configured start node.
/// </summary>
/// <remarks>
/// Umbraco's own combination (<c>UserExtensions.CombineStartNodes</c>) is internal, so it is not
/// reimplemented. The effective set always comes from Umbraco; these rules only classify against it
/// and attribute it. Reachability calls Umbraco's <see cref="ContentPermissions.HasPathAccess"/>, the
/// check enforcement uses, so the viewers can never disagree with what the server allows.
/// </remarks>
public static class StartNodeRules
{
    /// <summary>The recycle-bin segment as it appears in a comma-wrapped path.</summary>
    private const string RecycleBinSegment = ",-20,";

    /// <summary>Classifies one node against a subject's effective start nodes.</summary>
    /// <param name="nodePath">The node's materialised path, e.g. <c>-1,1050</c>; <c>-1</c> is the content root.</param>
    /// <param name="effectiveStartNodeIds">Umbraco's combined start node ids.</param>
    /// <param name="effectiveStartNodePaths">The paths of those start nodes.</param>
    /// <returns>Whether the subject can reach the node.</returns>
    public static StartNodeAccess Classify(
        string nodePath,
        IReadOnlyCollection<int> effectiveStartNodeIds,
        IEnumerable<string> effectiveStartNodePaths)
    {
        if (ContentPermissions.HasPathAccess(nodePath, effectiveStartNodeIds.ToArray(), Constants.System.RecycleBinContent))
        {
            return StartNodeAccess.Accessible;
        }

        return effectiveStartNodePaths.Any(startPath => IsStrictAncestor(nodePath, startPath))
            ? StartNodeAccess.PathOnly
            : StartNodeAccess.NotVisible;
    }

    /// <summary>Explains what became of each configured start node.</summary>
    /// <param name="sources">The configured start nodes, user groups first, then the user's own.</param>
    /// <param name="effectiveStartNodeIds">Umbraco's combined start node ids.</param>
    /// <param name="pathsById">Paths of every referenced node; a missing id means the node was deleted, except the content root (-1), which is implied.</param>
    /// <returns>One result per source, in source order.</returns>
    public static IReadOnlyList<StartNodeSourceResult> Attribute(
        IReadOnlyList<StartNodeSource> sources,
        IReadOnlyCollection<int> effectiveStartNodeIds,
        IReadOnlyDictionary<int, string> pathsById)
    {
        var userLevelIds = sources
            .Where(s => s.Kind == StartNodeSourceKind.User && s.StartNodeId.HasValue)
            .Select(s => s.StartNodeId!.Value)
            .ToHashSet();

        var paths = WithRootPath(pathsById);

        return sources
            .Select(s => AttributeOne(s, effectiveStartNodeIds, paths, userLevelIds))
            .ToList();
    }

    /// <summary>
    /// Returns a copy of the path map that always contains the content root. The root is not a real
    /// node, so a caller may omit it; a root start node must still be explained rather than reported
    /// as deleted.
    /// </summary>
    /// <param name="pathsById">The caller's paths, keyed by node id; may omit the root.</param>
    /// <returns>A new map that includes the root (<c>-1</c>) as <c>-1</c>, unless the caller supplied one.</returns>
    private static IReadOnlyDictionary<int, string> WithRootPath(IReadOnlyDictionary<int, string> pathsById)
    {
        var paths = new Dictionary<int, string>(pathsById);
        paths.TryAdd(Constants.System.Root, Constants.System.RootString);
        return paths;
    }

    /// <summary>Whether one path is a strict ancestor of another, compared on whole path segments.</summary>
    /// <param name="ancestorPath">The candidate ancestor path.</param>
    /// <param name="descendantPath">The candidate descendant path.</param>
    /// <returns><see langword="true"/> when <paramref name="descendantPath"/> lies under <paramref name="ancestorPath"/>.</returns>
    public static bool IsStrictAncestor(string ancestorPath, string descendantPath) =>
        descendantPath.StartsWith(ancestorPath + ",", StringComparison.Ordinal);

    /// <summary>Whether a path lies in the content recycle bin.</summary>
    /// <param name="path">The materialised path.</param>
    /// <returns><see langword="true"/> when the path passes through the recycle bin.</returns>
    public static bool IsInRecycleBin(string path) =>
        string.Concat(",", path, ",").Contains(RecycleBinSegment, StringComparison.Ordinal);

    /// <summary>Explains one configured start node.</summary>
    /// <param name="source">The configured start node.</param>
    /// <param name="effective">Umbraco's combined start node ids.</param>
    /// <param name="paths">Paths of every referenced node.</param>
    /// <param name="userLevelIds">The user's own start node ids.</param>
    /// <returns>The explained source.</returns>
    private static StartNodeSourceResult AttributeOne(
        StartNodeSource source,
        IReadOnlyCollection<int> effective,
        IReadOnlyDictionary<int, string> paths,
        IReadOnlySet<int> userLevelIds)
    {
        if (source.StartNodeId is not { } id)
        {
            var status = source.Kind == StartNodeSourceKind.UserGroup
                ? StartNodeSourceStatus.NoStartNode
                : StartNodeSourceStatus.NotSet;
            return new StartNodeSourceResult(source, status, null);
        }

        if (!paths.TryGetValue(id, out var path) || IsInRecycleBin(path))
        {
            return new StartNodeSourceResult(source, StartNodeSourceStatus.Ignored, null);
        }

        if (effective.Contains(id))
        {
            return new StartNodeSourceResult(source, StartNodeSourceStatus.InUse, null);
        }

        if (source.Kind == StartNodeSourceKind.UserGroup)
        {
            var replacing = FindEffective(effective, paths, (eid, epath) =>
                userLevelIds.Contains(eid) && (IsStrictAncestor(epath, path) || IsStrictAncestor(path, epath)));
            if (replacing is not null)
            {
                return new StartNodeSourceResult(source, StartNodeSourceStatus.ReplacedByUserStartNode, replacing);
            }

            var covering = FindEffective(effective, paths, (_, epath) => IsStrictAncestor(epath, path));
            if (covering is not null)
            {
                return new StartNodeSourceResult(source, StartNodeSourceStatus.CoveredBy, covering);
            }
        }
        else
        {
            var deeper = FindEffective(effective, paths, (eid, epath) =>
                userLevelIds.Contains(eid) && IsStrictAncestor(path, epath));
            if (deeper is not null)
            {
                return new StartNodeSourceResult(source, StartNodeSourceStatus.NarrowedBy, deeper);
            }
        }

        return new StartNodeSourceResult(source, StartNodeSourceStatus.NotApplied, null);
    }

    /// <summary>Finds the first effective start node matching a predicate.</summary>
    /// <param name="effective">Umbraco's combined start node ids.</param>
    /// <param name="paths">Paths of every referenced node.</param>
    /// <param name="predicate">Receives an effective id and its path.</param>
    /// <returns>The matching id, or <see langword="null"/>.</returns>
    private static int? FindEffective(
        IReadOnlyCollection<int> effective,
        IReadOnlyDictionary<int, string> paths,
        Func<int, string, bool> predicate)
    {
        foreach (var id in effective)
        {
            if (paths.TryGetValue(id, out var path) && predicate(id, path))
            {
                return id;
            }
        }

        return null;
    }
}
