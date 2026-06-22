namespace Umbraco.Community.AdvancedPermissions.AI.Services;

/// <summary>Resolves the root-to-node ancestor key path required by the permission resolver.</summary>
public interface IContentPathResolver
{
    /// <summary>Returns the ordered ancestor keys from the tree root down to and including the node.</summary>
    /// <param name="nodeKey">The content node to resolve the path for.</param>
    /// <returns>Ordered keys (root first, target last, inclusive), or an empty list if the node cannot be found.</returns>
    IReadOnlyList<Guid> GetPathFromRoot(Guid nodeKey);
}
