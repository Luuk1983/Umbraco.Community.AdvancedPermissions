using Umbraco.AI.Core.Tools;
using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.AI.Services;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;

namespace Umbraco.Community.AdvancedPermissions.AI.Tools;

/// <summary>Explains a role's effective permissions at a content node, including the reasoning chain.</summary>
/// <param name="permissions">The permission service used to resolve effective permissions.</param>
/// <param name="pathResolver">The resolver that builds the root-to-node ancestor key path.</param>
[AITool("uap_explain_role_access", "Explain role access", ScopeId = "advanced-permissions:read")]
public sealed class ExplainRoleAccessTool(
    IAdvancedPermissionService permissions,
    IContentPathResolver pathResolver)
    : AIToolBase<ExplainRoleAccessArgs>
{
    /// <inheritdoc />
    public override string Description =>
        "Returns what a role (user group, or '$everyone') is allowed at a content node for each permission verb, with the reasoning chain. Use to answer 'what can the Editors group do on this page?'. Pass a single verb to focus on one permission, or omit it for all verbs.";

    /// <inheritdoc />
    protected override async Task<object> ExecuteAsync(ExplainRoleAccessArgs args, CancellationToken cancellationToken = default)
    {
        var path = pathResolver.GetPathFromRoot(args.NodeKey);
        var verbs = string.IsNullOrWhiteSpace(args.Verb) ? null : new[] { args.Verb };
        return await permissions.ResolveForRoleAsync(args.RoleAlias, args.NodeKey, path, verbs, cancellationToken);
    }
}
