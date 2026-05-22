namespace Umbraco.Community.AdvancedPermissions.Core.Models;

/// <summary>
/// One row in the Create Audit listing: a candidate doc-type that the parent's allowed-children
/// list permits, together with whether the user may actually create it (after doc-type permissions
/// resolve) and the reasoning chain explaining why.
/// </summary>
/// <param name="ContentTypeKey">The candidate doc-type's key.</param>
/// <param name="ContentTypeAlias">The doc-type's alias, for display.</param>
/// <param name="ContentTypeName">The doc-type's name, for display.</param>
/// <param name="ContentTypeIcon">The doc-type's icon (Umbraco icon alias), if any.</param>
/// <param name="IsAllowed">Whether the user may create instances of this doc-type at the audited parent.</param>
/// <param name="IsExplicit">Whether the result came from an entry on the parent itself (vs inherited).</param>
/// <param name="Reasoning">The contributing entries, ordered by precedence.</param>
public sealed record DocTypeCreateAuditItem(
    Guid ContentTypeKey,
    string ContentTypeAlias,
    string ContentTypeName,
    string? ContentTypeIcon,
    bool IsAllowed,
    bool IsExplicit,
    IReadOnlyList<PermissionReasoning> Reasoning);
