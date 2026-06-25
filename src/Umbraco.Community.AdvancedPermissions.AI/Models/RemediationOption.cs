using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Models;

/// <summary>
/// The kind of configuration change a <see cref="RemediationOption"/> represents, in
/// least-privileged-first order. Used both to rank options and to drive the presenter's
/// plain-language wording.
/// </summary>
public enum RemediationActionKind
{
    /// <summary>
    /// Remove the contributing explicit Deny entry (or entries) on the node so the verb is no longer
    /// blocked there. The cheapest, least-privileged fix when an explicit Deny is the cause.
    /// </summary>
    RemoveDeny,

    /// <summary>
    /// Add a plain explicit Allow entry on the node. Flips an implicit (inherited or default) Deny;
    /// it cannot beat an explicit same-node Deny.
    /// </summary>
    AddAllowOnNode,

    /// <summary>
    /// Add an Allow entry on the nearest contributing ancestor with a scope that reaches the node.
    /// Heavier than an on-node Allow because it widens access for the whole subtree below the ancestor.
    /// </summary>
    AddAllowOnAncestor,

    /// <summary>
    /// Add a priority-override Allow entry on the node. The only fix that beats an explicit same-node
    /// Deny, and the heaviest because it suppresses every other rule in its tier on that node.
    /// </summary>
    AddPriorityOverrideAllow,
}

/// <summary>
/// A single confirmed remediation: a concrete permission-configuration change that, when simulated
/// against the pure resolver on top of the current entries, flips the resolved verdict for one verb
/// from Denied to Allowed. Every option in a returned list has been validated by re-resolution — none
/// is a guess.
/// </summary>
/// <remarks>
/// This is the raw, engine-level shape carrying role aliases, the raw verb, scope/state enums, and node
/// GUIDs. It is projected to the friendly, editor-facing <see cref="AccessRemediation"/> by
/// <see cref="Services.IPermissionPresenter"/> before any of it reaches the copilot.
/// </remarks>
/// <param name="Kind">The kind of change, used for ranking and wording.</param>
/// <param name="RoleAlias">
/// The role alias the change targets. For a removal this is the role whose Deny entry is removed; for an
/// addition it is the role the new Allow is created for. For multi-role removals this is the first
/// contributing role (see <see cref="RemovedRoleAliases"/> for the complete set).
/// </param>
/// <param name="Verb">The single verb the change applies to.</param>
/// <param name="NodeKey">The node the change is made on (the target node, or a contributing ancestor for <see cref="RemediationActionKind.AddAllowOnAncestor"/>).</param>
/// <param name="Scope">
/// The scope of the entry to add. <see langword="null"/> for a removal, which targets whatever scope(s)
/// the contributing Deny entries already use.
/// </param>
/// <param name="RemovedRoleAliases">
/// For a <see cref="RemediationActionKind.RemoveDeny"/> option, the complete set of role aliases whose
/// contributing Deny entries must all be removed together for the verdict to flip. Empty for additions.
/// </param>
public sealed record RemediationOption(
    RemediationActionKind Kind,
    string RoleAlias,
    string Verb,
    Guid NodeKey,
    PermissionScope? Scope,
    IReadOnlyList<string> RemovedRoleAliases);
