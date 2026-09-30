using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Community.AdvancedPermissions.Controllers.Models;
using Umbraco.Community.AdvancedPermissions.Core.Concurrency;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Exceptions;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.Controllers;

/// <summary>
/// Management API for the document-type permission editor and the Create Audit view.
/// </summary>
/// <remarks>
/// A management surface (doc-type editor + Create Audit) exposing a mutating save endpoint, so it is
/// gated on Users-section access.
/// </remarks>
/// <param name="docTypeService">The doc-type permission service.</param>
/// <param name="docTypeRepository">The doc-type permission repository (raw entry lookups for the reasoning dialog).</param>
/// <param name="contentTypeService">The Umbraco content-type service used to list doc-types.</param>
/// <param name="entityService">Used to resolve content node paths for the audit.</param>
/// <param name="userService">Used to look up the audited user's groups.</param>
[ApiVersion("1.0")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessUsers)]
public sealed class DocTypePermissionsController(
    IDocTypePermissionService docTypeService,
    IDocTypePermissionRepository docTypeRepository,
    IContentTypeService contentTypeService,
    IEntityService entityService,
    IUserService userService)
    : AdvancedPermissionsControllerBase
{
    /// <summary>
    /// Lists every non-element document type. Used by the editor's doc-type picker.
    /// </summary>
    /// <returns>The list of non-element doc-types.</returns>
    [HttpGet("doc-type-permissions/doc-types")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<IReadOnlyList<DocTypeListItemModel>>(StatusCodes.Status200OK)]
    [EndpointSummary("Lists non-element document types.")]
    public IActionResult GetDocTypes()
    {
        var allTypes = contentTypeService.GetAll();
        var items = allTypes
            .Where(ct => !ct.IsElement)
            .OrderBy(ct => ct.Name, StringComparer.OrdinalIgnoreCase)
            .Select(ct => new DocTypeListItemModel(
                ct.Key,
                ct.Alias,
                ct.Name ?? ct.Alias,
                ct.Icon))
            .ToList();

        return Ok(items);
    }

    /// <summary>
    /// Gets all stored entries for a selected (role, doc-type) combination, grouped by node with
    /// each node's own concurrency stamp. The editor uses these to render the tree with the
    /// current state per node, and sends the stamp back when saving that node's triple.
    /// </summary>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <param name="roleAlias">The role alias selected in the editor.</param>
    /// <param name="contentTypeKey">The doc-type selected in the editor.</param>
    /// <returns>One entry per node that has stored entries for the combination.</returns>
    [HttpGet("doc-type-permissions")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<IReadOnlyList<DocTypeEditorNodeResponseModel>>(StatusCodes.Status200OK)]
    [EndpointSummary("Gets all doc-type permission entries for a (role, content-type) combination.")]
    public async Task<IActionResult> GetForEditor(
        CancellationToken cancellationToken,
        string roleAlias,
        Guid contentTypeKey)
    {
        var entries = await docTypeService.GetEditorEntriesAsync(roleAlias, contentTypeKey, cancellationToken);

        var byNode = entries
            .GroupBy(e => e.NodeKey)
            .Select(g =>
            {
                var mapped = g.Select(MapDocTypeEntry).ToList();
                return new DocTypeEditorNodeResponseModel(g.Key, mapped, mapped.ComputeFromResponse());
            })
            .ToList();

        return Ok(byNode);
    }

    /// <summary>
    /// Saves (replaces) the entries for a (node, role, content-type) triple. Empty list clears.
    /// </summary>
    /// <param name="request">The entries to save.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>
    /// <see cref="StatusCodes.Status200OK"/> on success, or <see cref="StatusCodes.Status409Conflict"/>
    /// when <see cref="SaveDocTypePermissionsRequestModel.ExpectedStamp"/> no longer matches what is stored.
    /// </returns>
    [HttpPut("doc-type-permissions")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<BatchSaveDocTypeConflictResponseModel>(StatusCodes.Status409Conflict)]
    [EndpointSummary("Saves (replaces) doc-type permission entries for a node+role+content-type triple.")]
    public async Task<IActionResult> Save(
        [FromBody] SaveDocTypePermissionsRequestModel request,
        CancellationToken cancellationToken)
    {
        if (!TryMapEntries(request.Entries, out var mapped, out var problem))
        {
            return BadRequest(problem);
        }

        // The stamp is checked by the write itself, inside its transaction, not here. A check made
        // here and a write made afterwards are two operations, and two saves carrying the same
        // stamp could both pass the first and then both perform the second. Force withholds the
        // stamp, which is what tells the write to skip the check.
        try
        {
            await docTypeService.SaveEditorEntriesAsync(
                request.NodeKey,
                request.RoleAlias,
                request.ContentTypeKey,
                mapped,
                request.Force ? null : request.ExpectedStamp,
                cancellationToken);
        }
        catch (DocTypePermissionConcurrencyException ex)
        {
            return ConflictResult(ex);
        }

        return Ok();
    }

    /// <summary>
    /// Replaces doc-type permission entries for several node, user group and document type triples
    /// at once, refusing the whole batch if any triple has changed since the client read it.
    /// </summary>
    /// <remarks>
    /// All or nothing, deliberately — same reasoning as
    /// <see cref="AdvancedPermissionsPermissionController.BatchSavePermissions"/>. The one
    /// structural difference is the extra <c>ContentTypeKey</c> dimension: every key this endpoint
    /// compares, guards or reports on is the full (node, role, content-type) triple, never just
    /// the node and role.
    /// </remarks>
    /// <param name="request">The triples to write, each with the stamp the client read.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>
    /// <see cref="StatusCodes.Status200OK"/> with the new stamp per triple, or
    /// <see cref="StatusCodes.Status409Conflict"/> naming the triples that moved.
    /// </returns>
    [HttpPut("doc-type-permissions/batch")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<IReadOnlyList<BatchSavedDocTypeStamp>>(StatusCodes.Status200OK)]
    [ProducesResponseType<BatchSaveDocTypeConflictResponseModel>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [EndpointSummary("Saves doc-type permission entries for several node+role+content-type triples at once, all or nothing.")]
    public async Task<IActionResult> BatchSaveDocTypePermissions(
        [FromBody] BatchSaveDocTypePermissionsRequestModel request,
        CancellationToken cancellationToken)
    {
        // Validate everything first. A batch that cannot be mapped is a client bug, not a
        // conflict, and reporting it as one would send the user to a dialog about somebody else's
        // changes when nobody else has changed anything.
        var pending = new List<(Guid NodeKey, string RoleAlias, Guid ContentTypeKey, IReadOnlyList<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries, string? ExpectedStamp)>();

        foreach (var node in request.Nodes)
        {
            if (!TryMapEntries(node.Entries, out var mapped, out var problem))
            {
                return BadRequest(problem);
            }

            pending.Add((node.NodeKey, node.RoleAlias, node.ContentTypeKey, mapped, request.Force ? null : node.ExpectedStamp));
        }

        // A repeated (NodeKey, RoleAlias, ContentTypeKey) triple is a malformed request, the same
        // class of problem as a bad verb, so it is checked here alongside the other request-shape
        // validation. IDocTypePermissionService.SaveManyAsync (via the repository) also guards
        // this, but that guard is a last-resort invariant check deep in the write path, not a
        // request-validation mechanism — there is no global exception handler in this package, so
        // letting its ArgumentException escape from here would turn a client mistake into an
        // unhandled 500. The triple, not just the node and role, is what must be unique here: two
        // entries for the same node and role but different content types are entirely legitimate.
        var seenTriples = new HashSet<(Guid NodeKey, string RoleAlias, Guid ContentTypeKey)>();
        foreach (var node in request.Nodes)
        {
            if (!seenTriples.Add((node.NodeKey, node.RoleAlias, node.ContentTypeKey)))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Duplicate node, role and content-type triple",
                    Detail = $"Node '{node.NodeKey}', role '{node.RoleAlias}' and content type '{node.ContentTypeKey}' appear more than once in this batch. Each triple must appear at most once.",
                    Status = StatusCodes.Status400BadRequest,
                });
            }
        }

        // The stamps are checked by the write, inside its transaction — not by a pass over the
        // stored entries here, which would be a separate operation two saves could both pass. The
        // exception, when it comes, lists every conflicted triple and guarantees nothing was
        // written for any of them. Force withholds the stamps, which tells the write to skip the check.
        try
        {
            await docTypeService.SaveManyAsync(pending, cancellationToken);
        }
        catch (DocTypePermissionConcurrencyException ex)
        {
            return ConflictResult(ex);
        }

        // The new stamps, so the client can keep editing without a further read. Computed from
        // what was written rather than re-read, because the write just made them equal and a
        // further round trip per triple would buy nothing.
        var saved = request.Nodes
            .Select(n => new BatchSavedDocTypeStamp(
                n.NodeKey,
                n.RoleAlias,
                n.ContentTypeKey,
                PermissionStamp.ComputeFromNames(
                    n.Entries.Select(e => (e.Verb, e.State, e.Scope, e.IsPriorityOverride)))))
            .ToList();

        return Ok(saved);
    }

    /// <summary>
    /// Turns the exception a stale write raises into the <c>409 Conflict</c> the client is written
    /// against: a <see cref="ProblemDetails"/> carrying the conflicts as its <c>conflicts</c>
    /// extension. The single copy of this mapping, shared by <see cref="Save"/> and
    /// <see cref="BatchSaveDocTypePermissions"/>, so the two endpoints cannot drift into different
    /// bodies. The body's shape is a wire contract — see <see cref="ConflictProblemDetails"/>.
    /// </summary>
    /// <param name="exception">The exception listing every conflicted triple.</param>
    /// <returns>The 409 result.</returns>
    private ConflictObjectResult ConflictResult(DocTypePermissionConcurrencyException exception) =>
        Conflict(ConflictProblemDetails.Create<BatchSaveDocTypeConflict>(
            exception.Conflicts
                .Select(c => new BatchSaveDocTypeConflict(
                    c.NodeKey,
                    c.RoleAlias,
                    c.ContentTypeKey,
                    c.CurrentEntries.Select(MapDocTypeEntry).ToList(),
                    c.CurrentStamp))
                .ToList()));

    /// <summary>
    /// Validates and maps the entries of one save, or produces the problem describing why it
    /// cannot be mapped. The single copy of the verb/state/scope validation used by both
    /// <see cref="Save"/> and <see cref="BatchSaveDocTypePermissions"/> — a second copy is exactly how two
    /// endpoints on the same controller would drift into accepting different things.
    /// </summary>
    /// <param name="items">The raw entries from the request.</param>
    /// <param name="mapped">The mapped entries, when validation succeeds.</param>
    /// <param name="problem">The problem to return, when it does not.</param>
    /// <returns><see langword="true"/> when every entry is valid.</returns>
    private static bool TryMapEntries(
        IReadOnlyList<SavePermissionEntryItem> items,
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
                    Detail = $"'{entry.Scope}' is not a valid permission scope.",
                    Status = StatusCodes.Status400BadRequest,
                };
                return false;
            }

            if (!AdvancedPermissionsConstants.DocTypeVerbs.Contains(entry.Verb, StringComparer.Ordinal))
            {
                problem = new ProblemDetails
                {
                    Title = "Invalid verb",
                    Detail = $"'{entry.Verb}' is not a recognized doc-type permission verb.",
                    Status = StatusCodes.Status400BadRequest,
                };
                return false;
            }

            mapped.Add((entry.Verb, state, scope, entry.IsPriorityOverride));
        }

        return true;
    }

    /// <summary>
    /// Tree-style audit endpoint: for one node, returns one row per non-element doc-type with
    /// whether the audited subject may create it, plus an <c>IsInAllowedChildren</c> flag so
    /// the UI can render a distinct `n/a` state when the type is structurally disallowed under
    /// the parent (regardless of resolver outcome).
    ///
    /// Supply EITHER <paramref name="userKey"/> (audits a user — all their groups plus $everyone)
    /// or <paramref name="roleAlias"/> (audits a single role — just that role plus $everyone).
    /// </summary>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <param name="nodeKey">
    /// The parent node, or <c>AdvancedPermissionsConstants.VirtualRootNodeKey</c> for root-level audits.
    /// </param>
    /// <param name="userKey">The user whose effective permissions to compute. Mutually exclusive with <paramref name="roleAlias"/>.</param>
    /// <param name="roleAlias">A specific role to audit. Mutually exclusive with <paramref name="userKey"/>.</param>
    [HttpGet("doc-type-permissions/audit-for-node")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<DocTypeAuditForNodeResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [EndpointSummary("Audits which doc-types a subject may create under a given parent (tree-style).")]
    public async Task<IActionResult> AuditForNode(
        CancellationToken cancellationToken,
        Guid nodeKey,
        Guid? userKey = null,
        string? roleAlias = null)
    {
        var hasUser = userKey.HasValue && userKey.Value != Guid.Empty;
        var hasRole = !string.IsNullOrWhiteSpace(roleAlias);
        if (hasUser == hasRole)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid subject",
                Detail = "Supply exactly one of userKey or roleAlias.",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        IReadOnlyList<string> roleAliases;
        if (hasUser)
        {
            var user = await userService.GetAsync(userKey!.Value);
            if (user is null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "User not found",
                    Detail = $"No user with key {userKey}.",
                    Status = StatusCodes.Status404NotFound,
                });
            }
            var aliases = new List<string>(user.Groups.Count() + 1);
            aliases.AddRange(user.Groups.Select(g => g.Alias));
            aliases.Add(AdvancedPermissionsConstants.EveryoneRoleAlias);
            roleAliases = aliases;
        }
        else
        {
            // Role mode: just the chosen role + $everyone.
            roleAliases = [roleAlias!, AdvancedPermissionsConstants.EveryoneRoleAlias];
        }

        var isVirtualRoot = nodeKey == AdvancedPermissionsConstants.VirtualRootNodeKey;
        var path = isVirtualRoot
            ? [AdvancedPermissionsConstants.VirtualRootNodeKey]
            : BuildPathFromRoot(nodeKey, entityService);
        if (path.Count == 0)
        {
            path = [AdvancedPermissionsConstants.VirtualRootNodeKey];
        }

        var allowedChildren = await GetAllowedChildrenKeysAsync(nodeKey, isVirtualRoot);

        var candidates = contentTypeService.GetAll()
            .Where(ct => !ct.IsElement)
            .OrderBy(ct => ct.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rows = new List<DocTypeAuditForNodeRowResponseModel>(candidates.Count);
        foreach (var ct in candidates)
        {
            var effective = await docTypeService.ResolveCreateForRolesAsync(
                roleAliases,
                path,
                ct.Key,
                cancellationToken);

            rows.Add(new DocTypeAuditForNodeRowResponseModel(
                ContentTypeKey: ct.Key,
                ContentTypeAlias: ct.Alias,
                ContentTypeName: ct.Name ?? ct.Alias,
                ContentTypeIcon: ct.Icon,
                IsAllowed: effective.IsAllowed,
                IsExplicit: effective.IsExplicit,
                IsInAllowedChildren: allowedChildren.Contains(ct.Key),
                Reasoning: effective.Reasoning.Select(MapReasoningItem).ToList(),
                WasPriorityOverrideActive: effective.WasPriorityOverrideActive,
                SuppressedReasoning: (effective.SuppressedReasoning ?? []).Select(MapReasoningItem).ToList()));
        }

        return Ok(new DocTypeAuditForNodeResponseModel(nodeKey, rows));
    }

    /// <summary>
    /// Returns the inheritance path for a node together with all stored doc-type entries along
    /// that path filtered to the supplied content-type-key. Powers the reasoning dialog of the
    /// tree-style audit.
    /// </summary>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <param name="nodeKey">The audited node, or virtual root.</param>
    /// <param name="contentTypeKey">The doc-type whose reasoning to surface.</param>
    [HttpGet("doc-type-permissions/path-entries")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<DocTypePathEntriesResponseModel>(StatusCodes.Status200OK)]
    [EndpointSummary("Gets the inheritance path and stored doc-type entries along that path for a content-type.")]
    public async Task<IActionResult> PathEntries(
        CancellationToken cancellationToken,
        Guid nodeKey,
        Guid contentTypeKey)
    {
        var isVirtualRoot = nodeKey == AdvancedPermissionsConstants.VirtualRootNodeKey;
        var contentPath = isVirtualRoot ? [] : BuildPathFromRoot(nodeKey, entityService);

        var pathNodes = new List<PathNodeModel>
        {
            new(
                AdvancedPermissionsConstants.VirtualRootNodeKey,
                AdvancedPermissionsConstants.EveryoneRoleDisplayName,
                "icon-globe"),
        };

        if (contentPath.Count > 0)
        {
            var entities = entityService
                .GetAll(UmbracoObjectTypes.Document, contentPath.ToArray())
                .ToDictionary(e => e.Key);

            foreach (var key in contentPath)
            {
                if (entities.TryGetValue(key, out var entity))
                {
                    var icon = entity is IContentEntitySlim contentSlim ? contentSlim.ContentTypeIcon : null;
                    pathNodes.Add(new PathNodeModel(key, entity.Name ?? string.Empty, icon));
                }
            }
        }

        var entries = await docTypeRepository.GetByContentTypeAndNodesAsync(
            contentTypeKey,
            pathNodes.Select(p => p.Key),
            cancellationToken);

        var mapped = entries
            .Select(e => new DocTypePathEntryResponseModel(
                e.Id,
                e.NodeKey,
                e.ContentTypeKey,
                e.RoleAlias,
                e.Verb,
                e.State.ToString(),
                e.Scope.ToString(),
                e.IsPriorityOverride))
            .ToList();

        return Ok(new DocTypePathEntriesResponseModel(pathNodes, mapped));
    }

    /// <summary>
    /// Resolves the set of doc-type keys structurally allowed as children of the supplied parent.
    /// For the virtual root, returns the set of doc-types flagged <c>IsAllowedAsRoot</c>.
    /// </summary>
    /// <param name="parentNodeKey">The parent node key, or virtual root.</param>
    /// <param name="isVirtualRoot">Whether the parent is the virtual root.</param>
    /// <returns>A set of doc-type keys structurally permitted under the parent.</returns>
    private Task<HashSet<Guid>> GetAllowedChildrenKeysAsync(Guid parentNodeKey, bool isVirtualRoot)
    {
        if (isVirtualRoot)
        {
            return Task.FromResult(contentTypeService.GetAll()
                .Where(ct => !ct.IsElement && ct.AllowedAsRoot)
                .Select(ct => ct.Key)
                .ToHashSet());
        }

        // Resolve the parent's content type, then read its allowed-children list
        var parent = entityService.Get(parentNodeKey, UmbracoObjectTypes.Document);
        if (parent is null)
        {
            return Task.FromResult(new HashSet<Guid>());
        }

        // The parent's content-type key comes through IContentEntitySlim
        if (parent is not IContentEntitySlim slim)
        {
            return Task.FromResult(new HashSet<Guid>());
        }

        var parentContentType = contentTypeService.Get(slim.ContentTypeAlias);
        if (parentContentType?.AllowedContentTypes is null)
        {
            return Task.FromResult(new HashSet<Guid>());
        }

        return Task.FromResult(parentContentType.AllowedContentTypes
            .Select(c => c.Key)
            .Where(k => k != Guid.Empty)
            .ToHashSet());
    }

    /// <summary>
    /// Maps a domain entry to the API response shape.
    /// </summary>
    /// <param name="entry">The domain entry.</param>
    /// <returns>The response model.</returns>
    private static DocTypePermissionEntryResponseModel MapDocTypeEntry(DocTypePermissionEntry entry) =>
        new(
            entry.Id,
            entry.NodeKey,
            entry.ContentTypeKey,
            entry.RoleAlias,
            entry.Verb,
            entry.State.ToString(),
            entry.Scope.ToString(),
            entry.IsPriorityOverride);

    /// <summary>
    /// Maps a domain reasoning entry to the API <see cref="ReasoningItem"/> shape, threading
    /// the priority-override flag through to the response.
    /// </summary>
    /// <param name="r">The domain reasoning entry.</param>
    /// <returns>The mapped API item.</returns>
    private static ReasoningItem MapReasoningItem(PermissionReasoning r) =>
        new(
            r.ContributingRole,
            r.State.ToString(),
            r.IsExplicit,
            r.SourceNodeKey,
            r.SourceScope?.ToString(),
            r.IsFromGroupDefault,
            r.IsPriorityOverride);
}
