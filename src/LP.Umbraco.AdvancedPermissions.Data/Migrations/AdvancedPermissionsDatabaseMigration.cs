using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using LP.Umbraco.AdvancedPermissions.Data.Context;

namespace LP.Umbraco.AdvancedPermissions.Data.Migrations;

/// <summary>
/// Applies pending EF Core database migrations for the Advanced Security package when Umbraco starts.
/// </summary>
/// <remarks>
/// Only runs at <see cref="RuntimeLevel.Run"/> to avoid interfering with Umbraco's own install or upgrade process.
/// </remarks>
/// <param name="dbContext">The Advanced Security DbContext used to apply migrations.</param>
/// <param name="logger">Logger for migration status and errors.</param>
public sealed class AdvancedPermissionsDatabaseMigration(
    AdvancedPermissionsDbContext dbContext,
    ILogger<AdvancedPermissionsDatabaseMigration> logger)
    : INotificationAsyncHandler<UmbracoApplicationStartingNotification>
{
    /// <inheritdoc />
    public async Task HandleAsync(
        UmbracoApplicationStartingNotification notification,
        CancellationToken cancellationToken)
    {
        if (notification.RuntimeLevel != RuntimeLevel.Run)
        {
            logger.LogInformation(
                "Advanced Permissions: Skipping database migrations because runtime level is {RuntimeLevel}",
                notification.RuntimeLevel);
            return;
        }

        logger.LogInformation("Advanced Permissions: Running database migrations");

        try
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
            logger.LogInformation("Advanced Permissions: Database migrations completed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Advanced Permissions: An error occurred while migrating the database");
            throw;
        }
    }
}
