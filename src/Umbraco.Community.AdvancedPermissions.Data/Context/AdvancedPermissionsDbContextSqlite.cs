using Microsoft.EntityFrameworkCore;

namespace Umbraco.Community.AdvancedPermissions.Data.Context;

/// <summary>
/// SQLite-specific DbContext for the Advanced Security package.
/// Used for generating SQLite migrations and at runtime when SQLite is the configured provider.
/// </summary>
/// <remarks>
/// At runtime, the connection string is provided by Umbraco via <c>UseUmbracoDatabaseProvider</c>
/// and overrides the <see cref="OnConfiguring"/> default.
/// The default connection string is only used by the EF Core design-time tools for migration generation.
/// </remarks>
/// <example>
/// To generate a new SQLite migration from the Data project directory:
/// <code>
/// dotnet ef migrations add &lt;MigrationName&gt; --context AdvancedPermissionsDbContextSqlite --output-dir Migrations/Sqlite
/// </code>
/// </example>
public sealed class AdvancedPermissionsDbContextSqlite : AdvancedPermissionsDbContext
{
    /// <summary>
    /// Initializes a new instance with SQLite-specific options.
    /// </summary>
    /// <param name="options">The SQLite-configured DbContext options.</param>
    public AdvancedPermissionsDbContextSqlite(DbContextOptions<AdvancedPermissionsDbContextSqlite> options)
        : base(options)
    {
    }

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Only configure if not already configured (for design-time migration generation only).
        // At runtime, Umbraco's UseUmbracoDatabaseProvider configures the connection.
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite("Data Source=temp_migrations.db");
        }
    }
}
