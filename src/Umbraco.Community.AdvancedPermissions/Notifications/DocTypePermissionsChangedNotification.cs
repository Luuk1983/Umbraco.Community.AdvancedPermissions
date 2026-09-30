using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Community.AdvancedPermissions.Notifications;

/// <summary>
/// Published after document-type create permissions for one node and user group have been written
/// and the caches for them invalidated.
/// </summary>
/// <remarks>
/// <para>
/// The document-type counterpart of <see cref="AdvancedPermissionsChangedNotification"/>. Published by
/// <c>DocTypePermissionService</c> strictly after the write has committed and the cache has been
/// invalidated, from both the single-triple save and the batch save.
/// </para>
/// <para>
/// The <c>DocTypePermission</c> table holds both document types (<c>Umb.Document.CreateOfType</c>) and
/// Library element types (<c>Umb.Element.CreateOfType</c>), and several surfaces consume it. Deciding
/// which of them a change concerns has to happen here, at publish time, and is carried in
/// <paramref name="Family"/>: Umbraco's server event carries only an event type, a source and a single
/// key, so a consumer on the wire cannot inspect the content type to work it out for itself. The
/// family is resolved from the content type (whether it is an element type), not from the verbs
/// written, because the save endpoint accepts either verb family for any content type and a save that
/// clears every entry carries no verbs at all.
/// </para>
/// </remarks>
/// <param name="NodeKey">The content node key the rule is anchored to.</param>
/// <param name="RoleAlias">The user group alias, or <c>$everyone</c> for the All Users Group.</param>
/// <param name="ContentTypeKey">The document or element type whose entries were written.</param>
/// <param name="Family">
/// Whether <paramref name="ContentTypeKey"/> is a document type or an element type, or
/// <see cref="DocTypePermissionFamily.Unknown"/> when that could not be determined. Consumers must
/// treat <see cref="DocTypePermissionFamily.Unknown"/> as "concerns both".
/// </param>
public sealed record DocTypePermissionsChangedNotification(
    Guid NodeKey,
    string RoleAlias,
    Guid ContentTypeKey,
    DocTypePermissionFamily Family) : INotification;
