using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Services;

/// <summary>Default <see cref="IPermissionAuditAnalyzer"/> implementing the MVP rule set.</summary>
public sealed class PermissionAuditAnalyzer : IPermissionAuditAnalyzer
{
    /// <inheritdoc />
    public AuditReport Analyze(IReadOnlyList<AdvancedPermissionEntry> entries)
    {
        var findings = new List<AuditFinding>();

        foreach (var e in entries)
        {
            if (e.RoleAlias == AdvancedPermissionsConstants.EveryoneRoleAlias
                && e.State == PermissionState.Allow
                && e.Verb != AdvancedPermissionsConstants.VerbRead
                && e.NodeKey == AdvancedPermissionsConstants.VirtualRootNodeKey
                && e.Scope == PermissionScope.ThisNodeAndDescendants)
            {
                findings.Add(new AuditFinding(
                    "everyone-broad-write", AuditSeverity.Risk,
                    $"Everyone is allowed '{e.Verb}' across the whole tree from the root.",
                    e.NodeKey, e.RoleAlias, e.Verb));
            }

            if (e.Verb == AdvancedPermissionsConstants.VerbManagePermissions
                && e.State == PermissionState.Allow
                && e.Scope == PermissionScope.ThisNodeAndDescendants)
            {
                findings.Add(new AuditFinding(
                    "manage-permissions-descendants", AuditSeverity.Risk,
                    $"Role '{e.RoleAlias}' can manage permissions on this node and all descendants.",
                    e.NodeKey, e.RoleAlias, e.Verb));
            }

            if (e.IsPriorityOverride)
            {
                findings.Add(new AuditFinding(
                    "priority-override", AuditSeverity.Info,
                    $"Priority override is set for '{e.Verb}' on role '{e.RoleAlias}'.",
                    e.NodeKey, e.RoleAlias, e.Verb));
            }
        }

        var conflicts = entries
            .GroupBy(e => (e.NodeKey, e.RoleAlias, e.Verb))
            .Where(g => g.Any(x => x.State == PermissionState.Allow)
                     && g.Any(x => x.State == PermissionState.Deny));
        foreach (var g in conflicts)
        {
            findings.Add(new AuditFinding(
                "allow-deny-conflict", AuditSeverity.Warning,
                $"Role '{g.Key.RoleAlias}' has both Allow and Deny for '{g.Key.Verb}' on the same node.",
                g.Key.NodeKey, g.Key.RoleAlias, g.Key.Verb));
        }

        var ordered = findings.OrderByDescending(f => f.Severity).ToList();
        return new AuditReport(ordered, entries.Count);
    }
}
