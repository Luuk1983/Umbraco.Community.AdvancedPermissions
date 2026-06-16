using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.AuthorizationStatus;
using Umbraco.Extensions;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;

namespace Umbraco.Community.AdvancedPermissions.Services;

/// <summary>
/// Replaces Umbraco's built-in <see cref="IElementPermissionService"/> with the Advanced Security model
/// for library elements — explicit Allow/Deny states, per-verb scope, and tree inheritance through
/// element folders.
/// </summary>
/// <remarks>
/// <para>
/// The element analogue of <see cref="AdvancedContentPermissionService"/>: element-tree start-node
/// access is preserved (via <c>CalculateElementStartNodeIds</c> and the element recycle bin), while
/// per-verb permission decisions are resolved by <see cref="IElementNodePermissionService"/>. Element
/// folders contribute to the inheritance tree; their own container verbs are governed by
/// <see cref="AdvancedElementContainerPermissionService"/>.
/// </para>
/// <para>
/// Culture access is not part of the advanced permission model and is handled with the standard
/// Umbraco language group settings.
/// </para>
/// </remarks>
/// <param name="elementPermissionService">The element advanced permission service that resolves effective permissions.</param>
/// <param name="entityService">The Umbraco entity service used to get element paths and start nodes.</param>
/// <param name="appCaches">The Umbraco application caches, used for start node calculations.</param>
/// <param name="languageService">The language service used for culture access checks.</param>
public sealed class AdvancedElementPermissionService(
    IElementNodePermissionService elementPermissionService,
    IEntityService entityService,
    AppCaches appCaches,
    ILanguageService languageService)
    : IElementPermissionService
{
    /// <inheritdoc />
    public async Task<ElementAuthorizationStatus> AuthorizeAccessAsync(
        IUser user,
        IEnumerable<Guid> elementKeys,
        ISet<string> permissionsToCheck)
    {
        Guid[] keysArray = elementKeys.ToArray();

        if (keysArray.Length == 0)
        {
            return ElementAuthorizationStatus.NotFound;
        }

        TreeEntityPath[] entityPaths = entityService
            .GetAllPaths(UmbracoObjectTypes.Element, keysArray)
            .ToArray();

        if (entityPaths.Length == 0)
        {
            return ElementAuthorizationStatus.NotFound;
        }

        // Element-tree start-node access is orthogonal to advanced permissions and still applies.
        var startNodeIds = user.CalculateElementStartNodeIds(entityService, appCaches);
        foreach (TreeEntityPath entityPath in entityPaths)
        {
            if (!ContentPermissions.HasPathAccess(
                    entityPath.Path, startNodeIds, Constants.System.RecycleBinElement))
            {
                return ElementAuthorizationStatus.UnauthorizedMissingPathAccess;
            }
        }

        // Resolve advanced security permissions for each element — share one id-to-key cache.
        var idToKeyCache = new Dictionary<int, Guid>();
        foreach (TreeEntityPath entityPath in entityPaths)
        {
            IReadOnlyList<Guid> pathFromRoot =
                ElementTreePathResolver.BuildPathFromRoot(entityService, entityPath.Path, idToKeyCache);

            var allPerms = await elementPermissionService.ResolveAllAsync(
                user.Key, entityPath.Key, pathFromRoot, permissionsToCheck);

            if (allPerms.Values.Any(p => !p.IsAllowed))
            {
                return ElementAuthorizationStatus.UnauthorizedMissingPermissionAccess;
            }
        }

        return ElementAuthorizationStatus.Success;
    }

    /// <inheritdoc />
    public async Task<ElementAuthorizationStatus> AuthorizeDescendantsAccessAsync(
        IUser user,
        Guid parentKey,
        ISet<string> permissionsToCheck)
    {
        IEntitySlim? parentEntity =
            entityService.Get(parentKey, UmbracoObjectTypes.ElementContainer)
            ?? entityService.Get(parentKey, UmbracoObjectTypes.Element);

        if (parentEntity is null)
        {
            return ElementAuthorizationStatus.NotFound;
        }

        UmbracoObjectTypes parentObjectType = ObjectTypes.GetUmbracoObjectType(parentEntity.NodeObjectType);

        var denied = new List<IEntitySlim>();
        var page = 0;
        const int pageSize = 500;
        var total = long.MaxValue;
        var idToKeyCache = new Dictionary<int, Guid>();

        while (page * pageSize < total)
        {
            // Shallowest-to-deepest ordering lets a denied ancestor short-circuit its whole subtree.
            IEnumerable<IEntitySlim> descendants = entityService.GetPagedDescendants(
                parentKey,
                parentObjectType,
                [UmbracoObjectTypes.Element],
                page++,
                pageSize,
                out total,
                ordering: Ordering.By("path"));

            foreach (IEntitySlim descendant in descendants)
            {
                var hasPathAccess = user.HasElementPathAccess(descendant, entityService, appCaches);
                if (!hasPathAccess || denied.Any(d => descendant.Path.StartsWith($"{d.Path},")))
                {
                    denied.Add(descendant);
                    continue;
                }

                IReadOnlyList<Guid> pathFromRoot =
                    ElementTreePathResolver.BuildPathFromRoot(entityService, descendant.Path, idToKeyCache);

                var allPerms = await elementPermissionService.ResolveAllAsync(
                    user.Key, descendant.Key, pathFromRoot, permissionsToCheck);

                if (allPerms.Values.Any(p => !p.IsAllowed))
                {
                    denied.Add(descendant);
                }
            }
        }

        return denied.Count == 0
            ? ElementAuthorizationStatus.Success
            : ElementAuthorizationStatus.UnauthorizedMissingDescendantAccess;
    }

    /// <inheritdoc />
    public Task<ElementAuthorizationStatus> AuthorizeRootAccessAsync(IUser user, ISet<string> permissionsToCheck)
    {
        // The element root is a virtual node; access is governed by element start-node configuration.
        var startNodeIds = user.CalculateElementStartNodeIds(entityService, appCaches);
        var hasAccess = ContentPermissions.HasPathAccess(
            Constants.System.RootString, startNodeIds, Constants.System.RecycleBinElement);

        return Task.FromResult(hasAccess
            ? ElementAuthorizationStatus.Success
            : ElementAuthorizationStatus.UnauthorizedMissingRootAccess);
    }

    /// <inheritdoc />
    public Task<ElementAuthorizationStatus> AuthorizeBinAccessAsync(IUser user, ISet<string> permissionsToCheck)
    {
        // The element recycle bin is a virtual node; access is governed by element start-node configuration.
        var startNodeIds = user.CalculateElementStartNodeIds(entityService, appCaches);
        var hasAccess = ContentPermissions.HasPathAccess(
            Constants.System.RecycleBinElementString, startNodeIds, Constants.System.RecycleBinElement);

        return Task.FromResult(hasAccess
            ? ElementAuthorizationStatus.Success
            : ElementAuthorizationStatus.UnauthorizedMissingBinAccess);
    }

    /// <inheritdoc />
    public async Task<ElementAuthorizationStatus> AuthorizeCultureAccessAsync(IUser user, ISet<string> culturesToCheck)
    {
        // Culture access is not part of the advanced permission model; delegate to group language settings.
        if (user.Groups.Any(group => group.HasAccessToAllLanguages))
        {
            return ElementAuthorizationStatus.Success;
        }

        var allowedLanguages = user.Groups.SelectMany(g => g.AllowedLanguages).Distinct().ToArray();
        var allowedLanguageIsoCodes = await languageService.GetIsoCodesByIdsAsync(allowedLanguages);

        return culturesToCheck.All(culture => allowedLanguageIsoCodes.InvariantContains(culture))
            ? ElementAuthorizationStatus.Success
            : ElementAuthorizationStatus.UnauthorizedMissingCulture;
    }

    /// <summary>
    /// Gets the effective permissions for a user on the specified library elements, resolved through the
    /// Advanced Security model so the returned set reflects inheritance, scope and priority overrides.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the seam the backoffice Library tree uses to decide which items a user may browse:
    /// <c>ElementPermissionFilterService</c> calls it for every tree entity (elements <em>and</em>
    /// folders) and keeps those whose browse verb is present. The requested keys are therefore a mix of
    /// elements and element containers.
    /// </para>
    /// <para>
    /// Permissions are stored against the canonical <c>Umb.Element.*</c> verbs. For an element key the
    /// canonical verbs are returned as-is; for a folder key they are mapped back to the container verbs
    /// (<c>Umb.ElementContainer.*</c>) the tree filter expects — e.g. <c>Umb.ElementContainer.Read</c>,
    /// which the filter checks as a folder's browse permission. Exactly one <see cref="NodePermissions"/>
    /// is returned per requested key (an unresolved key returns an empty set).
    /// </para>
    /// </remarks>
    /// <param name="user">The user to resolve permissions for.</param>
    /// <param name="elementKeys">The identifiers of the elements and/or folders to resolve permissions for.</param>
    /// <returns>The effective (allowed) permission verbs for each requested element or folder.</returns>
    public async Task<IEnumerable<NodePermissions>> GetPermissionsAsync(IUser user, IEnumerable<Guid> elementKeys)
    {
        Guid[] keysArray = elementKeys.ToArray();
        var result = new List<NodePermissions>(keysArray.Length);

        if (keysArray.Length == 0)
        {
            return result;
        }

        // Resolve paths and object types across both elements and folders (the multi-object-type
        // overloads support containers, which the single-type ones reject).
        UmbracoObjectTypes[] objectTypes = [UmbracoObjectTypes.Element, UmbracoObjectTypes.ElementContainer];
        var pathsByKey = entityService
            .GetAllPaths(objectTypes, keysArray)
            .ToDictionary(p => p.Key, p => p.Path);
        var containerKeys = entityService
            .GetAll(objectTypes, keysArray)
            .Where(e => e.NodeObjectType == Constants.ObjectTypes.ElementContainer)
            .Select(e => e.Key)
            .ToHashSet();

        var idToKeyCache = new Dictionary<int, Guid>();

        foreach (Guid key in keysArray)
        {
            var allowedVerbs = new HashSet<string>(StringComparer.Ordinal);

            if (pathsByKey.TryGetValue(key, out var path))
            {
                IReadOnlyList<Guid> pathFromRoot =
                    ElementTreePathResolver.BuildPathFromRoot(entityService, path, idToKeyCache);

                if (pathFromRoot.Count > 0)
                {
                    var resolved = await elementPermissionService.ResolveAllAsync(user.Key, key, pathFromRoot);
                    var allowedCanonical = resolved.Values
                        .Where(p => p.IsAllowed)
                        .Select(p => p.Verb)
                        .ToHashSet(StringComparer.Ordinal);

                    if (containerKeys.Contains(key))
                    {
                        // Folder: surface the container verbs the tree filter expects, mapped from the
                        // canonical element verbs. Element-only verbs have no container counterpart.
                        foreach (var (containerVerb, canonical) in AdvancedPermissionsConstants.ElementContainerVerbToCanonical)
                        {
                            if (allowedCanonical.Contains(canonical))
                            {
                                allowedVerbs.Add(containerVerb);
                            }
                        }
                    }
                    else
                    {
                        foreach (var verb in allowedCanonical)
                        {
                            allowedVerbs.Add(verb);
                        }
                    }
                }
            }

            result.Add(new NodePermissions { NodeKey = key, Permissions = allowedVerbs });
        }

        return result;
    }
}
