using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Services;

/// <summary>
/// Maps raw permission data (role aliases, verb identifiers, scope/state enums, node GUIDs and the
/// reasoning chain) to friendly, editor-facing labels. The AI tools use this so the model only ever
/// sees human-readable text and never parrots code identifiers such as <c>$everyone</c>,
/// <c>Umb.Document.Delete</c>, <c>ThisNodeOnly</c>, or a raw node GUID back to an editor.
/// </summary>
public interface IPermissionPresenter
{
    /// <summary>
    /// Resolves a role alias to its display name: the special "All Users" label for the
    /// <c>$everyone</c> role, the user group's name for a known group alias, or the alias itself
    /// as a last-resort fallback.
    /// </summary>
    /// <param name="roleAlias">The raw role alias.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The friendly role display name.</returns>
    Task<string> GetRoleDisplayNameAsync(string roleAlias, CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts a full permission verb to its friendly action name — the substring after the last
    /// dot (e.g. <c>Umb.Document.Delete</c> becomes "Delete").
    /// </summary>
    /// <param name="verb">The raw verb identifier.</param>
    /// <returns>The friendly action name.</returns>
    string GetVerbDisplayName(string verb);

    /// <summary>Converts a permission scope to friendly text.</summary>
    /// <param name="scope">The scope to convert.</param>
    /// <returns>The friendly scope text.</returns>
    string GetScopeText(PermissionScope scope);

    /// <summary>Converts a permission state to friendly text ("Allowed" or "Denied").</summary>
    /// <param name="state">The state to convert.</param>
    /// <returns>The friendly state text.</returns>
    string GetStateText(PermissionState state);

    /// <summary>
    /// Resolves a content node key to a friendly name: the special "All content (root-level default)"
    /// label for the virtual-root sentinel, the content node's name when it can be resolved, or the
    /// generic "this node" fallback when it cannot.
    /// </summary>
    /// <param name="nodeKey">The content node key.</param>
    /// <returns>The friendly node name.</returns>
    string GetNodeName(Guid nodeKey);

    /// <summary>
    /// Resolves a content-type (document type) key to its friendly display name via the content-type
    /// service, falling back to a generic "this document type" label when it cannot be resolved. Used by
    /// the type-create aspect so the model never sees a raw content-type alias or GUID.
    /// </summary>
    /// <param name="contentTypeKey">The document type key.</param>
    /// <returns>The friendly document type name.</returns>
    string GetContentTypeName(Guid contentTypeKey);

    /// <summary>
    /// Maps a resolved doc-type create <see cref="EffectivePermission"/> (and its reasoning chain) for a
    /// single document type to a friendly <see cref="TypeCreateVerdict"/>. The verb is presented as the
    /// human-facing "Insert" / "Create of type" action rather than the raw <c>Umb.Document.CreateOfType</c>
    /// identifier, and a structurally-disallowed type is reported as "Not applicable".
    /// </summary>
    /// <param name="contentTypeKey">The document type the permission was resolved for.</param>
    /// <param name="permission">The resolved effective create permission and its reasoning chain.</param>
    /// <param name="isInAllowedChildren">
    /// <see langword="false"/> when the type is structurally not an allowed child of the parent; the
    /// verdict is then "Not applicable" regardless of <paramref name="permission"/>.
    /// </param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The friendly type-create verdict.</returns>
    Task<TypeCreateVerdict> ToTypeCreateVerdictAsync(
        Guid contentTypeKey,
        EffectivePermission permission,
        bool isInAllowedChildren,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Maps a resolved <see cref="EffectivePermission"/> (and its reasoning chain) to a friendly
    /// <see cref="AccessVerdict"/> with no raw identifiers.
    /// </summary>
    /// <param name="permission">The resolved effective permission.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The friendly verdict.</returns>
    Task<AccessVerdict> ToVerdictAsync(EffectivePermission permission, CancellationToken cancellationToken = default);

    /// <summary>
    /// Projects a confirmed engine-level <see cref="RemediationOption"/> to a friendly, editor-facing
    /// <see cref="AccessRemediation"/>. The role alias, raw verb, scope/state enums, and node GUID are all
    /// replaced by display names, and a plain-language administrator-action sentence is built. Every
    /// returned remediation is a confirmed fact — the change has already been validated to flip the
    /// verdict to Allowed — so the wording states the outcome positively.
    /// </summary>
    /// <param name="option">The confirmed remediation option to project.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The friendly remediation.</returns>
    Task<AccessRemediation> ToRemediationAsync(RemediationOption option, CancellationToken cancellationToken = default);

    /// <summary>
    /// Maps a verb-to-effective-permission dictionary and a node key to a friendly
    /// <see cref="AccessExplanation"/> with one verdict per verb.
    /// </summary>
    /// <param name="permissions">The resolved permissions keyed by verb.</param>
    /// <param name="nodeKey">The node the permissions were resolved at.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The friendly explanation.</returns>
    Task<AccessExplanation> ToExplanationAsync(
        IReadOnlyDictionary<string, EffectivePermission> permissions,
        Guid nodeKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Projects a raw <see cref="AuditReport"/> to a <see cref="FriendlyAuditReport"/>, rewriting each
    /// finding's role, action, node, and message to friendly labels free of raw identifiers.
    /// </summary>
    /// <param name="report">The raw audit report.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The friendly audit report.</returns>
    Task<FriendlyAuditReport> ToFriendlyAuditAsync(AuditReport report, CancellationToken cancellationToken = default);
}
