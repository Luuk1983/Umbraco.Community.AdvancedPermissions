using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Community.AdvancedPermissions.AI.Services;

/// <summary>
/// Default <see cref="IContentPathResolver"/> that derives the root-to-node ancestor key path
/// from Umbraco's materialized content path. Mirrors the proven logic used by the main package's
/// management API controllers so the permission resolver receives an identically-shaped path.
/// </summary>
/// <param name="entityService">The Umbraco entity service used to read materialized paths and map ids to keys.</param>
public sealed class ContentPathResolver(IEntityService entityService) : IContentPathResolver
{
    /// <inheritdoc />
    public IReadOnlyList<Guid> GetPathFromRoot(Guid nodeKey)
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
}
