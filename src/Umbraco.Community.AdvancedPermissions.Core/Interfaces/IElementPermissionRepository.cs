namespace Umbraco.Community.AdvancedPermissions.Core.Interfaces;

/// <summary>
/// Data access for library element and element-folder advanced security permission entries
/// (the <c>ElementPermission</c> table).
/// </summary>
/// <remarks>
/// An element-specific marker over the shared <see cref="INodePermissionRepository"/> contract. It
/// carries no members of its own — it exists so the element permission target can be registered and
/// resolved independently of the content target (<see cref="IAdvancedPermissionRepository"/>) while
/// sharing one storage shape and implementation base. A single table holds both element verbs
/// (<c>Umb.Element.*</c>) and folder verbs (mapped to the canonical element verbs); the verb string
/// distinguishes them.
/// </remarks>
public interface IElementPermissionRepository : INodePermissionRepository;
