using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using UmbracoAdvancedSecurity.Data.Context;

namespace UmbracoAdvancedSecurity.Data.Migrations;

/// <summary>
/// Design-time factory for SQL Server migrations.
/// Used by the EF Core CLI tooling (<c>dotnet ef migrations add</c>) to create SQL Server-specific migrations.
/// </summary>
/// <example>
/// Run from the <c>src/UmbracoAdvancedSecurity.Data</c> directory:
/// <code>
/// dotnet ef migrations add InitialCreate --context AdvancedSecurityDbContextSqlServer --output-dir Migrations/SqlServer
/// </code>
/// </example>
public sealed class AdvancedSecurityDbContextSqlServerFactory : IDesignTimeDbContextFactory<AdvancedSecurityDbContextSqlServer>
{
    /// <summary>
    /// Creates a new SQL Server DbContext instance for design-time tooling.
    /// </summary>
    /// <param name="args">Command-line arguments (not used).</param>
    /// <returns>A configured SQL Server DbContext instance.</returns>
    public AdvancedSecurityDbContextSqlServer CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AdvancedSecurityDbContextSqlServer>();
        optionsBuilder.UseSqlServer(
            "Server=(localdb)\\mssqllocaldb;Database=TempAdvancedSecurityMigrations;Trusted_Connection=True;");
        return new AdvancedSecurityDbContextSqlServer(optionsBuilder.Options);
    }
}

/// <summary>
/// Design-time factory for SQLite migrations.
/// Used by the EF Core CLI tooling (<c>dotnet ef migrations add</c>) to create SQLite-specific migrations.
/// </summary>
/// <example>
/// Run from the <c>src/UmbracoAdvancedSecurity.Data</c> directory:
/// <code>
/// dotnet ef migrations add InitialCreate --context AdvancedSecurityDbContextSqlite --output-dir Migrations/Sqlite
/// </code>
/// </example>
public sealed class AdvancedSecurityDbContextSqliteFactory : IDesignTimeDbContextFactory<AdvancedSecurityDbContextSqlite>
{
    /// <summary>
    /// Creates a new SQLite DbContext instance for design-time tooling.
    /// </summary>
    /// <param name="args">Command-line arguments (not used).</param>
    /// <returns>A configured SQLite DbContext instance.</returns>
    public AdvancedSecurityDbContextSqlite CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AdvancedSecurityDbContextSqlite>();
        optionsBuilder.UseSqlite("Data Source=temp_migrations.db");
        return new AdvancedSecurityDbContextSqlite(optionsBuilder.Options);
    }
}
