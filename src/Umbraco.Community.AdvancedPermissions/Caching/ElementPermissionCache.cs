using Umbraco.Cms.Core.Cache;

namespace Umbraco.Community.AdvancedPermissions.Caching;

/// <summary>
/// Two-level cache for library element/folder advanced security permission data. Uses the
/// <c>UAP.Element</c> key prefix (L1 keys <c>UAP.Element.L1.*</c>, L2 keys <c>UAP.Element.L2.*</c>),
/// keeping it isolated from the content cache. All behaviour comes from <see cref="NodePermissionCache"/>.
/// </summary>
/// <param name="appCaches">The Umbraco application caches providing the runtime cache store.</param>
public sealed class ElementPermissionCache(AppCaches appCaches)
    : NodePermissionCache(appCaches, "UAP.Element");
