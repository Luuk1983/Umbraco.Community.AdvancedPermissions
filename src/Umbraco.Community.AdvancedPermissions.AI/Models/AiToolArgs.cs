using System.ComponentModel;

namespace Umbraco.Community.AdvancedPermissions.AI.Models;

/// <summary>Arguments for the explain-user-access tool.</summary>
/// <param name="UserKey">The key of the user whose access is being explained.</param>
/// <param name="NodeKey">The content node to evaluate access at.</param>
/// <param name="Verb">Optional single verb; when omitted, all verbs are returned.</param>
public sealed record ExplainUserAccessArgs(
    [property: Description("The GUID key of the user whose access to explain.")] Guid UserKey,
    [property: Description("The GUID key of the content node to evaluate access at.")] Guid NodeKey,
    [property: Description("Optional verb such as 'Umb.Document.Publish'. Omit to return all verbs.")] string? Verb = null);

/// <summary>Arguments for the explain-role-access tool.</summary>
/// <param name="RoleAlias">The role/user-group alias, or '$everyone'.</param>
/// <param name="NodeKey">The content node to evaluate access at.</param>
/// <param name="Verb">Optional single verb; when omitted, all verbs are returned.</param>
public sealed record ExplainRoleAccessArgs(
    [property: Description("The role or user-group alias, or '$everyone'.")] string RoleAlias,
    [property: Description("The GUID key of the content node to evaluate access at.")] Guid NodeKey,
    [property: Description("Optional verb such as 'Umb.Document.Publish'. Omit to return all verbs.")] string? Verb = null);

/// <summary>Arguments for the who-can tool.</summary>
/// <param name="NodeKey">The content node to evaluate access at.</param>
/// <param name="Verb">The single permission verb to test each role against.</param>
public sealed record WhoCanArgs(
    [property: Description("The GUID key of the content node to evaluate access at.")] Guid NodeKey,
    [property: Description("The permission verb to test, such as 'Umb.Document.Publish'.")] string Verb);

/// <summary>Arguments for the audit-permissions tool.</summary>
/// <param name="RoleAlias">The role/user-group alias whose stored entries are audited, or '$everyone'.</param>
public sealed record AuditPermissionsArgs(
    [property: Description("The role or user-group alias whose stored permission entries to audit, or '$everyone'.")] string RoleAlias);
