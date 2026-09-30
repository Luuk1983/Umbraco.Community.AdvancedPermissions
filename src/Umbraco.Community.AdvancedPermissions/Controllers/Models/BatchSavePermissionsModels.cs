namespace Umbraco.Community.AdvancedPermissions.Controllers.Models;

/// <summary>
/// A request to replace permission entries for several node-and-user-group pairs at once.
/// </summary>
/// <remarks>
/// Applied all or nothing. The editors change several nodes before saving, and writing some of
/// them while refusing others would leave a state nothing afterwards could interpret.
/// </remarks>
/// <param name="Nodes">The pairs to write.</param>
/// <param name="Force">
/// Whether to skip the concurrency check entirely. Sent only after a user has been shown what
/// they would overwrite and has confirmed.
/// </param>
public sealed record BatchSavePermissionsRequestModel(
    IReadOnlyList<BatchSavePermissionsNode> Nodes,
    bool Force = false);

/// <summary>
/// One node-and-user-group pair within a batch save.
/// </summary>
/// <param name="NodeKey">
/// The content node key. Use <c>AdvancedPermissionsConstants.VirtualRootNodeKey</c> for
/// virtual-root entries.
/// </param>
/// <param name="RoleAlias">The user group alias, or <c>$everyone</c> for the All Users Group.</param>
/// <param name="Entries">The replacement entries. An empty list removes all entries for the pair.</param>
/// <param name="ExpectedStamp">
/// The stamp this client was given when it read these entries. When null the concurrency check is
/// skipped for this pair, which is how an older client keeps working.
/// </param>
public sealed record BatchSavePermissionsNode(
    Guid NodeKey,
    string RoleAlias,
    IReadOnlyList<SavePermissionEntryItem> Entries,
    string? ExpectedStamp = null);

/// <summary>
/// The body of the <c>409 Conflict</c> a batch save is refused with.
/// </summary>
/// <param name="Conflicts">
/// Only the pairs whose stored entries moved. Pairs that would have written cleanly are not
/// listed, because nothing was written and there is nothing to say about them.
/// </param>
public sealed record BatchSaveConflictResponseModel(
    IReadOnlyList<BatchSaveConflict> Conflicts);

/// <summary>
/// One pair whose stored entries no longer match what the client loaded.
/// </summary>
/// <param name="NodeKey">The content node key.</param>
/// <param name="RoleAlias">The user group alias.</param>
/// <param name="CurrentEntries">
/// What is stored right now. The client renders "stored X, yours Y" from this rather than from
/// what it guessed when the change notification arrived.
/// </param>
/// <param name="CurrentStamp">
/// The stamp of <paramref name="CurrentEntries"/>, so a client that resolves the conflict can
/// retry without a further read.
/// </param>
public sealed record BatchSaveConflict(
    Guid NodeKey,
    string RoleAlias,
    IReadOnlyList<PermissionEntryResponseModel> CurrentEntries,
    string CurrentStamp);

/// <summary>
/// One pair's new stamp after a successful batch save.
/// </summary>
/// <remarks>
/// Its own type rather than a <see cref="BatchSaveConflict"/> with an empty entry list: a success
/// response shaped like a conflict reads as one at every call site that handles it, and the client
/// would be pulling a field named "current" off an object named "conflict" on the happy path.
/// </remarks>
/// <param name="NodeKey">The content node key.</param>
/// <param name="RoleAlias">The user group alias.</param>
/// <param name="Stamp">
/// The stamp of what was just written, so the client can keep editing without re-reading.
/// </param>
public sealed record BatchSavedStamp(
    Guid NodeKey,
    string RoleAlias,
    string Stamp);
