using NSubstitute;
using Umbraco.AI.Core.Tools;
using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.AI.Services;
using Umbraco.Community.AdvancedPermissions.AI.Tools;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Tests;

/// <summary>
/// Unit tests for <see cref="AuditPermissionsTool"/>. The tool is invoked through the public
/// <see cref="IAITool.ExecuteAsync(object?, System.Threading.CancellationToken)"/> entry point so the
/// real <see cref="AIToolBase{TArgs}"/> base-class plumbing (argument cast + dispatch) is exercised.
/// </summary>
public sealed class AuditPermissionsToolTests
{
    /// <summary>The mocked repository the tool loads role-wide entries from.</summary>
    private readonly IAdvancedPermissionRepository _repository = Substitute.For<IAdvancedPermissionRepository>();

    /// <summary>The mocked analyzer the tool delegates the audit to.</summary>
    private readonly IPermissionAuditAnalyzer _analyzer = Substitute.For<IPermissionAuditAnalyzer>();

    /// <summary>
    /// The tool loads all entries for the role, hands them to the analyzer, and returns the
    /// analyzer's report verbatim — calling each collaborator exactly once.
    /// </summary>
    [Fact]
    public async Task Audit_LoadsRoleEntries_DelegatesToAnalyzer_ReturnsReport()
    {
        const string roleAlias = "editors";
        var nodeKey = Guid.NewGuid();

        var entries = new List<AdvancedPermissionEntry>
        {
            new(Guid.NewGuid(), nodeKey, roleAlias, "Umb.Document.Read", PermissionState.Allow, PermissionScope.ThisNodeAndDescendants),
            new(Guid.NewGuid(), nodeKey, roleAlias, "Umb.Document.Delete", PermissionState.Deny, PermissionScope.ThisNodeOnly),
        };

        var expectedReport = new AuditReport(
            new[]
            {
                new AuditFinding(
                    "conflicting-allow-deny",
                    AuditSeverity.Risk,
                    "Allow and Deny conflict for the same verb.",
                    nodeKey,
                    roleAlias,
                    "Umb.Document.Delete"),
            },
            EntriesAnalyzed: 2);

        _repository.GetByRoleAsync(roleAlias, Arg.Any<CancellationToken>()).Returns(entries);
        _analyzer.Analyze(entries).Returns(expectedReport);

        var tool = new AuditPermissionsTool(_repository, _analyzer);
        var result = await ((IAITool)tool).ExecuteAsync(
            new AuditPermissionsArgs(roleAlias), CancellationToken.None);

        var report = Assert.IsType<AuditReport>(result);
        Assert.Same(expectedReport, report);
        Assert.Equal(2, report.EntriesAnalyzed);
        Assert.Single(report.Findings);
        Assert.Equal("conflicting-allow-deny", report.Findings[0].RuleId);
        Assert.Equal(AuditSeverity.Risk, report.Findings[0].Severity);

        await _repository.Received(1).GetByRoleAsync(roleAlias, Arg.Any<CancellationToken>());
        _analyzer.Received(1).Analyze(entries);
    }
}
