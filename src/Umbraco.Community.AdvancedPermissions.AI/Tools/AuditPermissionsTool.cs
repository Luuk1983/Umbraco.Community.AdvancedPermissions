using Umbraco.AI.Core.Tools;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.AI.Services;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Tools;

/// <summary>
/// Scans stored permission entries for misconfigurations, conflicts, and risks and reports them in a
/// fully friendly form (role display names, friendly actions, node names, identifier-free messages —
/// never raw aliases/verbs/GUIDs). The slice that is audited is selected by
/// <see cref="AuditPermissionsArgs.Scope"/>:
/// <list type="bullet">
/// <item><description><b>Role</b> — every entry for one role across the tree, via <see cref="IAdvancedPermissionRepository.GetByRoleAsync"/>.</description></item>
/// <item><description><b>Subtree</b> — every entry on a node and its descendant document nodes; descendant keys are gathered from <see cref="IEntityService"/> and entries loaded via <see cref="IAdvancedPermissionRepository.GetByNodesAsync"/>.</description></item>
/// <item><description><b>All</b> — a best-effort sweep of the whole configuration: every live document node (descendants of root) plus the virtual-root sentinel, loaded via <see cref="IAdvancedPermissionRepository.GetByNodesAsync"/>. See the limitation note on the description.</description></item>
/// </list>
/// In every case the entries are handed to the analyzer, the findings are optionally filtered to a
/// minimum severity, and the report is projected through the presenter.
/// </summary>
/// <param name="repository">The repository used to load stored permission entries.</param>
/// <param name="analyzer">The analyzer that inspects the entries and produces findings.</param>
/// <param name="presenter">The presenter that maps the raw report to friendly labels.</param>
/// <param name="entityService">The Umbraco entity service used to enumerate descendant document nodes for subtree/all scopes.</param>
[AITool("uap_audit_permissions", "Audit permissions", ScopeId = "advanced-permissions:read")]
public sealed class AuditPermissionsTool(
    IAdvancedPermissionRepository repository,
    IPermissionAuditAnalyzer analyzer,
    IPermissionPresenter presenter,
    IEntityService entityService)
    : AIToolBase<AuditPermissionsArgs>
{
    /// <summary>The page size used when enumerating descendant document nodes.</summary>
    private const int PageSize = 500;

    /// <summary>
    /// The maximum number of findings returned. Larger sweeps (notably <see cref="AuditScope.All"/>) can
    /// produce many findings; the report is capped to keep the answer readable and truncation is noted.
    /// </summary>
    private const int MaxFindings = 100;

    /// <inheritdoc />
    public override string Description =>
        "Scan stored permission configuration for risks, conflicts and over-broad grants " +
        "(over-broad grants to All Users, Allow/Deny conflicts, risky 'this node and descendants' rules, priority overrides). " +
        "Set `scope`: role (all entries for one role — default), subtree (everything under a node), or all (whole config). " +
        "Optionally filter by minimum severity. " +
        "Use for 'audit/review the permissions for role X', 'any risks under /News?', 'is anything misconfigured?'. " +
        "Note: 'all' is best-effort — it sweeps every live document node plus the root-level defaults, so entries " +
        "left behind on deleted/trashed nodes are not included.";

    /// <inheritdoc />
    protected override async Task<object> ExecuteAsync(AuditPermissionsArgs args, CancellationToken cancellationToken = default)
    {
        var entriesResult = await LoadEntriesAsync(args, cancellationToken);
        if (entriesResult.Error is not null)
        {
            return entriesResult.Error;
        }

        var report = analyzer.Analyze(entriesResult.Entries!);
        var filtered = ApplyFilters(report, args.SeverityMin);
        return await presenter.ToFriendlyAuditAsync(filtered, cancellationToken);
    }

    /// <summary>
    /// Loads the stored entries to audit for the requested scope, or returns a friendly error when a
    /// required argument for that scope is missing.
    /// </summary>
    /// <param name="args">The tool arguments.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>The loaded entries, or an <see cref="AccessError"/> describing the missing argument.</returns>
    private async Task<(IReadOnlyList<AdvancedPermissionEntry>? Entries, AccessError? Error)> LoadEntriesAsync(
        AuditPermissionsArgs args,
        CancellationToken cancellationToken)
    {
        switch (args.Scope)
        {
            case AuditScope.Role:
                if (string.IsNullOrWhiteSpace(args.RoleAlias))
                {
                    return (null, new AccessError("A role is required when auditing a single role."));
                }

                return (await repository.GetByRoleAsync(args.RoleAlias, cancellationToken), null);

            case AuditScope.Subtree:
                if (args.NodeKey is null)
                {
                    return (null, new AccessError("A node is required when auditing a subtree."));
                }

                var subtreeKeys = GatherSubtreeKeys(args.NodeKey.Value);
                return (await repository.GetByNodesAsync(subtreeKeys, cancellationToken), null);

            case AuditScope.All:
                return (await repository.GetByNodesAsync(GatherAllKeys(), cancellationToken), null);

            default:
                return (null, new AccessError("Unknown audit scope."));
        }
    }

    /// <summary>
    /// Gathers the node itself plus every descendant document node key, so the subtree audit covers the
    /// whole branch rooted at the node.
    /// </summary>
    /// <param name="nodeKey">The root of the subtree to audit.</param>
    /// <returns>The node key followed by all descendant document keys.</returns>
    private IReadOnlyList<Guid> GatherSubtreeKeys(Guid nodeKey)
    {
        var keys = new List<Guid> { nodeKey };
        keys.AddRange(EnumerateDescendantKeys(nodeKey));
        return keys;
    }

    /// <summary>
    /// Gathers every live document node key (the descendants of the tree root) plus the virtual-root
    /// sentinel that carries root-level default entries. This is the best-effort key set for the
    /// whole-configuration audit; entries on nodes that no longer exist cannot be included.
    /// </summary>
    /// <returns>All live document keys plus the virtual-root sentinel.</returns>
    private IReadOnlyList<Guid> GatherAllKeys()
    {
        var keys = new List<Guid> { AdvancedPermissionsConstants.VirtualRootNodeKey };

        var pageIndex = 0L;
        long total;
        do
        {
            var page = entityService
                .GetPagedDescendants(UmbracoObjectTypes.Document, pageIndex, PageSize, out total)
                .ToList();
            keys.AddRange(page.Select(e => e.Key));
            pageIndex++;
        }
        while (pageIndex * PageSize < total);

        return keys;
    }

    /// <summary>
    /// Enumerates all descendant document node keys of the given node, paging through the entity service
    /// until every descendant has been collected. The node is resolved to its integer id first so the
    /// unambiguous id-based descendants overload can be used.
    /// </summary>
    /// <param name="nodeKey">The node whose descendants to enumerate.</param>
    /// <returns>The descendant document keys.</returns>
    private IEnumerable<Guid> EnumerateDescendantKeys(Guid nodeKey)
    {
        var idAttempt = entityService.GetId(nodeKey, UmbracoObjectTypes.Document);
        if (!idAttempt.Success)
        {
            return [];
        }

        return EnumerateDescendantKeys(idAttempt.Result);
    }

    /// <summary>
    /// Enumerates all descendant document node keys of the node with the given integer id, paging through
    /// the entity service until every descendant has been collected.
    /// </summary>
    /// <param name="nodeId">The integer id of the node whose descendants to enumerate.</param>
    /// <returns>The descendant document keys.</returns>
    private IEnumerable<Guid> EnumerateDescendantKeys(int nodeId)
    {
        var pageIndex = 0L;
        long total;
        do
        {
            var page = entityService
                .GetPagedDescendants(nodeId, UmbracoObjectTypes.Document, pageIndex, PageSize, out total)
                .ToList();

            foreach (var entity in page)
            {
                yield return entity.Key;
            }

            pageIndex++;
        }
        while (pageIndex * PageSize < total);
    }

    /// <summary>
    /// Applies the optional minimum-severity filter and caps the number of findings, recording how many
    /// entries were analyzed (unchanged) so the friendly report still reports the true inspected count.
    /// </summary>
    /// <param name="report">The raw analyzer report.</param>
    /// <param name="severityMin">The optional minimum severity; <see langword="null"/> keeps all findings.</param>
    /// <returns>A report whose findings honour the severity filter and the findings cap.</returns>
    private static AuditReport ApplyFilters(AuditReport report, AuditSeverity? severityMin)
    {
        IEnumerable<AuditFinding> findings = report.Findings;

        if (severityMin is { } min)
        {
            findings = findings.Where(f => f.Severity >= min);
        }

        var capped = findings.Take(MaxFindings).ToList();
        return report with { Findings = capped };
    }
}
