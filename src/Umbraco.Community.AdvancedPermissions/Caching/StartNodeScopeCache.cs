using Umbraco.Cms.Core.Cache;
using Umbraco.Extensions;
using Umbraco.Community.AdvancedPermissions.Services.StartNodes;

namespace Umbraco.Community.AdvancedPermissions.Caching;

/// <summary>
/// Caches the start-node scope of a user or user group per tree, so the viewers, which ask the
/// server once per row, load a subject's start nodes once instead of once per row.
/// </summary>
/// <remarks>
/// Key: <c>UAP.StartNodes.{tree}.U.{userKey}</c> or <c>UAP.StartNodes.{tree}.G.{alias}</c>. The cache
/// is cleared as a whole by <c>StartNodeScopeCacheInvalidator</c> whenever start nodes or their paths
/// can change. The short absolute lifetime bounds anything the invalidation cannot see, such as a
/// move on another server of a load-balanced site.
/// </remarks>
/// <param name="appCaches">Umbraco's application cache facade.</param>
public sealed class StartNodeScopeCache(AppCaches appCaches)
{
    /// <summary>The prefix of every key this cache writes.</summary>
    private const string Prefix = "UAP.StartNodes.";

    /// <summary>How long a scope is kept before it is loaded again regardless of events.</summary>
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

    /// <summary>Gets a user's cached scope.</summary>
    /// <param name="userKey">The user's key.</param>
    /// <param name="tree">The tree.</param>
    /// <returns>The cached scope, or <see langword="null"/> on a miss.</returns>
    public StartNodeScope? GetForUser(Guid userKey, StartNodeTree tree) =>
        appCaches.RuntimeCache.GetCacheItem<StartNodeScope>(UserKey(userKey, tree));

    /// <summary>Stores a user's scope.</summary>
    /// <param name="userKey">The user's key.</param>
    /// <param name="scope">The scope; its tree is part of the key.</param>
    public void SetForUser(Guid userKey, StartNodeScope scope) =>
        appCaches.RuntimeCache.InsertCacheItem(UserKey(userKey, scope.Tree), () => scope, Ttl);

    /// <summary>Gets a user group's cached scope.</summary>
    /// <param name="userGroupAlias">The user group alias.</param>
    /// <param name="tree">The tree.</param>
    /// <returns>The cached scope, or <see langword="null"/> on a miss.</returns>
    public StartNodeScope? GetForUserGroup(string userGroupAlias, StartNodeTree tree) =>
        appCaches.RuntimeCache.GetCacheItem<StartNodeScope>(GroupKey(userGroupAlias, tree));

    /// <summary>Stores a user group's scope.</summary>
    /// <param name="userGroupAlias">The user group alias.</param>
    /// <param name="scope">The scope; its tree is part of the key.</param>
    public void SetForUserGroup(string userGroupAlias, StartNodeScope scope) =>
        appCaches.RuntimeCache.InsertCacheItem(GroupKey(userGroupAlias, scope.Tree), () => scope, Ttl);

    /// <summary>Forgets every cached scope.</summary>
    public void Clear() => appCaches.RuntimeCache.ClearByKey(Prefix);

    /// <summary>Builds the key of a user's scope.</summary>
    /// <param name="userKey">The user's key.</param>
    /// <param name="tree">The tree.</param>
    /// <returns>The cache key.</returns>
    private static string UserKey(Guid userKey, StartNodeTree tree) => $"{Prefix}{tree}.U.{userKey}";

    /// <summary>Builds the key of a user group's scope.</summary>
    /// <param name="userGroupAlias">The user group alias.</param>
    /// <param name="tree">The tree.</param>
    /// <returns>The cache key.</returns>
    private static string GroupKey(string userGroupAlias, StartNodeTree tree) => $"{Prefix}{tree}.G.{userGroupAlias}";
}
