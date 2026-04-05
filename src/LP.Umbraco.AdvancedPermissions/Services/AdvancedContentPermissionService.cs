using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.AuthorizationStatus;
using Umbraco.Extensions;
using LP.Umbraco.AdvancedPermissions.Core.Interfaces;

namespace LP.Umbraco.AdvancedPermissions.Services;

/// <summary>
/// Replaces Umbraco's built-in <see cref="IContentPermissionService"/> with the Advanced Security
/// permission model that supports explicit Allow/Deny states, per-verb scope, and tree inheritance.
/// </summary>
/// <remarks>
/// <para>
/// The Advanced Security system is always the single authority for content permission decisions.
/// Umbraco group default permissions are respected via the <see cref="IAdvancedPermissionService"/>
/// resolution pipeline (they are treated as virtual root entries with <c>ThisNodeAndDescendants</c> scope).
/// </para>
/// <para>
/// Culture access is not part of the advanced permission model and is handled directly
/// using the standard Umbraco language group settings.
/// </para>
/// </remarks>
/// <param name="advancedPermissionService">The advanced permission service that resolves effective permissions.</param>
/// <param name="entityService">The Umbraco entity service used to get content paths and start nodes.</param>
/// <param name="appCaches">The Umbraco application caches, used for start node calculations.</param>
/// <param name="languageService">The language service used for culture access checks.</param>
public sealed class AdvancedContentPermissionService(
    IAdvancedPermissionService advancedPermissionService,
    IEntityService entityService,
    AppCaches appCaches,
    ILanguageService languageService)
    : IContentPermissionService
{
    /// <inheritdoc />
    public async Task<ContentAuthorizationStatus> AuthorizeAccessAsync(
        IUser user,
        IEnumerable<Guid> contentKeys,
        ISet<string> permissionsToCheck)
    {
        Guid[] keysArray = contentKeys.ToArray();

        if (keysArray.Length == 0)
        {
            return ContentAuthorizationStatus.Success;
        }

        TreeEntityPath[] entityPaths = entityService
            .GetAllPaths(UmbracoObjectTypes.Document, keysArray)
            .ToArray();

        if (entityPaths.Length == 0)
        {
            return ContentAuthorizationStatus.NotFound;
        }

        // Check that each content item falls within the user's start node subtree
        int[]? startNodeIds = user.CalculateContentStartNodeIds(entityService, appCaches);
        foreach (TreeEntityPath entityPath in entityPaths)
        {
            if (!ContentPermissions.HasPathAccess(
                    entityPath.Path, startNodeIds, Constants.System.RecycleBinContent))
            {
                return ContentAuthorizationStatus.UnauthorizedMissingPathAccess;
            }
        }

        // Resolve advanced security permissions for each content item — all verbs in one call.
        // Share a single ID-to-Key cache across all path lookups to avoid redundant DB calls.
        var idToKeyCache = new Dictionary<int, Guid>();
        foreach (TreeEntityPath entityPath in entityPaths)
        {
            IReadOnlyList<Guid> pathFromRoot = BuildPathFromRoot(entityPath.Path, idToKeyCache);

            var allPerms = await advancedPermissionService.ResolveAllAsync(
                user.Key, entityPath.Key, pathFromRoot, permissionsToCheck);

            if (allPerms.Values.Any(p => !p.IsAllowed))
            {
                return ContentAuthorizationStatus.UnauthorizedMissingPermissionAccess;
            }
        }

        return ContentAuthorizationStatus.Success;
    }

    /// <inheritdoc />
    public async Task<ContentAuthorizationStatus> AuthorizeDescendantsAccessAsync(
        IUser user,
        Guid parentKey,
        ISet<string> permissionsToCheck)
    {
        var denied = new List<IEntitySlim>();
        var page = 0;
        const int pageSize = 500;
        var total = long.MaxValue;

        IEntitySlim? parentEntity = entityService.Get(parentKey, UmbracoObjectTypes.Document);

        if (parentEntity is null)
        {
            return ContentAuthorizationStatus.NotFound;
        }

        // Share a single ID-to-Key cache across all descendant path lookups
        var idToKeyCache = new Dictionary<int, Guid>();

        while (page * pageSize < total)
        {
            // Order descendants from shallowest to deepest to allow early exit
            IEnumerable<IEntitySlim> descendants = entityService.GetPagedDescendants(
                parentEntity.Id,
                UmbracoObjectTypes.Document,
                page++,
                pageSize,
                out total,
                ordering: Ordering.By("path"));

            foreach (IEntitySlim descendant in descendants)
            {
                bool hasPathAccess = user.HasContentPathAccess(descendant, entityService, appCaches);

                if (!hasPathAccess || denied.Any(d => descendant.Path.StartsWith($"{d.Path},")))
                {
                    denied.Add(descendant);
                    continue;
                }

                IReadOnlyList<Guid> pathFromRoot = BuildPathFromRoot(descendant.Path, idToKeyCache);

                var allPerms = await advancedPermissionService.ResolveAllAsync(
                    user.Key, descendant.Key, pathFromRoot, permissionsToCheck);

                if (allPerms.Values.Any(p => !p.IsAllowed))
                {
                    denied.Add(descendant);
                }
            }
        }

        return denied.Count == 0
            ? ContentAuthorizationStatus.Success
            : ContentAuthorizationStatus.UnauthorizedMissingDescendantAccess;
    }

    /// <inheritdoc />
    public Task<ContentAuthorizationStatus> AuthorizeRootAccessAsync(IUser user, ISet<string> permissionsToCheck)
    {
        // The content root (-1) is a virtual node, not a real content item.
        // We use HasPathAccess against the root path to check start node access.
        int[]? startNodeIds = user.CalculateContentStartNodeIds(entityService, appCaches);
        var hasAccess = ContentPermissions.HasPathAccess(
            Constants.System.RootString, startNodeIds, Constants.System.RecycleBinContent);

        return Task.FromResult(hasAccess
            ? ContentAuthorizationStatus.Success
            : ContentAuthorizationStatus.UnauthorizedMissingRootAccess);
    }

    /// <inheritdoc />
    public Task<ContentAuthorizationStatus> AuthorizeBinAccessAsync(IUser user, ISet<string> permissionsToCheck)
    {
        // The recycle bin (-20) is a virtual node, not a real content item.
        int[]? startNodeIds = user.CalculateContentStartNodeIds(entityService, appCaches);
        var hasAccess = ContentPermissions.HasPathAccess(
            Constants.System.RecycleBinContentString, startNodeIds, Constants.System.RecycleBinContent);

        return Task.FromResult(hasAccess
            ? ContentAuthorizationStatus.Success
            : ContentAuthorizationStatus.UnauthorizedMissingBinAccess);
    }

    /// <inheritdoc />
    public async Task<ContentAuthorizationStatus> AuthorizeCultureAccessAsync(
        IUser user,
        ISet<string> culturesToCheck)
    {
        // Culture access is not part of the advanced permission model.
        // We delegate to the standard Umbraco group language settings.
        if (user.Groups.Any(group => group.HasAccessToAllLanguages))
        {
            return ContentAuthorizationStatus.Success;
        }

        var allowedLanguages = user.Groups.SelectMany(g => g.AllowedLanguages).Distinct().ToArray();
        var allowedLanguageIsoCodes = await languageService.GetIsoCodesByIdsAsync(allowedLanguages);

        return culturesToCheck.All(culture => allowedLanguageIsoCodes.InvariantContains(culture))
            ? ContentAuthorizationStatus.Success
            : ContentAuthorizationStatus.UnauthorizedMissingCulture;
    }

    /// <inheritdoc />
    public async Task<ISet<Guid>> FilterAuthorizedAccessAsync(
        IUser user,
        IEnumerable<Guid> contentKeys,
        ISet<string> permissionsToCheck)
    {
        Guid[] keysArray = contentKeys.ToArray();
        var authorizedKeys = new HashSet<Guid>();

        if (keysArray.Length == 0)
        {
            return authorizedKeys;
        }

        TreeEntityPath[] entityPaths = entityService
            .GetAllPaths(UmbracoObjectTypes.Document, keysArray)
            .ToArray();

        if (entityPaths.Length == 0)
        {
            return authorizedKeys;
        }

        int[]? startNodeIds = user.CalculateContentStartNodeIds(entityService, appCaches);
        var idToKeyCache = new Dictionary<int, Guid>();

        foreach (TreeEntityPath entityPath in entityPaths)
        {
            if (!ContentPermissions.HasPathAccess(
                    entityPath.Path, startNodeIds, Constants.System.RecycleBinContent))
            {
                continue;
            }

            IReadOnlyList<Guid> pathFromRoot = BuildPathFromRoot(entityPath.Path, idToKeyCache);

            var allPerms = await advancedPermissionService.ResolveAllAsync(
                user.Key, entityPath.Key, pathFromRoot, permissionsToCheck);

            if (allPerms.Values.All(p => p.IsAllowed))
            {
                authorizedKeys.Add(entityPath.Key);
            }
        }

        return authorizedKeys;
    }

    /// <summary>
    /// Parses an Umbraco content path string (e.g. <c>"-1,1001,1002,1003"</c>) and returns the ordered
    /// list of content node Guids from the root down to the target node.
    /// Maintains an in-memory cache of ID-to-Key mappings to reduce database calls when resolving
    /// multiple paths within a single authorization operation.
    /// </summary>
    /// <remarks>
    /// The root node (<c>-1</c>) is excluded from the result because it is a virtual node.
    /// Group defaults cover the "virtual root" layer via the resolution algorithm.
    /// </remarks>
    /// <param name="path">The Umbraco path string, e.g. <c>"-1,1001,1002"</c>.</param>
    /// <param name="idToKeyCache">
    /// Optional dictionary that accumulates ID-to-Key mappings across multiple calls.
    /// Prevents repeated database lookups for ancestors shared by sibling content nodes.
    /// </param>
    /// <returns>An ordered list of Guids for the real content nodes in the path.</returns>
    private IReadOnlyList<Guid> BuildPathFromRoot(string path, Dictionary<int, Guid>? idToKeyCache = null)
    {
        int[] pathIds = path.Split(',')
            .Select(int.Parse)
            .Where(id => id > 0)
            .ToArray();

        if (pathIds.Length == 0)
        {
            return [];
        }

        // Find IDs that are not yet cached
        int[] uncachedIds = idToKeyCache is null
            ? pathIds
            : pathIds.Where(id => !idToKeyCache.ContainsKey(id)).ToArray();

        if (uncachedIds.Length > 0)
        {
            var fetched = entityService
                .GetAll(UmbracoObjectTypes.Document, uncachedIds)
                .ToDictionary(e => e.Id, e => e.Key);

            if (idToKeyCache is not null)
            {
                foreach (var kvp in fetched)
                {
                    idToKeyCache[kvp.Key] = kvp.Value;
                }
            }
            else
            {
                idToKeyCache = fetched;
            }
        }

        idToKeyCache ??= [];

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
