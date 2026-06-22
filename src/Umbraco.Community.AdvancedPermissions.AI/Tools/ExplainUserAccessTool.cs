using Umbraco.AI.Core.Tools;
using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.AI.Services;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;

namespace Umbraco.Community.AdvancedPermissions.AI.Tools;

/// <summary>Explains a user's effective permissions at a content node, including the reasoning chain.</summary>
/// <param name="permissions">The permission service used to resolve effective permissions.</param>
/// <param name="pathResolver">The resolver that builds the root-to-node ancestor key path.</param>
[AITool("uap_explain_user_access", "Explain user access", ScopeId = "advanced-permissions:read")]
public sealed class ExplainUserAccessTool(
    IAdvancedPermissionService permissions,
    IContentPathResolver pathResolver)
    : AIToolBase<ExplainUserAccessArgs>
{
    /// <inheritdoc />
    public override string Description =>
        "Returns whether a user is allowed each permission verb at a content node, with the full reasoning chain (which role and which node each decision came from, explicit vs inherited). Use to answer questions like 'why can't this user delete this page?'. Pass a single verb to focus on one permission, or omit it for all verbs.";

    /// <inheritdoc />
    protected override async Task<object> ExecuteAsync(ExplainUserAccessArgs args, CancellationToken cancellationToken = default)
    {
        var path = pathResolver.GetPathFromRoot(args.NodeKey);
        return string.IsNullOrWhiteSpace(args.Verb)
            ? await permissions.ResolveAllAsync(args.UserKey, args.NodeKey, path, null, cancellationToken)
            : await permissions.ResolveAsync(args.UserKey, args.NodeKey, path, args.Verb, cancellationToken);
    }
}
