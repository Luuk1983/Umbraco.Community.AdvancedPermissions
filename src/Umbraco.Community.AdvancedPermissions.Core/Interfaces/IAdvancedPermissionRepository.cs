namespace Umbraco.Community.AdvancedPermissions.Core.Interfaces;

/// <summary>
/// Data access for content-node advanced security permission entries (the <c>AdvancedPermission</c> table).
/// </summary>
/// <remarks>
/// A content-specific marker over the shared <see cref="INodePermissionRepository"/> contract. It carries
/// no members of its own — it exists so the content permission target can be registered and resolved
/// independently of other node-based targets (e.g. library elements via
/// <see cref="IElementPermissionRepository"/>) while sharing one storage shape and implementation base.
/// </remarks>
public interface IAdvancedPermissionRepository : INodePermissionRepository;
