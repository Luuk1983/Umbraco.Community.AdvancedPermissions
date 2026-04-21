using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Umbraco.Community.AdvancedPermissions.Data.Context;

namespace Umbraco.Community.AdvancedPermissions.Data.Migrations;

/// <summary>
/// Design-time factory for SQL Server migrations.
/// Used by the EF Core CLI tooling (<c>dotnet ef migrations add</c>) to create SQL Server-specific migrations.
/// </summary>
/// <example>
/// Run from the <c>src/Umbraco.Community.AdvancedPermissions.Data</c> directory:
/// <code>
/// dotnet ef migrations add InitialCreate --context AdvancedPermissionsDbContextSqlServer --output-dir Migrations/SqlServer
/// </code>
/// </example>
public sealed class AdvancedPermissionsDbContextSqlServerFactory : IDesignTimeDbContextFactory<AdvancedPermissionsDbContextSqlServer>
{
    /// <summary>
    /// Creates a new SQL Server DbContext instance for design-time tooling.
    /// </summary>
    /// <param name="args">Command-line arguments (not used).</param>
    /// <returns>A configured SQL Server DbContext instance.</returns>
    public AdvancedPermissionsDbContextSqlServer CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AdvancedPermissionsDbContextSqlServer>();
        optionsBuilder.UseSqlServer(
            "Server=(localdb)\\mssqllocaldb;Database=TempAdvancedPermissionsMigrations;Trusted_Connection=True;");
        return new AdvancedPermissionsDbContextSqlServer(optionsBuilder.Options);
    }
}

/// <summary>
/// Design-time factory for SQLite migrations.
/// Used by the EF Core CLI tooling (<c>dotnet ef migrations add</c>) to create SQLite-specific migrations.
/// </summary>
/// <example>
/// Run from the <c>src/Umbraco.Community.AdvancedPermissions.Data</c> directory:
/// <code>
/// dotnet ef migrations add InitialCreate --context AdvancedPermissionsDbContextSqlite --output-dir Migrations/Sqlite
/// </code>
/// </example>
public sealed class AdvancedPermissionsDbContextSqliteFactory : IDesignTimeDbContextFactory<AdvancedPermissionsDbContextSqlite>
{
    /// <summary>
    /// Creates a new SQLite DbContext instance for design-time tooling.
    /// </summary>
    /// <param name="args">Command-line arguments (not used).</param>
    /// <returns>A configured SQLite DbContext instance.</returns>
    public AdvancedPermissionsDbContextSqlite CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AdvancedPermissionsDbContextSqlite>();
        optionsBuilder.UseSqlite("Data Source=temp_migrations.db");
        return new AdvancedPermissionsDbContextSqlite(optionsBuilder.Options);
    }
}
