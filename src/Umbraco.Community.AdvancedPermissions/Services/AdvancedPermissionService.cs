using Umbraco.Community.AdvancedPermissions.Caching;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;
using Umbraco.Community.AdvancedPermissions.Notifications;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Community.AdvancedPermissions.Services;

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
/// <param name="eventAggregator">
/// Used to publish permission-change notifications after a write has been persisted and the cache
/// invalidated, so other packages can react without depending on how the write happened.
/// </param>
/// <param name="logger">
/// Used to record a notification handler's failure without letting it propagate — see
/// <see cref="PublishSafelyAsync"/>.
/// </param>
public sealed class AdvancedPermissionService(
    IAdvancedPermissionRepository repository,
    IPermissionResolver resolver,
    IUserService userService,
    AdvancedPermissionCache cache,
    IEventAggregator eventAggregator,
    ILogger<AdvancedPermissionService> logger)
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
        var all = resolver.ResolveAll(context, AdvancedPermissionsConstants.AllVerbs);
        cache.SetResolved(userKey, nodeKey, all);

        return FilterVerbs(all, verbs);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AdvancedPermissionEntry>> GetEntriesAsync(
        Guid nodeKey,
        string roleAlias,
        CancellationToken cancellationToken = default) =>
        repository.GetByNodeAndRoleAsync(nodeKey, roleAlias, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<AdvancedPermissionEntry>> GetEntriesByNodesAndRoleAsync(
        IEnumerable<Guid> nodeKeys,
        string roleAlias,
        CancellationToken cancellationToken = default) =>
        repository.GetByNodesAndRoleAsync(nodeKeys, roleAlias, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<AdvancedPermissionEntry>> GetEntriesByNodeAsync(
        Guid nodeKey,
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
        // Resolve using only this role's own entries — a role is self-contained.
        // $everyone is intentionally excluded: its entries are separate and should not
        // influence the effective permissions shown for a specific role.
        var roles = new List<string> { roleAlias };

        var storedEntries = await GetEntriesForRolesAndPathAsync(roles, pathFromRoot, cancellationToken);

        var context = new PermissionResolutionContext(
            TargetNodeKey: nodeKey,
            PathFromRoot: pathFromRoot,
            RoleAliases: roles,
            StoredEntries: storedEntries);

        var verbList = verbs ?? AdvancedPermissionsConstants.AllVerbs;
        return resolver.ResolveAll(context, verbList);
    }

    /// <inheritdoc />
    public async Task SaveEntriesAsync(
        Guid nodeKey,
        string roleAlias,
        IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> entries,
        CancellationToken cancellationToken = default)
    {
        // Materialised once: the sequence is enumerated by the repository and again for the
        // notification's verb list, and a caller is entitled to hand us a lazy one.
        var materialised = entries as IList<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)>
            ?? entries.ToList();

        await repository.SaveAsync(nodeKey, roleAlias, materialised, cancellationToken);

        // Invalidate L1 for this role (entries changed), and ALL L2 (any user's resolution may be stale)
        cache.InvalidateRoleEntries(roleAlias);
        cache.InvalidateAllResolved();

        // Strictly after invalidation. A handler that refetches on this notification while the
        // cache still holds the old value would read the very snapshot this write replaced — and,
        // having consumed its notification, would never ask again.
        await PublishSafelyAsync(
            new AdvancedPermissionsChangedNotification(
                nodeKey,
                roleAlias,
                materialised.Select(e => e.Verb).Distinct(StringComparer.Ordinal).ToList()),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteEntryAsync(
        Guid nodeKey,
        string roleAlias,
        string verb,
        CancellationToken cancellationToken = default)
    {
        await repository.DeleteAsync(nodeKey, roleAlias, verb, cancellationToken);

        // Invalidate L1 for this role (entries changed), and ALL L2 (any user's resolution may be stale)
        cache.InvalidateRoleEntries(roleAlias);
        cache.InvalidateAllResolved();

        // Strictly after invalidation — see the note in SaveEntriesAsync above.
        await PublishSafelyAsync(
            new AdvancedPermissionsChangedNotification(nodeKey, roleAlias, [verb]),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task SaveManyAsync(
        IReadOnlyList<(Guid NodeKey, string RoleAlias, IReadOnlyList<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries)> batch,
        CancellationToken cancellationToken = default)
    {
        // Nothing was written, so nothing is stale — skip the transaction and the L2 flush.
        if (batch.Count == 0)
        {
            return;
        }

        await repository.SaveManyAsync(
            batch.Select(b => (b.NodeKey, b.RoleAlias,
                (IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)>)b.Entries)),
            cancellationToken);

        // Invalidated once for the whole batch rather than per pair: L2 is dropped wholesale
        // anyway, and doing it per pair would drop it N times for no further effect.
        foreach (var roleAlias in batch.Select(b => b.RoleAlias).Distinct(StringComparer.Ordinal))
        {
            cache.InvalidateRoleEntries(roleAlias);
        }

        cache.InvalidateAllResolved();

        // Again strictly after invalidation, and one notification per pair so a handler sees the
        // same granularity it would from a single save. Each publish is wrapped individually
        // (inside the loop) so one bad pair's handler failure does not stop the remaining pairs
        // from being announced.
        foreach (var (nodeKey, roleAlias, entries) in batch)
        {
            await PublishSafelyAsync(
                new AdvancedPermissionsChangedNotification(
                    nodeKey,
                    roleAlias,
                    entries.Select(e => e.Verb).Distinct(StringComparer.Ordinal).ToList()),
                cancellationToken);
        }
    }

    /// <summary>
    /// Publishes a permission-change notification, swallowing any exception thrown by a handler
    /// so it cannot mask a successful write.
    /// </summary>
    /// <remarks>
    /// By the time this runs, the write has already committed and the cache has already been
    /// invalidated — the change is real and already visible to every other reader. Letting a
    /// handler's failure propagate from here would turn a successful save into a reported
    /// failure: the caller (a controller) would return an error, and the editor would see "save
    /// failed" for a save that in fact succeeded — then retry, or reload and lose work, over
    /// nothing. A degraded live update (silently not going out) is the correct failure mode here;
    /// a false negative on the save itself is not. <see cref="OperationCanceledException"/> is
    /// deliberately not caught: a cancelled request is not a handler failure, and swallowing it
    /// would hide a genuine cancellation.
    /// </remarks>
    /// <param name="notification">The notification to publish.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    private async Task PublishSafelyAsync(
        AdvancedPermissionsChangedNotification notification,
        CancellationToken cancellationToken)
    {
        try
        {
            await eventAggregator.PublishAsync(notification, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to publish {Notification} for node {NodeKey} and user group {RoleAlias}. " +
                "The write already committed and the cache was already invalidated — only the live-update notification was lost.",
                nameof(AdvancedPermissionsChangedNotification),
                notification.NodeKey,
                notification.RoleAlias);
        }
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
        roleAliases.Add(AdvancedPermissionsConstants.EveryoneRoleAlias);

        // Load stored entries (including virtual-root entries which act as global defaults)
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
        // Build a set of node keys that are relevant (path + VirtualRootNodeKey for global default entries)
        var pathSet = new HashSet<Guid>(pathFromRoot)
        {
            AdvancedPermissionsConstants.VirtualRootNodeKey, // include virtual-root entries
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
