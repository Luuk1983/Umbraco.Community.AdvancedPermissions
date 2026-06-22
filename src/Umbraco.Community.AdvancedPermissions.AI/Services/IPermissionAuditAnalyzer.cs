using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Services;

/// <summary>Analyzes stored permission entries for misconfigurations and risks.</summary>
public interface IPermissionAuditAnalyzer
{
    /// <summary>Inspects the supplied entries and returns an ordered set of findings.</summary>
    /// <param name="entries">The stored permission entries to analyze.</param>
    /// <returns>A report containing all findings, most-severe first.</returns>
    AuditReport Analyze(IReadOnlyList<AdvancedPermissionEntry> entries);
}
