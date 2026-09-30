namespace Umbraco.Community.AdvancedPermissions.Notifications;

/// <summary>
/// Which kind of content type a <see cref="DocTypePermissionsChangedNotification"/> concerns, so a
/// consumer can wake only the surfaces that show that kind.
/// </summary>
public enum DocTypePermissionFamily
{
    /// <summary>
    /// The family could not be determined, because the content type no longer exists or the lookup
    /// returned nothing or failed. This is the default value on purpose, and consumers must treat it as
    /// "wake both": a spurious wake costs one refetch that reconciles to no change, whereas a missed
    /// wake leaves a screen silently stale.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// A document type (a content type that is not an element type), governed by
    /// <c>Umb.Document.CreateOfType</c>.
    /// </summary>
    Document = 1,

    /// <summary>
    /// An element type (a content type flagged as an element type), governed by
    /// <c>Umb.Element.CreateOfType</c>. This follows <c>IsElement</c> alone, the same rule the pickers
    /// use to tell element types from document types.
    /// </summary>
    Element = 2,
}
