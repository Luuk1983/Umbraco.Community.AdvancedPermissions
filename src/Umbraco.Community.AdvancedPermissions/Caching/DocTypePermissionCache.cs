using Umbraco.Cms.Core.Cache;
using Umbraco.Extensions;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.Caching;

/// <summary>
/// Two-level cache for document-type permission entries. Mirrors the L1/L2 pattern used by
/// <see cref="AdvancedPermissionCache"/> but with cache keys scoped to doc-type entries so the
/// two caches don't collide.
/// </summary>
/// <remarks>
/// <para>
/// <b>Level 1 (L1)</b> — per-role entries:<br/>
/// Key: <c>UAP.DocType.L1.{roleAlias}</c> → all stored doc-type entries for that role.
/// </para>
/// <para>
/// <b>Level 2 (L2)</b> — resolved per-(user, parent, content-type) result:<br/>
/// Key: <c>UAP.DocType.L2.{userKey}.{parentNodeKey}.{contentTypeKey}</c> → the boolean outcome of
/// the resolver call. The Create-button-filter calls the resolver once per candidate type per
/// click, so caching the boolean keeps the inner loop O(1).
/// </para>
/// </remarks>
/// <param name="appCaches">Umbraco's application cache facade.</param>
public sealed class DocTypePermissionCache(AppCaches appCaches)
{
    private const string L1Prefix = "UAP.DocType.L1.";
    private const string L2Prefix = "UAP.DocType.L2.";

    private static readonly TimeSpan L1Ttl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan L2Ttl = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Gets all cached doc-type entries for a role, or <see langword="null"/> on a cache miss.
    /// </summary>
    /// <param name="roleAlias">The role to look up.</param>
    /// <returns>The cached entries, or <see langword="null"/>.</returns>
    public IReadOnlyList<DocTypePermissionEntry>? GetRoleEntries(string roleAlias) =>
        appCaches.RuntimeCache.GetCacheItem<IReadOnlyList<DocTypePermissionEntry>>(L1Prefix + roleAlias);

    /// <summary>
    /// Stores all doc-type entries for a role in the L1 cache.
    /// </summary>
    /// <param name="roleAlias">The role alias.</param>
    /// <param name="entries">The entries to cache.</param>
    public void SetRoleEntries(string roleAlias, IReadOnlyList<DocTypePermissionEntry> entries) =>
        appCaches.RuntimeCache.InsertCacheItem(L1Prefix + roleAlias, () => entries, L1Ttl);

    /// <summary>
    /// Invalidates the L1 cache entry for one role.
    /// </summary>
    /// <param name="roleAlias">The role to invalidate.</param>
    public void InvalidateRoleEntries(string roleAlias) =>
        appCaches.RuntimeCache.Clear(L1Prefix + roleAlias);

    /// <summary>
    /// Invalidates ALL L1 entries.
    /// </summary>
    public void InvalidateAllRoleEntries() =>
        appCaches.RuntimeCache.ClearByKey(L1Prefix);

    /// <summary>
    /// Gets the cached resolved result for a (user, parent, content-type), or <see langword="null"/>.
    /// </summary>
    /// <param name="userKey">The user key.</param>
    /// <param name="parentNodeKey">The parent node key.</param>
    /// <param name="contentTypeKey">The candidate content-type key.</param>
    /// <returns>The cached <see cref="EffectivePermission"/>, or <see langword="null"/>.</returns>
    public EffectivePermission? GetResolved(Guid userKey, Guid parentNodeKey, Guid contentTypeKey) =>
        appCaches.RuntimeCache.GetCacheItem<EffectivePermission>(
            $"{L2Prefix}{userKey}.{parentNodeKey}.{contentTypeKey}");

    /// <summary>
    /// Stores the resolved result for a (user, parent, content-type).
    /// </summary>
    /// <param name="userKey">The user key.</param>
    /// <param name="parentNodeKey">The parent node key.</param>
    /// <param name="contentTypeKey">The candidate content-type key.</param>
    /// <param name="resolved">The result to cache.</param>
    public void SetResolved(Guid userKey, Guid parentNodeKey, Guid contentTypeKey, EffectivePermission resolved) =>
        appCaches.RuntimeCache.InsertCacheItem(
            $"{L2Prefix}{userKey}.{parentNodeKey}.{contentTypeKey}",
            () => resolved,
            L2Ttl);

    /// <summary>
    /// Invalidates all L2 entries for one user.
    /// </summary>
    /// <param name="userKey">The user key.</param>
    public void InvalidateUser(Guid userKey) =>
        appCaches.RuntimeCache.ClearByKey($"{L2Prefix}{userKey}.");

    /// <summary>
    /// Invalidates ALL L2 entries.
    /// </summary>
    public void InvalidateAllResolved() =>
        appCaches.RuntimeCache.ClearByKey(L2Prefix);
}
