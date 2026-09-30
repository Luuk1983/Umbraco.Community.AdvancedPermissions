using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;
using Umbraco.Community.AdvancedPermissions.Caching;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Services;
using Umbraco.Community.AdvancedPermissions.Data.Context;
using Umbraco.Community.AdvancedPermissions.Data.Repositories;
using Umbraco.Community.AdvancedPermissions.Data.Migrations;
using Umbraco.Community.AdvancedPermissions.Filters;
using Umbraco.Community.AdvancedPermissions.Migrations;
using Umbraco.Community.AdvancedPermissions.Notifications;
using Umbraco.Community.AdvancedPermissions.ServerEvents;
using Umbraco.Community.AdvancedPermissions.Services;

namespace Umbraco.Community.AdvancedPermissions.Composing;

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
                (sp, optionsBuilder, _, _) => optionsBuilder.UseUmbracoDatabaseProvider(sp),
                shareUmbracoConnection: true);

            builder.Services.AddScoped<AdvancedPermissionsDbContext>(sp =>
                sp.GetRequiredService<AdvancedPermissionsDbContextSqlServer>());

            builder.Services.AddSingleton<IDbContextFactory<AdvancedPermissionsDbContext>>(sp =>
                new DbContextFactoryAdapter<AdvancedPermissionsDbContextSqlServer>(
                    sp.GetRequiredService<IDbContextFactory<AdvancedPermissionsDbContextSqlServer>>()));
        }
        else
        {
            builder.Services.AddUmbracoDbContext<AdvancedPermissionsDbContextSqlite>(
                (sp, optionsBuilder, _, _) => optionsBuilder.UseUmbracoDatabaseProvider(sp),
                shareUmbracoConnection: true);

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

        // Doc-type permissions
        builder.Services.AddSingleton<IDocTypePermissionRepository, DocTypePermissionRepository>();
        builder.Services.AddSingleton<IDocTypePermissionResolver, DocTypePermissionResolver>();
        builder.Services.AddSingleton<DocTypePermissionCache>();
        builder.Services.AddSingleton<IDocTypePermissionService, DocTypePermissionService>();

        // Register the IContentTypeFilter that enforces doc-type create restrictions in
        // Umbraco's allowed-children / allowed-at-root pipelines.
        builder.ContentTypeFilters().Append<DocTypeCreateContentTypeFilter>();

        // Library element permissions — element items and folders share one node-permission stack
        // (the IPermissionResolver above is reused). Both Umbraco enforcement services are replaced so
        // Advanced Security governs element and element-container access decisions.
        builder.Services.AddSingleton<IElementPermissionRepository, ElementPermissionRepository>();
        builder.Services.AddSingleton<ElementPermissionCache>();
        builder.Services.AddSingleton<IElementNodePermissionService, ElementNodePermissionService>();
        builder.Services.AddUnique<IElementPermissionService, AdvancedElementPermissionService>();
        builder.Services.AddUnique<IElementContainerPermissionService, AdvancedElementContainerPermissionService>();

        // Server events: publish this package's changes on Umbraco's built-in hub. The authorizer
        // is what makes the sources reachable at all - core delivers no source that no authorizer
        // claims - so this line and AdvancedPermissionsEventAuthorizer are the whole access story.
        // One authorizer claims every source, so there is no second list to fall out of step.
        builder.EventSourceAuthorizers().Append<AdvancedPermissionsEventAuthorizer>();
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

        // 2b. Seed element defaults on first boot ($everyone read + group element/folder defaults) so the
        //     library is not locked down once element enforcement is active. Runs after schema migration.
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartingNotification, ElementPermissionsDataImport>();

        // 3. Heal installs that stored verbs the package no longer manages — e.g. after Umbraco added new
        //    default permission verbs (Umbraco 18's Umb.Document.PropertyValue.* / Umb.Element.*) that an
        //    earlier package version copied verbatim into our store. Idempotent; runs after schema migration.
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartingNotification, UnrecognizedVerbCleanup>();

        // Invalidate caches when content structure or user/group membership changes
        builder.AddNotificationHandler<ContentMovedNotification, AdvancedPermissionCacheInvalidator>();
        builder.AddNotificationHandler<ContentMovedToRecycleBinNotification, AdvancedPermissionCacheInvalidator>();
        builder.AddNotificationHandler<UserGroupSavedNotification, AdvancedPermissionCacheInvalidator>();
        builder.AddNotificationHandler<UserSavedNotification, AdvancedPermissionCacheInvalidator>();

        // Clean up orphaned permission entries when content is permanently deleted.
        // ContentDeletedNotification fires per-item even during "empty recycle bin", so a
        // separate ContentEmptiedRecycleBinNotification handler is not needed.
        builder.AddNotificationAsyncHandler<ContentDeletedNotification, AdvancedPermissionCleanup>();

        // Clean up orphaned permission entries when a user group is deleted.
        builder.AddNotificationAsyncHandler<UserGroupDeletedNotification, AdvancedPermissionCleanup>();

        // Seed root permission entries for newly created user groups
        builder.AddNotificationAsyncHandler<UserGroupSavedNotification, UserGroupPermissionSeeder>();

        // Doc-type permission cache invalidation
        builder.AddNotificationHandler<ContentMovedNotification, DocTypePermissionCacheInvalidator>();
        builder.AddNotificationHandler<ContentMovedToRecycleBinNotification, DocTypePermissionCacheInvalidator>();
        builder.AddNotificationHandler<UserGroupSavedNotification, DocTypePermissionCacheInvalidator>();
        builder.AddNotificationHandler<UserSavedNotification, DocTypePermissionCacheInvalidator>();
        builder.AddNotificationHandler<ContentTypeSavedNotification, DocTypePermissionCacheInvalidator>();

        // Doc-type permission cleanup on permanent deletion
        builder.AddNotificationAsyncHandler<ContentDeletedNotification, DocTypePermissionCleanup>();
        builder.AddNotificationAsyncHandler<ContentTypeDeletedNotification, DocTypePermissionCleanup>();
        builder.AddNotificationAsyncHandler<UserGroupDeletedNotification, DocTypePermissionCleanup>();

        // Element (Library) permission cache invalidation
        builder.AddNotificationHandler<ElementMovedNotification, ElementPermissionCacheInvalidator>();
        builder.AddNotificationHandler<ElementMovedToRecycleBinNotification, ElementPermissionCacheInvalidator>();
        builder.AddNotificationHandler<EntityContainerMovedNotification, ElementPermissionCacheInvalidator>();
        builder.AddNotificationHandler<EntityContainerMovedToRecycleBinNotification, ElementPermissionCacheInvalidator>();
        builder.AddNotificationHandler<UserGroupSavedNotification, ElementPermissionCacheInvalidator>();
        builder.AddNotificationHandler<UserSavedNotification, ElementPermissionCacheInvalidator>();

        // Element (Library) permission cleanup on permanent deletion
        builder.AddNotificationAsyncHandler<ElementDeletedNotification, ElementPermissionCleanup>();
        builder.AddNotificationAsyncHandler<EntityContainerDeletedNotification, ElementPermissionCleanup>();
        builder.AddNotificationAsyncHandler<UserGroupDeletedNotification, ElementPermissionCleanup>();

        // Server-event handlers. Registered LAST, and that position is load-bearing: Umbraco runs
        // the handlers for one notification in registration order, and every notification below that
        // Umbraco raises is also handled by a cache invalidator above. A client that refetched on an
        // event which overtook its invalidation would read the snapshot the change replaced - and,
        // having consumed its one notification, would never ask again. Do not move these up, and add
        // any new handler above this block, not below it. ServerEventRegistrationTests fails if either
        // handler is registered before any other handler of the same notification.
        builder.AddNotificationAsyncHandler<AdvancedPermissionsChangedNotification, AdvancedPermissionsServerEventHandler>();
        builder.AddNotificationAsyncHandler<ElementPermissionsChangedNotification, AdvancedPermissionsServerEventHandler>();
        builder.AddNotificationAsyncHandler<DocTypePermissionsChangedNotification, AdvancedPermissionsServerEventHandler>();
        builder.AddNotificationAsyncHandler<UserGroupSavedNotification, AccessServerEventHandler>();
        builder.AddNotificationAsyncHandler<UserGroupDeletedNotification, AccessServerEventHandler>();
        builder.AddNotificationAsyncHandler<UserSavedNotification, AccessServerEventHandler>();
        builder.AddNotificationAsyncHandler<ContentMovedNotification, AccessServerEventHandler>();
        builder.AddNotificationAsyncHandler<ContentMovedToRecycleBinNotification, AccessServerEventHandler>();
        builder.AddNotificationAsyncHandler<ContentDeletedNotification, AccessServerEventHandler>();
        builder.AddNotificationAsyncHandler<ElementMovedNotification, AccessServerEventHandler>();
        builder.AddNotificationAsyncHandler<ElementMovedToRecycleBinNotification, AccessServerEventHandler>();
        builder.AddNotificationAsyncHandler<ElementDeletedNotification, AccessServerEventHandler>();
        builder.AddNotificationAsyncHandler<EntityContainerMovedNotification, AccessServerEventHandler>();
        builder.AddNotificationAsyncHandler<EntityContainerMovedToRecycleBinNotification, AccessServerEventHandler>();
        builder.AddNotificationAsyncHandler<EntityContainerDeletedNotification, AccessServerEventHandler>();
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
