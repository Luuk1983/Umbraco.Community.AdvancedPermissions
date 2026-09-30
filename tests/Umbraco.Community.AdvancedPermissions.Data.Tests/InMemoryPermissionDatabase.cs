using System.Data.Common;
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

/// <summary>
/// Records the text of every SQL command a context executes, in order, so a test can assert on the
/// mechanism a write used rather than only on its outcome.
/// </summary>
/// <remarks>
/// A class rather than a record: EF Core interceptors are registered by instance and must derive
/// from <see cref="DbCommandInterceptor"/>. Reader, non-query and scalar commands are all captured,
/// in both their synchronous and asynchronous forms — EF sends inserts through a reader and
/// <c>ExecuteDelete</c> through a non-query, so watching only one kind would miss half the picture.
/// </remarks>
internal sealed class RecordingCommandInterceptor : DbCommandInterceptor
{
    /// <summary>The recorded command texts, oldest first.</summary>
    private readonly List<string> _statements = [];

    /// <summary>
    /// Gets a snapshot of the command texts recorded since creation or the last <see cref="Clear"/>.
    /// </summary>
    public IReadOnlyList<string> Statements => _statements.ToList();

    /// <summary>
    /// Forgets everything recorded so far, so a test can seed data and then record only the
    /// operation it is actually asserting on.
    /// </summary>
    public void Clear() => _statements.Clear();

    /// <inheritdoc />
    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        _statements.Add(command.CommandText);
        return result;
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        _statements.Add(command.CommandText);
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc />
    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        _statements.Add(command.CommandText);
        return result;
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        _statements.Add(command.CommandText);
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc />
    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        _statements.Add(command.CommandText);
        return result;
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        _statements.Add(command.CommandText);
        return ValueTask.FromResult(result);
    }
}
