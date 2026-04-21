namespace Umbraco.Community.AdvancedPermissions.Core.Models;

/// <summary>
/// Represents the explicit state of a permission entry.
/// Only Allow and Deny are stored; the absence of an entry means Inherit (no opinion).
/// </summary>
public enum PermissionState
{
    /// <summary>
    /// Explicitly grants the permission.
    /// </summary>
    Allow = 1,

    /// <summary>
    /// Explicitly denies the permission.
    /// </summary>
    Deny = 2,
}
