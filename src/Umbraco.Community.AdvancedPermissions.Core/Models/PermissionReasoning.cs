namespace Umbraco.Community.AdvancedPermissions.Core.Models;

/// <summary>
/// Explains why a specific effective permission was resolved the way it was.
/// Used by the Access Viewer to show a detailed breakdown to administrators.
/// </summary>
/// <param name="ContributingRole">The role alias that determined the final effective permission.</param>
/// <param name="State">The permission state contributed by this role.</param>
/// <param name="IsExplicit">
/// <see langword="true"/> if the permission was set directly on the target node;
/// <see langword="false"/> if inherited from an ancestor or group defaults.
/// </param>
/// <param name="SourceNodeKey">
/// The node key where the permission was set.
/// <c>AdvancedPermissionsConstants.VirtualRootNodeKey</c> indicates a virtual-root (group default) entry.
/// </param>
/// <param name="SourceScope">
/// The scope of the permission entry, or <see langword="null"/> for group defaults.
/// </param>
/// <param name="IsFromGroupDefault">
/// <see langword="true"/> if this permission comes from the Umbraco group's default permissions
/// rather than an explicit advanced security entry.
/// </param>
public sealed record PermissionReasoning(
    string ContributingRole,
    PermissionState State,
    bool IsExplicit,
    Guid SourceNodeKey,
    PermissionScope? SourceScope,
    bool IsFromGroupDefault);
