using Umbraco.Cms.Core.Cache;
using Umbraco.Extensions;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.Caching;

/// <summary>
/// Two-level cache for node-keyed advanced security permission data, shared by every node-based
/// permission target (content documents, library elements/folders). Each target supplies its own
/// key prefix so the L1/L2 stores are isolated.
/// </summary>
/// <remarks>
/// <para>
/// <b>Level 1 (L1)</b> — global, per-role entry cache:<br/>
/// Key: <c>{prefix}.L1.{roleAlias}</c> → all stored permission entries for that role (all nodes).<br/>
/// Loading all entries per role in one query means resolution walks in-memory, not per-request to the database.
/// </para>
/// <para>
/// <b>Level 2 (L2)</b> — per-user+node resolved permission cache:<br/>
/// Key: <c>{prefix}.L2.{userKey}.{nodeKey}</c> → fully resolved permissions for all of the target's verbs.<br/>
/// Subsequent checks for the same user+node combination are O(1) lookups.
/// </para>
/// </remarks>
/// <param name="appCaches">The Umbraco application caches providing the runtime cache store.</param>
/// <param name="keyPrefix">
/// The cache-key prefix that isolates this target's entries from other targets, e.g. <c>UAP</c> for
/// content or <c>UAP.Element</c> for library elements. The full prefixes become
/// <c>{keyPrefix}.L1.</c> and <c>{keyPrefix}.L2.</c>.
/// </param>
public abstract class NodePermissionCache(AppCaches appCaches, string keyPrefix)
{
    private readonly string _l1Prefix = keyPrefix + ".L1.";
    private readonly string _l2Prefix = keyPrefix + ".L2.";

    /// <summary>L1 entries change infrequently — 30 minute safety-net TTL.</summary>
    private static readonly TimeSpan L1Ttl = TimeSpan.FromMinutes(30);

    /// <summary>L2 resolved results are more sensitive — 10 minute TTL.</summary>
    private static readonly TimeSpan L2Ttl = TimeSpan.FromMinutes(10);

    // ──────────────────────────────────────────
    // L1: stored entries per role (all nodes)
    // ──────────────────────────────────────────

    /// <summary>
    /// Gets all cached permission entries for a role, or <see langword="null"/> on a cache miss.
    /// </summary>
    /// <param name="roleAlias">The role alias to look up.</param>
    /// <returns>The cached entries, or <see langword="null"/> if not in cache.</returns>
    public IReadOnlyList<AdvancedPermissionEntry>? GetRoleEntries(string roleAlias) =>
        appCaches.RuntimeCache.GetCacheItem<IReadOnlyList<AdvancedPermissionEntry>>(_l1Prefix + roleAlias);

    /// <summary>
    /// Stores all permission entries for a role in the L1 cache.
    /// </summary>
    /// <param name="roleAlias">The role alias.</param>
    /// <param name="entries">The entries to cache.</param>
    public void SetRoleEntries(string roleAlias, IReadOnlyList<AdvancedPermissionEntry> entries) =>
        appCaches.RuntimeCache.InsertCacheItem(_l1Prefix + roleAlias, () => entries, L1Ttl);

    /// <summary>
    /// Removes the L1 cache entry for a specific role.
    /// Call this whenever entries for the role are saved or deleted.
    /// </summary>
    /// <param name="roleAlias">The role alias to invalidate.</param>
    public void InvalidateRoleEntries(string roleAlias) =>
        appCaches.RuntimeCache.Clear(_l1Prefix + roleAlias);

    /// <summary>
    /// Removes ALL L1 cache entries for all roles.
    /// </summary>
    public void InvalidateAllRoleEntries() =>
        appCaches.RuntimeCache.ClearByKey(_l1Prefix);

    // ──────────────────────────────────────────
    // L2: resolved permissions per user+node
    // ──────────────────────────────────────────

    /// <summary>
    /// Gets the cached resolved permissions for a specific user and node,
    /// or <see langword="null"/> on a cache miss.
    /// </summary>
    /// <param name="userKey">The user key.</param>
    /// <param name="nodeKey">The node key.</param>
    /// <returns>The cached resolved permissions, or <see langword="null"/> if not in cache.</returns>
    public IReadOnlyDictionary<string, EffectivePermission>? GetResolved(Guid userKey, Guid nodeKey) =>
        appCaches.RuntimeCache.GetCacheItem<IReadOnlyDictionary<string, EffectivePermission>>(
            $"{_l2Prefix}{userKey}.{nodeKey}");

    /// <summary>
    /// Stores the resolved permissions for a specific user and node in the L2 cache.
    /// </summary>
    /// <param name="userKey">The user key.</param>
    /// <param name="nodeKey">The node key.</param>
    /// <param name="resolved">The fully resolved permissions for all verbs.</param>
    public void SetResolved(Guid userKey, Guid nodeKey, IReadOnlyDictionary<string, EffectivePermission> resolved) =>
        appCaches.RuntimeCache.InsertCacheItem($"{_l2Prefix}{userKey}.{nodeKey}", () => resolved, L2Ttl);

    /// <summary>
    /// Removes all L2 cache entries for a specific user.
    /// Call this when the user's group membership changes.
    /// </summary>
    /// <param name="userKey">The user whose cached results to invalidate.</param>
    public void InvalidateUser(Guid userKey) =>
        appCaches.RuntimeCache.ClearByKey($"{_l2Prefix}{userKey}.");

    /// <summary>
    /// Removes ALL L2 cache entries for all users and nodes.
    /// Call this when permission entries change or the node structure changes.
    /// </summary>
    public void InvalidateAllResolved() =>
        appCaches.RuntimeCache.ClearByKey(_l2Prefix);
}
