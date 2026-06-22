using Umbraco.AI.Core.Tools;
using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.AI.Services;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;

namespace Umbraco.Community.AdvancedPermissions.AI.Tools;

/// <summary>
/// Scans all stored permission entries for a single role across the whole content tree and reports
/// misconfigurations, conflicts, and risks. The audit is role-scoped: it loads every entry for the
/// given role via <see cref="IAdvancedPermissionRepository.GetByRoleAsync"/> and hands them to the analyzer.
/// </summary>
/// <param name="repository">The repository used to load all entries for the role across the tree.</param>
/// <param name="analyzer">The analyzer that inspects the entries and produces findings.</param>
[AITool("uap_audit_permissions", "Audit permissions", ScopeId = "advanced-permissions:read")]
public sealed class AuditPermissionsTool(
    IAdvancedPermissionRepository repository,
    IPermissionAuditAnalyzer analyzer)
    : AIToolBase<AuditPermissionsArgs>
{
    /// <inheritdoc />
    public override string Description =>
        "Scans the stored permission entries for a single role (a user group, or '$everyone') across the entire content tree and reports misconfigurations, conflicts, and security risks (e.g. an Allow and Deny on the same verb, or broad descendant grants). The audit is scoped per role: pass the role alias to audit. Use to answer 'is the Editors group's permission setup safe?' or 'what risks exist in this role's configuration?'.";

    /// <inheritdoc />
    protected override async Task<object> ExecuteAsync(AuditPermissionsArgs args, CancellationToken cancellationToken = default)
    {
        var entries = await repository.GetByRoleAsync(args.RoleAlias, cancellationToken);
        return analyzer.Analyze(entries);
    }
}
