using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;
using UmbracoAdvancedSecurity.Caching;
using UmbracoAdvancedSecurity.Core.Interfaces;
using UmbracoAdvancedSecurity.Core.Services;
using UmbracoAdvancedSecurity.Data.Context;
using UmbracoAdvancedSecurity.Data.Repositories;
using UmbracoAdvancedSecurity.Data.Migrations;
using UmbracoAdvancedSecurity.Notifications;
using UmbracoAdvancedSecurity.Services;

namespace UmbracoAdvancedSecurity.Composing;

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
public sealed class AdvancedSecurityComposer : IComposer
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
    /// singleton services can use <c>IDbContextFactory&lt;AdvancedSecurityDbContext&gt;</c>.
    /// </remarks>
    /// <param name="builder">The Umbraco builder.</param>
    private static void RegisterDbContext(IUmbracoBuilder builder)
    {
        var providerName = builder.Config["ConnectionStrings:umbracoDbDSN_ProviderName"]
            ?? "Microsoft.Data.Sqlite";

        if (providerName.Contains("SqlClient", StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.AddUmbracoDbContext<AdvancedSecurityDbContextSqlServer>(
                (sp, optionsBuilder, _, _) => optionsBuilder.UseUmbracoDatabaseProvider(sp));

            builder.Services.AddScoped<AdvancedSecurityDbContext>(sp =>
                sp.GetRequiredService<AdvancedSecurityDbContextSqlServer>());

            builder.Services.AddSingleton<IDbContextFactory<AdvancedSecurityDbContext>>(sp =>
                new DbContextFactoryAdapter<AdvancedSecurityDbContextSqlServer>(
                    sp.GetRequiredService<IDbContextFactory<AdvancedSecurityDbContextSqlServer>>()));
        }
        else
        {
            builder.Services.AddUmbracoDbContext<AdvancedSecurityDbContextSqlite>(
                (sp, optionsBuilder, _, _) => optionsBuilder.UseUmbracoDatabaseProvider(sp));

            builder.Services.AddScoped<AdvancedSecurityDbContext>(sp =>
                sp.GetRequiredService<AdvancedSecurityDbContextSqlite>());

            builder.Services.AddSingleton<IDbContextFactory<AdvancedSecurityDbContext>>(sp =>
                new DbContextFactoryAdapter<AdvancedSecurityDbContextSqlite>(
                    sp.GetRequiredService<IDbContextFactory<AdvancedSecurityDbContextSqlite>>()));
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
        // Apply EF Core migrations on application startup
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartingNotification, AdvancedSecurityDatabaseMigration>();

        // Invalidate caches when content structure or user/group membership changes
        builder.AddNotificationHandler<ContentMovedNotification, AdvancedPermissionCacheInvalidator>();
        builder.AddNotificationHandler<ContentMovedToRecycleBinNotification, AdvancedPermissionCacheInvalidator>();
        builder.AddNotificationHandler<UserGroupSavedNotification, AdvancedPermissionCacheInvalidator>();
        builder.AddNotificationHandler<UserSavedNotification, AdvancedPermissionCacheInvalidator>();
    }

    /// <summary>
    /// Adapts <see cref="IDbContextFactory{TDerived}"/> (registered by <c>AddUmbracoDbContext</c>) to
    /// <see cref="IDbContextFactory{AdvancedSecurityDbContext}"/> so that singleton services can create
    /// short-lived DbContext instances without depending on a specific provider.
    /// </summary>
    /// <typeparam name="TDerived">The provider-specific DbContext type (SQL Server or SQLite).</typeparam>
    private sealed class DbContextFactoryAdapter<TDerived>(
        IDbContextFactory<TDerived> innerFactory)
        : IDbContextFactory<AdvancedSecurityDbContext>
        where TDerived : AdvancedSecurityDbContext
    {
        /// <inheritdoc />
        public AdvancedSecurityDbContext CreateDbContext() => innerFactory.CreateDbContext();
    }
}
