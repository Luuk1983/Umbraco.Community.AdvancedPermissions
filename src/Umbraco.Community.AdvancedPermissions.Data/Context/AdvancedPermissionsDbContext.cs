using Microsoft.EntityFrameworkCore;
using Umbraco.Community.AdvancedPermissions.Data.Entities;

namespace Umbraco.Community.AdvancedPermissions.Data.Context;

/// <summary>
/// Base Entity Framework Core DbContext for the Advanced Security package.
/// Manages the <c>AdvancedPermission</c> table that stores all explicit permission entries.
/// </summary>
/// <remarks>
/// Provider-specific derived contexts (<see cref="AdvancedPermissionsDbContextSqlite"/> and
/// <see cref="AdvancedPermissionsDbContextSqlServer"/>) handle database-provider configuration.
/// At runtime, Umbraco's connection string is provided via <c>UseUmbracoDatabaseProvider</c>.
/// </remarks>
public class AdvancedPermissionsDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance for direct DI resolution.
    /// </summary>
    /// <param name="options">The DbContext options configured by the DI container.</param>
    public AdvancedPermissionsDbContext(DbContextOptions<AdvancedPermissionsDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Initializes a new instance for use by provider-specific derived classes.
    /// Allows derived classes to pass their typed <c>DbContextOptions&lt;TDerived&gt;</c> to the base.
    /// </summary>
    /// <param name="options">The DbContext options from the derived class.</param>
    protected AdvancedPermissionsDbContext(DbContextOptions options)
        : base(options)
    {
    }

    /// <summary>
    /// Gets or sets the set of advanced security permission entries.
    /// </summary>
    public DbSet<AdvancedPermissionEntity> Permissions { get; set; } = null!;

    /// <summary>
    /// Gets or sets the set of document-type permission entries (keyed on node + content-type).
    /// </summary>
    public DbSet<DocTypePermissionEntity> DocTypePermissions { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigurePermissions(modelBuilder);
        ConfigureDocTypePermissions(modelBuilder);
    }

    /// <summary>
    /// Configures the <see cref="AdvancedPermissionEntity"/> mapping.
    /// Defines the table name, primary key, column types, and indexes.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    private static void ConfigurePermissions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AdvancedPermissionEntity>(entity =>
        {
            entity.ToTable("AdvancedPermission");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("Id")
                .ValueGeneratedNever()
                .IsRequired();

            entity.Property(e => e.NodeKey)
                .HasColumnName("NodeKey")
                .IsRequired();

            entity.Property(e => e.RoleAlias)
                .HasColumnName("RoleAlias")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.Verb)
                .HasColumnName("Verb")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.State)
                .HasColumnName("State")
                .IsRequired();

            entity.Property(e => e.Scope)
                .HasColumnName("Scope")
                .IsRequired();

            entity.Property(e => e.IsPriorityOverride)
                .HasColumnName("IsPriorityOverride")
                .HasDefaultValue(false)
                .IsRequired();

            // Index for the most common lookup: by node + role
            entity.HasIndex(e => new { e.NodeKey, e.RoleAlias })
                .HasDatabaseName("IX_AdvancedPermission_NodeKey_RoleAlias");

            // Index for cache-loading by role (single query per role)
            entity.HasIndex(e => e.RoleAlias)
                .HasDatabaseName("IX_AdvancedPermission_RoleAlias");

            // Index for node-level lookups (all roles for a node)
            entity.HasIndex(e => e.NodeKey)
                .HasDatabaseName("IX_AdvancedPermission_NodeKey");

            // Unique constraint to prevent duplicate entries for the same node+role+verb+scope
            entity.HasIndex(e => new { e.NodeKey, e.RoleAlias, e.Verb, e.Scope })
                .IsUnique()
                .HasDatabaseName("IX_AdvancedPermission_Unique");
        });
    }

    /// <summary>
    /// Configures the <see cref="DocTypePermissionEntity"/> mapping. Mirrors the
    /// <see cref="AdvancedPermissionEntity"/> shape with an additional <c>ContentTypeKey</c>
    /// column folded into the indexes and unique constraint.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    private static void ConfigureDocTypePermissions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DocTypePermissionEntity>(entity =>
        {
            entity.ToTable("DocTypePermission");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("Id")
                .ValueGeneratedNever()
                .IsRequired();

            entity.Property(e => e.NodeKey)
                .HasColumnName("NodeKey")
                .IsRequired();

            entity.Property(e => e.ContentTypeKey)
                .HasColumnName("ContentTypeKey")
                .IsRequired();

            entity.Property(e => e.RoleAlias)
                .HasColumnName("RoleAlias")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.Verb)
                .HasColumnName("Verb")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.State)
                .HasColumnName("State")
                .IsRequired();

            entity.Property(e => e.Scope)
                .HasColumnName("Scope")
                .IsRequired();

            entity.Property(e => e.IsPriorityOverride)
                .HasColumnName("IsPriorityOverride")
                .HasDefaultValue(false)
                .IsRequired();

            // Primary query path for the filter: role + content-type + verb
            entity.HasIndex(e => new { e.RoleAlias, e.ContentTypeKey, e.Verb })
                .HasDatabaseName("IX_DocTypePermission_RoleAlias_ContentTypeKey_Verb");

            // For loading all entries for a role into the L1 cache
            entity.HasIndex(e => e.RoleAlias)
                .HasDatabaseName("IX_DocTypePermission_RoleAlias");

            // For per-node and per-type cleanup queries
            entity.HasIndex(e => e.NodeKey)
                .HasDatabaseName("IX_DocTypePermission_NodeKey");

            entity.HasIndex(e => e.ContentTypeKey)
                .HasDatabaseName("IX_DocTypePermission_ContentTypeKey");

            // Unique constraint mirroring the node-level table, with ContentTypeKey added
            entity.HasIndex(e => new { e.NodeKey, e.ContentTypeKey, e.RoleAlias, e.Verb, e.Scope })
                .IsUnique()
                .HasDatabaseName("IX_DocTypePermission_Unique");
        });
    }
}
