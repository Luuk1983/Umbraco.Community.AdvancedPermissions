namespace Umbraco.Community.AdvancedPermissions.AI.Models;

/// <summary>
/// A friendly "who can do this action here" roster for a single action at a content node: which roles
/// are allowed and which are denied. Every field is an editor-facing label — there are no raw verb
/// identifiers, role aliases, or node GUIDs, so the answer surfaced to an editor never leaks code
/// identifiers.
/// </summary>
/// <param name="Action">The friendly action that was evaluated, e.g. <c>Publish</c>.</param>
/// <param name="Node">The friendly name of the content node the action was evaluated at.</param>
/// <param name="AllowedRoles">The display names of roles whose effective permission for the action is allowed.</param>
/// <param name="DeniedRoles">The display names of roles whose effective permission for the action is denied.</param>
public sealed record AccessRoster(
    string Action,
    string Node,
    IReadOnlyList<string> AllowedRoles,
    IReadOnlyList<string> DeniedRoles);

/// <summary>
/// A friendly roster report covering several actions at a content node: one <see cref="AccessRoster"/>
/// per evaluated action. Used when the all-roles explanation is asked for every action at once.
/// </summary>
/// <param name="Node">The friendly name of the content node the actions were evaluated at.</param>
/// <param name="Actions">One allowed/denied roster per evaluated action.</param>
public sealed record AccessRosterReport(
    string Node,
    IReadOnlyList<AccessRoster> Actions);
