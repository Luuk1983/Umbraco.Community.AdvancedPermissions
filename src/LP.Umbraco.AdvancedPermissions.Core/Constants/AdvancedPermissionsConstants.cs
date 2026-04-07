namespace LP.Umbraco.AdvancedPermissions.Core.Constants;

/// <summary>
/// Constants used throughout the Advanced Security package.
/// </summary>
public static class AdvancedPermissionsConstants
{
    /// <summary>
    /// A sentinel <see cref="Guid"/> used as the <c>NodeKey</c> for virtual-root (default) permission entries.
    /// Replaces <see langword="null"/> to make the column non-nullable and eliminate the risk of accidental null matches.
    /// All-ones is visually distinct and will never collide with a real Umbraco content node key (UUIDv4).
    /// </summary>
    public static readonly Guid VirtualRootNodeKey = Guid.Parse("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF");

    /// <summary>
    /// The alias used to represent the virtual "Everyone" role.
    /// This role is implicitly assigned to every user regardless of their group membership.
    /// </summary>
    public const string EveryoneRoleAlias = "$everyone";

    /// <summary>
    /// The display name shown in the UI for the Everyone role.
    /// </summary>
    public const string EveryoneRoleDisplayName = "Umbraco Users";

    /// <summary>
    /// Standard Umbraco permission verb for reading/browsing a document.
    /// </summary>
    public const string VerbRead = "Umb.Document.Read";

    /// <summary>
    /// Standard Umbraco permission verb for creating a document.
    /// </summary>
    public const string VerbCreate = "Umb.Document.Create";

    /// <summary>
    /// Standard Umbraco permission verb for updating a document.
    /// </summary>
    public const string VerbUpdate = "Umb.Document.Update";

    /// <summary>
    /// Standard Umbraco permission verb for deleting a document.
    /// </summary>
    public const string VerbDelete = "Umb.Document.Delete";

    /// <summary>
    /// Standard Umbraco permission verb for publishing a document.
    /// </summary>
    public const string VerbPublish = "Umb.Document.Publish";

    /// <summary>
    /// Standard Umbraco permission verb for unpublishing a document.
    /// </summary>
    public const string VerbUnpublish = "Umb.Document.Unpublish";

    /// <summary>
    /// Standard Umbraco permission verb for duplicating (copying) a document.
    /// </summary>
    public const string VerbDuplicate = "Umb.Document.Duplicate";

    /// <summary>
    /// Standard Umbraco permission verb for moving a document (also used for restoring from recycle bin).
    /// </summary>
    public const string VerbMove = "Umb.Document.Move";

    /// <summary>
    /// Standard Umbraco permission verb for sorting child documents.
    /// </summary>
    public const string VerbSort = "Umb.Document.Sort";

    /// <summary>
    /// Standard Umbraco permission verb for creating a document blueprint (template).
    /// </summary>
    public const string VerbCreateBlueprint = "Umb.Document.CreateBlueprint";

    /// <summary>
    /// Standard Umbraco permission verb for managing document notifications.
    /// </summary>
    public const string VerbNotifications = "Umb.Document.Notifications";

    /// <summary>
    /// Standard Umbraco permission verb for assigning culture and hostnames to a document.
    /// </summary>
    public const string VerbCultureAndHostnames = "Umb.Document.CultureAndHostnames";

    /// <summary>
    /// Standard Umbraco permission verb for managing public access protection on a document.
    /// </summary>
    public const string VerbPublicAccess = "Umb.Document.PublicAccess";

    /// <summary>
    /// Standard Umbraco permission verb for rolling back a document to a previous version.
    /// </summary>
    public const string VerbRollback = "Umb.Document.Rollback";

    /// <summary>
    /// Standard Umbraco permission verb for managing permissions on a document.
    /// </summary>
    public const string VerbManagePermissions = "Umb.Document.Permissions";

    /// <summary>
    /// The default verbs granted to the Everyone role at the virtual root node (ThisNodeAndDescendants scope).
    /// Every user can read documents by default.
    /// </summary>
    public static readonly IReadOnlyList<string> EveryoneDefaultVerbs = [VerbRead];

    /// <summary>
    /// All standard permission verbs defined by the Advanced Security package.
    /// Used when resolving effective permissions for all verbs at once.
    /// </summary>
    public static readonly IReadOnlyList<string> AllVerbs =
    [
        VerbRead,
        VerbCreate,
        VerbUpdate,
        VerbDelete,
        VerbPublish,
        VerbUnpublish,
        VerbDuplicate,
        VerbMove,
        VerbSort,
        VerbCreateBlueprint,
        VerbNotifications,
        VerbCultureAndHostnames,
        VerbPublicAccess,
        VerbRollback,
        VerbManagePermissions,
    ];
}
