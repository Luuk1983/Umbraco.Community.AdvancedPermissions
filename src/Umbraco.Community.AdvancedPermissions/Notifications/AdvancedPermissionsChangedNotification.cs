using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Community.AdvancedPermissions.Notifications;

/// <summary>
/// Published after advanced permission entries for one node and user group have been written and
/// the caches for them invalidated.
/// </summary>
/// <remarks>
/// This is the package's public extensibility seam for permission writes: another package can
/// handle it to audit, mirror or react to a change without taking a dependency on how the write
/// happened. The package's own server-event handler is one such consumer, not a special case.
/// </remarks>
/// <param name="NodeKey">
/// The content node key. <c>AdvancedPermissionsConstants.VirtualRootNodeKey</c> indicates the
/// virtual root.
/// </param>
/// <param name="RoleAlias">The user group alias, or <c>$everyone</c> for the All Users Group.</param>
/// <param name="Verbs">The verbs whose entries were written. Empty when all entries were removed.</param>
public sealed record AdvancedPermissionsChangedNotification(
    Guid NodeKey,
    string RoleAlias,
    IReadOnlyList<string> Verbs) : INotification;
