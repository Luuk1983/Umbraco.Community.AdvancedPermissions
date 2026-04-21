using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Models.Membership.Permissions;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Runtime;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Sync;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
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

    private async Task ImportAsync(CancellationToken cancellationToken)
    {
        var entries = new List<AdvancedPermissionEntity>();

        // $everyone always gets Allow Read at the virtual root
        entries.Add(new AdvancedPermissionEntity
        {
            Id = Guid.NewGuid(),
            NodeKey = AdvancedPermissionsConstants.VirtualRootNodeKey,
            RoleAlias = AdvancedPermissionsConstants.EveryoneRoleAlias,
            Verb = AdvancedPermissionsConstants.VerbRead,
            State = PermissionState.Allow,
            Scope = PermissionScope.ThisNodeAndDescendants,
        });

        // Load all user groups and compute entries from their native permissions
        var skip = 0;
        const int take = 100;
        while (true)
        {
            var page = await userGroupService.GetAllAsync(skip, take);
            foreach (var group in page.Items)
            {
                AppendGroupEntries(entries, group);
            }

            skip += take;
            if (skip >= page.Total)
            {
                break;
            }
        }

        await dbContext.Permissions.AddRangeAsync(entries, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Advanced Permissions: Imported {Count} permission entries from native Umbraco settings",
            entries.Count);
    }

    /// <summary>
    /// Computes the <see cref="AdvancedPermissionEntity"/> entries to create for a single user group.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Group defaults become virtual-root (<c>NodeKey = VirtualRootNodeKey</c>) Allow entries with
    /// <see cref="PermissionScope.ThisNodeAndDescendants"/>, replicating the "fallback to defaults
    /// for any node with no explicit permissions" behaviour of the native system.
    /// </para>
    /// <para>
    /// Granular per-node permissions replace the defaults at their node and cascade to descendants.
    /// For each node with granular permissions:
    /// <list type="bullet">
    ///   <item>Verbs present in the granular set → Allow (ThisNodeAndDescendants)</item>
    ///   <item>Verbs present in group defaults but absent from the granular set → Deny (ThisNodeAndDescendants)</item>
    /// </list>
    /// This replicates the native "granular permissions override all defaults" behaviour.
    /// </para>
    /// </remarks>
    private static void AppendGroupEntries(List<AdvancedPermissionEntity> entries, IUserGroup group)
    {
        var defaultVerbs = group.Permissions.ToHashSet(StringComparer.Ordinal);

        // Virtual-root Allow entries for each verb in the group's native defaults
        foreach (var verb in defaultVerbs)
        {
            entries.Add(new AdvancedPermissionEntity
            {
                Id = Guid.NewGuid(),
                NodeKey = AdvancedPermissionsConstants.VirtualRootNodeKey,
                RoleAlias = group.Alias,
                Verb = verb,
                State = PermissionState.Allow,
                Scope = PermissionScope.ThisNodeAndDescendants,
            });
        }

        // Per-node overrides from native granular document permissions
        var nodeGroups = group.GranularPermissions
            .OfType<DocumentGranularPermission>()
            .GroupBy(p => p.Key);

        foreach (var nodeGroup in nodeGroups)
        {
            var nodeKey = nodeGroup.Key;
            var allowedVerbs = nodeGroup.Select(p => p.Permission)
                .ToHashSet(StringComparer.Ordinal);

            // Allow entries for verbs that the granular permission grants
            foreach (var verb in allowedVerbs)
            {
                entries.Add(new AdvancedPermissionEntity
                {
                    Id = Guid.NewGuid(),
                    NodeKey = nodeKey,
                    RoleAlias = group.Alias,
                    Verb = verb,
                    State = PermissionState.Allow,
                    Scope = PermissionScope.ThisNodeAndDescendants,
                });
            }

            // Deny entries for default verbs NOT in the granular set
            // Replicates the native "granular entry replaces all defaults" semantic
            foreach (var verb in defaultVerbs.Except(allowedVerbs, StringComparer.Ordinal))
            {
                entries.Add(new AdvancedPermissionEntity
                {
                    Id = Guid.NewGuid(),
                    NodeKey = nodeKey,
                    RoleAlias = group.Alias,
                    Verb = verb,
                    State = PermissionState.Deny,
                    Scope = PermissionScope.ThisNodeAndDescendants,
                });
            }
        }
    }
}
