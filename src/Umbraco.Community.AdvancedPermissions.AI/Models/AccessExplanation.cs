namespace Umbraco.Community.AdvancedPermissions.AI.Models;

/// <summary>
/// Why a single verdict was reached. Every field is a friendly, editor-facing label —
/// there are no raw role aliases, verb identifiers, enum names, or node GUIDs.
/// </summary>
/// <param name="Role">The friendly role name that contributed this decision (e.g. "Editors", "All Users").</param>
/// <param name="Decision">The friendly decision contributed by this role ("Allowed" or "Denied").</param>
/// <param name="Scope">The friendly scope of the contributing entry (e.g. "This node only").</param>
/// <param name="SetOn">The friendly name of the node the contributing entry was set on.</param>
/// <param name="Inherited">
/// <see langword="true"/> when this decision was inherited from an ancestor or a group default;
/// <see langword="false"/> when it was set directly on the evaluated node.
/// </param>
/// <param name="PriorityOverride">
/// <see langword="true"/> when the contributing entry used the priority-override ("!important") path.
/// </param>
public sealed record AccessReason(
    string Role,
    string Decision,
    string Scope,
    string SetOn,
    bool Inherited,
    bool PriorityOverride);

/// <summary>
/// One action's friendly verdict at a content node: whether it is allowed or denied, and why.
/// </summary>
/// <param name="Action">The friendly action name (e.g. "Delete", "Publish").</param>
/// <param name="Result">The friendly result ("Allowed" or "Denied").</param>
/// <param name="Reasons">The ordered friendly reasons that led to the result, highest priority first.</param>
/// <param name="Remediations">
/// When access is <c>Denied</c> and remediation was requested, the confirmed permission changes that
/// would make it <c>Allowed</c> — each one already validated by re-resolving the pure resolver against
/// the change, ranked least-privileged-first. <see langword="null"/> when remediation was not requested,
/// the verdict is <c>Allowed</c>, or no change could flip it.
/// </param>
public sealed record AccessVerdict(
    string Action,
    string Result,
    IReadOnlyList<AccessReason> Reasons,
    IReadOnlyList<AccessRemediation>? Remediations = null);

/// <summary>
/// A friendly access explanation for a content node: the node name plus one verdict per action.
/// Surfaced to editors verbatim, so it contains no raw identifiers.
/// </summary>
/// <param name="Node">The friendly name of the content node the access was evaluated at.</param>
/// <param name="Permissions">One friendly verdict per evaluated action.</param>
public sealed record AccessExplanation(
    string Node,
    IReadOnlyList<AccessVerdict> Permissions);
