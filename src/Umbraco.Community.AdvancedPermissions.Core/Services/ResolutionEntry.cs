using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.Core.Services;

/// <summary>
/// Minimal entry shape consumed by <see cref="ResolutionEngine"/>.
/// </summary>
/// <remarks>
/// The engine is verb-agnostic and content-type-agnostic. Callers pre-filter entries
/// (e.g. by verb or by content-type-key) and map their domain records to this shape.
/// Anything beyond <c>NodeKey</c>, <c>RoleAlias</c>, <c>State</c>, and <c>Scope</c>
/// is irrelevant to the resolution algorithm.
/// </remarks>
/// <param name="NodeKey">
/// The key of the node the entry is set on. Use
/// <c>AdvancedPermissionsConstants.VirtualRootNodeKey</c> for virtual-root entries.
/// </param>
/// <param name="RoleAlias">The role alias (user group alias or <c>$everyone</c>).</param>
/// <param name="State">The permission state (Allow or Deny).</param>
/// <param name="Scope">The scope at which this entry applies.</param>
public sealed record ResolutionEntry(
    Guid NodeKey,
    string RoleAlias,
    PermissionState State,
    PermissionScope Scope);
