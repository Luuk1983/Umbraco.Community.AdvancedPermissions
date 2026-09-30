using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Models;
using Umbraco.Community.AdvancedPermissions.Data.Repositories;

namespace Umbraco.Community.AdvancedPermissions.Data.Tests;

/// <summary>
/// Guards, by mechanism, the requirement that replacing entries uses a predicate <c>DELETE</c> —
/// <c>ExecuteDeleteAsync</c> — issued before the inserts, rather than loading the old rows and
/// removing them tracked.
/// </summary>
/// <remarks>
/// <para>
/// The unique index on node, user group, verb and scope means an ordinary tracked save collides
/// whenever a replacement row has the same verb and scope as the row it replaces — which is the
/// everyday Permissions Editor save, flipping Allow to Deny. Only a delete that provably reaches the
/// database before the first insert avoids it. Until this test existed that requirement was held up
/// by a comment alone: swapping <c>ExecuteDeleteAsync</c> for a <c>ToListAsync</c> plus
/// <c>RemoveRange</c> left every other test passing, because EF's own command ordering happens to
/// get it right on SQLite.
/// </para>
/// <para>
/// So this test looks at what is sent to the database. It records every command while a replace
/// runs and asserts that a <c>DELETE</c> on the key columns executes, that it precedes the first
/// <c>INSERT</c>, and that no <c>SELECT</c> is issued at all — a load-then-remove implementation
/// necessarily issues one. The replaces here pass no expected stamp: with one, the write
/// legitimately reads the stored rows to check it, and that read is not the loading being guarded
/// against.
/// </para>
/// <para>
/// SQLite only. The assertions are on SQLite's command text through its provider's interceptor
/// hooks; nothing here says what SQL Server is sent, though the same repository code runs there.
/// </para>
/// </remarks>
public sealed class ReplaceWriteStatementTests : IAsyncLifetime
{
    /// <summary>The user group every test saves for.</summary>
    private const string Role = "editors";

    /// <summary>The interceptor recording every command the repositories under test execute.</summary>
    private readonly RecordingCommandInterceptor _commands = new();

    /// <summary>The in-memory database the interceptor is attached to.</summary>
    private InMemoryPermissionDatabase _database = null!;

    /// <inheritdoc />
    public async Task InitializeAsync() =>
        _database = await InMemoryPermissionDatabase.CreateAsync(_commands);

    /// <inheritdoc />
    public async Task DisposeAsync() => await _database.DisposeAsync();

    /// <summary>
    /// Asserts the three properties of a predicate replace against the commands recorded since the
    /// last <see cref="RecordingCommandInterceptor.Clear"/>.
    /// </summary>
    /// <param name="table">The table being replaced into, as it appears in the SQL.</param>
    /// <param name="keyColumns">The key columns the delete must be predicated on.</param>
    private void AssertPredicateDeleteThenInsertWithoutAnySelect(string table, params string[] keyColumns)
    {
        var statements = _commands.Statements.Select(s => s.TrimStart()).ToList();

        // Any SELECT means the old rows were loaded, which is the tracked-removal shape.
        Assert.DoesNotContain(statements, s => s.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase));

        var firstDelete = statements.FindIndex(s =>
            s.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase)
            && s.Contains(table, StringComparison.Ordinal)
            && keyColumns.All(c => s.Contains($"\"{c}\"", StringComparison.Ordinal)));
        Assert.True(
            firstDelete >= 0,
            $"Expected a DELETE on {table} predicated on {string.Join(", ", keyColumns)}, but recorded:\n{string.Join("\n", statements)}");

        var firstInsert = statements.FindIndex(s => s.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.True(firstInsert >= 0, "Expected the replacement rows to be inserted.");
        Assert.True(
            firstDelete < firstInsert,
            $"The DELETE must execute before the first INSERT, but recorded:\n{string.Join("\n", statements)}");
    }

    /// <summary>
    /// <c>SaveAsync</c> replacing an entry with the same verb and scope but a different state — the
    /// everyday save — deletes by node and user group first, inserts after, and never selects.
    /// </summary>
    [Fact]
    public async Task NodeRepository_SaveAsync_ReplacesWithPredicateDeleteBeforeInsertAndNeverSelects()
    {
        var repository = new AdvancedPermissionRepository(_database.Factory);
        var nodeKey = Guid.NewGuid();
        await repository.SaveAsync(nodeKey, Role,
            [(AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)]);
        _commands.Clear();

        await repository.SaveAsync(nodeKey, Role,
            [(AdvancedPermissionsConstants.VerbRead, PermissionState.Deny, PermissionScope.ThisNodeOnly, false)]);

        AssertPredicateDeleteThenInsertWithoutAnySelect("AdvancedPermission", "NodeKey", "RoleAlias");
    }

    /// <summary>
    /// <c>SaveManyAsync</c> replacing entries across several pairs does the same, for every pair: no
    /// select, and the delete before the first insert.
    /// </summary>
    [Fact]
    public async Task NodeRepository_SaveManyAsync_ReplacesWithPredicateDeleteBeforeInsertAndNeverSelects()
    {
        var repository = new AdvancedPermissionRepository(_database.Factory);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await repository.SaveManyAsync(
        [
            (first, Role, [(AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], null),
            (second, Role, [(AdvancedPermissionsConstants.VerbDelete, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], null),
        ]);
        _commands.Clear();

        await repository.SaveManyAsync(
        [
            (first, Role, [(AdvancedPermissionsConstants.VerbRead, PermissionState.Deny, PermissionScope.ThisNodeOnly, false)], null),
            (second, Role, [(AdvancedPermissionsConstants.VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeOnly, false)], null),
        ]);

        AssertPredicateDeleteThenInsertWithoutAnySelect("AdvancedPermission", "NodeKey", "RoleAlias");
    }

    /// <summary>The document-type repository's <c>SaveAsync</c> is held to the same requirement.</summary>
    [Fact]
    public async Task DocTypeRepository_SaveAsync_ReplacesWithPredicateDeleteBeforeInsertAndNeverSelects()
    {
        var repository = new DocTypePermissionRepository(_database.Factory);
        var nodeKey = Guid.NewGuid();
        var typeKey = Guid.NewGuid();
        await repository.SaveAsync(nodeKey, Role, typeKey,
            [(AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)]);
        _commands.Clear();

        await repository.SaveAsync(nodeKey, Role, typeKey,
            [(AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Deny, PermissionScope.ThisNodeOnly, false)]);

        AssertPredicateDeleteThenInsertWithoutAnySelect("DocTypePermission", "NodeKey", "RoleAlias", "ContentTypeKey");
    }

    /// <summary>The document-type repository's <c>SaveManyAsync</c> is held to the same requirement.</summary>
    [Fact]
    public async Task DocTypeRepository_SaveManyAsync_ReplacesWithPredicateDeleteBeforeInsertAndNeverSelects()
    {
        var repository = new DocTypePermissionRepository(_database.Factory);
        var nodeKey = Guid.NewGuid();
        var typeA = Guid.NewGuid();
        var typeB = Guid.NewGuid();
        await repository.SaveManyAsync(
        [
            (nodeKey, Role, typeA, [(AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], null),
            (nodeKey, Role, typeB, [(AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], null),
        ]);
        _commands.Clear();

        await repository.SaveManyAsync(
        [
            (nodeKey, Role, typeA, [(AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Deny, PermissionScope.ThisNodeOnly, false)], null),
            (nodeKey, Role, typeB, [(AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Deny, PermissionScope.ThisNodeOnly, false)], null),
        ]);

        AssertPredicateDeleteThenInsertWithoutAnySelect("DocTypePermission", "NodeKey", "RoleAlias", "ContentTypeKey");
    }
}
