using UmbracoAdvancedSecurity.Caching;
using UmbracoAdvancedSecurity.Core.Constants;
using UmbracoAdvancedSecurity.Core.Interfaces;
using UmbracoAdvancedSecurity.Core.Models;
using Umbraco.Cms.Core.Services;

namespace UmbracoAdvancedSecurity.Services;

/// <summary>
/// Orchestrates the repository, permission resolver, and cache to resolve and manage
/// advanced security permissions.
/// </summary>
/// <remarks>
/// <para>
/// This service is the main entry point for the permission resolution pipeline.
/// It integrates a two-level cache: L1 (stored entries per role) and L2 (resolved
/// permissions per user+node). See <see cref="AdvancedPermissionCache"/> for details.
/// </para>
/// </remarks>
/// <param name="repository">The repository for reading and writing raw permission entries.</param>
/// <param name="resolver">The pure resolver that applies inheritance and priority rules.</param>
/// <param name="userService">The Umbraco user service used to look up user group memberships and defaults.</param>
/// <param name="cache">The two-level permission cache.</param>
public sealed class AdvancedPermissionService(
    IAdvancedPermissionRepository repository,
    IPermissionResolver resolver,
    IUserService userService,
    AdvancedPermissionCache cache)
    : IAdvancedPermissionService
{
    /// <inheritdoc />
    public async Task<EffectivePermission> ResolveAsync(
        Guid userKey,
        Guid nodeKey,
        IReadOnlyList<Guid> pathFromRoot,
        string verb,
        CancellationToken cancellationToken = default)
    {
        // Delegate to ResolveAllAsync so the L2 cache is populated for all verbs at once
        var all = await ResolveAllAsync(userKey, nodeKey, pathFromRoot, cancellationToken: cancellationToken);
        return all.TryGetValue(verb, out var result)
            ? result
            : new EffectivePermission(verb, IsAllowed: false, IsExplicit: false, Reasoning: []);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, EffectivePermission>> ResolveAllAsync(
        Guid userKey,
        Guid nodeKey,
        IReadOnlyList<Guid> pathFromRoot,
        IEnumerable<string>? verbs = null,
        CancellationToken cancellationToken = default)
    {
        // Check L2 cache: stores all verbs, so we always resolve everything and filter on return
        var cached = cache.GetResolved(userKey, nodeKey);
        if (cached is not null)
        {
            return FilterVerbs(cached, verbs);
        }

        var context = await BuildUserContextAsync(userKey, nodeKey, pathFromRoot, cancellationToken);

        // Always resolve ALL verbs when caching — never cache a partial result
        var all = resolver.ResolveAll(context, AdvancedSecurityConstants.AllVerbs);
        cache.SetResolved(userKey, nodeKey, all);

        return FilterVerbs(all, verbs);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AdvancedPermissionEntry>> GetEntriesAsync(
        Guid? nodeKey,
        string roleAlias,
        CancellationToken cancellationToken = default) =>
        repository.GetByNodeAndRoleAsync(nodeKey, roleAlias, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<AdvancedPermissionEntry>> GetEntriesByNodeAsync(
        Guid? nodeKey,
        CancellationToken cancellationToken = default) =>
        repository.GetByNodeAsync(nodeKey, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, EffectivePermission>> ResolveForRoleAsync(
        string roleAlias,
        Guid nodeKey,
        IReadOnlyList<Guid> pathFromRoot,
        IEnumerable<string>? verbs = null,
        CancellationToken cancellationToken = default)
    {
        // Resolve as if user has exactly this role plus $everyone — uses L1 cache for entries
        var roles = new List<string> { roleAlias, AdvancedSecurityConstants.EveryoneRoleAlias };

        var storedEntries = await GetEntriesForRolesAndPathAsync(roles, pathFromRoot, cancellationToken);

        var context = new PermissionResolutionContext(
            TargetNodeKey: nodeKey,
            PathFromRoot: pathFromRoot,
            RoleAliases: roles,
            StoredEntries: storedEntries);

        var verbList = verbs ?? AdvancedSecurityConstants.AllVerbs;
        return resolver.ResolveAll(context, verbList);
    }

    /// <inheritdoc />
    public async Task SaveEntriesAsync(
        Guid? nodeKey,
        string roleAlias,
        IEnumerable<(string Verb, PermissionState State, PermissionScope Scope)> entries,
        CancellationToken cancellationToken = default)
    {
        await repository.SaveAsync(nodeKey, roleAlias, entries, cancellationToken);

        // Invalidate L1 for this role (entries changed), and ALL L2 (any user's resolution may be stale)
        cache.InvalidateRoleEntries(roleAlias);
        cache.InvalidateAllResolved();
    }

    /// <inheritdoc />
    public async Task DeleteEntryAsync(
        Guid? nodeKey,
        string roleAlias,
        string verb,
        CancellationToken cancellationToken = default)
    {
        await repository.DeleteAsync(nodeKey, roleAlias, verb, cancellationToken);

        // Invalidate L1 for this role (entries changed), and ALL L2 (any user's resolution may be stale)
        cache.InvalidateRoleEntries(roleAlias);
        cache.InvalidateAllResolved();
    }

    /// <summary>
    /// Builds the <see cref="PermissionResolutionContext"/> for a given user and node by loading
    /// the required data from the user service and L1 cache/repository.
    /// </summary>
    /// <param name="userKey">The key of the user to resolve for.</param>
    /// <param name="nodeKey">The target node key.</param>
    /// <param name="pathFromRoot">The path from root to the target node (as Guid list).</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>A fully populated resolution context.</returns>
    private async Task<PermissionResolutionContext> BuildUserContextAsync(
        Guid userKey,
        Guid nodeKey,
        IReadOnlyList<Guid> pathFromRoot,
        CancellationToken cancellationToken)
    {
        var user = await userService.GetAsync(userKey);

        // Collect role aliases: all user groups + the virtual $everyone role
        var groups = user?.Groups.ToList() ?? [];
        var roleAliases = new List<string>(groups.Count + 1);
        roleAliases.AddRange(groups.Select(g => g.Alias));
        roleAliases.Add(AdvancedSecurityConstants.EveryoneRoleAlias);

        // Load stored entries (including root-level null-NodeKey entries which act as defaults)
        var storedEntries = await GetEntriesForRolesAndPathAsync(roleAliases, pathFromRoot, cancellationToken);

        return new PermissionResolutionContext(
            TargetNodeKey: nodeKey,
            PathFromRoot: pathFromRoot,
            RoleAliases: roleAliases,
            StoredEntries: storedEntries);
    }

    /// <summary>
    /// Loads stored permission entries for the given roles and path, using the L1 cache.
    /// Entries for each role are loaded in full (all nodes) and then filtered to the path in-memory.
    /// </summary>
    /// <param name="roleAliases">The role aliases to load entries for.</param>
    /// <param name="pathFromRoot">The path nodes to filter entries to.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The stored entries relevant to this resolution.</returns>
    private async Task<IReadOnlyList<AdvancedPermissionEntry>> GetEntriesForRolesAndPathAsync(
        IReadOnlyList<string> roleAliases,
        IReadOnlyList<Guid> pathFromRoot,
        CancellationToken cancellationToken)
    {
        // Build a set of node keys that are relevant (path + null for root-level entries)
        var pathSet = new HashSet<Guid?>(pathFromRoot.Select(k => (Guid?)k))
        {
            null, // include root-level (null NodeKey) entries
        };

        var result = new List<AdvancedPermissionEntry>();

        foreach (var roleAlias in roleAliases)
        {
            // Try L1 cache first; populate from DB on miss
            var roleEntries = cache.GetRoleEntries(roleAlias);
            if (roleEntries is null)
            {
                roleEntries = await repository.GetByRoleAsync(roleAlias, cancellationToken);
                cache.SetRoleEntries(roleAlias, roleEntries);
            }

            // Filter in-memory to the path nodes only
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

    /// <summary>
    /// Filters a resolved permissions dictionary to only the requested verbs,
    /// or returns it unchanged when <paramref name="verbs"/> is <see langword="null"/>.
    /// </summary>
    /// <param name="all">The full resolved permissions dictionary.</param>
    /// <param name="verbs">The verbs to keep, or <see langword="null"/> to return all.</param>
    /// <returns>The filtered or original dictionary.</returns>
    private static IReadOnlyDictionary<string, EffectivePermission> FilterVerbs(
        IReadOnlyDictionary<string, EffectivePermission> all,
        IEnumerable<string>? verbs)
    {
        if (verbs is null)
        {
            return all;
        }

        return all
            .Where(kvp => verbs.Contains(kvp.Key, StringComparer.Ordinal))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }
}
