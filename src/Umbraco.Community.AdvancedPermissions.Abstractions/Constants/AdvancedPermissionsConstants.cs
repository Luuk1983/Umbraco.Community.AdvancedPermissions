namespace Umbraco.Community.AdvancedPermissions.Core.Constants;

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
    public const string EveryoneRoleDisplayName = "All Users";

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
    /// Document-type-scoped verb gating whether a user may create instances of a specific
    /// document type. Distinct from <see cref="VerbCreate"/>, which is node-scoped and gates
    /// whether the Create menu is shown at all. Resolved with default state Allow — entries
    /// narrow the menu by exception.
    /// </summary>
    public const string VerbCreateOfType = "Umb.Document.CreateOfType";

    /// <summary>
    /// The default verbs granted to the Everyone role at the virtual root node (ThisNodeAndDescendants scope).
    /// Every user can read documents by default.
    /// </summary>
    public static readonly IReadOnlyList<string> EveryoneDefaultVerbs = [VerbRead];

    /// <summary>
    /// Verbs that live in the document-type permission table (keyed on both NodeKey and ContentTypeKey)
    /// rather than the node-level permission table. Used by the doc-type-permissions editor and audit.
    /// </summary>
    public static readonly IReadOnlyList<string> DocTypeVerbs = [VerbCreateOfType];

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

    // ── Library element verbs ─────────────────────────────────────────────────
    // Umbraco 18 splits library permissions into two resources — elements (Umb.Element.*) and
    // element folders/containers (Umb.ElementContainer.*). The package stores entries against the
    // canonical Umb.Element.* verbs; the folder enforcement adapter maps its container verbs onto the
    // canonical set (see ElementContainerVerbToCanonical) so a single stored entry serves both
    // resources along the shared element tree. The four element-only verbs (Publish/Unpublish/
    // Duplicate/Rollback) have no folder counterpart.

    /// <summary>Element verb: read/browse a library element.</summary>
    public const string VerbElementRead = "Umb.Element.Read";

    /// <summary>Element verb: create a library element (evaluated at the parent location).</summary>
    public const string VerbElementCreate = "Umb.Element.Create";

    /// <summary>Element verb: update/save a library element.</summary>
    public const string VerbElementUpdate = "Umb.Element.Update";

    /// <summary>Element verb: delete a library element.</summary>
    public const string VerbElementDelete = "Umb.Element.Delete";

    /// <summary>Element verb: publish a library element.</summary>
    public const string VerbElementPublish = "Umb.Element.Publish";

    /// <summary>Element verb: unpublish a library element.</summary>
    public const string VerbElementUnpublish = "Umb.Element.Unpublish";

    /// <summary>Element verb: duplicate (copy) a library element.</summary>
    public const string VerbElementDuplicate = "Umb.Element.Duplicate";

    /// <summary>Element verb: move a library element.</summary>
    public const string VerbElementMove = "Umb.Element.Move";

    /// <summary>Element verb: roll a library element back to a previous version.</summary>
    public const string VerbElementRollback = "Umb.Element.Rollback";

    /// <summary>
    /// The canonical set of verbs managed for library elements and folders (the nine
    /// <c>Umb.Element.*</c> verbs). Resolution always computes and caches every verb in this set.
    /// </summary>
    public static readonly IReadOnlyList<string> ElementVerbs =
    [
        VerbElementRead,
        VerbElementCreate,
        VerbElementUpdate,
        VerbElementDelete,
        VerbElementPublish,
        VerbElementUnpublish,
        VerbElementDuplicate,
        VerbElementMove,
        VerbElementRollback,
    ];

    /// <summary>
    /// The default element verbs granted to the Everyone role at the virtual root node
    /// (ThisNodeAndDescendants scope). Every user can read library elements (and, via the canonical
    /// mapping, folders) by default.
    /// </summary>
    public static readonly IReadOnlyList<string> EveryoneDefaultElementVerbs = [VerbElementRead];

    /// <summary>Element-container (folder) verb: read/browse a folder. Maps to <see cref="VerbElementRead"/>.</summary>
    public const string VerbElementContainerRead = "Umb.ElementContainer.Read";

    /// <summary>Element-container (folder) verb: create a child folder. Maps to <see cref="VerbElementCreate"/>.</summary>
    public const string VerbElementContainerCreate = "Umb.ElementContainer.Create";

    /// <summary>Element-container (folder) verb: update/rename a folder. Maps to <see cref="VerbElementUpdate"/>.</summary>
    public const string VerbElementContainerUpdate = "Umb.ElementContainer.Update";

    /// <summary>Element-container (folder) verb: delete a folder. Maps to <see cref="VerbElementDelete"/>.</summary>
    public const string VerbElementContainerDelete = "Umb.ElementContainer.Delete";

    /// <summary>Element-container (folder) verb: move a folder. Maps to <see cref="VerbElementMove"/>.</summary>
    public const string VerbElementContainerMove = "Umb.ElementContainer.Move";

    /// <summary>
    /// Maps each native element-container (folder) verb to the canonical <c>Umb.Element.*</c> verb the
    /// package stores. The folder enforcement adapter translates an incoming container verb to its
    /// canonical equivalent before resolving, so one stored entry governs both the folder and the
    /// elements beneath it.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> ElementContainerVerbToCanonical =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [VerbElementContainerRead] = VerbElementRead,
            [VerbElementContainerCreate] = VerbElementCreate,
            [VerbElementContainerUpdate] = VerbElementUpdate,
            [VerbElementContainerDelete] = VerbElementDelete,
            [VerbElementContainerMove] = VerbElementMove,
        };

    /// <summary>
    /// Element-type-scoped verb gating whether a user may create an instance of a specific element type
    /// in the Library. The library analogue of <see cref="VerbCreateOfType"/>; resolved with default
    /// state Allow (entries narrow the create options by exception). Library element-type filtering is
    /// section-global (Umbraco supplies no parent context), so entries live on the virtual root.
    /// </summary>
    public const string VerbElementCreateOfType = "Umb.Element.CreateOfType";

    /// <summary>
    /// Verbs that gate element-type creation in the Library. Stored in the doc-type permission table
    /// (keyed on NodeKey + ContentTypeKey) alongside <see cref="DocTypeVerbs"/>; the verb string keeps
    /// the two targets distinct.
    /// </summary>
    public static readonly IReadOnlyList<string> ElementTypeVerbs = [VerbElementCreateOfType];
}
