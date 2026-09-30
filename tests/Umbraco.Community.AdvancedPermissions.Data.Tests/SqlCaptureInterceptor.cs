using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Umbraco.Community.AdvancedPermissions.Data.Tests;

/// <summary>
/// A <see cref="DbCommandInterceptor"/> that records the text of every SQL command a context
/// executes, in order, so a test can assert on the <em>mechanism</em> a repository used rather than
/// only on the outcome it produced.
/// </summary>
/// <remarks>
/// <para>
/// It exists for the replace-semantics tests. Whether a save deletes with a predicate
/// <c>DELETE</c> or loads rows and removes them through the change tracker is invisible in the
/// outcome on the EF Core version in use — both orderings happen to work — so the only observable
/// difference is the SQL. This is a class rather than a record because it must derive from
/// <see cref="DbCommandInterceptor"/>.
/// </para>
/// <para>
/// The interceptor is SQLite-only in practice: it is attached to a SQLite context and the assertions
/// built on it (see <see cref="AssertPredicateDeleteThenInsert"/>) match SQLite's statement shape.
/// </para>
/// </remarks>
internal sealed class SqlCaptureInterceptor : DbCommandInterceptor
{
    /// <summary>
    /// The statements captured so far, in execution order.
    /// </summary>
    private readonly List<string> _statements = [];

    /// <summary>
    /// Gets a snapshot of the statements captured so far, in execution order.
    /// </summary>
    public IReadOnlyList<string> Statements => _statements.ToList();

    /// <inheritdoc />
    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        Capture(command);
        return result;
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Capture(command);
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc />
    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        Capture(command);
        return result;
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Capture(command);
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc />
    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        Capture(command);
        return result;
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        Capture(command);
        return ValueTask.FromResult(result);
    }

    /// <summary>
    /// Asserts that a replace issued a predicate delete followed by the inserts, and loaded nothing.
    /// </summary>
    /// <remarks>
    /// Three things are checked, each ruling out a different way of getting the same outcome:
    /// <list type="number">
    /// <item><description>
    /// A <c>DELETE</c> ran whose text names every one of <paramref name="keyColumns"/> — a predicate
    /// delete. Tracked removal deletes by primary key (<c>WHERE "Id" = ...</c>) and names none of them.
    /// </description></item>
    /// <item><description>That <c>DELETE</c> ran before the first <c>INSERT</c>.</description></item>
    /// <item><description>
    /// No <c>SELECT</c> ran at all. Tracked removal must first load the rows it is going to remove, so
    /// any <c>SELECT</c> means something was loaded to be removed.
    /// </description></item>
    /// </list>
    /// </remarks>
    /// <param name="statements">The captured statements, in execution order.</param>
    /// <param name="keyColumns">The column names the delete's predicate must reference.</param>
    public static void AssertPredicateDeleteThenInsert(IReadOnlyList<string> statements, params string[] keyColumns)
    {
        var deleteIndex = statements
            .Select((sql, index) => (sql, index))
            .Where(s => s.sql.TrimStart().StartsWith("DELETE", StringComparison.OrdinalIgnoreCase)
                     && keyColumns.All(column => s.sql.Contains(column, StringComparison.Ordinal)))
            .Select(s => s.index)
            .DefaultIfEmpty(-1)
            .First();

        var insertIndex = statements
            .Select((sql, index) => (sql, index))
            .Where(s => s.sql.TrimStart().StartsWith("INSERT", StringComparison.OrdinalIgnoreCase))
            .Select(s => s.index)
            .DefaultIfEmpty(-1)
            .First();

        var log = string.Join(Environment.NewLine + "---" + Environment.NewLine, statements);

        Assert.True(
            deleteIndex >= 0,
            $"Expected a predicate DELETE naming {string.Join(", ", keyColumns)}, but none ran. Statements:{Environment.NewLine}{log}");
        Assert.True(
            insertIndex >= 0,
            $"Expected an INSERT for the replacement rows, but none ran. Statements:{Environment.NewLine}{log}");
        Assert.True(
            deleteIndex < insertIndex,
            $"The predicate DELETE must execute before the first INSERT. Statements:{Environment.NewLine}{log}");
        Assert.DoesNotContain(
            statements,
            sql => sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Records one command's text.
    /// </summary>
    /// <param name="command">The command about to execute.</param>
    private void Capture(DbCommand command) => _statements.Add(command.CommandText);
}
