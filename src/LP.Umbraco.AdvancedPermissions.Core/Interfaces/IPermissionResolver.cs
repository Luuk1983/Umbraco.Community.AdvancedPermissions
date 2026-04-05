using LP.Umbraco.AdvancedPermissions.Core.Models;

namespace LP.Umbraco.AdvancedPermissions.Core.Interfaces;

/// <summary>
/// Resolves effective permissions for a user at a specific content node by applying
/// the advanced security inheritance and priority rules.
/// </summary>
/// <remarks>
/// <para>
/// The resolver is a pure function — it performs no I/O and has no dependencies beyond its inputs.
/// All data (stored entries, group defaults, node path) must be provided via
/// <see cref="PermissionResolutionContext"/>.
/// </para>
/// <para>
/// Priority order (highest to lowest):
/// <list type="number">
///   <item>Explicit Deny from any role (set directly on the target node)</item>
///   <item>Explicit Allow from any role (set directly on the target node)</item>
///   <item>Implicit Deny (inherited from an ancestor node)</item>
///   <item>Implicit Allow (inherited from ancestor or from group defaults)</item>
///   <item>No opinion from any role → Deny (safe by default)</item>
/// </list>
/// </para>
/// </remarks>
public interface IPermissionResolver
{
    /// <summary>
    /// Resolves the effective permission for a single verb given the provided context.
    /// </summary>
    /// <param name="context">
    /// All data required for resolution: path, roles, and stored entries (including root-level defaults).
    /// </param>
    /// <param name="verb">The permission verb to resolve, e.g. <c>Umb.Document.Read</c>.</param>
    /// <returns>
    /// An <see cref="EffectivePermission"/> describing whether the verb is allowed,
    /// whether the result is explicit or implicit, and the full reasoning.
    /// </returns>
    EffectivePermission Resolve(PermissionResolutionContext context, string verb);

    /// <summary>
    /// Resolves effective permissions for multiple verbs given the provided context.
    /// </summary>
    /// <param name="context">
    /// All data required for resolution: path, roles, and stored entries (including root-level defaults).
    /// </param>
    /// <param name="verbs">The permission verbs to resolve.</param>
    /// <returns>
    /// A dictionary mapping each verb to its <see cref="EffectivePermission"/>.
    /// </returns>
    IReadOnlyDictionary<string, EffectivePermission> ResolveAll(
        PermissionResolutionContext context,
        IEnumerable<string> verbs);
}
