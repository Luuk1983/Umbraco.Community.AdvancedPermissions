using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Umbraco.Community.AdvancedPermissions.Core.Concurrency;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Models;
using Umbraco.Community.AdvancedPermissions.Data.Context;
using Umbraco.Community.AdvancedPermissions.Data.Repositories;

namespace Umbraco.Community.AdvancedPermissions.Data.Tests;

/// <summary>
/// Integration tests for <see cref="DocTypePermissionRepository.SaveManyAsync"/> using a SQLite
/// in-memory database.
/// </summary>
/// <remarks>
/// Document-type permissions do not share the node-keyed repository base — their key has a third
/// part, the document type — so they carry their own copy of the batch save and need their own proof
/// that it is all-or-nothing, that its delete provably precedes its insert, and that it rejects a
/// duplicate triple before touching the database.
/// </remarks>
public sealed class DocTypePermissionRepositoryBatchTests : IAsyncLifetime
{
    /// <summary>
    /// The open SQLite connection backing the in-memory database for the current test instance.
    /// </summary>
    private SqliteConnection _connection = null!;

    /// <summary>
    /// The repository under test, wired to <see cref="_connection"/>.
    /// </summary>
    private DocTypePermissionRepository _repository = null!;

    /// <summary>
    /// The interceptor recording the statements issued through <see cref="_interceptedOptions"/>.
    /// </summary>
    private readonly SqlCaptureInterceptor _sql = new();

    /// <summary>
    /// Context options over the same connection as <see cref="_repository"/> plus <see cref="_sql"/>,
    /// used to build a repository whose every statement is recorded.
    /// </summary>
    private DbContextOptions<AdvancedPermissionsDbContext> _interceptedOptions = null!;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AdvancedPermissionsDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using var db = new AdvancedPermissionsDbContext(options);
        await db.Database.EnsureCreatedAsync();

        _repository = new DocTypePermissionRepository(new SingleConnectionDbContextFactory(options));

        _interceptedOptions = new DbContextOptionsBuilder<AdvancedPermissionsDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(_sql)
            .Options;
    }

    /// <inheritdoc />
    public async Task DisposeAsync() => await _connection.DisposeAsync();

    /// <summary>
    /// Proves the happy path: one call covering two triples writes an entry for each.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_MultipleTriples_WritesAll()
    {
        var node1 = Guid.NewGuid();
        var node2 = Guid.NewGuid();
        var type1 = Guid.NewGuid();
        var type2 = Guid.NewGuid();
        const string role = "editors";

        await _repository.SaveManyAsync(
        [
            (node1, role, type1, new[] { (AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants, false) }, null),
            (node2, role, type2, new[] { (AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Deny, PermissionScope.ThisNodeOnly, false) }, null),
        ]);

        var forType1 = await _repository.GetByRoleAndContentTypeAsync(role, type1);
        var forType2 = await _repository.GetByRoleAndContentTypeAsync(role, type2);

        Assert.Single(forType1);
        Assert.Equal(node1, forType1[0].NodeKey);
        Assert.Equal(PermissionState.Allow, forType1[0].State);

        Assert.Single(forType2);
        Assert.Equal(node2, forType2[0].NodeKey);
        Assert.Equal(PermissionState.Deny, forType2[0].State);
    }

    /// <summary>
    /// Proves atomicity: when the second triple fails at the database, the first writes nothing
    /// either. The failure is a real unique-index collision (the same node, user group, document
    /// type, verb and scope twice), which only surfaces once EF flushes the staged inserts — after the
    /// valid first triple has already been staged — so a flush per triple would fail this test.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_FailureMidway_WritesNothing()
    {
        var node1 = Guid.NewGuid();
        var node2 = Guid.NewGuid();
        var type1 = Guid.NewGuid();
        var type2 = Guid.NewGuid();
        const string role = "editors";

        var batch = new (Guid NodeKey, string RoleAlias, Guid ContentTypeKey, IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries, string? ExpectedStamp)[]
        {
            (node1, role, type1, new[] { (AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Allow, PermissionScope.ThisNodeOnly, false) }, null),
            (node2, role, type2, new[]
            {
                (AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Allow, PermissionScope.ThisNodeOnly, false),
                (AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Deny, PermissionScope.ThisNodeOnly, false),
            }, null),
        };

        await Assert.ThrowsAsync<DbUpdateException>(() => _repository.SaveManyAsync(batch));

        Assert.Empty(await _repository.GetByRoleAndContentTypeAsync(role, type1));
        Assert.Empty(await _repository.GetByRoleAndContentTypeAsync(role, type2));
    }

    /// <summary>
    /// Proves the explicit transaction is load-bearing: the deletes run immediately while the inserts
    /// stay staged, so a failure midway must roll a triple's previous entries back rather than leave
    /// them deleted with nothing written in their place. Seeds a stored entry so the immediate delete
    /// has something to destroy — <c>SaveManyAsync_FailureMidway_WritesNothing</c> alone cannot catch a
    /// missing transaction.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_FailureMidway_LeavesPreviouslyStoredEntriesIntact()
    {
        var node1 = Guid.NewGuid();
        var node2 = Guid.NewGuid();
        var type1 = Guid.NewGuid();
        var type2 = Guid.NewGuid();
        const string role = "editors";

        await _repository.SaveAsync(node1, role, type1,
        [
            (AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Allow, PermissionScope.ThisNodeOnly, false),
        ]);

        var batch = new (Guid NodeKey, string RoleAlias, Guid ContentTypeKey, IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries, string? ExpectedStamp)[]
        {
            (node1, role, type1, new[] { (AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Deny, PermissionScope.ThisNodeOnly, false) }, null),
            (node2, role, type2, new[]
            {
                (AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Allow, PermissionScope.ThisNodeOnly, false),
                (AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Deny, PermissionScope.ThisNodeOnly, false),
            }, null),
        };

        await Assert.ThrowsAsync<DbUpdateException>(() => _repository.SaveManyAsync(batch));

        var survivors = await _repository.GetByRoleAndContentTypeAsync(role, type1);
        Assert.Single(survivors);
        Assert.Equal(PermissionState.Allow, survivors[0].State);
    }

    /// <summary>
    /// Proves the ordinary save: replacing an entry with the same verb and scope but a different state
    /// succeeds, which only holds if the delete provably reaches the database before the insert.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_ReplacesEntryWithSameVerbAndScope_Succeeds()
    {
        var nodeKey = Guid.NewGuid();
        var typeKey = Guid.NewGuid();
        const string role = "editors";

        await _repository.SaveAsync(nodeKey, role, typeKey,
        [
            (AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Allow, PermissionScope.ThisNodeOnly, false),
        ]);

        await _repository.SaveManyAsync(
        [
            (nodeKey, role, typeKey, new[] { (AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Deny, PermissionScope.ThisNodeOnly, false) }, null),
        ]);

        var results = await _repository.GetByRoleAndContentTypeAsync(role, typeKey);

        Assert.Single(results);
        Assert.Equal(PermissionState.Deny, results[0].State);
    }

    /// <summary>
    /// Protects the requirement that <c>SaveManyAsync</c> replaces a triple's entries with a predicate
    /// delete (<c>ExecuteDeleteAsync</c>) that runs before the inserts, and never by loading the rows
    /// and removing them through the change tracker. The doc-type twin of
    /// <c>NodePermissionRepositoryBatchTestsBase.SaveManyAsync_Replace_UsesPredicateDelete_NotTrackedRemoval</c>.
    /// </summary>
    /// <remarks>
    /// No outcome-based test can guard this: on the EF version in use, tracked removal happens to order
    /// correctly against the unique index, so
    /// <c>SaveManyAsync_ReplacesEntryWithSameVerbAndScope_Succeeds</c> passes either way. Only the
    /// mechanism is observable, so this test asserts on the SQL issued. It is SQLite-only: it asserts on
    /// the statement text SQLite receives and must not be run against another provider.
    /// </remarks>
    [Fact]
    public async Task SaveManyAsync_Replace_UsesPredicateDelete_NotTrackedRemoval()
    {
        var nodeKey = Guid.NewGuid();
        var typeKey = Guid.NewGuid();
        const string role = "editors";

        await _repository.SaveAsync(nodeKey, role, typeKey,
        [
            (AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Allow, PermissionScope.ThisNodeOnly, false),
        ]);

        var intercepted = new DocTypePermissionRepository(new SingleConnectionDbContextFactory(_interceptedOptions));
        await intercepted.SaveManyAsync(
        [
            (nodeKey, role, typeKey, new[] { (AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Deny, PermissionScope.ThisNodeOnly, false) }, null),
        ]);

        SqlCaptureInterceptor.AssertPredicateDeleteThenInsert(_sql.Statements, "NodeKey", "RoleAlias", "ContentTypeKey");

        var results = await _repository.GetByRoleAndContentTypeAsync(role, typeKey);
        Assert.Single(results);
        Assert.Equal(PermissionState.Deny, results[0].State);
    }

    /// <summary>
    /// The single-save twin of <c>SaveManyAsync_Replace_UsesPredicateDelete_NotTrackedRemoval</c>:
    /// <c>SaveAsync</c> has the same requirement for the same reason, and the same blind spot.
    /// SQLite-only, for the same reason.
    /// </summary>
    [Fact]
    public async Task SaveAsync_Replace_UsesPredicateDelete_NotTrackedRemoval()
    {
        var nodeKey = Guid.NewGuid();
        var typeKey = Guid.NewGuid();
        const string role = "editors";

        await _repository.SaveAsync(nodeKey, role, typeKey,
        [
            (AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Allow, PermissionScope.ThisNodeOnly, false),
        ]);

        var intercepted = new DocTypePermissionRepository(new SingleConnectionDbContextFactory(_interceptedOptions));
        await intercepted.SaveAsync(nodeKey, role, typeKey,
        [
            (AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Deny, PermissionScope.ThisNodeOnly, false),
        ]);

        SqlCaptureInterceptor.AssertPredicateDeleteThenInsert(_sql.Statements, "NodeKey", "RoleAlias", "ContentTypeKey");

        var results = await _repository.GetByRoleAndContentTypeAsync(role, typeKey);
        Assert.Single(results);
        Assert.Equal(PermissionState.Deny, results[0].State);
    }

    /// <summary>
    /// Proves the batch only replaces the triples it names: the same node and user group with a
    /// different document type is a different triple and must be left alone.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_LeavesOtherDocumentTypesUntouched()
    {
        var nodeKey = Guid.NewGuid();
        var typeA = Guid.NewGuid();
        var typeB = Guid.NewGuid();
        const string role = "editors";

        await _repository.SaveAsync(nodeKey, role, typeB,
        [
            (AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Allow, PermissionScope.ThisNodeOnly, false),
        ]);

        await _repository.SaveManyAsync(
        [
            (nodeKey, role, typeA, new[] { (AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Deny, PermissionScope.ThisNodeOnly, false) }, null),
        ]);

        Assert.Single(await _repository.GetByRoleAndContentTypeAsync(role, typeB));
        Assert.Single(await _repository.GetByRoleAndContentTypeAsync(role, typeA));
    }

    /// <summary>
    /// Proves that an empty entry list inside a triple removes what was stored.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_EmptyEntriesForTriple_RemovesExistingEntries()
    {
        var nodeKey = Guid.NewGuid();
        var typeKey = Guid.NewGuid();
        const string role = "editors";

        await _repository.SaveAsync(nodeKey, role, typeKey,
        [
            (AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Allow, PermissionScope.ThisNodeOnly, false),
        ]);

        await _repository.SaveManyAsync(
        [
            (nodeKey, role, typeKey, Array.Empty<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)>(), null),
        ]);

        Assert.Empty(await _repository.GetByRoleAndContentTypeAsync(role, typeKey));
    }

    /// <summary>
    /// Proves the upfront guard: two entries for the same triple throw
    /// <see cref="ArgumentException"/> before any database work, instead of merging silently or
    /// colliding on the unique index with a confusing exception.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_DuplicateTriple_ThrowsArgumentException()
    {
        var nodeKey = Guid.NewGuid();
        var typeKey = Guid.NewGuid();
        const string role = "editors";

        var batch = new (Guid NodeKey, string RoleAlias, Guid ContentTypeKey, IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries, string? ExpectedStamp)[]
        {
            (nodeKey, role, typeKey, new[] { (AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Allow, PermissionScope.ThisNodeOnly, false) }, null),
            (nodeKey, role, typeKey, new[] { (AdvancedPermissionsConstants.VerbElementCreateOfType, PermissionState.Deny, PermissionScope.ThisNodeOnly, false) }, null),
        };

        await Assert.ThrowsAsync<ArgumentException>(() => _repository.SaveManyAsync(batch));

        Assert.Empty(await _repository.GetByRoleAndContentTypeAsync(role, typeKey));
    }

    /// <summary>
    /// Pins the invariant the batch endpoint depends on: the stamp of what a client submitted equals
    /// the stamp of what is stored afterwards, even when the state and scope arrive in mixed casing.
    /// If storage ever normalised anything, every second save would report a conflict nobody caused.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_StampOfSubmittedEntries_MatchesStampOfStoredEntries()
    {
        var nodeKey = Guid.NewGuid();
        var typeKey = Guid.NewGuid();
        const string role = "editors";

        var submitted = new (string Verb, string State, string Scope, bool IsPriorityOverride)[]
        {
            (AdvancedPermissionsConstants.VerbCreateOfType, "allow", "ThisNodeAndDescendants", false),
            (AdvancedPermissionsConstants.VerbCreateOfType, "DENY", "descendantsonly", true),
        };

        var toSave = submitted
            .Select(e => (
                e.Verb,
                Enum.Parse<PermissionState>(e.State, ignoreCase: true),
                Enum.Parse<PermissionScope>(e.Scope, ignoreCase: true),
                e.IsPriorityOverride))
            .ToArray();

        await _repository.SaveManyAsync([(nodeKey, role, typeKey, toSave, null)]);

        var stored = await _repository.GetByRoleAndContentTypeAsync(role, typeKey);

        Assert.Equal(
            PermissionStamp.ComputeFromNames(submitted),
            PermissionStamp.ComputeFromNames(
                stored.Select(e => (e.Verb, e.State.ToString(), e.Scope.ToString(), e.IsPriorityOverride))));
    }
}
