using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.Caching;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;
using Umbraco.Community.AdvancedPermissions.Notifications;

namespace Umbraco.Community.AdvancedPermissions.Services;

/// <summary>
/// Orchestrates a node-permission repository, the pure resolver, and a two-level cache to resolve and
/// manage advanced security permissions. Shared by every node-based permission target; the target
/// supplies its own repository, cache, and verb set.
/// </summary>
/// <remarks>
/// <para>
/// This is the main entry point for the permission resolution pipeline. It integrates a two-level
/// cache: L1 (stored entries per role) and L2 (resolved permissions per user+node). See
/// <see cref="NodePermissionCache"/> for details.
/// </para>
/// <para>
/// The <c>$everyone</c> role and virtual-root sentinel are shared across targets; only the set of
/// verbs that gets fully resolved-and-cached (<paramref name="allVerbs"/>) is target-specific.
/// </para>
/// <para>
/// Every write goes through the same three steps, in this order: write, invalidate the caches, then
/// publish a change notification. The order is the point. A client that refetches on a notification
/// which overtook the invalidation reads the very snapshot the change replaced and — having consumed
/// its one notification — never asks again. Because the sequence lives here rather than in a concrete
/// service, every family that derives from this class gets it, and cannot lose it by accident.
/// </para>
/// <para>
/// The type parameter is the notification record the family raises (content raises
/// <see cref="AdvancedPermissionsChangedNotification"/>, library elements raise
/// <see cref="ElementPermissionsChangedNotification"/>). It is a type parameter rather than an
/// <see cref="INotification"/> return because <see cref="IEventAggregator.PublishAsync{TNotification}"/>
/// finds its handlers by the compile-time type: publishing through the interface would find none and
/// fail silently. Making the notification a type parameter guarantees two things: within one service,
/// the single-save and batch paths cannot publish different notification types, and publishing through
/// a bare <see cref="INotification"/> — which would silently resolve no handlers — is impossible. It
/// does not stop a future subclass choosing the wrong type argument; that is pinned by the per-family
/// tests, not by the compiler.
/// </para>
/// </remarks>
/// <typeparam name="TNotification">The change notification this family publishes after a write.</typeparam>
/// <param name="repository">The repository for reading and writing raw permission entries.</param>
/// <param name="resolver">The pure resolver that applies inheritance and priority rules (default Deny).</param>
/// <param name="userService">The Umbraco user service used to look up user group memberships.</param>
/// <param name="cache">The two-level permission cache for this target.</param>
/// <param name="allVerbs">
/// The complete set of verbs this target manages. Resolution always computes and caches every verb in
/// this set so the L2 cache never holds a partial result.
/// </param>
/// <param name="eventAggregator">
/// Used to publish permission-change notifications after a write has been persisted and the cache
/// invalidated, so other packages can react without depending on how the write happened.
/// </param>
/// <param name="logger">
/// Used to record a notification handler's failure without letting it propagate — see
/// <see cref="PermissionChangePublisher"/>.
/// </param>
public abstract class NodePermissionServiceBase<TNotification>(
    INodePermissionRepository repository,
    IPermissionResolver resolver,
    IUserService userService,
    NodePermissionCache cache,
    IReadOnlyList<string> allVerbs,
    IEventAggregator eventAggregator,
    ILogger logger)
    : INodePermissionService
    where TNotification : INotification
{
    /// <summary>
    /// Creates the change notification for this family.
    /// </summary>
    /// <param name="nodeKey">The node the entries were written for.</param>
    /// <param name="roleAlias">The user group alias the entries were written for.</param>
    /// <param name="verbs">The verbs whose entries were written. Empty when all entries were removed.</param>
    /// <returns>The notification to publish, of the family's own type.</returns>
    protected abstract TNotification CreateChangedNotification(
        Guid nodeKey,
        string roleAlias,
        IReadOnlyList<string> verbs);

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
        var all = resolver.ResolveAll(context, allVerbs);
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

        var verbList = verbs ?? allVerbs;
        return resolver.ResolveAll(context, verbList);
    }

    /// <summary>
    /// Saves the entries for a node and user group with no concurrency check, by delegating to the
    /// stamped overload with a <see langword="null"/> stamp.
    /// </summary>
    /// <param name="nodeKey">The node key.</param>
    /// <param name="roleAlias">The user group alias.</param>
    /// <param name="entries">The replacement entries.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    // Not marked [Obsolete] here: this class is not the published contract, and the obsolete warning
    // fires on the interface member, which is where every caller is steered away from it.
    // Implementing an obsolete interface member does not itself raise a warning.
    public Task SaveEntriesAsync(
        Guid nodeKey,
        string roleAlias,
        IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> entries,
        CancellationToken cancellationToken = default) =>
        SaveEntriesAsync(nodeKey, roleAlias, entries, (string?)null, cancellationToken);

    /// <summary>
    /// Saves the entries for a node and user group, refusing the write if the stored entries no
    /// longer match <paramref name="expectedStamp"/>, then invalidates caches and announces the
    /// change. This is the real implementation of the interface's stamped overload - it overrides
    /// the interface's default implementation, which would ignore the stamp.
    /// </summary>
    /// <param name="nodeKey">The node key.</param>
    /// <param name="roleAlias">The user group alias.</param>
    /// <param name="entries">The replacement entries.</param>
    /// <param name="expectedStamp">The stamp the caller read, or <see langword="null"/> to skip the check.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    public async Task SaveEntriesAsync(
        Guid nodeKey,
        string roleAlias,
        IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> entries,
        string? expectedStamp,
        CancellationToken cancellationToken = default)
    {
        // Materialised once: the sequence is enumerated by the repository and again for the
        // notification's verb list, and a caller is entitled to hand us a lazy one.
        var materialised = entries as IList<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)>
            ?? entries.ToList();

        // The stamp check happens inside the repository's write transaction. A refusal throws from
        // here, before the cache is touched and before anything is published: nothing was written,
        // so there is nothing to invalidate and nothing to announce.
        await repository.SaveAsync(nodeKey, roleAlias, materialised, expectedStamp, cancellationToken);

        // Invalidate L1 for this role (entries changed), and ALL L2 (any user's resolution may be stale)
        cache.InvalidateRoleEntries(roleAlias);
        cache.InvalidateAllResolved();

        // Strictly after invalidation. A handler that refetches on this notification while the
        // cache still holds the old value would read the very snapshot this write replaced — and,
        // having consumed its notification, would never ask again.
        await PublishChangedAsync(
            nodeKey,
            roleAlias,
            materialised.Select(e => e.Verb).Distinct(StringComparer.Ordinal).ToList(),
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
        await PublishChangedAsync(nodeKey, roleAlias, [verb], cancellationToken);
    }

    /// <inheritdoc />
    public async Task SaveManyAsync(
        IReadOnlyList<(Guid NodeKey, string RoleAlias, IReadOnlyList<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries, string? ExpectedStamp)> batch,
        CancellationToken cancellationToken = default)
    {
        // Nothing was written, so nothing is stale — skip the transaction and the L2 flush.
        if (batch.Count == 0)
        {
            return;
        }

        // A stale stamp throws from here, before any invalidation or publish - see SaveEntriesAsync.
        await repository.SaveManyAsync(
            batch.Select(b => (b.NodeKey, b.RoleAlias,
                (IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)>)b.Entries,
                b.ExpectedStamp)),
            cancellationToken);

        // Invalidated once per distinct user group rather than per pair, and the resolved (L2) cache
        // once for the whole batch: L2 is dropped wholesale anyway, and doing it per pair would drop
        // it N times for no further effect.
        foreach (var roleAlias in batch.Select(b => b.RoleAlias).Distinct(StringComparer.Ordinal))
        {
            cache.InvalidateRoleEntries(roleAlias);
        }

        cache.InvalidateAllResolved();

        // Again strictly after invalidation, and one notification per pair so a handler sees the
        // same granularity it would from a single save. Each publish is isolated individually (in
        // PublishChangedAsync) so one bad pair's handler failure does not stop the remaining pairs
        // from being announced.
        foreach (var (nodeKey, roleAlias, entries, _) in batch)
        {
            await PublishChangedAsync(
                nodeKey,
                roleAlias,
                entries.Select(e => e.Verb).Distinct(StringComparer.Ordinal).ToList(),
                cancellationToken);
        }
    }

    /// <summary>
    /// Publishes this family's change notification, isolating any handler failure so it cannot mask a
    /// write that has already committed. Must only be called after the cache has been invalidated.
    /// </summary>
    /// <param name="nodeKey">The node the entries were written for.</param>
    /// <param name="roleAlias">The user group alias the entries were written for.</param>
    /// <param name="verbs">The verbs whose entries were written.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>A task that completes once the notification has been published or its failure logged.</returns>
    private Task PublishChangedAsync(
        Guid nodeKey,
        string roleAlias,
        IReadOnlyList<string> verbs,
        CancellationToken cancellationToken) =>
        PermissionChangePublisher.PublishSafelyAsync(
            eventAggregator,
            logger,
            CreateChangedNotification(nodeKey, roleAlias, verbs),
            $"node {nodeKey} and user group {roleAlias}",
            cancellationToken);

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
