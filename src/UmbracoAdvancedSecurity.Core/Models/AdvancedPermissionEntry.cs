namespace UmbracoAdvancedSecurity.Core.Models;

/// <summary>
/// Represents a single stored permission entry in the advanced security system.
/// An entry defines the state (Allow/Deny) and scope for a specific role and permission verb
/// on a specific content node.
/// </summary>
/// <param name="Id">The unique database identifier for this entry.</param>
/// <param name="NodeKey">
/// The key of the content node this entry applies to.
/// <see langword="null"/> represents the virtual root, affecting all nodes.
/// </param>
/// <param name="RoleAlias">
/// The alias of the role (user group alias or <c>$everyone</c> for the virtual Everyone role).
/// </param>
/// <param name="Verb">The permission verb, e.g. <c>Umb.Document.Read</c>.</param>
/// <param name="State">Whether this entry allows or denies the permission.</param>
/// <param name="Scope">The scope at which this entry takes effect within the tree.</param>
public sealed record AdvancedPermissionEntry(
    int Id,
    Guid? NodeKey,
    string RoleAlias,
    string Verb,
    PermissionState State,
    PermissionScope Scope);
