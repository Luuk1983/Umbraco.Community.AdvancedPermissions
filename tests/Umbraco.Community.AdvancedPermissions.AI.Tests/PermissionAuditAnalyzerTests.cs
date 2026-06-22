using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.AI.Services;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Tests;

/// <summary>
/// Unit tests for <see cref="PermissionAuditAnalyzer"/>.
/// Tests are written test-first to define the MVP audit rule set.
/// </summary>
public class PermissionAuditAnalyzerTests
{
    /// <summary>Creates an entry with a generated id and sensible defaults.</summary>
    /// <param name="nodeKey">The node the entry applies to.</param>
    /// <param name="role">The role alias the entry is for.</param>
    /// <param name="verb">The verb the entry grants or denies.</param>
    /// <param name="state">Whether the entry allows or denies.</param>
    /// <param name="scope">The scope of the entry; defaults to ThisNodeAndDescendants.</param>
    /// <param name="priority">Whether the entry is a priority override; defaults to false.</param>
    /// <returns>A new <see cref="AdvancedPermissionEntry"/>.</returns>
    private static AdvancedPermissionEntry Entry(
        Guid nodeKey, string role, string verb,
        PermissionState state, PermissionScope scope = PermissionScope.ThisNodeAndDescendants,
        bool priority = false)
        => new(Guid.NewGuid(), nodeKey, role, verb, state, scope, priority);

    /// <summary>The system under test.</summary>
    private readonly IPermissionAuditAnalyzer _sut = new PermissionAuditAnalyzer();

    /// <summary>An empty entry set produces an empty report with a zero analyzed count.</summary>
    [Fact]
    public void Analyze_NoEntries_ReturnsEmptyReport()
    {
        var report = _sut.Analyze([]);
        Assert.Empty(report.Findings);
        Assert.Equal(0, report.EntriesAnalyzed);
    }

    /// <summary>Everyone allowed a non-read verb across the whole tree from the root is flagged as a risk.</summary>
    [Fact]
    public void Analyze_EveryoneAllowedWriteAtRootDescendants_FlagsRisk()
    {
        var entries = new[]
        {
            Entry(AdvancedPermissionsConstants.VirtualRootNodeKey,
                  AdvancedPermissionsConstants.EveryoneRoleAlias,
                  AdvancedPermissionsConstants.VerbDelete,
                  PermissionState.Allow),
        };
        var report = _sut.Analyze(entries);
        Assert.Contains(report.Findings, f => f.RuleId == "everyone-broad-write" && f.Severity == AuditSeverity.Risk);
    }

    /// <summary>Everyone allowed Read at the root is benign and must not be flagged.</summary>
    [Fact]
    public void Analyze_EveryoneAllowedReadAtRoot_DoesNotFlag()
    {
        var entries = new[]
        {
            Entry(AdvancedPermissionsConstants.VirtualRootNodeKey,
                  AdvancedPermissionsConstants.EveryoneRoleAlias,
                  AdvancedPermissionsConstants.VerbRead,
                  PermissionState.Allow),
        };
        var report = _sut.Analyze(entries);
        Assert.DoesNotContain(report.Findings, f => f.RuleId == "everyone-broad-write");
    }

    /// <summary>The same node/role/verb having both an Allow and a Deny entry is flagged as a conflict.</summary>
    [Fact]
    public void Analyze_SameNodeRoleVerbHasAllowAndDeny_FlagsConflict()
    {
        var node = Guid.NewGuid();
        var entries = new[]
        {
            Entry(node, "editors", AdvancedPermissionsConstants.VerbPublish, PermissionState.Allow),
            Entry(node, "editors", AdvancedPermissionsConstants.VerbPublish, PermissionState.Deny),
        };
        var report = _sut.Analyze(entries);
        Assert.Contains(report.Findings, f => f.RuleId == "allow-deny-conflict" && f.Severity == AuditSeverity.Warning);
    }

    /// <summary>Granting the manage-permissions verb to descendants is flagged as a risk.</summary>
    [Fact]
    public void Analyze_ManagePermissionsAllowedOnDescendants_FlagsRisk()
    {
        var entries = new[]
        {
            Entry(Guid.NewGuid(), "editors",
                  AdvancedPermissionsConstants.VerbManagePermissions,
                  PermissionState.Allow, PermissionScope.ThisNodeAndDescendants),
        };
        var report = _sut.Analyze(entries);
        Assert.Contains(report.Findings, f => f.RuleId == "manage-permissions-descendants" && f.Severity == AuditSeverity.Risk);
    }

    /// <summary>A priority override entry is surfaced as an informational finding.</summary>
    [Fact]
    public void Analyze_PriorityOverridePresent_FlagsInfo()
    {
        var entries = new[]
        {
            Entry(Guid.NewGuid(), "editors", AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, priority: true),
        };
        var report = _sut.Analyze(entries);
        Assert.Contains(report.Findings, f => f.RuleId == "priority-override" && f.Severity == AuditSeverity.Info);
    }

    /// <summary>Findings are ordered most-severe first, so a Risk finding precedes an Info finding.</summary>
    [Fact]
    public void Analyze_OrdersFindingsBySeverityDescending()
    {
        var entries = new[]
        {
            Entry(Guid.NewGuid(), "editors", AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, priority: true),
            Entry(AdvancedPermissionsConstants.VirtualRootNodeKey,
                  AdvancedPermissionsConstants.EveryoneRoleAlias,
                  AdvancedPermissionsConstants.VerbDelete, PermissionState.Allow),
        };
        var report = _sut.Analyze(entries);
        Assert.Equal(AuditSeverity.Risk, report.Findings[0].Severity);
    }
}
