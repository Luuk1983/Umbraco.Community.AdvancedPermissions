using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.Core.Exceptions;

/// <summary>
/// One node-and-user-group pair whose stored entries no longer match the stamp a writer expected.
/// </summary>
/// <remarks>
/// Carries everything a caller needs to tell the person what changed underneath them: what is
/// stored right now, and the stamp of it so that a retry after resolving the conflict needs no
/// further read.
/// </remarks>
/// <param name="NodeKey">The content node key of the conflicted pair.</param>
/// <param name="RoleAlias">The user group alias of the conflicted pair.</param>
/// <param name="CurrentEntries">The entries stored at the moment the conflict was detected.</param>
/// <param name="CurrentStamp">
/// The stamp of <paramref name="CurrentEntries"/>, as computed by
/// <c>PermissionStamp</c>.
/// </param>
public sealed record PermissionConflict(
    Guid NodeKey,
    string RoleAlias,
    IReadOnlyList<AdvancedPermissionEntry> CurrentEntries,
    string CurrentStamp);
