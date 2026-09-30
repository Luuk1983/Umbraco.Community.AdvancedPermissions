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
/// <remarks>
/// Document-type permissions carry a three-part key (node, user group, document type), so this service
/// does not derive from the node-keyed <see cref="NodePermissionServiceBase{TNotification}"/> and holds
/// its own copy of the write, invalidate, publish sequence. It shares the isolation rule with the
/// node-keyed services through <see cref="PermissionChangePublisher"/>.
/// </remarks>
/// <param name="repository">The doc-type permission repository.</param>
/// <param name="resolver">The pure doc-type permission resolver.</param>
/// <param name="userService">The Umbraco user service used to look up group memberships.</param>
/// <param name="contentTypeService">
/// Used at publish time to tell a document type from an element type, so the change notification can
/// say which family it concerns. The content type repository behind it uses a full-dataset cache
/// policy, so after warm-up the lookup is served from memory.
/// </param>
/// <param name="cache">The two-level doc-type permission cache.</param>
/// <param name="eventAggregator">
/// Used to publish <see cref="DocTypePermissionsChangedNotification"/> after a write has been
/// persisted and the cache invalidated.
/// </param>
/// <param name="logger">Used to record a notification handler's failure without letting it propagate.</param>
public sealed class DocTypePermissionService(
    IDocTypePermissionRepository repository,
    IDocTypePermissionResolver resolver,
    IUserService userService,
    IContentTypeService contentTypeService,
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

        var result = await ResolveCreateForRolesAsync(
            roleAliases, parentPathFromRoot, contentTypeKey, cancellationToken: cancellationToken);

        cache.SetResolved(userKey, parentNodeKey, contentTypeKey, result);
        return result;
    }

    /// <inheritdoc />
    public async Task<EffectivePermission> ResolveCreateForRolesAsync(
        IReadOnlyList<string> roleAliases,
        IReadOnlyList<Guid> parentPathFromRoot,
        Guid contentTypeKey,
        string verb = AdvancedPermissionsConstants.VerbCreateOfType,
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

        return resolver.Resolve(ctx, verb);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DocTypePermissionEntry>> GetEditorEntriesAsync(
        string roleAlias,
        Guid contentTypeKey,
        CancellationToken cancellationToken = default) =>
        repository.GetByRoleAndContentTypeAsync(roleAlias, contentTypeKey, cancellationToken);

    /// <summary>
    /// Saves the editor's entries for a node, user group and document type triple with no
    /// concurrency check, by delegating to the stamped overload with a <see langword="null"/> stamp.
    /// </summary>
    /// <param name="nodeKey">The node the entries apply to (or virtual root).</param>
    /// <param name="roleAlias">The user group alias.</param>
    /// <param name="contentTypeKey">The document type key.</param>
    /// <param name="entries">The replacement entries.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    // Not marked [Obsolete] here: this class is not the published contract, and the obsolete warning
    // fires on the interface member, which is where every caller is steered away from it.
    // Implementing an obsolete interface member does not itself raise a warning.
    public Task SaveEditorEntriesAsync(
        Guid nodeKey,
        string roleAlias,
        Guid contentTypeKey,
        IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> entries,
        CancellationToken cancellationToken = default) =>
        SaveEditorEntriesAsync(nodeKey, roleAlias, contentTypeKey, entries, (string?)null, cancellationToken);

    /// <summary>
    /// Saves the editor's entries for a node, user group and document type triple, refusing the
    /// write if the stored entries no longer match <paramref name="expectedStamp"/>, then
    /// invalidates the cache and announces the change. This is the real implementation of the
    /// interface's stamped overload - it overrides the interface's default implementation, which
    /// would ignore the stamp.
    /// </summary>
    /// <param name="nodeKey">The node the entries apply to (or virtual root).</param>
    /// <param name="roleAlias">The user group alias.</param>
    /// <param name="contentTypeKey">The document type key.</param>
    /// <param name="entries">The replacement entries.</param>
    /// <param name="expectedStamp">The stamp the caller read, or <see langword="null"/> to skip the check.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    public async Task SaveEditorEntriesAsync(
        Guid nodeKey,
        string roleAlias,
        Guid contentTypeKey,
        IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> entries,
        string? expectedStamp,
        CancellationToken cancellationToken = default)
    {
        // The stamp check happens inside the repository's write transaction. A stale stamp throws
        // from here, before the cache is touched and before anything is published: nothing was
        // written, so there is nothing to invalidate and nothing to announce.
        await repository.SaveAsync(nodeKey, roleAlias, contentTypeKey, entries, expectedStamp, cancellationToken);

        cache.InvalidateRoleEntries(roleAlias);
        cache.InvalidateAllResolved();

        // Strictly after invalidation, for the reason set out on NodePermissionServiceBase: a
        // handler that refetched on this while the cache still held the old value would read the
        // snapshot this write replaced, and would never be told again.
        await PublishChangedAsync(
            nodeKey, roleAlias, contentTypeKey, await ResolveFamilyAsync(contentTypeKey), cancellationToken);
    }

    /// <inheritdoc />
    public async Task SaveManyAsync(
        IReadOnlyList<(Guid NodeKey, string RoleAlias, Guid ContentTypeKey, IReadOnlyList<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries, string? ExpectedStamp)> batch,
        CancellationToken cancellationToken = default)
    {
        // Nothing was written, so nothing is stale — skip the transaction and the L2 flush.
        if (batch.Count == 0)
        {
            return;
        }

        // The stamp check happens inside the repository's write transaction. A stale stamp throws
        // from here, before any invalidation or publish: nothing was written, so there is nothing
        // to invalidate and nothing to announce.
        await repository.SaveManyAsync(
            batch.Select(b => (b.NodeKey, b.RoleAlias, b.ContentTypeKey,
                (IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)>)b.Entries,
                b.ExpectedStamp)),
            cancellationToken);

        // Invalidated once per distinct user group rather than per triple, and the resolved (L2)
        // cache once for the whole batch: L2 is dropped wholesale anyway.
        foreach (var roleAlias in batch.Select(b => b.RoleAlias).Distinct(StringComparer.Ordinal))
        {
            cache.InvalidateRoleEntries(roleAlias);
        }

        cache.InvalidateAllResolved();

        // Each distinct content type is looked up once, however many nodes the batch touches it on.
        var families = new Dictionary<Guid, DocTypePermissionFamily>();
        foreach (var contentTypeKey in batch.Select(b => b.ContentTypeKey).Distinct())
        {
            families[contentTypeKey] = await ResolveFamilyAsync(contentTypeKey);
        }

        // Again strictly after invalidation, and one notification per triple so a handler sees the
        // same granularity it would from a single save. Each publish is isolated individually so one
        // bad triple's handler failure does not stop the remaining triples from being announced.
        foreach (var (nodeKey, roleAlias, contentTypeKey, _, _) in batch)
        {
            await PublishChangedAsync(nodeKey, roleAlias, contentTypeKey, families[contentTypeKey], cancellationToken);
        }
    }

    /// <summary>
    /// Works out whether a content type is a document type or an element type, for the change
    /// notification's family discriminator. Uses the content type itself rather than the verbs written,
    /// because the save endpoint accepts either verb family for any content type and a save that clears
    /// every entry carries no verbs at all.
    /// </summary>
    /// <remarks>
    /// The rule is <c>IsElement</c>, the same one the pickers apply. Any failure to find out — the type
    /// is gone, the lookup returns nothing, or it throws — yields
    /// <see cref="DocTypePermissionFamily.Unknown"/>, which consumers treat as "wake both": a spurious
    /// wake costs one refetch that reconciles to no change, whereas a missed wake is a silently stale
    /// screen. A throw is contained rather than propagated because by now the write has committed, and a
    /// failed lookup must not turn it into a reported failure.
    /// </remarks>
    /// <param name="contentTypeKey">The content type whose family to resolve.</param>
    /// <returns>The content type's family, or <see cref="DocTypePermissionFamily.Unknown"/>.</returns>
    private async Task<DocTypePermissionFamily> ResolveFamilyAsync(Guid contentTypeKey)
    {
        try
        {
            var contentType = await contentTypeService.GetAsync(contentTypeKey);

            return contentType switch
            {
                null => DocTypePermissionFamily.Unknown,
                { IsElement: true } => DocTypePermissionFamily.Element,
                _ => DocTypePermissionFamily.Document,
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Could not resolve whether content type {ContentTypeKey} is a document type or an element type; " +
                "its change notification will be published with an unknown family, which wakes every consumer.",
                contentTypeKey);
            return DocTypePermissionFamily.Unknown;
        }
    }

    /// <summary>
    /// Publishes the doc-type change notification for one triple, isolating any handler failure so it
    /// cannot mask a write that has already committed. Must only be called after the cache has been
    /// invalidated.
    /// </summary>
    /// <param name="nodeKey">The node the entries were written for.</param>
    /// <param name="roleAlias">The user group alias the entries were written for.</param>
    /// <param name="contentTypeKey">The document or element type the entries were written for.</param>
    /// <param name="family">Whether <paramref name="contentTypeKey"/> is a document or element type.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>A task that completes once the notification has been published or its failure logged.</returns>
    private Task PublishChangedAsync(
        Guid nodeKey,
        string roleAlias,
        Guid contentTypeKey,
        DocTypePermissionFamily family,
        CancellationToken cancellationToken) =>
        PermissionChangePublisher.PublishSafelyAsync(
            eventAggregator,
            logger,
            new DocTypePermissionsChangedNotification(nodeKey, roleAlias, contentTypeKey, family),
            $"node {nodeKey}, user group {roleAlias} and content type {contentTypeKey}",
            cancellationToken);

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
