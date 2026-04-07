using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using LP.Umbraco.AdvancedPermissions.Core.Constants;
using LP.Umbraco.AdvancedPermissions.Core.Models;
using LP.Umbraco.AdvancedPermissions.Data.Context;
using LP.Umbraco.AdvancedPermissions.Data.Entities;

namespace LP.Umbraco.AdvancedPermissions.Notifications;

/// <summary>
/// When a user group is saved for the first time (no existing advanced security entries),
/// seeds root-level permission entries from the group's native Umbraco permissions.
/// </summary>
/// <remarks>
/// This ensures that user groups created after the initial data import also receive a
/// sensible starting set of permissions, consistent with what the admin set in the native UI.
/// If the admin creates a group without any native permissions, no entries are seeded and
/// the group defaults to Deny all — permissions must be configured via the Security Editor.
/// </remarks>
/// <param name="dbContextFactory">Factory for creating short-lived database contexts.</param>
/// <param name="logger">Logger for seeding status messages.</param>
public sealed class UserGroupPermissionSeeder(
    IDbContextFactory<AdvancedPermissionsDbContext> dbContextFactory,
    ILogger<UserGroupPermissionSeeder> logger)
    : INotificationAsyncHandler<UserGroupSavedNotification>
{
    /// <inheritdoc />
    public async Task HandleAsync(
        UserGroupSavedNotification notification,
        CancellationToken cancellationToken)
    {
        foreach (var group in notification.SavedEntities)
        {
            try
            {
                await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

                // If ANY entries already exist for this role, it has been configured — don't overwrite
                var hasEntries = await db.Permissions
                    .AnyAsync(p => p.RoleAlias == group.Alias, cancellationToken);

                if (hasEntries)
                {
                    continue;
                }

                // New group — seed virtual-root entries from native group permissions
                var entriesToAdd = group.Permissions
                    .Select(verb => new AdvancedPermissionEntity
                    {
                        Id = Guid.NewGuid(),
                        NodeKey = AdvancedPermissionsConstants.VirtualRootNodeKey,
                        RoleAlias = group.Alias,
                        Verb = verb,
                        State = PermissionState.Allow,
                        Scope = PermissionScope.ThisNodeAndDescendants,
                    })
                    .ToList();

                if (entriesToAdd.Count == 0)
                {
                    logger.LogDebug(
                        "Advanced Permissions: New group '{Group}' has no native permissions — starting with Deny all",
                        group.Alias);
                    continue;
                }

                await db.Permissions.AddRangeAsync(entriesToAdd, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);

                logger.LogInformation(
                    "Advanced Permissions: Seeded {Count} root permission entries for new group '{Group}'",
                    entriesToAdd.Count,
                    group.Alias);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Advanced Permissions: Failed to seed permissions for group '{Group}'. " +
                    "Configure permissions manually via the Security Editor.",
                    group.Alias);
            }
        }
    }
}
