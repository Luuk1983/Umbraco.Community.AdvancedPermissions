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

/// <summary>
/// A single audit finding projected to friendly, editor-facing labels. Carries the same information
/// as <see cref="AuditFinding"/> but with the role alias, verb, and node key replaced by display
/// names, and with a message free of any raw identifiers.
/// </summary>
/// <param name="RuleId">Stable identifier of the rule that produced the finding.</param>
/// <param name="Severity">How serious the finding is, as text ("Info", "Warning", "Risk").</param>
/// <param name="Message">Human-readable explanation, free of raw aliases/verbs/GUIDs.</param>
/// <param name="Role">The friendly role name the finding relates to, if any.</param>
/// <param name="Action">The friendly action name the finding relates to, if any.</param>
/// <param name="Node">The friendly node name the finding relates to, if any.</param>
public sealed record FriendlyAuditFinding(
    string RuleId,
    string Severity,
    string Message,
    string? Role = null,
    string? Action = null,
    string? Node = null);

/// <summary>
/// The friendly projection of an <see cref="AuditReport"/>, surfaced to editors verbatim.
/// Contains no raw role aliases, verb identifiers, enum names, or node GUIDs.
/// </summary>
/// <param name="Findings">All friendly findings, ordered most-severe first.</param>
/// <param name="EntriesAnalyzed">How many stored entries were inspected.</param>
public sealed record FriendlyAuditReport(
    IReadOnlyList<FriendlyAuditFinding> Findings,
    int EntriesAnalyzed);
