using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Umbraco.Community.AdvancedPermissions.Controllers;

/// <summary>
/// Builds the body of the <c>409 Conflict</c> that the permission save endpoints refuse a stale
/// write with.
/// </summary>
/// <remarks>
/// The body has to be a <see cref="ProblemDetails"/>, not a bare <c>{"conflicts":[...]}</c>. The
/// backoffice configures its HTTP client through Umbraco's <c>authContext.configureClient</c>,
/// which installs a catch-all response interceptor. For any non-401/403 error it keeps the
/// response body only when it passes <c>isProblemDetailsLike</c> — that is, it has <c>type</c>,
/// <c>title</c> and <c>status</c> — and otherwise replaces it with a generic error. A bare
/// conflicts body fails that check, so the conflicts never reach the client and the conflict
/// dialog can never open. <c>System.Text.Json</c> writes <see cref="ProblemDetails.Extensions"/>
/// entries as top-level properties, so the conflicts ride along intact on a body that passes.
/// </remarks>
internal static class ConflictProblemDetails
{
    /// <summary>The <c>type</c> reported on every conflict body; part of what the interceptor checks.</summary>
    internal const string ProblemType = "Conflict";

    /// <summary>The extension key under which the conflicts are written, and the client reads them from.</summary>
    internal const string ConflictsKey = "conflicts";

    /// <summary>
    /// Creates the ProblemDetails for a refused write, carrying the conflicts as the
    /// <c>conflicts</c> extension.
    /// </summary>
    /// <typeparam name="TConflict">The conflict item type, which differs between the node and doc-type endpoints.</typeparam>
    /// <param name="conflicts">The pairs or triples whose stored entries moved.</param>
    /// <returns>A ProblemDetails with <c>Type</c>, <c>Title</c> and <c>Status</c> all populated.</returns>
    internal static ProblemDetails Create<TConflict>(IReadOnlyList<TConflict> conflicts)
    {
        var problem = new ProblemDetails
        {
            Type = ProblemType,
            Title = "Permissions changed since they were loaded",
            Status = StatusCodes.Status409Conflict,
            Detail = $"{conflicts.Count} permission set(s) no longer match the stamp the client sent, so nothing was written. "
                + "The current stored entries for each are in the 'conflicts' extension member. "
                + "Resend with force=true to overwrite them.",
        };
        problem.Extensions[ConflictsKey] = conflicts;
        return problem;
    }
}
