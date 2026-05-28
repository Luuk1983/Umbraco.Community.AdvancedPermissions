using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.Caching;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.Services;

/// <summary>
/// Orchestrates the doc-type permission repository, resolver, and cache. Mirrors the structure of
/// <see cref="AdvancedPermissionService"/> but for the doc-type-scoped permission flow.
/// </summary>
/// <param name="repository">The doc-type permission repository.</param>
/// <param name="resolver">The pure doc-type permission resolver.</param>
/// <param name="userService">The Umbraco user service used to look up group memberships.</param>
/// <param name="cache">The two-level doc-type permission cache.</param>
public sealed class DocTypePermissionService(
    IDocTypePermissionRepository repository,
    IDocTypePermissionResolver resolver,
    IUserService userService,
    DocTypePermissionCache cache)
    : IDocTypePermissionService
{
    /// <inheritdoc />
    public async Task<EffectivePermission> ResolveCreateAsync(
        Guid userKey,
        Guid parentNodeKey,
        IReadOnlyList<Guid> parentPathFromRoot,
        Guid contentTypeKey,
        CancellationToken cancellationToken = default)
    {
        var cached = cache.GetResolved(userKey, parentNodeKey, contentTypeKey);
        if (cached is not null)
        {
            return cached;
        }

        var user = await userService.GetAsync(userKey);
        var groups = user?.Groups.ToList() ?? [];
        var roleAliases = new List<string>(groups.Count + 1);
        roleAliases.AddRange(groups.Select(g => g.Alias));
        roleAliases.Add(AdvancedPermissionsConstants.EveryoneRoleAlias);

        var result = await ResolveCreateForRolesAsync(roleAliases, parentPathFromRoot, contentTypeKey, cancellationToken);

        cache.SetResolved(userKey, parentNodeKey, contentTypeKey, result);
        return result;
    }

    /// <inheritdoc />
    public async Task<EffectivePermission> ResolveCreateForRolesAsync(
        IReadOnlyList<string> roleAliases,
        IReadOnlyList<Guid> parentPathFromRoot,
        Guid contentTypeKey,
        CancellationToken cancellationToken = default)
    {
        var entries = await GetEntriesForRolesAndPathAsync(roleAliases, parentPathFromRoot, cancellationToken);

        var parentKey = parentPathFromRoot.Count > 0
            ? parentPathFromRoot[^1]
            : AdvancedPermissionsConstants.VirtualRootNodeKey;

        var ctx = new DocTypePermissionResolutionContext(
            ContentTypeKey: contentTypeKey,
            ParentNodeKey: parentKey,
            PathFromRoot: parentPathFromRoot,
            RoleAliases: roleAliases,
            StoredEntries: entries);

        return resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbCreateOfType);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DocTypePermissionEntry>> GetEditorEntriesAsync(
        string roleAlias,
        Guid contentTypeKey,
        CancellationToken cancellationToken = default) =>
        repository.GetByRoleAndContentTypeAsync(roleAlias, contentTypeKey, cancellationToken);

    /// <inheritdoc />
    public async Task SaveEditorEntriesAsync(
        Guid nodeKey,
        string roleAlias,
        Guid contentTypeKey,
        IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> entries,
        CancellationToken cancellationToken = default)
    {
        await repository.SaveAsync(nodeKey, roleAlias, contentTypeKey, entries, cancellationToken);

        cache.InvalidateRoleEntries(roleAlias);
        cache.InvalidateAllResolved();
    }

    /// <summary>
    /// Loads entries relevant to the resolution: all entries for the user's roles, filtered to
    /// the path nodes (plus virtual root) in memory.
    /// </summary>
    /// <param name="roleAliases">The role aliases to load.</param>
    /// <param name="pathFromRoot">Path nodes to filter by.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The filtered entries.</returns>
    private async Task<IReadOnlyList<DocTypePermissionEntry>> GetEntriesForRolesAndPathAsync(
        IReadOnlyList<string> roleAliases,
        IReadOnlyList<Guid> pathFromRoot,
        CancellationToken cancellationToken)
    {
        var pathSet = new HashSet<Guid>(pathFromRoot)
        {
            AdvancedPermissionsConstants.VirtualRootNodeKey,
        };

        var result = new List<DocTypePermissionEntry>();

        foreach (var roleAlias in roleAliases)
        {
            var roleEntries = cache.GetRoleEntries(roleAlias);
            if (roleEntries is null)
            {
                roleEntries = await repository.GetByRoleAsync(roleAlias, cancellationToken);
                cache.SetRoleEntries(roleAlias, roleEntries);
            }

            foreach (var entry in roleEntries)
            {
                if (pathSet.Contains(entry.NodeKey))
                {
                    result.Add(entry);
                }
            }
        }

        return result;
    }
}
