namespace Umbraco.Community.AdvancedPermissions.AI.Models;

/// <summary>Severity of an audit finding.</summary>
public enum AuditSeverity
{
    /// <summary>Informational — surfaced for awareness, not necessarily a problem.</summary>
    Info,
    /// <summary>A potential misconfiguration worth reviewing.</summary>
    Warning,
    /// <summary>A likely security risk that should be addressed.</summary>
    Risk,
}

/// <summary>A single issue discovered by the permission audit analyzer.</summary>
/// <param name="RuleId">Stable identifier of the rule that produced the finding.</param>
/// <param name="Severity">How serious the finding is.</param>
/// <param name="Message">Human-readable explanation of the finding.</param>
/// <param name="NodeKey">The content node the finding relates to, if any.</param>
/// <param name="RoleAlias">The role the finding relates to, if any.</param>
/// <param name="Verb">The verb the finding relates to, if any.</param>
public sealed record AuditFinding(
    string RuleId,
    AuditSeverity Severity,
    string Message,
    Guid? NodeKey = null,
    string? RoleAlias = null,
    string? Verb = null);

/// <summary>The complete result of an audit run.</summary>
/// <param name="Findings">All findings, ordered most-severe first.</param>
/// <param name="EntriesAnalyzed">How many stored entries were inspected.</param>
public sealed record AuditReport(
    IReadOnlyList<AuditFinding> Findings,
    int EntriesAnalyzed);
