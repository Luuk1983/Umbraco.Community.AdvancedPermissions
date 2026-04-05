using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;
using LP.Umbraco.AdvancedPermissions.Caching;
using LP.Umbraco.AdvancedPermissions.Core.Interfaces;
using LP.Umbraco.AdvancedPermissions.Core.Services;
using LP.Umbraco.AdvancedPermissions.Data.Context;
using LP.Umbraco.AdvancedPermissions.Data.Repositories;
using LP.Umbraco.AdvancedPermissions.Data.Migrations;
using LP.Umbraco.AdvancedPermissions.Migrations;
using LP.Umbraco.AdvancedPermissions.Notifications;
using LP.Umbraco.AdvancedPermissions.Services;

namespace LP.Umbraco.AdvancedPermissions.Composing;

/// <summary>
/// Registers all services, repositories, and notification handlers for the Advanced Security package.
/// </summary>
/// <remarks>
/// <para>
/// The EF Core DbContext is registered using <c>AddUmbracoDbContext</c> for the appropriate
/// database provider (SQL Server or SQLite), determined by the
/// <c>ConnectionStrings:umbracoDbDSN_ProviderName</c> configuration value.
/// </para>
/// <para>
/// The native <see cref="IContentPermissionService"/> is replaced by
/// <see cref="AdvancedContentPermissionService"/> using <c>AddUnique</c>, making the
/// Advanced Security system the sole authority for all content permission decisions.
/// </para>
/// </remarks>
public sealed class AdvancedPermissionsComposer : IComposer
{
    /// <inheritdoc />
    public void Compose(IUmbracoBuilder builder)
    {
        RegisterDbContext(builder);
        RegisterServices(builder);
        RegisterNotificationHandlers(builder);
    }

    /// <summary>
    /// Registers the EF Core DbContext for the database provider configured in appsettings.
    /// </summary>
    /// <remarks>
    /// <c>AddUmbracoDbContext</c> registers <c>IDbContextFactory&lt;TDerived&gt;</c> and a scoped
    /// <c>TDerived</c> instance. The adapter bridges the derived factory to the base type so that
    /// singleton services can use <c>IDbContextFactory&lt;AdvancedPermissionsDbContext&gt;</c>.
    /// </remarks>
    /// <param name="builder">The Umbraco builder.</param>
    private static void RegisterDbContext(IUmbracoBuilder builder)
    {
        var providerName = builder.Config["ConnectionStrings:umbracoDbDSN_ProviderName"]
            ?? "Microsoft.Data.Sqlite";

        if (providerName.Contains("SqlClient", StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.AddUmbracoDbContext<AdvancedPermissionsDbContextSqlServer>(
                (sp, optionsBuilder, _, _) => optionsBuilder.UseUmbracoDatabaseProvider(sp));

            builder.Services.AddScoped<AdvancedPermissionsDbContext>(sp =>
                sp.GetRequiredService<AdvancedPermissionsDbContextSqlServer>());

            builder.Services.AddSingleton<IDbContextFactory<AdvancedPermissionsDbContext>>(sp =>
                new DbContextFactoryAdapter<AdvancedPermissionsDbContextSqlServer>(
                    sp.GetRequiredService<IDbContextFactory<AdvancedPermissionsDbContextSqlServer>>()));
        }
        else
        {
            builder.Services.AddUmbracoDbContext<AdvancedPermissionsDbContextSqlite>(
                (sp, optionsBuilder, _, _) => optionsBuilder.UseUmbracoDatabaseProvider(sp));

            builder.Services.AddScoped<AdvancedPermissionsDbContext>(sp =>
                sp.GetRequiredService<AdvancedPermissionsDbContextSqlite>());

            builder.Services.AddSingleton<IDbContextFactory<AdvancedPermissionsDbContext>>(sp =>
                new DbContextFactoryAdapter<AdvancedPermissionsDbContextSqlite>(
                    sp.GetRequiredService<IDbContextFactory<AdvancedPermissionsDbContextSqlite>>()));
        }
    }

    /// <summary>
    /// Registers the core services, repositories, and cache.
    /// </summary>
    /// <param name="builder">The Umbraco builder.</param>
    private static void RegisterServices(IUmbracoBuilder builder)
    {
        // Repository — singleton because it uses IDbContextFactory for short-lived contexts
        builder.Services.AddSingleton<IAdvancedPermissionRepository, AdvancedPermissionRepository>();

        // Pure resolver — stateless, singleton
        builder.Services.AddSingleton<IPermissionResolver, PermissionResolver>();

        // Two-level cache — singleton, wraps Umbraco's RuntimeCache
        builder.Services.AddSingleton<AdvancedPermissionCache>();

        // Main permission service — singleton (no scoped state, uses async repo + cache)
        builder.Services.AddSingleton<IAdvancedPermissionService, AdvancedPermissionService>();

        // Replace Umbraco's built-in IContentPermissionService with our implementation
        builder.Services.AddUnique<IContentPermissionService, AdvancedContentPermissionService>();
    }

    /// <summary>
    /// Registers Umbraco notification handlers for migration and cache invalidation.
    /// </summary>
    /// <param name="builder">The Umbraco builder.</param>
    private static void RegisterNotificationHandlers(IUmbracoBuilder builder)
    {
        // 1. Apply EF Core schema migrations (schema must exist before data import)
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartingNotification, AdvancedPermissionsDatabaseMigration>();

        // 2. Import native Umbraco permissions on first boot (runs after schema migration)
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartingNotification, AdvancedPermissionsDataImport>();

        // Invalidate caches when content structure or user/group membership changes
        builder.AddNotificationHandler<ContentMovedNotification, AdvancedPermissionCacheInvalidator>();
        builder.AddNotificationHandler<ContentMovedToRecycleBinNotification, AdvancedPermissionCacheInvalidator>();
        builder.AddNotificationHandler<UserGroupSavedNotification, AdvancedPermissionCacheInvalidator>();
        builder.AddNotificationHandler<UserSavedNotification, AdvancedPermissionCacheInvalidator>();

        // Clean up orphaned permission entries when content is permanently deleted.
        // ContentDeletedNotification fires per-item even during "empty recycle bin", so a
        // separate ContentEmptiedRecycleBinNotification handler is not needed.
        builder.AddNotificationAsyncHandler<ContentDeletedNotification, AdvancedPermissionCleanup>();

        // Seed root permission entries for newly created user groups
        builder.AddNotificationAsyncHandler<UserGroupSavedNotification, UserGroupPermissionSeeder>();
    }

    /// <summary>
    /// Adapts <see cref="IDbContextFactory{TDerived}"/> (registered by <c>AddUmbracoDbContext</c>) to
    /// <see cref="IDbContextFactory{AdvancedPermissionsDbContext}"/> so that singleton services can create
    /// short-lived DbContext instances without depending on a specific provider.
    /// </summary>
    /// <typeparam name="TDerived">The provider-specific DbContext type (SQL Server or SQLite).</typeparam>
    private sealed class DbContextFactoryAdapter<TDerived>(
        IDbContextFactory<TDerived> innerFactory)
        : IDbContextFactory<AdvancedPermissionsDbContext>
        where TDerived : AdvancedPermissionsDbContext
    {
        /// <inheritdoc />
        public AdvancedPermissionsDbContext CreateDbContext() => innerFactory.CreateDbContext();
    }
}
