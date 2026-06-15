using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Community.AdvancedPermissions.Services;

/// <summary>
/// Helper for translating an Umbraco element-tree path string (e.g. <c>"-1,1001,1002"</c>) into the
/// ordered list of node <see cref="Guid"/> keys from root down to the target, shared by the element
/// and element-container enforcement adapters.
/// </summary>
/// <remarks>
/// The element tree mixes elements (<see cref="UmbracoObjectTypes.Element"/>) and folders
/// (<see cref="UmbracoObjectTypes.ElementContainer"/>), so a single path can contain ids of both
/// object types. Id-to-key resolution therefore queries both. The root node (<c>-1</c>) is excluded;
/// virtual-root defaults are applied by the resolver, not the path.
/// </remarks>
internal static class ElementTreePathResolver
{
    /// <summary>
    /// Resolves the ordered list of node keys from root to (and including) the given element or folder.
    /// Returns an empty list when the node cannot be found in the element tree.
    /// </summary>
    /// <param name="entityService">The Umbraco entity service.</param>
    /// <param name="nodeKey">The key of the target element or folder.</param>
    /// <returns>An ordered list of node keys from root to the target node (inclusive), or empty.</returns>
    public static IReadOnlyList<Guid> BuildPathFromRoot(IEntityService entityService, Guid nodeKey)
    {
        // The key may be an element or a folder; try both object types.
        var pathEntry = entityService.GetAllPaths(UmbracoObjectTypes.Element, nodeKey).FirstOrDefault()
            ?? entityService.GetAllPaths(UmbracoObjectTypes.ElementContainer, nodeKey).FirstOrDefault();

        return pathEntry is null
            ? []
            : BuildPathFromRoot(entityService, pathEntry.Path);
    }

    /// <summary>
    /// Parses an element-tree path string and returns the ordered list of node keys from root to target.
    /// </summary>
    /// <param name="entityService">The Umbraco entity service used to map ids to keys.</param>
    /// <param name="path">The Umbraco path string, e.g. <c>"-1,1001,1002"</c>.</param>
    /// <param name="idToKeyCache">
    /// Optional dictionary that accumulates id-to-key mappings across calls, avoiding repeated lookups
    /// for ancestors shared by sibling nodes within a single authorization operation.
    /// </param>
    /// <returns>An ordered list of node keys for the real element-tree nodes in the path.</returns>
    public static IReadOnlyList<Guid> BuildPathFromRoot(
        IEntityService entityService,
        string path,
        Dictionary<int, Guid>? idToKeyCache = null)
    {
        var pathIds = path.Split(',')
            .Select(s => int.TryParse(s, out var id) ? id : 0)
            .Where(id => id > 0)
            .ToArray();

        if (pathIds.Length == 0)
        {
            return [];
        }

        idToKeyCache ??= [];

        var uncachedIds = pathIds.Where(id => !idToKeyCache.ContainsKey(id)).ToArray();
        if (uncachedIds.Length > 0)
        {
            // Elements and folders share the tree, so resolve both object types in one call. The
            // multi-object-type overload queries by object-type GUID directly; the single-type
            // GetAll(UmbracoObjectTypes.ElementContainer, ids) overload throws "not supported here"
            // because element containers have no CLR entity type mapping.
            foreach (var entity in entityService.GetAll(
                new[] { UmbracoObjectTypes.Element, UmbracoObjectTypes.ElementContainer },
                uncachedIds))
            {
                idToKeyCache[entity.Id] = entity.Key;
            }
        }

        var result = new List<Guid>(pathIds.Length);
        foreach (var id in pathIds)
        {
            if (idToKeyCache.TryGetValue(id, out var key))
            {
                result.Add(key);
            }
        }

        return result;
    }
}
