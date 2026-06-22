using Umbraco.AI.Core.Tools;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.AI.Services;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;

namespace Umbraco.Community.AdvancedPermissions.AI.Tools;

/// <summary>
/// Lists which roles can and cannot perform a given verb at a content node.
/// Enumerates every assignable role (each Umbraco user group plus the virtual <c>$everyone</c> role),
/// resolves the verb for each, and partitions the roles into allowed and denied buckets.
/// </summary>
/// <param name="userGroupService">The Umbraco user group service used to enumerate assignable roles.</param>
/// <param name="pathResolver">The resolver that builds the root-to-node ancestor key path.</param>
/// <param name="permissions">The permission service used to resolve effective permissions per role.</param>
[AITool("uap_who_can", "Who can", ScopeId = "advanced-permissions:read")]
public sealed class WhoCanTool(
    IUserGroupService userGroupService,
    IContentPathResolver pathResolver,
    IAdvancedPermissionService permissions)
    : AIToolBase<WhoCanArgs>
{
    /// <summary>The page size used when enumerating user groups, mirroring the roles metadata endpoint.</summary>
    private const int PageSize = 100;

    /// <inheritdoc />
    public override string Description =>
        "Lists which roles can and cannot perform a single permission verb at a content node. Enumerates every user group plus the '$everyone' role, resolves the verb for each, and returns two lists: roles whose effective permission is allowed and roles whose effective permission is denied. Use to answer questions like 'who can publish here?' or 'which groups are denied delete on this page?'.";

    /// <inheritdoc />
    protected override async Task<object> ExecuteAsync(WhoCanArgs args, CancellationToken cancellationToken = default)
    {
        var path = pathResolver.GetPathFromRoot(args.NodeKey);
        var verbs = new[] { args.Verb };

        var allowedRoles = new List<string>();
        var deniedRoles = new List<string>();

        foreach (var roleAlias in await GetRoleAliasesAsync())
        {
            var resolved = await permissions.ResolveForRoleAsync(roleAlias, args.NodeKey, path, verbs, cancellationToken);
            if (!resolved.TryGetValue(args.Verb, out var permission))
            {
                continue;
            }

            if (permission.IsAllowed)
            {
                allowedRoles.Add(roleAlias);
            }
            else
            {
                deniedRoles.Add(roleAlias);
            }
        }

        return new WhoCanResult(args.Verb, args.NodeKey, allowedRoles, deniedRoles);
    }

    /// <summary>
    /// Enumerates all assignable role aliases: the virtual <c>$everyone</c> role first,
    /// followed by every Umbraco user group alias. Mirrors the roles metadata endpoint.
    /// </summary>
    /// <returns>The ordered list of role aliases.</returns>
    private async Task<IReadOnlyList<string>> GetRoleAliasesAsync()
    {
        var aliases = new List<string> { AdvancedPermissionsConstants.EveryoneRoleAlias };

        var skip = 0;
        while (true)
        {
            var page = await userGroupService.GetAllAsync(skip, PageSize);
            foreach (var group in page.Items)
            {
                aliases.Add(group.Alias);
            }

            skip += PageSize;
            if (skip >= page.Total)
            {
                break;
            }
        }

        return aliases;
    }
}
