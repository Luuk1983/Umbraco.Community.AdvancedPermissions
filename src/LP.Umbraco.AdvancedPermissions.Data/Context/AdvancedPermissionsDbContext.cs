using Microsoft.EntityFrameworkCore;
using LP.Umbraco.AdvancedPermissions.Data.Entities;

namespace LP.Umbraco.AdvancedPermissions.Data.Context;

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

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigurePermissions(modelBuilder);
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
}
