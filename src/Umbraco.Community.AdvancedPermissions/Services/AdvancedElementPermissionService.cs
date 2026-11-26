using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.AuthorizationStatus;
using Umbraco.Extensions;
using Umbraco.Community.AdvancedPermissions.Abstractions.Constants;
using Umbraco.Community.AdvancedPermissions.Abstractions.Interfaces;

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

    /// <summary>
    /// Filters a set of library elements down to those the user may access with every requested verb.
    /// </summary>
    /// <remarks>
    /// Umbraco 19 added this for batch checks, such as filtering the Library items a reference list shows.
    /// Its default implementation calls <see cref="AuthorizeAccessAsync(IUser, IEnumerable{Guid}, ISet{string})"/>
    /// once per key, which reads paths and start nodes again for every element. This override decides each
    /// element the same way but reads all paths, the start nodes and the shared ancestor ids once. Unknown
    /// elements and elements outside the user's Library start nodes are left out.
    /// </remarks>
    /// <param name="user">The user to authorize.</param>
    /// <param name="elementKeys">The elements to check.</param>
    /// <param name="permissionsToCheck">The verbs that must all be allowed.</param>
    /// <returns>The keys of the elements the user may access.</returns>
    public async Task<ISet<Guid>> FilterAuthorizedAccessAsync(
        IUser user,
        IEnumerable<Guid> elementKeys,
        ISet<string> permissionsToCheck)
    {
        var authorizedKeys = new HashSet<Guid>();
        Guid[] keysArray = elementKeys.Distinct().ToArray();

        if (keysArray.Length == 0)
        {
            return authorizedKeys;
        }

        TreeEntityPath[] entityPaths = entityService
            .GetAllPaths(UmbracoObjectTypes.Element, keysArray)
            .ToArray();

        if (entityPaths.Length == 0)
        {
            return authorizedKeys;
        }

        var startNodeIds = user.CalculateElementStartNodeIds(entityService, appCaches);
        var idToKeyCache = new Dictionary<int, Guid>();

        foreach (TreeEntityPath entityPath in entityPaths)
        {
            if (!ContentPermissions.HasPathAccess(
                    entityPath.Path, startNodeIds, Constants.System.RecycleBinElement))
            {
                continue;
            }

            IReadOnlyList<Guid> pathFromRoot =
                ElementTreePathResolver.BuildPathFromRoot(entityService, entityPath.Path, idToKeyCache);

            var allPerms = await elementPermissionService.ResolveAllAsync(
                user.Key, entityPath.Key, pathFromRoot, permissionsToCheck);

            if (allPerms.Values.All(p => p.IsAllowed))
            {
                authorizedKeys.Add(entityPath.Key);
            }
        }

        return authorizedKeys;
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
    /// <remarks>
    /// Umbraco asks this whenever an element is created, moved, copied or published at the Library root. The
    /// root is not a real node: the user needs start-node access to it, and every requested verb must be
    /// allowed at the package's virtual root, which holds the group defaults. That is the same answer the
    /// Library Access Viewer shows for the root.
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
    /// Recycle bin check. Umbraco asks this to delete an element permanently or empty the bin (Delete), to
    /// restore one (Move) and to look up its original parent (Read).
    /// </summary>
    /// <remarks>
    /// The Library recycle bin is not a real node: the user needs start-node access to it, and, as in
    /// Umbraco's own service, every requested verb must be allowed by the group defaults, which the package
    /// keeps at its virtual root.
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
            permissionsToCheck);

        return resolved.Values.All(p => p.IsAllowed);
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
    /// Filters the user-group default ("fallback") permissions Umbraco sends to the backoffice for the current
    /// user, removing every element verb the package does not allow at the Library root.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The backoffice falls back to these permissions where it has no node to ask about. Since Umbraco
    /// 19.0.0-rc2 the Transfer to Library dialog asks the server for a selected folder, but it still decides
    /// the Library root from these permissions, because the root has no key. Without this filter the dialog
    /// would follow the native group defaults at the root and ignore the package's Library root rules.
    /// </para>
    /// <para>
    /// The Library root is resolved as the package's virtual root, the same way the Library Access Viewer
    /// resolves it. Umbraco's contract only honours removals and asks each service to remove only the verbs it
    /// owns, so only the canonical <c>Umb.Element.*</c> verbs are filtered. Folder verbs
    /// (<c>Umb.ElementContainer.*</c>), document verbs and any other verb pass through unchanged.
    /// </para>
    /// </remarks>
    /// <param name="user">The current user.</param>
    /// <param name="fallbackPermissions">The fallback permissions aggregated from the user's groups.</param>
    /// <returns>The fallback permissions without the element verbs the package denies at the Library root.</returns>
    public async Task<ISet<string>> FilterFallbackPermissionsAsync(IUser user, ISet<string> fallbackPermissions)
    {
        var filtered = new HashSet<string>(fallbackPermissions, StringComparer.Ordinal);
        if (!filtered.Any(IsElementVerb))
        {
            return filtered;
        }

        var resolved = await elementPermissionService.ResolveAllAsync(
            user.Key,
            AdvancedPermissionsConstants.VirtualRootNodeKey,
            [AdvancedPermissionsConstants.VirtualRootNodeKey]);

        filtered.RemoveWhere(verb =>
            IsElementVerb(verb) && !(resolved.TryGetValue(verb, out var permission) && permission.IsAllowed));

        return filtered;
    }

    /// <summary>
    /// Whether a verb is one of the canonical element verbs this service owns.
    /// </summary>
    /// <param name="verb">The verb to check.</param>
    /// <returns><see langword="true"/> for an <c>Umb.Element.*</c> verb the package manages.</returns>
    private static bool IsElementVerb(string verb) =>
        AdvancedPermissionsConstants.ElementVerbs.Contains(verb, StringComparer.Ordinal);

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
    /// canonical verbs are returned as-is. For a folder key the canonical verbs are returned too, plus the
    /// container verbs (<c>Umb.ElementContainer.*</c>) the tree filter expects — e.g.
    /// <c>Umb.ElementContainer.Read</c>, which the filter checks as a folder's browse permission. Exactly one
    /// <see cref="NodePermissions"/> is returned per requested key (an unresolved key returns an empty set).
    /// </para>
    /// <para>
    /// It is also what Umbraco's current-user endpoint (<c>GET /user/current/permissions/element</c>) returns,
    /// which the package's element and folder conditions call per node. For a new, unsaved element those
    /// conditions ask for the parent folder instead, so the folder's canonical verbs (Publish included) are
    /// what the new element will inherit there.
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

                    foreach (var verb in allowedCanonical)
                    {
                        allowedVerbs.Add(verb);
                    }

                    if (containerKeys.Contains(key))
                    {
                        // Folder: also surface the container verbs the tree filter expects, mapped from the
                        // canonical element verbs. Element-only verbs have no container counterpart.
                        foreach (var (containerVerb, canonical) in AdvancedPermissionsConstants.ElementContainerVerbToCanonical)
                        {
                            if (allowedCanonical.Contains(canonical))
                            {
                                allowedVerbs.Add(containerVerb);
                            }
                        }
                    }
                }
            }

            result.Add(new NodePermissions { NodeKey = key, Permissions = allowedVerbs });
        }

        return result;
    }
}
