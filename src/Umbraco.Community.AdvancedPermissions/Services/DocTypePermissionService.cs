using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.Caching;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;
using Umbraco.Community.AdvancedPermissions.Notifications;

namespace Umbraco.Community.AdvancedPermissions.Services;

/// <summary>
/// Orchestrates the doc-type permission repository, resolver, and cache. Mirrors the structure of
/// <see cref="AdvancedPermissionService"/> but for the doc-type-scoped permission flow.
/// </summary>
/// <param name="repository">The doc-type permission repository.</param>
/// <param name="resolver">The pure doc-type permission resolver.</param>
/// <param name="userService">The Umbraco user service used to look up group memberships.</param>
/// <param name="cache">The two-level doc-type permission cache.</param>
/// <param name="eventAggregator">
/// Used to publish permission-change notifications after a write has been persisted and the cache
/// invalidated, so other packages (and the SignalR relay) can react without depending on how the
/// write happened.
/// </param>
/// <param name="logger">
/// Used to record a notification handler's failure without letting it propagate — see
/// <see cref="PublishSafelyAsync"/>.
/// </param>
public sealed class DocTypePermissionService(
    IDocTypePermissionRepository repository,
    IDocTypePermissionResolver resolver,
    IUserService userService,
    DocTypePermissionCache cache,
    IEventAggregator eventAggregator,
    ILogger<DocTypePermissionService> logger)
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

        // Strictly after invalidation, for the reason set out on AdvancedPermissionService: a
        // handler that refetched on this while the cache still held the old value would read the
        // snapshot this write replaced, and would never be told again.
        await PublishSafelyAsync(
            new DocTypePermissionsChangedNotification(nodeKey, roleAlias, [contentTypeKey]),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task SaveManyAsync(
        IReadOnlyList<(Guid NodeKey, string RoleAlias, Guid ContentTypeKey, IReadOnlyList<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries)> batch,
        CancellationToken cancellationToken = default)
    {
        // Nothing was written, so nothing is stale — skip the transaction and the L2 flush.
        if (batch.Count == 0)
        {
            return;
        }

        await repository.SaveManyAsync(
            batch.Select(b => (b.NodeKey, b.RoleAlias, b.ContentTypeKey,
                (IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)>)b.Entries)),
            cancellationToken);

        // Invalidated once for the whole batch rather than per triple: L2 is dropped wholesale
        // anyway, and doing it per triple would drop it N times for no further effect.
        foreach (var roleAlias in batch.Select(b => b.RoleAlias).Distinct(StringComparer.Ordinal))
        {
            cache.InvalidateRoleEntries(roleAlias);
        }

        cache.InvalidateAllResolved();

        // Again strictly after invalidation, and one notification per triple so a handler sees the
        // same granularity it would from a single save. Each publish is wrapped individually
        // (inside the loop) so one bad triple's handler failure does not stop the remaining triples
        // from being announced.
        foreach (var (nodeKey, roleAlias, contentTypeKey, _) in batch)
        {
            await PublishSafelyAsync(
                new DocTypePermissionsChangedNotification(nodeKey, roleAlias, [contentTypeKey]),
                cancellationToken);
        }
    }

    /// <summary>
    /// Publishes a doc-type permission-change notification, swallowing any exception thrown by a
    /// handler so it cannot mask a successful write.
    /// </summary>
    /// <remarks>
    /// By the time this runs, the write has already committed and the cache has already been
    /// invalidated — the change is real and already visible to every other reader. Letting a
    /// handler's failure propagate from here would turn a successful save into a reported failure.
    /// A degraded live update (silently not going out) is the correct failure mode here; a false
    /// negative on the save itself is not. <see cref="OperationCanceledException"/> is deliberately
    /// not caught: a cancelled request is not a handler failure, and swallowing it would hide a
    /// genuine cancellation.
    /// </remarks>
    /// <param name="notification">The notification to publish.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    private async Task PublishSafelyAsync(
        DocTypePermissionsChangedNotification notification,
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
                nameof(DocTypePermissionsChangedNotification),
                notification.NodeKey,
                notification.RoleAlias);
        }
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
