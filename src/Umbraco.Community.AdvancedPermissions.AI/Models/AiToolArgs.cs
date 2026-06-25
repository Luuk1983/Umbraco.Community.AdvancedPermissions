using System.ComponentModel;

namespace Umbraco.Community.AdvancedPermissions.AI.Models;

/// <summary>
/// Identifies whose effective access the <c>uap_explain_access</c> tool should evaluate at a content node.
/// </summary>
public enum ExplainSubject
{
    /// <summary>The backoffice user currently asking the question (resolved from the authenticated session).</summary>
    CurrentUser,

    /// <summary>A specific user identified by their key.</summary>
    User,

    /// <summary>A single role (user group, or the special "All Users" role).</summary>
    Role,

    /// <summary>Every assignable role, partitioned into who is allowed and who is denied.</summary>
    AllRoles,
}

/// <summary>
/// Selects which dimension of access the <c>uap_explain_access</c> tool explains at a content node:
/// the node's action permissions, or which document types can be created (inserted) under the node.
/// </summary>
public enum ExplainAspect
{
    /// <summary>
    /// Node action permissions — edit, delete, publish, move, etc. — resolved via the node-level
    /// permission system. This is the default and unchanged behaviour.
    /// </summary>
    Node,

    /// <summary>
    /// Document-type creation ("Insert Options") — which document types may be created under the node,
    /// resolved via the separate doc-type permission system. Answers the "why can't I create/insert
    /// type X here?", "what types can I create here?", and "who can create type X here?" questions.
    /// </summary>
    TypeCreate,
}

/// <summary>
/// Controls how much detail the <c>uap_explain_access</c> tool returns for a single- or all-verb explanation.
/// </summary>
public enum ExplainResponseFormat
{
    /// <summary>Each action's decision plus a single summarized reason per action.</summary>
    Concise,

    /// <summary>Each action's decision plus the full reasoning chain (all contributing reasons).</summary>
    Detailed,
}

/// <summary>
/// Arguments for the consolidated <c>uap_explain_access</c> tool. A single parameterized shape covers
/// explaining access for the current user, a specific user, a single role, or all roles at a content node.
/// </summary>
/// <param name="Subject">Whose access to evaluate.</param>
/// <param name="NodeKey">The content node to evaluate access at.</param>
/// <param name="UserKey">The user key; required when <see cref="ExplainSubject.User"/> is chosen.</param>
/// <param name="RoleAlias">The role alias (accepts '$everyone'); required when <see cref="ExplainSubject.Role"/> is chosen.</param>
/// <param name="Verb">Optional single verb to focus on; omit to evaluate all standard verbs.</param>
/// <param name="ResponseFormat">How much reasoning detail to return.</param>
/// <param name="Aspect">Which dimension of access to explain: node action permissions, or document-type creation.</param>
/// <param name="ContentTypeKey">Optional document type to focus on when <see cref="ExplainAspect.TypeCreate"/> is chosen.</param>
/// <param name="SuggestFix">
/// When <see langword="true"/> and access is denied, also return the concrete, confirmed permission
/// changes that would grant it. Only honoured for the node aspect, the current-user/user/role subjects,
/// and when a single <see cref="Verb"/> is supplied.
/// </param>
public sealed record ExplainAccessArgs(
    [property: Description("Whose access to evaluate: current-user (the editor asking about themselves), user (a specific user, requires userKey), role (a user group or 'All Users', requires roleAlias), or all-roles (who can/can't do this).")]
    ExplainSubject Subject,
    [property: Description("The GUID key of the content node to evaluate access at. For aspect=type-create this is the PARENT node under which creation is evaluated.")]
    Guid NodeKey,
    [property: Description("The GUID key of the user to evaluate. Required when subject is 'user'.")]
    Guid? UserKey = null,
    [property: Description("The role or user-group alias to evaluate, or '$everyone' for All Users. Required when subject is 'role'.")]
    string? RoleAlias = null,
    [property: Description("Optional verb such as 'Umb.Document.Delete' to focus on a single action. Omit to evaluate all actions. Ignored when aspect is 'type-create'.")]
    string? Verb = null,
    [property: Description("How much detail to return: concise (decision plus one summary reason per action) or detailed (full reasoning chain).")]
    ExplainResponseFormat ResponseFormat = ExplainResponseFormat.Concise,
    [property: Description("node = action permissions like edit/delete/publish; type-create = which document types can be created under this node")]
    ExplainAspect Aspect = ExplainAspect.Node,
    [property: Description("optional — focus a single document type when Aspect=type-create")]
    Guid? ContentTypeKey = null,
    [property: Description("When true, and access is denied, also return the concrete permission changes that would grant it.")]
    bool SuggestFix = false);

/// <summary>
/// Selects which slice of the stored permission configuration the <c>uap_audit_permissions</c> tool audits.
/// </summary>
public enum AuditScope
{
    /// <summary>Audit every stored entry for a single role across the whole tree (requires a role alias).</summary>
    Role,

    /// <summary>Audit every stored entry on a node and all of its descendant document nodes (requires a node key).</summary>
    Subtree,

    /// <summary>Audit the whole stored configuration across every node and role (best-effort; see the tool description).</summary>
    All,
}

/// <summary>Arguments for the audit-permissions tool.</summary>
/// <param name="Scope">
/// Which slice of the configuration to audit: a single role (default), a node's subtree, or the whole configuration.
/// </param>
/// <param name="RoleAlias">The role/user-group alias whose stored entries are audited, or '$everyone'. Required when <see cref="AuditScope.Role"/>.</param>
/// <param name="NodeKey">The content node whose subtree (this node plus descendants) is audited. Required when <see cref="AuditScope.Subtree"/>.</param>
/// <param name="SeverityMin">Optional minimum severity; when set, only findings at or above this severity are returned.</param>
public sealed record AuditPermissionsArgs(
    [property: Description("What to audit: 'role' (all entries for one role across the site — the default, requires roleAlias), 'subtree' (everything stored on a node and its descendants, requires nodeKey), or 'all' (the whole stored configuration).")]
    AuditScope Scope = AuditScope.Role,
    [property: Description("The role or user-group alias whose stored permission entries to audit, or '$everyone'. Required when scope is 'role'.")]
    string? RoleAlias = null,
    [property: Description("The GUID key of the content node whose subtree (this node and all descendants) to audit. Required when scope is 'subtree'.")]
    Guid? NodeKey = null,
    [property: Description("Optional minimum severity filter: 'Info', 'Warning', or 'Risk'. When set, only findings at or above this severity are returned.")]
    AuditSeverity? SeverityMin = null);
