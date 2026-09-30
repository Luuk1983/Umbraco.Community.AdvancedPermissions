using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Community.AdvancedPermissions.Notifications;

/// <summary>
/// Published after document-type create permissions for one node and user group have been written
/// and the caches for them invalidated.
/// </summary>
/// <remarks>
/// The document-type counterpart of <see cref="AdvancedPermissionsChangedNotification"/>.
/// Published by <c>DocTypePermissionService</c> strictly after the write has committed and the
/// cache has been invalidated, from both the single-triple save and the batch save.
/// </remarks>
/// <param name="NodeKey">The content node key the rule is anchored to.</param>
/// <param name="RoleAlias">The user group alias, or <c>$everyone</c> for the All Users Group.</param>
/// <param name="ContentTypeKeys">The document types whose entries were written.</param>
public sealed record DocTypePermissionsChangedNotification(
    Guid NodeKey,
    string RoleAlias,
    IReadOnlyList<Guid> ContentTypeKeys) : INotification;
