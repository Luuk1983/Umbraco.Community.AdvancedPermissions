using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using UmbracoAdvancedSecurity.Data.Context;

namespace UmbracoAdvancedSecurity.Data.Migrations;

/// <summary>
/// Applies pending EF Core database migrations for the Advanced Security package when Umbraco starts.
/// </summary>
/// <remarks>
/// Only runs at <see cref="RuntimeLevel.Run"/> to avoid interfering with Umbraco's own install or upgrade process.
/// </remarks>
/// <param name="dbContext">The Advanced Security DbContext used to apply migrations.</param>
/// <param name="logger">Logger for migration status and errors.</param>
public sealed class AdvancedSecurityDatabaseMigration(
    AdvancedSecurityDbContext dbContext,
    ILogger<AdvancedSecurityDatabaseMigration> logger)
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
                "Advanced Security: Skipping database migrations because runtime level is {RuntimeLevel}",
                notification.RuntimeLevel);
            return;
        }

        logger.LogInformation("Advanced Security: Running database migrations");

        try
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
            logger.LogInformation("Advanced Security: Database migrations completed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Advanced Security: An error occurred while migrating the database");
            throw;
        }
    }
}
