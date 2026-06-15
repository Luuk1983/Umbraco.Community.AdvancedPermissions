using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Runtime;
using Umbraco.Cms.Core.Sync;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Data.Context;

namespace Umbraco.Community.AdvancedPermissions.Notifications;

/// <summary>
/// On startup, removes stored node-level permission entries whose verb is not one this package manages
/// (<see cref="AdvancedPermissionsConstants.AllVerbs"/>).
/// </summary>
/// <remarks>
/// The store should only ever contain the document node verbs this package owns. A verb outside that set is
/// stale, unresolvable data — it can never be displayed or evaluated, and the editor re-sending it makes the
/// save endpoint reject the whole save. Such rows can leak when Umbraco adds new default permission verbs that
/// an earlier package version copied verbatim (e.g. Umbraco 18's <c>Umb.Document.PropertyValue.*</c> and
/// <c>Umb.Element.*</c>). This handler heals those installs automatically. It is idempotent and cheap, so it
/// runs on every boot, guarded like the data import so only the writable main instance acts.
/// </remarks>
/// <param name="dbContextFactory">Factory for creating short-lived database contexts.</param>
/// <param name="mainDom">Guard ensuring only the main domain instance writes.</param>
/// <param name="serverRoleAccessor">Guard ensuring subscriber instances stay read-only.</param>
/// <param name="logger">Logger for cleanup status messages.</param>
public sealed class UnrecognizedVerbCleanup(
    IDbContextFactory<AdvancedPermissionsDbContext> dbContextFactory,
    IMainDom mainDom,
    IServerRoleAccessor serverRoleAccessor,
    ILogger<UnrecognizedVerbCleanup> logger)
    : INotificationAsyncHandler<UmbracoApplicationStartingNotification>
{
    /// <inheritdoc />
    public async Task HandleAsync(
        UmbracoApplicationStartingNotification notification,
        CancellationToken cancellationToken)
    {
        // Only run at full runtime — not during install or upgrade.
        if (notification.RuntimeLevel != global::Umbraco.Cms.Core.RuntimeLevel.Run)
        {
            return;
        }

        // Only the MainDom instance should write data (prevents duplicate work in multi-instance setups).
        if (!mainDom.IsMainDom)
        {
            return;
        }

        // Subscriber instances are read-only in load-balanced setups.
        if (serverRoleAccessor.CurrentServerRole == ServerRole.Subscriber)
        {
            return;
        }

        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            // string[] translates to a reliable SQL IN/NOT IN for every provider.
            var managedVerbs = AdvancedPermissionsConstants.AllVerbs.ToArray();

            var stale = await db.Permissions
                .Where(p => !managedVerbs.Contains(p.Verb))
                .ToListAsync(cancellationToken);

            if (stale.Count == 0)
            {
                return;
            }

            db.Permissions.RemoveRange(stale);
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Advanced Permissions: Removed {Count} stored permission entr{Plural} with verbs the package no longer manages.",
                stale.Count,
                stale.Count == 1 ? "y" : "ies");
        }
        catch (Exception ex)
        {
            // A failed cleanup must never prevent Umbraco from starting.
            logger.LogWarning(
                ex,
                "Advanced Permissions: Failed to clean up unrecognized permission verbs. Entries with verbs "
                + "outside the managed set may remain and could block saving until removed.");
        }
    }
}
