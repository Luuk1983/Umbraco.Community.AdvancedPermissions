namespace Umbraco.Community.AdvancedPermissions.Core.Interfaces;

/// <summary>
/// High-level operations for managing and resolving advanced security permissions on library
/// elements and element folders. This is the main entry point for element permission logic.
/// </summary>
/// <remarks>
/// An element-specific marker over the shared <see cref="INodePermissionService"/> contract. It
/// carries no members of its own — it exists so the element target can be registered and consumed
/// independently of the content target (<see cref="IAdvancedPermissionService"/>) while sharing one
/// orchestration implementation. The two Umbraco enforcement adapters
/// (<c>IElementPermissionService</c> and <c>IElementContainerPermissionService</c>) both delegate to
/// this service.
/// </remarks>
public interface IElementNodePermissionService : INodePermissionService;
