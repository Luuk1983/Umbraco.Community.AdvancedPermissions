using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.AuthorizationStatus;
using Umbraco.Community.AdvancedPermissions.Abstractions.Constants;
using Umbraco.Community.AdvancedPermissions.Abstractions.Interfaces;

namespace Umbraco.Community.AdvancedPermissions.Services;

/// <summary>
/// Replaces Umbraco's built-in <see cref="IElementContainerPermissionService"/> with the Advanced
/// Security model for library folders (element containers).
/// </summary>
/// <remarks>
/// <para>
/// Folder permissions are stored against the canonical <c>Umb.Element.*</c> verbs alongside element
/// permissions in one table. This adapter translates incoming container verbs
/// (<c>Umb.ElementContainer.*</c>) to their canonical equivalents
/// (<see cref="AdvancedPermissionsConstants.ElementContainerVerbToCanonical"/>) before resolving, so a
/// single stored entry on a folder governs both the folder itself and the elements beneath it.
/// </para>
/// <para>
/// Element-tree start-node access is preserved exactly as for elements. Resolution is delegated to the
/// shared <see cref="IElementNodePermissionService"/>.
/// </para>
/// </remarks>
/// <param name="elementPermissionService">The element advanced permission service that resolves effective permissions.</param>
/// <param name="entityService">The Umbraco entity service used to get folder paths and start nodes.</param>
/// <param name="appCaches">The Umbraco application caches, used for start node calculations.</param>
public sealed class AdvancedElementContainerPermissionService(
    IElementNodePermissionService elementPermissionService,
    IEntityService entityService,
    AppCaches appCaches)
    : IElementContainerPermissionService
{
    /// <inheritdoc />
    public async Task<ElementAuthorizationStatus> AuthorizeAccessAsync(
        IUser user,
        IEnumerable<Guid> containerKeys,
        ISet<string> permissionsToCheck)
    {
        Guid[] keysArray = containerKeys.ToArray();

        if (keysArray.Length == 0)
        {
            return ElementAuthorizationStatus.NotFound;
        }

        TreeEntityPath[] entityPaths = entityService
            .GetAllPaths(UmbracoObjectTypes.ElementContainer, keysArray)
            .ToArray();

        if (entityPaths.Length == 0)
        {
            return ElementAuthorizationStatus.NotFound;
        }

        var startNodeIds = user.CalculateElementStartNodeIds(entityService, appCaches);
        foreach (TreeEntityPath entityPath in entityPaths)
        {
            if (!ContentPermissions.HasPathAccess(
                    entityPath.Path, startNodeIds, Constants.System.RecycleBinElement))
            {
                return ElementAuthorizationStatus.UnauthorizedMissingPathAccess;
            }
        }

        var canonicalVerbs = ToCanonicalVerbs(permissionsToCheck);

        var idToKeyCache = new Dictionary<int, Guid>();
        foreach (TreeEntityPath entityPath in entityPaths)
        {
            IReadOnlyList<Guid> pathFromRoot =
                ElementTreePathResolver.BuildPathFromRoot(entityService, entityPath.Path, idToKeyCache);

            var allPerms = await elementPermissionService.ResolveAllAsync(
                user.Key, entityPath.Key, pathFromRoot, canonicalVerbs);

            if (allPerms.Values.Any(p => !p.IsAllowed))
            {
                return ElementAuthorizationStatus.UnauthorizedMissingPermissionAccess;
            }
        }

        return ElementAuthorizationStatus.Success;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Umbraco asks this whenever a folder is created or moved at the Library root. The root is not a real
    /// node: the user needs start-node access to it, and every requested verb, mapped to its canonical
    /// element verb, must be allowed at the package's virtual root, which holds the group defaults.
    /// </remarks>
    public async Task<ElementAuthorizationStatus> AuthorizeRootAccessAsync(IUser user, ISet<string> permissionsToCheck)
    {
        var startNodeIds = user.CalculateElementStartNodeIds(entityService, appCaches);
        if (!ContentPermissions.HasPathAccess(
                Constants.System.RootString, startNodeIds, Constants.System.RecycleBinElement))
        {
            return ElementAuthorizationStatus.UnauthorizedMissingRootAccess;
        }

        return await AreAllowedByGroupDefaultsAsync(user, permissionsToCheck)
            ? ElementAuthorizationStatus.Success
            : ElementAuthorizationStatus.UnauthorizedMissingPermissionAccess;
    }

    /// <summary>
    /// Recycle bin check. Umbraco asks this to delete a folder permanently (Delete), to restore one (Move) and
    /// to look up its original parent (Read).
    /// </summary>
    /// <remarks>
    /// The Library recycle bin is not a real node: the user needs start-node access to it, and, as in
    /// Umbraco's own service, every requested verb, mapped to its canonical element verb, must be allowed by
    /// the group defaults, which the package keeps at its virtual root.
    /// </remarks>
    /// <param name="user">The user to authorize.</param>
    /// <param name="permissionsToCheck">The verbs that must all be allowed.</param>
    /// <returns>The authorization status.</returns>
    public async Task<ElementAuthorizationStatus> AuthorizeBinAccessAsync(IUser user, ISet<string> permissionsToCheck)
    {
        var startNodeIds = user.CalculateElementStartNodeIds(entityService, appCaches);
        if (!ContentPermissions.HasPathAccess(
                Constants.System.RecycleBinElementString, startNodeIds, Constants.System.RecycleBinElement))
        {
            return ElementAuthorizationStatus.UnauthorizedMissingBinAccess;
        }

        return await AreAllowedByGroupDefaultsAsync(user, permissionsToCheck)
            ? ElementAuthorizationStatus.Success
            : ElementAuthorizationStatus.UnauthorizedMissingPermissionAccess;
    }

    /// <summary>
    /// Whether every verb is allowed by the user's group defaults, which the package keeps at its virtual
    /// root. The root and the recycle bin are not nodes in the tree, so their checks are decided here.
    /// </summary>
    /// <param name="user">The user to resolve for.</param>
    /// <param name="permissionsToCheck">The verbs that must all be allowed; none means allowed.</param>
    /// <returns><see langword="true"/> when every verb is allowed.</returns>
    private async Task<bool> AreAllowedByGroupDefaultsAsync(IUser user, ISet<string> permissionsToCheck)
    {
        if (permissionsToCheck.Count == 0)
        {
            return true;
        }

        var resolved = await elementPermissionService.ResolveAllAsync(
            user.Key,
            AdvancedPermissionsConstants.VirtualRootNodeKey,
            [AdvancedPermissionsConstants.VirtualRootNodeKey],
            ToCanonicalVerbs(permissionsToCheck));

        return resolved.Values.All(p => p.IsAllowed);
    }

    /// <summary>
    /// Maps the supplied container verbs to the canonical element verbs the package stores, passing
    /// through any verb that has no container mapping (defensive — should not occur for folder checks).
    /// </summary>
    /// <param name="permissionsToCheck">The incoming container verbs (<c>Umb.ElementContainer.*</c>).</param>
    /// <returns>The canonical <c>Umb.Element.*</c> verbs to resolve.</returns>
    private static HashSet<string> ToCanonicalVerbs(ISet<string> permissionsToCheck)
    {
        var canonical = new HashSet<string>(StringComparer.Ordinal);
        foreach (var verb in permissionsToCheck)
        {
            canonical.Add(
                AdvancedPermissionsConstants.ElementContainerVerbToCanonical.TryGetValue(verb, out var mapped)
                    ? mapped
                    : verb);
        }

        return canonical;
    }
}
