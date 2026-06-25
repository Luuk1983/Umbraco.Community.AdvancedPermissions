namespace Umbraco.Community.AdvancedPermissions.AI.Models;

/// <summary>
/// A single, friendly, editor-facing remediation: one concrete permission change that — confirmed by
/// re-resolving the package's pure resolver against the change — would flip a denied action to allowed.
/// Every field is a friendly label; there are no raw role aliases, verb identifiers, scope/state enum
/// names, or node GUIDs. The wording is phrased as an administrator action ("An administrator could …")
/// because applying the change requires permission-management rights the asker may not have, and because
/// this companion never writes anything itself.
/// </summary>
/// <param name="Description">
/// A plain-language sentence describing the change and that it would result in the action being allowed,
/// e.g. "An administrator could remove the Deny for Delete on News set for Editors, which would allow it."
/// </param>
/// <param name="Action">The friendly change verb: "Remove", "Add", or "Override".</param>
/// <param name="Role">The friendly role the change targets (e.g. "Editors", "All Users").</param>
/// <param name="Permission">The friendly action the change is about (e.g. "Delete", "Publish").</param>
/// <param name="Scope">
/// The friendly scope of the entry to add (e.g. "This node only"), or <see langword="null"/> for a
/// removal, which carries no new scope of its own.
/// </param>
/// <param name="SetOn">The friendly name of the node the change is made on.</param>
public sealed record AccessRemediation(
    string Description,
    string Action,
    string Role,
    string Permission,
    string? Scope,
    string SetOn);
