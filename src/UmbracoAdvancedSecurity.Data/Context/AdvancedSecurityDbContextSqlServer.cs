using Microsoft.EntityFrameworkCore;

namespace UmbracoAdvancedSecurity.Data.Context;

/// <summary>
/// SQL Server-specific DbContext for the Advanced Security package.
/// Used for generating SQL Server migrations and at runtime when SQL Server is the configured provider.
/// </summary>
/// <remarks>
/// At runtime, the connection string is provided by Umbraco via <c>UseUmbracoDatabaseProvider</c>
/// and overrides the <see cref="OnConfiguring"/> default.
/// The default connection string is only used by the EF Core design-time tools for migration generation.
/// </remarks>
/// <example>
/// To generate a new SQL Server migration from the Data project directory:
/// <code>
/// dotnet ef migrations add &lt;MigrationName&gt; --context AdvancedSecurityDbContextSqlServer --output-dir Migrations/SqlServer
/// </code>
/// </example>
public sealed class AdvancedSecurityDbContextSqlServer : AdvancedSecurityDbContext
{
    /// <summary>
    /// Initializes a new instance with SQL Server-specific options.
    /// </summary>
    /// <param name="options">The SQL Server-configured DbContext options.</param>
    public AdvancedSecurityDbContextSqlServer(DbContextOptions<AdvancedSecurityDbContextSqlServer> options)
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
            optionsBuilder.UseSqlServer(
                "Server=(localdb)\\mssqllocaldb;Database=TempAdvancedSecurityMigrations;Trusted_Connection=True;");
        }
    }
}
