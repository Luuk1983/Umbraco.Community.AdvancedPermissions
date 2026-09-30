using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Community.AdvancedPermissions.Controllers.Models;
using Umbraco.Community.AdvancedPermissions.Core.Concurrency;
using Umbraco.Community.AdvancedPermissions.Core.Exceptions;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.Controllers;

/// <summary>
/// The request handling shared by every permission save endpoint: validating and mapping the
/// entries of a request, refusing a malformed batch, and turning a stale write into the
/// <c>409 Conflict</c> the client is written against.
/// </summary>
/// <remarks>
/// <para>
/// There is exactly one copy of the verb, state and scope validation, and one of the duplicate-key
/// guard, used by the content, library element and document-type controllers. A second copy is how
/// two endpoints on the same surface drift into accepting different things.
/// </para>
/// <para>
/// The stamp is <em>not</em> checked here. It is handed to the service and checked by the write
/// itself, inside its transaction: a check made in a controller and a write made afterwards are two
/// operations, and two saves carrying the same stamp could both pass the first and then both perform
/// the second. What this class does is validate the request's shape, hand over the stamps (withheld
/// when the client forces the save, which is what tells the write to skip the check) and report the
/// refusal when it comes.
/// </para>
/// </remarks>
internal static class PermissionSaveRequests
{
    /// <summary>
    /// Validates and maps the entries of one save, or produces the problem describing why they cannot
    /// be mapped.
    /// </summary>
    /// <param name="items">The raw entries from the request.</param>
    /// <param name="validVerbs">The verbs this endpoint accepts.</param>
    /// <param name="verbDescription">
    /// What to call the verb set in the error message, e.g. <c>element permission verb</c>.
    /// </param>
    /// <param name="mapped">The mapped entries, when validation succeeds.</param>
    /// <param name="problem">The problem to return, when it does not.</param>
    /// <returns><see langword="true"/> when every entry is valid.</returns>
    internal static bool TryMapEntries(
        IReadOnlyList<SavePermissionEntryItem> items,
        IReadOnlyList<string> validVerbs,
        string verbDescription,
        out List<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> mapped,
        out ProblemDetails? problem)
    {
        mapped = [];
        problem = null;

        foreach (var entry in items)
        {
            if (!Enum.TryParse<PermissionState>(entry.State, ignoreCase: true, out var state))
            {
                problem = new ProblemDetails
                {
                    Title = "Invalid state",
                    Detail = $"'{entry.State}' is not a valid permission state. Use 'Allow' or 'Deny'.",
                    Status = StatusCodes.Status400BadRequest,
                };
                return false;
            }

            if (!Enum.TryParse<PermissionScope>(entry.Scope, ignoreCase: true, out var scope))
            {
                problem = new ProblemDetails
                {
                    Title = "Invalid scope",
                    Detail = $"'{entry.Scope}' is not a valid permission scope. Use 'ThisNodeOnly', 'ThisNodeAndDescendants', or 'DescendantsOnly'.",
                    Status = StatusCodes.Status400BadRequest,
                };
                return false;
            }

            if (!validVerbs.Contains(entry.Verb, StringComparer.Ordinal))
            {
                problem = new ProblemDetails
                {
                    Title = "Invalid verb",
                    Detail = $"'{entry.Verb}' is not a recognized {verbDescription}.",
                    Status = StatusCodes.Status400BadRequest,
                };
                return false;
            }

            mapped.Add((entry.Verb, state, scope, entry.IsPriorityOverride));
        }

        return true;
    }

    /// <summary>
    /// Saves one node and user group's entries: validates the request, hands it to the service with
    /// the client's stamp, and answers <c>200</c>, <c>400</c> or <c>409</c>.
    /// </summary>
    /// <param name="service">The node-keyed service (content or library element) to write through.</param>
    /// <param name="request">The save request.</param>
    /// <param name="validVerbs">The verbs the endpoint accepts.</param>
    /// <param name="verbDescription">What to call the verb set in the error message.</param>
    /// <param name="mapEntry">Maps a stored entry to its response model, for the conflict body.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The result to return from the action.</returns>
    internal static async Task<IActionResult> SaveNodeAsync(
        INodePermissionService service,
        SavePermissionsRequestModel request,
        IReadOnlyList<string> validVerbs,
        string verbDescription,
        Func<AdvancedPermissionEntry, PermissionEntryResponseModel> mapEntry,
        CancellationToken cancellationToken)
    {
        if (!TryMapEntries(request.Entries, validVerbs, verbDescription, out var mapped, out var problem))
        {
            return new BadRequestObjectResult(problem);
        }

        // The stamp is checked by the write itself, inside its transaction, not here. Force
        // withholds the stamp, which is what tells the write to skip the check.
        try
        {
            await service.SaveEntriesAsync(
                request.NodeKey,
                request.RoleAlias,
                mapped,
                request.Force ? null : request.ExpectedStamp,
                cancellationToken);
        }
        catch (PermissionConcurrencyException ex)
        {
            return NodeConflict(ex, mapEntry);
        }

        return new OkResult();
    }

    /// <summary>
    /// Saves several nodes and user groups at once, all or nothing: validates the whole request
    /// first, hands it to the service with each pair's stamp, and answers <c>200</c> with the new
    /// stamps, <c>400</c> or <c>409</c>.
    /// </summary>
    /// <remarks>
    /// Everything is validated before anything is written, and a batch that cannot be mapped is a
    /// <c>400</c>, never a <c>409</c>: reporting a client bug as a conflict would send the user to a
    /// dialog about somebody else's changes when nobody else has changed anything.
    /// </remarks>
    /// <param name="service">The node-keyed service (content or library element) to write through.</param>
    /// <param name="request">The batch request.</param>
    /// <param name="validVerbs">The verbs the endpoint accepts.</param>
    /// <param name="verbDescription">What to call the verb set in the error message.</param>
    /// <param name="mapEntry">Maps a stored entry to its response model, for the conflict body.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The result to return from the action.</returns>
    internal static async Task<IActionResult> SaveNodeBatchAsync(
        INodePermissionService service,
        BatchSavePermissionsRequestModel request,
        IReadOnlyList<string> validVerbs,
        string verbDescription,
        Func<AdvancedPermissionEntry, PermissionEntryResponseModel> mapEntry,
        CancellationToken cancellationToken)
    {
        var pending = new List<(Guid NodeKey, string RoleAlias, IReadOnlyList<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries, string? ExpectedStamp)>();

        foreach (var node in request.Nodes)
        {
            if (!TryMapEntries(node.Entries, validVerbs, verbDescription, out var mapped, out var problem))
            {
                return new BadRequestObjectResult(problem);
            }

            pending.Add((node.NodeKey, node.RoleAlias, mapped, request.Force ? null : node.ExpectedStamp));
        }

        // A repeated (NodeKey, RoleAlias) pair is a malformed request, the same class of problem as
        // a bad verb, so it is checked here alongside the other request-shape validation, before any
        // stamp work. The repository guards this too, but that guard is a last-resort invariant check
        // deep in the write path, not a request-validation mechanism: there is no global exception
        // handler in this package, so letting its ArgumentException escape would turn a client
        // mistake into an unhandled 500.
        var seenPairs = new HashSet<(Guid NodeKey, string RoleAlias)>();
        foreach (var node in request.Nodes)
        {
            if (!seenPairs.Add((node.NodeKey, node.RoleAlias)))
            {
                return new BadRequestObjectResult(new ProblemDetails
                {
                    Title = "Duplicate node and role pair",
                    Detail = $"Node '{node.NodeKey}' and role '{node.RoleAlias}' appear more than once in this batch. Each node+role pair must appear at most once.",
                    Status = StatusCodes.Status400BadRequest,
                });
            }
        }

        // As with the single save, the stamps are checked by the write, inside its transaction, not
        // by a pass over the stored entries here. The exception, when it comes, lists every
        // conflicted pair and guarantees nothing was written for any pair.
        try
        {
            await service.SaveManyAsync(pending, cancellationToken);
        }
        catch (PermissionConcurrencyException ex)
        {
            return NodeConflict(ex, mapEntry);
        }

        // The new stamps, so the client can keep editing without a further read. Computed from what
        // was written rather than re-read, because the write just made them equal and a second round
        // trip per node would buy nothing.
        var saved = request.Nodes
            .Select(n => new BatchSavedStamp(
                n.NodeKey,
                n.RoleAlias,
                PermissionStamp.ComputeFromNames(
                    n.Entries.Select(e => (e.Verb, e.State, e.Scope, e.IsPriorityOverride)))))
            .ToList();

        return new OkObjectResult(saved);
    }

    /// <summary>
    /// Saves one document-type triple's entries: validates the request, hands it to the service with
    /// the client's stamp, and answers <c>200</c>, <c>400</c> or <c>409</c>.
    /// </summary>
    /// <param name="service">The document-type service to write through.</param>
    /// <param name="request">The save request.</param>
    /// <param name="validVerbs">The verbs the endpoint accepts.</param>
    /// <param name="verbDescription">What to call the verb set in the error message.</param>
    /// <param name="mapEntry">Maps a stored entry to its response model, for the conflict body.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The result to return from the action.</returns>
    internal static async Task<IActionResult> SaveDocTypeAsync(
        IDocTypePermissionService service,
        SaveDocTypePermissionsRequestModel request,
        IReadOnlyList<string> validVerbs,
        string verbDescription,
        Func<DocTypePermissionEntry, DocTypePermissionEntryResponseModel> mapEntry,
        CancellationToken cancellationToken)
    {
        if (!TryMapEntries(request.Entries, validVerbs, verbDescription, out var mapped, out var problem))
        {
            return new BadRequestObjectResult(problem);
        }

        // The stamp is checked by the write itself, inside its transaction, not here. Force
        // withholds the stamp, which is what tells the write to skip the check.
        try
        {
            await service.SaveEditorEntriesAsync(
                request.NodeKey,
                request.RoleAlias,
                request.ContentTypeKey,
                mapped,
                request.Force ? null : request.ExpectedStamp,
                cancellationToken);
        }
        catch (DocTypePermissionConcurrencyException ex)
        {
            return DocTypeConflict(ex, mapEntry);
        }

        return new OkResult();
    }

    /// <summary>
    /// Saves several document-type triples at once, all or nothing, with the same contract as
    /// <see cref="SaveNodeBatchAsync"/> but keyed on node, user group and document type.
    /// </summary>
    /// <param name="service">The document-type service to write through.</param>
    /// <param name="request">The batch request.</param>
    /// <param name="validVerbs">The verbs the endpoint accepts.</param>
    /// <param name="verbDescription">What to call the verb set in the error message.</param>
    /// <param name="mapEntry">Maps a stored entry to its response model, for the conflict body.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The result to return from the action.</returns>
    internal static async Task<IActionResult> SaveDocTypeBatchAsync(
        IDocTypePermissionService service,
        BatchSaveDocTypePermissionsRequestModel request,
        IReadOnlyList<string> validVerbs,
        string verbDescription,
        Func<DocTypePermissionEntry, DocTypePermissionEntryResponseModel> mapEntry,
        CancellationToken cancellationToken)
    {
        var pending = new List<(Guid NodeKey, string RoleAlias, Guid ContentTypeKey, IReadOnlyList<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries, string? ExpectedStamp)>();

        foreach (var node in request.Nodes)
        {
            if (!TryMapEntries(node.Entries, validVerbs, verbDescription, out var mapped, out var problem))
            {
                return new BadRequestObjectResult(problem);
            }

            pending.Add((node.NodeKey, node.RoleAlias, node.ContentTypeKey, mapped, request.Force ? null : node.ExpectedStamp));
        }

        // The triple, not just the node and role, is what must be unique: two entries for the same
        // node and role but different content types are entirely legitimate. Checked here for the
        // reason given on SaveNodeBatchAsync - the repository's guard must never be what reports it.
        var seenTriples = new HashSet<(Guid NodeKey, string RoleAlias, Guid ContentTypeKey)>();
        foreach (var node in request.Nodes)
        {
            if (!seenTriples.Add((node.NodeKey, node.RoleAlias, node.ContentTypeKey)))
            {
                return new BadRequestObjectResult(new ProblemDetails
                {
                    Title = "Duplicate node, role and content-type triple",
                    Detail = $"Node '{node.NodeKey}', role '{node.RoleAlias}' and content type '{node.ContentTypeKey}' appear more than once in this batch. Each triple must appear at most once.",
                    Status = StatusCodes.Status400BadRequest,
                });
            }
        }

        try
        {
            await service.SaveManyAsync(pending, cancellationToken);
        }
        catch (DocTypePermissionConcurrencyException ex)
        {
            return DocTypeConflict(ex, mapEntry);
        }

        var saved = request.Nodes
            .Select(n => new BatchSavedDocTypeStamp(
                n.NodeKey,
                n.RoleAlias,
                n.ContentTypeKey,
                PermissionStamp.ComputeFromNames(
                    n.Entries.Select(e => (e.Verb, e.State, e.Scope, e.IsPriorityOverride)))))
            .ToList();

        return new OkObjectResult(saved);
    }

    /// <summary>
    /// Turns the exception a stale document-type write raises into the <c>409 Conflict</c> the client
    /// is written against, as <see cref="NodeConflict"/> does for the node-keyed families.
    /// </summary>
    /// <param name="exception">The exception listing every conflicted triple.</param>
    /// <param name="mapEntry">Maps a stored entry to its response model.</param>
    /// <returns>The 409 result.</returns>
    private static ConflictObjectResult DocTypeConflict(
        DocTypePermissionConcurrencyException exception,
        Func<DocTypePermissionEntry, DocTypePermissionEntryResponseModel> mapEntry) =>
        new(ConflictProblemDetails.Create(
            exception.Conflicts
                .Select(c => new BatchSaveDocTypeConflict(
                    c.NodeKey,
                    c.RoleAlias,
                    c.ContentTypeKey,
                    c.CurrentEntries.Select(mapEntry).ToList(),
                    c.CurrentStamp))
                .ToList()));

    /// <summary>
    /// Turns the exception a stale node write raises into the <c>409 Conflict</c> the client is
    /// written against: a <see cref="ProblemDetails"/> carrying the conflicts as its
    /// <c>conflicts</c> extension. The body's shape is a wire contract - see
    /// <see cref="ConflictProblemDetails"/>.
    /// </summary>
    /// <param name="exception">The exception listing every conflicted pair.</param>
    /// <param name="mapEntry">Maps a stored entry to its response model.</param>
    /// <returns>The 409 result.</returns>
    private static ConflictObjectResult NodeConflict(
        PermissionConcurrencyException exception,
        Func<AdvancedPermissionEntry, PermissionEntryResponseModel> mapEntry) =>
        new(ConflictProblemDetails.Create(
            exception.Conflicts
                .Select(c => new BatchSaveConflict(
                    c.NodeKey,
                    c.RoleAlias,
                    c.CurrentEntries.Select(mapEntry).ToList(),
                    c.CurrentStamp))
                .ToList()));
}
