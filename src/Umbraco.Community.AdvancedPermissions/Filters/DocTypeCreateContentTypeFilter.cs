using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.Filters;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;

namespace Umbraco.Community.AdvancedPermissions.Filters;

/// <summary>
/// Filters Umbraco's "allowed content types" lists based on the current user's doc-type
/// permissions. Registered via <c>builder.ContentTypeFilters().Append&lt;...&gt;()</c> in the
/// composer; Umbraco invokes each registered filter in sequence before showing the Create menu.
/// </summary>
/// <param name="docTypePermissionService">The doc-type permission service.</param>
/// <param name="backOfficeSecurityAccessor">Accesses the currently authenticated backoffice user.</param>
/// <param name="entityService">Used to resolve the parent content key into a content path.</param>
/// <param name="logger">Logger used for diagnostic messages when filtering fails open.</param>
public sealed class DocTypeCreateContentTypeFilter(
    IDocTypePermissionService docTypePermissionService,
    IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
    IEntityService entityService,
    ILogger<DocTypeCreateContentTypeFilter> logger)
    : IContentTypeFilter
{
    /// <inheritdoc />
    public Task<IEnumerable<TItem>> FilterAllowedAtRootAsync<TItem>(IEnumerable<TItem> contentTypes)
        where TItem : IContentTypeComposition
    {
        // Root-level create has no parent content key — use the virtual root + an empty path.
        // Entries with scope ThisNodeAndDescendants on the virtual root still apply.
        var pathFromRoot = new List<Guid> { AdvancedPermissionsConstants.VirtualRootNodeKey };
        return FilterAsync(contentTypes, pathFromRoot, ct => ct.Key);
    }

    /// <inheritdoc />
    public Task<IEnumerable<TItem>> FilterAllowedInLibraryAsync<TItem>(IEnumerable<TItem> contentTypes)
        where TItem : IContentTypeComposition
    {
        // Library element-type create-filtering is section-global (Umbraco supplies no parent context),
        // so entries live on the virtual root. The candidates here are element types; resolve against the
        // element-type verb so they are governed independently of document create-filtering in the shared store.
        var pathFromRoot = new List<Guid> { AdvancedPermissionsConstants.VirtualRootNodeKey };
        return FilterAsync(contentTypes, pathFromRoot, ct => ct.Key, AdvancedPermissionsConstants.VerbElementCreateOfType);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ContentTypeSort>> FilterAllowedChildrenAsync(
        IEnumerable<ContentTypeSort> contentTypes,
        Guid parentContentTypeKey,
        Guid? parentContentKey)
    {
        var pathFromRoot = parentContentKey.HasValue
            ? BuildPathFromRoot(parentContentKey.Value)
            : [AdvancedPermissionsConstants.VirtualRootNodeKey];

        return await FilterAsync(contentTypes, pathFromRoot, ct => ct.Key);
    }

    /// <summary>
    /// Shared filtering logic: for each candidate, ask the resolver whether the current user may
    /// create that doc-type under the supplied path. Drops candidates the resolver denies.
    /// </summary>
    /// <typeparam name="TItem">The candidate type (either <see cref="IContentTypeComposition"/> or <see cref="ContentTypeSort"/>).</typeparam>
    /// <param name="contentTypes">Candidates to filter.</param>
    /// <param name="pathFromRoot">The parent's path from root (or just virtual root for root-level creates).</param>
    /// <param name="keySelector">How to extract the candidate's content-type key.</param>
    /// <param name="verb">
    /// The create-of-type verb to resolve against — the document verb for content create-filtering, or
    /// the element-type verb for library create-filtering.
    /// </param>
    /// <returns>The filtered candidates.</returns>
    private async Task<IEnumerable<TItem>> FilterAsync<TItem>(
        IEnumerable<TItem> contentTypes,
        IReadOnlyList<Guid> pathFromRoot,
        Func<TItem, Guid> keySelector,
        string verb = AdvancedPermissionsConstants.VerbCreateOfType)
    {
        var roleAliases = GetCurrentUserRoleAliases();
        if (roleAliases is null)
        {
            // No backoffice user in context — leave the list untouched. The filter is best-effort;
            // if the API was reached without a user, the caller's own authorization will handle it.
            return contentTypes;
        }

        var result = new List<TItem>();

        foreach (var candidate in contentTypes)
        {
            try
            {
                var effective = await docTypePermissionService.ResolveCreateForRolesAsync(
                    roleAliases,
                    pathFromRoot,
                    keySelector(candidate),
                    verb);

                if (effective.IsAllowed)
                {
                    result.Add(candidate);
                }
            }
            catch (Exception ex)
            {
                // Fail open: if resolution throws for any reason, keep the candidate so that a bad
                // permission entry never causes the whole Create menu to vanish silently.
                logger.LogWarning(
                    ex,
                    "Advanced Permissions: doc-type create filter failed for content type {ContentTypeKey}",
                    keySelector(candidate));
                result.Add(candidate);
            }
        }

        return result;
    }

    /// <summary>
    /// Builds the current user's role aliases (group aliases + <c>$everyone</c>), or
    /// <see langword="null"/> when no backoffice user is in context.
    /// </summary>
    /// <returns>The role aliases, or <see langword="null"/>.</returns>
    private IReadOnlyList<string>? GetCurrentUserRoleAliases()
    {
        var user = backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser;
        if (user is null)
        {
            return null;
        }

        var groups = user.Groups.ToList();
        var roles = new List<string>(groups.Count + 1);
        roles.AddRange(groups.Select(g => g.Alias));
        roles.Add(AdvancedPermissionsConstants.EveryoneRoleAlias);

        return roles;
    }

    /// <summary>
    /// Resolves the content key to a path of GUID keys from root to the node.
    /// </summary>
    /// <param name="parentContentKey">The parent node key.</param>
    /// <returns>An ordered list of node keys, or just the virtual root if the lookup fails.</returns>
    private IReadOnlyList<Guid> BuildPathFromRoot(Guid parentContentKey)
    {
        var paths = entityService.GetAllPaths(UmbracoObjectTypes.Document, parentContentKey);
        var pathEntry = paths.FirstOrDefault();
        if (pathEntry is null)
        {
            return [AdvancedPermissionsConstants.VirtualRootNodeKey];
        }

        var pathIds = pathEntry.Path
            .Split(',')
            .Select(s => int.TryParse(s, out var id) ? id : 0)
            .Where(id => id > 0)
            .ToArray();

        if (pathIds.Length == 0)
        {
            return [AdvancedPermissionsConstants.VirtualRootNodeKey];
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
