using Umbraco.Community.AdvancedPermissions.Controllers.Models;
using Umbraco.Community.AdvancedPermissions.Core.Concurrency;

namespace Umbraco.Community.AdvancedPermissions.Controllers;

/// <summary>
/// Bridges the API response models to <see cref="PermissionStamp"/>, which lives in Core and
/// therefore cannot see them.
/// </summary>
public static class PermissionStampExtensions
{
    /// <summary>
    /// Computes the concurrency stamp for a set of response entries.
    /// </summary>
    /// <param name="entries">The entries being returned for one node and user group.</param>
    /// <returns>The stamp, as lowercase hex.</returns>
    public static string ComputeFromResponse(this IEnumerable<PermissionEntryResponseModel> entries) =>
        PermissionStamp.ComputeFromNames(
            entries.Select(e => (e.Verb, e.State, e.Scope, e.IsPriorityOverride)));

    /// <summary>
    /// Computes the concurrency stamp for a set of doc-type response entries.
    /// </summary>
    /// <param name="entries">
    /// The entries being returned for one node, user group and document type triple.
    /// </param>
    /// <returns>The stamp, as lowercase hex.</returns>
    public static string ComputeFromResponse(this IEnumerable<DocTypePermissionEntryResponseModel> entries) =>
        PermissionStamp.ComputeFromNames(
            entries.Select(e => (e.Verb, e.State, e.Scope, e.IsPriorityOverride)));
}
