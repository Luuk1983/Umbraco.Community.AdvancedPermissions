using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.Core.Interfaces;

/// <summary>
/// Resolves the effective doc-type permission for a (user roles, parent path, content-type, verb)
/// combination using the shared resolution engine with default state Allow.
/// </summary>
/// <remarks>
/// This is a pure function — it performs no I/O. The caller pre-loads entries via the repository
/// (or the L1 cache) and supplies them through <see cref="DocTypePermissionResolutionContext"/>.
/// </remarks>
public interface IDocTypePermissionResolver
{
    /// <summary>
    /// Resolves the doc-type verb for the supplied context.
    /// </summary>
    /// <param name="context">All data required for resolution.</param>
    /// <param name="verb">The verb to resolve, e.g. <c>Umb.Document.CreateOfType</c>.</param>
    /// <returns>
    /// The effective permission with its reasoning chain. When no role expresses an opinion,
    /// the result is <c>IsAllowed = true</c> (default Allow).
    /// </returns>
    EffectivePermission Resolve(DocTypePermissionResolutionContext context, string verb);
}
