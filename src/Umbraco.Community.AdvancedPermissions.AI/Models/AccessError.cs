namespace Umbraco.Community.AdvancedPermissions.AI.Models;

/// <summary>
/// A friendly error result returned by the <c>uap_explain_access</c> tool when it cannot proceed —
/// for example a required argument for the chosen subject is missing, or the current backoffice user
/// could not be determined. The tool returns this instead of throwing so the copilot can relay a clear
/// message to the editor rather than surfacing an exception.
/// </summary>
/// <param name="Error">The human-readable explanation of why the request could not be answered.</param>
public sealed record AccessError(string Error);
