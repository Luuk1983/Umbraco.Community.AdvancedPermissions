using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.Core.Exceptions;

/// <summary>
/// One node, user group and document type triple whose stored entries no longer match the stamp a
/// writer expected.
/// </summary>
/// <remarks>
/// The triple, not the pair, is the unit of identity here: two entries that share a node and a user
/// group but differ in document type are entirely separate sets and conflict independently.
/// </remarks>
/// <param name="NodeKey">The content node key of the conflicted triple.</param>
/// <param name="RoleAlias">The user group alias of the conflicted triple.</param>
/// <param name="ContentTypeKey">The document type key of the conflicted triple.</param>
/// <param name="CurrentEntries">The entries stored at the moment the conflict was detected.</param>
/// <param name="CurrentStamp">
/// The stamp of <paramref name="CurrentEntries"/>, as computed by
/// <c>PermissionStamp</c>.
/// </param>
public sealed record DocTypePermissionConflict(
    Guid NodeKey,
    string RoleAlias,
    Guid ContentTypeKey,
    IReadOnlyList<DocTypePermissionEntry> CurrentEntries,
    string CurrentStamp);
