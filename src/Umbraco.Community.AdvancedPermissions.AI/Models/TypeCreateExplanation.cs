namespace Umbraco.Community.AdvancedPermissions.AI.Models;

/// <summary>
/// One document type's friendly create ("Insert Options") verdict under a parent node: whether the
/// subject may create that type, and why. Every field is an editor-facing label — there are no raw
/// content-type aliases/GUIDs, role aliases, verb identifiers, or node GUIDs.
/// </summary>
/// <param name="DocumentType">The friendly display name of the candidate document type (e.g. "Article").</param>
/// <param name="Result">
/// The friendly result: "Allowed" (may be created), "Denied" (blocked by a permission entry), or
/// "Not applicable" (structurally not an allowed child of the parent, regardless of permissions).
/// </param>
/// <param name="Reasons">
/// The ordered friendly reasons that led to the result, highest priority first. Empty for a purely
/// structural "Not applicable" verdict, or when creation is allowed by default with no narrowing entry.
/// </param>
public sealed record TypeCreateVerdict(
    string DocumentType,
    string Result,
    IReadOnlyList<AccessReason> Reasons);

/// <summary>
/// A friendly "what document types can be created here / why can't I create type X here" explanation
/// for a single subject at a parent node. Surfaced to editors verbatim, so it contains no raw
/// identifiers.
/// </summary>
/// <param name="Node">The friendly name of the parent node under which creation was evaluated.</param>
/// <param name="DocumentTypes">One friendly verdict per evaluated document type.</param>
public sealed record TypeCreateExplanation(
    string Node,
    IReadOnlyList<TypeCreateVerdict> DocumentTypes);

/// <summary>
/// A friendly "who can create this document type here" roster for a single document type under a parent
/// node: which roles are allowed and which are denied. Every field is an editor-facing label, so the
/// answer surfaced to an editor never leaks code identifiers.
/// </summary>
/// <param name="DocumentType">The friendly display name of the candidate document type.</param>
/// <param name="Node">The friendly name of the parent node under which creation was evaluated.</param>
/// <param name="IsApplicable">
/// <see langword="false"/> when the document type is structurally not an allowed child of the parent
/// (regardless of permissions); the allowed/denied rosters are empty in that case.
/// </param>
/// <param name="AllowedRoles">The display names of roles whose effective create permission for the type is allowed.</param>
/// <param name="DeniedRoles">The display names of roles whose effective create permission for the type is denied.</param>
public sealed record TypeCreateRoster(
    string DocumentType,
    string Node,
    bool IsApplicable,
    IReadOnlyList<string> AllowedRoles,
    IReadOnlyList<string> DeniedRoles);

/// <summary>
/// A friendly roster report covering several document types under a parent node: one
/// <see cref="TypeCreateRoster"/> per evaluated document type. Used for the all-roles type-create
/// roster (who can create each type here).
/// </summary>
/// <param name="Node">The friendly name of the parent node under which creation was evaluated.</param>
/// <param name="DocumentTypes">One allowed/denied roster per evaluated document type.</param>
public sealed record TypeCreateRosterReport(
    string Node,
    IReadOnlyList<TypeCreateRoster> DocumentTypes);
