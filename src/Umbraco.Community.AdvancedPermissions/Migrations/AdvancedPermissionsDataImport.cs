using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Models.Membership.Permissions;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Runtime;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.Navigation;
using Umbraco.Cms.Core.Sync;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;
using Umbraco.Community.AdvancedPermissions.Data.Context;
using Umbraco.Community.AdvancedPermissions.Data.Entities;

namespace Umbraco.Community.AdvancedPermissions.Migrations;

/// <summary>
/// On first boot (when the permission table is empty), imports native Umbraco group permissions
/// so the effective security state matches what existed before the package was installed.
/// </summary>
/// <remarks>
/// <para>
/// Runs on <see cref="UmbracoApplicationStartingNotification"/> after the EF Core schema migration.
/// Guarded by <see cref="IMainDom"/> and <see cref="IServerRoleAccessor"/> to ensure only one
/// instance runs the import in load-balanced deployments.
/// </para>
/// <para>
/// Any failure during import is logged as a warning and does not prevent Umbraco from starting.
/// The system remains functional: all verbs default to Deny until entries are configured via
/// the Security Editor.
/// </para>
/// </remarks>
public sealed class AdvancedPermissionsDataImport(
    AdvancedPermissionsDbContext dbContext,
    IUserGroupService userGroupService,
    IDocumentNavigationQueryService navigationQueryService,
    IPermissionResolver permissionResolver,
    IMainDom mainDom,
    IServerRoleAccessor serverRoleAccessor,
    ILogger<AdvancedPermissionsDataImport> logger)
    : INotificationAsyncHandler<UmbracoApplicationStartingNotification>
{
    /// <inheritdoc />
    public async Task HandleAsync(
        UmbracoApplicationStartingNotification notification,
        CancellationToken cancellationToken)
    {
        // Only run at full runtime — not during install or upgrade
        if (notification.RuntimeLevel != global::Umbraco.Cms.Core.RuntimeLevel.Run)
        {
            return;
        }

        // Only the MainDom instance should write data (prevents duplicate imports in multi-instance setups)
        if (!mainDom.IsMainDom)
        {
            logger.LogDebug("Advanced Permissions: Skipping data import — not the main domain instance");
            return;
        }

        // Subscriber instances are read-only in load-balanced setups
        if (serverRoleAccessor.CurrentServerRole == ServerRole.Subscriber)
        {
            logger.LogDebug("Advanced Permissions: Skipping data import — subscriber instance");
            return;
        }

        // Only import when the table is completely empty (first install)
        var hasAnyEntries = await dbContext.Permissions.AnyAsync(cancellationToken);
        if (hasAnyEntries)
        {
            return;
        }

        logger.LogInformation("Advanced Permissions: Permission table is empty — importing from native Umbraco settings");

        try
        {
            await ImportAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Do NOT rethrow — a failed import must never prevent Umbraco from starting.
            // All verbs will default to Deny until the admin configures permissions via the Security Editor.
            logger.LogWarning(
                ex,
                "Advanced Permissions: Failed to import native permissions. " +
                "The system will start with empty permissions (all Deny by default). " +
                "Use the Security Editor to configure permissions manually.");
        }
    }

    /// <summary>
    /// Seeds the <c>$everyone</c> row and then iterates user groups page-by-page, flushing each
    /// group's computed rows to the DB independently so memory stays bounded to one group's
    /// worth of entries at any time.
    /// </summary>
    private async Task ImportAsync(CancellationToken cancellationToken)
    {
        var totalInserted = 0;

        // $everyone always gets Allow Read at the virtual root. Persist it first, in isolation —
        // it is never consulted during per-group resolver calls (each role resolves in isolation).
        var everyoneRow = new AdvancedPermissionEntity
        {
            Id = Guid.NewGuid(),
            NodeKey = AdvancedPermissionsConstants.VirtualRootNodeKey,
            RoleAlias = AdvancedPermissionsConstants.EveryoneRoleAlias,
            Verb = AdvancedPermissionsConstants.VerbRead,
            State = PermissionState.Allow,
            Scope = PermissionScope.ThisNodeAndDescendants,
        };
        await dbContext.Permissions.AddAsync(everyoneRow, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        totalInserted += 1;

        // Paginate user groups and flush per group.
        var skip = 0;
        const int take = 100;
        while (true)
        {
            var page = await userGroupService.GetAllAsync(skip, take);
            foreach (var group in page.Items)
            {
                var rows = ComputeGroupEntries(group);
                if (rows.Count == 0)
                {
                    continue;
                }

                await dbContext.Permissions.AddRangeAsync(rows, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                totalInserted += rows.Count;
            }

            skip += take;
            if (skip >= page.Total)
            {
                break;
            }
        }

        logger.LogInformation(
            "Advanced Permissions: Imported {Count} permission entries from native Umbraco settings",
            totalInserted);
    }

    /// <summary>
    /// Computes the minimal set of <see cref="AdvancedPermissionEntity"/> rows required to make the
    /// <see cref="IPermissionResolver"/> produce the same effective permissions for
    /// <paramref name="group"/> as native Umbraco.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Group defaults become virtual-root (<c>NodeKey = VirtualRootNodeKey</c>) Allow entries with
    /// <see cref="PermissionScope.ThisNodeAndDescendants"/>, replicating the "fallback to defaults
    /// for any node with no explicit permissions" behaviour of the native system.
    /// </para>
    /// <para>
    /// For every native <see cref="DocumentGranularPermission"/> node, the method:
    /// </para>
    /// <list type="number">
    ///   <item>Resolves the node's <c>PathFromRoot</c> via <see cref="IDocumentNavigationQueryService"/>.
    ///   Nodes whose key no longer exists in the content tree are skipped with a warning.</item>
    ///   <item>Sorts the granular nodes ancestors-first so the resolver sees ancestor entries
    ///   before their descendants.</item>
    ///   <item>For each verb in the union of the group's defaults and the node's granular set,
    ///   asks the resolver what the current effective state is given the entries already emitted
    ///   for this group. If that state differs from what native Umbraco would produce at that node
    ///   (<c>Allow</c> iff the verb is in the granular set, otherwise <c>Deny</c>), a single row is
    ///   emitted to force the correction — otherwise nothing is written.</item>
    /// </list>
    /// <para>
    /// This produces the minimal, resolver-equivalent row set — including correct behaviour for
    /// nested granular permissions where an inner node re-enables a verb that an outer node stripped.
    /// </para>
    /// </remarks>
    private List<AdvancedPermissionEntity> ComputeGroupEntries(IUserGroup group)
    {
        var defaults = group.Permissions.ToHashSet(StringComparer.Ordinal);

        // The in-memory view of entries the resolver will see for this group.
        // Grows as we emit, so descendants observe the effects of their ancestors' entries.
        var entriesForResolver = new List<AdvancedPermissionEntry>(defaults.Count);

        // The parallel list of entities to persist.
        var persistEntities = new List<AdvancedPermissionEntity>(defaults.Count);

        // Virtual-root Allow entries for each verb in the group's native defaults.
        foreach (var verb in defaults)
        {
            var entry = NewEntry(
                AdvancedPermissionsConstants.VirtualRootNodeKey,
                group.Alias,
                verb,
                PermissionState.Allow);
            entriesForResolver.Add(entry);
            persistEntities.Add(ToEntity(entry));
        }

        // Group native granular permissions by node.
        var granularByNode = group.GranularPermissions
            .OfType<DocumentGranularPermission>()
            .GroupBy(p => p.Key)
            .ToDictionary(
                g => g.Key,
                g => g.Select(p => p.Permission).ToHashSet(StringComparer.Ordinal));

        if (granularByNode.Count == 0)
        {
            return persistEntities;
        }

        // Resolve the root-to-node path for every granular node, skipping orphans.
        var plannedNodes = new List<(Guid NodeKey, IReadOnlyList<Guid> PathFromRoot, HashSet<string> GranularVerbs)>(granularByNode.Count);
        foreach (var (nodeKey, granularVerbs) in granularByNode)
        {
            if (!navigationQueryService.TryGetAncestorsOrSelfKeys(nodeKey, out IEnumerable<Guid> ancestorsOrSelf))
            {
                logger.LogWarning(
                    "Advanced Permissions: Skipping granular permission for group '{Alias}' at node {NodeKey} — node no longer exists in the content tree.",
                    group.Alias,
                    nodeKey);
                continue;
            }

            // TryGetAncestorsOrSelfKeys returns [self, parent, …, root]; PathFromRoot needs the reverse.
            var pathFromRoot = ancestorsOrSelf.Reverse().ToArray();
            plannedNodes.Add((nodeKey, pathFromRoot, granularVerbs));
        }

        // Process ancestors first so descendants see the ancestor's entries when the resolver runs.
        plannedNodes.Sort((a, b) => a.PathFromRoot.Count.CompareTo(b.PathFromRoot.Count));

        var singleRoleAliases = new[] { group.Alias };

        foreach (var (nodeKey, pathFromRoot, granularVerbs) in plannedNodes)
        {
            // For every verb in either the defaults or the granular set, ask the resolver
            // what it currently returns at this node and emit a correction only where needed.
            foreach (var verb in defaults.Concat(granularVerbs).Distinct(StringComparer.Ordinal))
            {
                var context = new PermissionResolutionContext(
                    TargetNodeKey: nodeKey,
                    PathFromRoot: pathFromRoot,
                    RoleAliases: singleRoleAliases,
                    StoredEntries: entriesForResolver);

                var current = permissionResolver.Resolve(context, verb);
                var desiredAllow = granularVerbs.Contains(verb);

                if (current.IsAllowed == desiredAllow)
                {
                    continue;
                }

                var entry = NewEntry(
                    nodeKey,
                    group.Alias,
                    verb,
                    desiredAllow ? PermissionState.Allow : PermissionState.Deny);
                entriesForResolver.Add(entry);
                persistEntities.Add(ToEntity(entry));
            }
        }

        return persistEntities;
    }

    private static AdvancedPermissionEntry NewEntry(Guid nodeKey, string roleAlias, string verb, PermissionState state) =>
        new(
            Id: Guid.NewGuid(),
            NodeKey: nodeKey,
            RoleAlias: roleAlias,
            Verb: verb,
            State: state,
            Scope: PermissionScope.ThisNodeAndDescendants);

    private static AdvancedPermissionEntity ToEntity(AdvancedPermissionEntry entry) =>
        new()
        {
            Id = entry.Id,
            NodeKey = entry.NodeKey,
            RoleAlias = entry.RoleAlias,
            Verb = entry.Verb,
            State = entry.State,
            Scope = entry.Scope,
        };
}
