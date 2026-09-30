using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Umbraco.Community.AdvancedPermissions.Data.Context;

namespace Umbraco.Community.AdvancedPermissions.Data.Tests;

/// <summary>
/// A SQLite in-memory database with the package's schema, plus a context factory over it — the
/// fixture the repository tests share so each does not repeat the connection and schema set-up.
/// </summary>
/// <remarks>
/// The schema is created through a context that has no interceptors attached, so what an
/// interceptor records is only what a test does afterwards, never the DDL that built the database.
/// </remarks>
/// <param name="connection">The open connection holding the in-memory database, kept alive until disposal.</param>
/// <param name="factory">The factory creating contexts over <paramref name="connection"/>.</param>
internal sealed class InMemoryPermissionDatabase(
    SqliteConnection connection,
    IDbContextFactory<AdvancedPermissionsDbContext> factory) : IAsyncDisposable
{
    /// <summary>
    /// Gets the factory that hands out short-lived contexts over the shared connection, with any
    /// interceptors supplied at creation attached.
    /// </summary>
    public IDbContextFactory<AdvancedPermissionsDbContext> Factory { get; } = factory;

    /// <summary>
    /// Creates the database, builds the schema, and returns a fixture whose factory has the given
    /// interceptors attached.
    /// </summary>
    /// <param name="interceptors">Interceptors to attach to every context the factory creates.</param>
    /// <returns>The ready fixture. Dispose it to close the connection.</returns>
    public static async Task<InMemoryPermissionDatabase> CreateAsync(params IInterceptor[] interceptors)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var schemaOptions = new DbContextOptionsBuilder<AdvancedPermissionsDbContext>()
            .UseSqlite(connection)
            .Options;
        await using (var db = new AdvancedPermissionsDbContext(schemaOptions))
        {
            await db.Database.EnsureCreatedAsync();
        }

        var options = new DbContextOptionsBuilder<AdvancedPermissionsDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(interceptors)
            .Options;

        return new InMemoryPermissionDatabase(connection, new SingleConnectionDbContextFactory(options));
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => connection.DisposeAsync();
}
