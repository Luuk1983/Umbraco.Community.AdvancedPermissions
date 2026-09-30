using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Community.AdvancedPermissions.Notifications;

/// <summary>
/// Published after library element permission entries for one element or folder and user group have
/// been written and the caches for them invalidated.
/// </summary>
/// <remarks>
/// The library counterpart of <see cref="AdvancedPermissionsChangedNotification"/>. It is a distinct
/// type on purpose: the two families share one service implementation, but a handler that refreshes
/// the content editors must not be woken by a library save, nor the reverse. Published by the shared
/// node-permission service strictly after the write has committed and the cache has been invalidated,
/// from both the single-node save and the batch save.
/// </remarks>
/// <param name="NodeKey">
/// The element or folder key. <c>AdvancedPermissionsConstants.VirtualRootNodeKey</c> indicates the
/// virtual root.
/// </param>
/// <param name="RoleAlias">The user group alias, or <c>$everyone</c> for the All Users Group.</param>
/// <param name="Verbs">The verbs whose entries were written. Empty when all entries were removed.</param>
public sealed record ElementPermissionsChangedNotification(
    Guid NodeKey,
    string RoleAlias,
    IReadOnlyList<string> Verbs) : INotification;
