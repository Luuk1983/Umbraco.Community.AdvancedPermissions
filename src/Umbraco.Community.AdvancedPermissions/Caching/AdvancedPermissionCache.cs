using Umbraco.Cms.Core.Cache;

namespace Umbraco.Community.AdvancedPermissions.Caching;

/// <summary>
/// Two-level cache for content-node advanced security permission data. Uses the <c>UAP</c> key prefix
/// (L1 keys <c>UAP.L1.*</c>, L2 keys <c>UAP.L2.*</c>). All behaviour comes from
/// <see cref="NodePermissionCache"/>.
/// </summary>
/// <param name="appCaches">The Umbraco application caches providing the runtime cache store.</param>
public sealed class AdvancedPermissionCache(AppCaches appCaches)
    : NodePermissionCache(appCaches, "UAP");
