using Umbraco.Cms.Core.Cache;
using Umbraco.Extensions;
using LP.Umbraco.AdvancedPermissions.Core.Models;

namespace LP.Umbraco.AdvancedPermissions.Caching;

/// <summary>
/// Two-level cache for advanced security permission data.
/// </summary>
/// <remarks>
/// <para>
/// <b>Level 1 (L1)</b> — global, per-role entry cache:<br/>
/// Key: <c>UAS.L1.{roleAlias}</c> → all stored permission entries for that role (all nodes).<br/>
/// Loading all entries per role in one query means resolution walks in-memory, not per-request to the database.
/// </para>
/// <para>
/// <b>Level 2 (L2)</b> — per-user+node resolved permission cache:<br/>
/// Key: <c>UAS.L2.{userKey}.{nodeKey}</c> → fully resolved permissions for all verbs.<br/>
/// Subsequent checks for the same user+node combination are O(1) lookups.
/// </para>
/// </remarks>
/// <param name="appCaches">The Umbraco application caches providing the runtime cache store.</param>
public sealed class AdvancedPermissionCache(AppCaches appCaches)
{
    private const string L1Prefix = "UAP.L1.";
    private const string L2Prefix = "UAP.L2.";

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
        appCaches.RuntimeCache.GetCacheItem<IReadOnlyList<AdvancedPermissionEntry>>(L1Prefix + roleAlias);

    /// <summary>
    /// Stores all permission entries for a role in the L1 cache.
    /// </summary>
    /// <param name="roleAlias">The role alias.</param>
    /// <param name="entries">The entries to cache.</param>
    public void SetRoleEntries(string roleAlias, IReadOnlyList<AdvancedPermissionEntry> entries) =>
        appCaches.RuntimeCache.InsertCacheItem(L1Prefix + roleAlias, () => entries, L1Ttl);

    /// <summary>
    /// Removes the L1 cache entry for a specific role.
    /// Call this whenever entries for the role are saved or deleted.
    /// </summary>
    /// <param name="roleAlias">The role alias to invalidate.</param>
    public void InvalidateRoleEntries(string roleAlias) =>
        appCaches.RuntimeCache.Clear(L1Prefix + roleAlias);

    /// <summary>
    /// Removes ALL L1 cache entries for all roles.
    /// </summary>
    public void InvalidateAllRoleEntries() =>
        appCaches.RuntimeCache.ClearByKey(L1Prefix);

    // ──────────────────────────────────────────
    // L2: resolved permissions per user+node
    // ──────────────────────────────────────────

    /// <summary>
    /// Gets the cached resolved permissions for a specific user and node,
    /// or <see langword="null"/> on a cache miss.
    /// </summary>
    /// <param name="userKey">The user key.</param>
    /// <param name="nodeKey">The content node key.</param>
    /// <returns>The cached resolved permissions, or <see langword="null"/> if not in cache.</returns>
    public IReadOnlyDictionary<string, EffectivePermission>? GetResolved(Guid userKey, Guid nodeKey) =>
        appCaches.RuntimeCache.GetCacheItem<IReadOnlyDictionary<string, EffectivePermission>>(
            $"{L2Prefix}{userKey}.{nodeKey}");

    /// <summary>
    /// Stores the resolved permissions for a specific user and node in the L2 cache.
    /// </summary>
    /// <param name="userKey">The user key.</param>
    /// <param name="nodeKey">The content node key.</param>
    /// <param name="resolved">The fully resolved permissions for all verbs.</param>
    public void SetResolved(Guid userKey, Guid nodeKey, IReadOnlyDictionary<string, EffectivePermission> resolved) =>
        appCaches.RuntimeCache.InsertCacheItem($"{L2Prefix}{userKey}.{nodeKey}", () => resolved, L2Ttl);

    /// <summary>
    /// Removes all L2 cache entries for a specific user.
    /// Call this when the user's group membership changes.
    /// </summary>
    /// <param name="userKey">The user whose cached results to invalidate.</param>
    public void InvalidateUser(Guid userKey) =>
        appCaches.RuntimeCache.ClearByKey($"{L2Prefix}{userKey}.");

    /// <summary>
    /// Removes ALL L2 cache entries for all users and nodes.
    /// Call this when permission entries change or content structure changes.
    /// </summary>
    public void InvalidateAllResolved() =>
        appCaches.RuntimeCache.ClearByKey(L2Prefix);
}
