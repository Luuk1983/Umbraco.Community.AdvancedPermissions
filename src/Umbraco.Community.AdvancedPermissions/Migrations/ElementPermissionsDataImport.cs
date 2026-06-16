using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models.Membership;
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
/// On first boot (when the <c>ElementPermission</c> table is empty), seeds the <c>$everyone</c> element
/// read default and imports each user group's native element and element-folder default permissions, so
/// the library remains accessible exactly as it was before the package took over element enforcement.
/// </summary>
/// <remarks>
/// <para>
/// The element analogue of <see cref="AdvancedPermissionsDataImport"/>. It imports only the group
/// <em>default</em> permissions (the native "Element permissions" / "Element Folder permissions"
/// toggles), mapping native folder verbs (<c>Umb.ElementContainer.*</c>) onto the canonical element
/// verbs. Per-element granular permissions are not imported: the library is new in Umbraco 18 and the
/// package replaces the native per-element granular UI, so there is no established granular data to
/// preserve. Without this seed the freshly-registered element enforcement would default to Deny and
/// lock the library down.
/// </para>
/// <para>
/// Runs on <see cref="UmbracoApplicationStartingNotification"/> after the schema migration, guarded by
/// <see cref="IMainDom"/> and <see cref="IServerRoleAccessor"/> so only one instance imports in
/// load-balanced deployments. Any failure is logged and never prevents Umbraco from starting.
/// </para>
/// </remarks>
/// <param name="dbContext">The Advanced Security DbContext.</param>
/// <param name="userGroupService">The Umbraco user group service used to enumerate groups.</param>
/// <param name="mainDom">Guards against duplicate imports in multi-instance setups.</param>
/// <param name="serverRoleAccessor">Identifies subscriber instances, which must not write.</param>
/// <param name="logger">Logger for import status messages.</param>
public sealed class ElementPermissionsDataImport(
    AdvancedPermissionsDbContext dbContext,
    IUserGroupService userGroupService,
    IMainDom mainDom,
    IServerRoleAccessor serverRoleAccessor,
    ILogger<ElementPermissionsDataImport> logger)
    : INotificationAsyncHandler<UmbracoApplicationStartingNotification>
{
    /// <inheritdoc />
    public async Task HandleAsync(
        UmbracoApplicationStartingNotification notification,
        CancellationToken cancellationToken)
    {
        if (notification.RuntimeLevel != global::Umbraco.Cms.Core.RuntimeLevel.Run)
        {
            return;
        }

        if (!mainDom.IsMainDom)
        {
            logger.LogDebug("Advanced Permissions: Skipping element data import — not the main domain instance");
            return;
        }

        if (serverRoleAccessor.CurrentServerRole == ServerRole.Subscriber)
        {
            logger.LogDebug("Advanced Permissions: Skipping element data import — subscriber instance");
            return;
        }

        var hasAnyEntries = await dbContext.ElementPermissions.AnyAsync(cancellationToken);
        if (hasAnyEntries)
        {
            return;
        }

        logger.LogInformation("Advanced Permissions: Element permission table is empty — seeding element defaults");

        try
        {
            await ImportAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Do NOT rethrow — a failed import must never prevent Umbraco from starting.
            logger.LogWarning(
                ex,
                "Advanced Permissions: Failed to seed element permissions. The library may default to Deny " +
                "until permissions are configured via the Library permissions editor.");
        }
    }

    /// <summary>
    /// Seeds the <c>$everyone</c> element read default, then imports each group's native element and
    /// element-folder default permissions as virtual-root Allow entries.
    /// </summary>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    private async Task ImportAsync(CancellationToken cancellationToken)
    {
        var totalInserted = 0;

        // $everyone gets the element read default at the virtual root so the library is browsable.
        foreach (var verb in AdvancedPermissionsConstants.EveryoneDefaultElementVerbs)
        {
            await dbContext.ElementPermissions.AddAsync(
                NewEntity(AdvancedPermissionsConstants.EveryoneRoleAlias, verb),
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        totalInserted += AdvancedPermissionsConstants.EveryoneDefaultElementVerbs.Count;

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

                await dbContext.ElementPermissions.AddRangeAsync(rows, cancellationToken);
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
            "Advanced Permissions: Seeded {Count} element permission entries from native Umbraco settings",
            totalInserted);
    }

    /// <summary>
    /// Computes the virtual-root element default entries for a group from its native element and
    /// element-folder default permissions, mapping folder verbs onto the canonical element verbs and
    /// de-duplicating the result.
    /// </summary>
    /// <param name="group">The user group whose native defaults to translate.</param>
    /// <returns>The element permission rows to persist for the group.</returns>
    private static List<ElementPermissionEntity> ComputeGroupEntries(IUserGroup group)
    {
        var canonicalVerbs = new HashSet<string>(StringComparer.Ordinal);

        foreach (var verb in group.Permissions)
        {
            if (AdvancedPermissionsConstants.ElementVerbs.Contains(verb, StringComparer.Ordinal))
            {
                canonicalVerbs.Add(verb);
            }
            else if (AdvancedPermissionsConstants.ElementContainerVerbToCanonical.TryGetValue(verb, out var canonical))
            {
                canonicalVerbs.Add(canonical);
            }
        }

        return canonicalVerbs
            .Select(verb => NewEntity(group.Alias, verb))
            .ToList();
    }

    /// <summary>
    /// Creates a virtual-root Allow element permission entity for a role and verb.
    /// </summary>
    /// <param name="roleAlias">The role alias.</param>
    /// <param name="verb">The canonical element verb.</param>
    /// <returns>The new entity.</returns>
    private static ElementPermissionEntity NewEntity(string roleAlias, string verb) =>
        new()
        {
            Id = Guid.NewGuid(),
            NodeKey = AdvancedPermissionsConstants.VirtualRootNodeKey,
            RoleAlias = roleAlias,
            Verb = verb,
            State = PermissionState.Allow,
            Scope = PermissionScope.ThisNodeAndDescendants,
            IsPriorityOverride = false,
        };
}
