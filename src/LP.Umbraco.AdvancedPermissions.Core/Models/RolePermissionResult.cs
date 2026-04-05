namespace LP.Umbraco.AdvancedPermissions.Core.Models;

/// <summary>
/// Represents the resolved permission state for a single role during resolution.
/// </summary>
/// <param name="RoleAlias">The alias of the role that produced this result.</param>
/// <param name="State">The resolved permission state (Allow or Deny).</param>
/// <param name="IsExplicit">
/// <see langword="true"/> if the state was determined by an entry set directly on the target node;
/// <see langword="false"/> if it was inherited from an ancestor node or from group defaults.
/// </param>
/// <param name="SourceNodeKey">
/// The key of the node where the determining entry was found,
/// or <see langword="null"/> if derived from group defaults (virtual root).
/// </param>
/// <param name="SourceScope">
/// The scope of the determining entry, or <see langword="null"/> if derived from group defaults.
/// </param>
internal sealed record RolePermissionResult(
    string RoleAlias,
    PermissionState State,
    bool IsExplicit,
    Guid? SourceNodeKey,
    PermissionScope? SourceScope);
