namespace Umbraco.Community.AdvancedPermissions.Core.Interfaces;

/// <summary>
/// High-level operations for managing and resolving advanced security permissions on content nodes.
/// This is the main entry point for content permission logic in the package.
/// </summary>
/// <remarks>
/// A content-specific marker over the shared <see cref="INodePermissionService"/> contract. It carries
/// no members of its own — it exists so the content target can be registered and consumed independently
/// of other node-based targets (e.g. library elements via <see cref="IElementNodePermissionService"/>)
/// while sharing one orchestration implementation.
/// </remarks>
public interface IAdvancedPermissionService : INodePermissionService;
