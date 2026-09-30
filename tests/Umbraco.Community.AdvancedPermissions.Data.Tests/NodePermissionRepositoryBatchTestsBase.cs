using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Umbraco.Community.AdvancedPermissions.Core.Concurrency;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;
using Umbraco.Community.AdvancedPermissions.Data.Context;

namespace Umbraco.Community.AdvancedPermissions.Data.Tests;

/// <summary>
/// The batch-save contract, written once against <see cref="INodePermissionRepository"/> and run
/// against every node-keyed family that inherits <c>SaveManyAsync</c> from
/// <c>NodePermissionRepositoryBase</c> — content and library elements today.
/// </summary>
/// <remarks>
/// <para>
/// The Permissions Editors let a user change permissions across several nodes and then hit Save
/// once. Before the concurrency stamp can mean anything, the write underneath it has to be
/// all-or-nothing: a partial save (some nodes updated, some not) is a worse outcome than either
/// completing or refusing, because nothing afterwards can tell which half landed.
/// </para>
/// <para>
/// It is an abstract base rather than a copy per family on purpose. The implementation lives in the
/// shared repository base, so a test that ran for only one family could pass while the other
/// silently had no protection. Each concrete subclass supplies its own repository and its own verb
/// vocabulary and inherits every test below, which is what proves the inheritance actually holds.
/// </para>
/// </remarks>
public abstract partial class NodePermissionRepositoryBatchTestsBase : IAsyncLifetime
{
    /// <summary>
    /// The open SQLite connection backing the in-memory database for the current test instance.
    /// </summary>
    private SqliteConnection _connection = null!;

    /// <summary>
    /// Gets a verb that is valid for the family under test and is used as the "first" verb.
    /// </summary>
    protected abstract string VerbA { get; }

    /// <summary>
    /// Gets a second, different verb valid for the family under test.
    /// </summary>
    protected abstract string VerbB { get; }

    /// <summary>
    /// Gets a third, different verb valid for the family under test.
    /// </summary>
    protected abstract string VerbC { get; }

    /// <summary>
    /// Gets the repository under test, created by the concrete subclass over the shared connection.
    /// </summary>
    protected INodePermissionRepository Repository { get; private set; } = null!;

    /// <summary>
    /// Gets the DbContext options for the in-memory database, so a subclass can query a table
    /// directly rather than through the repository.
    /// </summary>
    protected DbContextOptions<AdvancedPermissionsDbContext> Options { get; private set; } = null!;

    /// <summary>
    /// Creates the concrete repository for the family under test.
    /// </summary>
    /// <param name="factory">A factory handing out contexts over the shared in-memory connection.</param>
    /// <returns>The repository to test.</returns>
    protected abstract INodePermissionRepository CreateRepository(IDbContextFactory<AdvancedPermissionsDbContext> factory);

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        // Keep a single connection open so the in-memory SQLite database persists for the test.
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        Options = new DbContextOptionsBuilder<AdvancedPermissionsDbContext>()
            .UseSqlite(_connection)
            .Options;

        // Create the schema from EF's model, including the unique indexes several tests rely on.
        await using var db = new AdvancedPermissionsDbContext(Options);
        await db.Database.EnsureCreatedAsync();

        Repository = CreateRepository(new SingleConnectionDbContextFactory(Options));
    }

    /// <inheritdoc />
    public async Task DisposeAsync() => await _connection.DisposeAsync();

    /// <summary>
    /// Proves the happy path: a single <c>SaveManyAsync</c> call covering two different node keys
    /// writes an entry for each one. This is the everyday shape of an editor's multi-node save.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_MultipleNodes_WritesAll()
    {
        var node1 = Guid.NewGuid();
        var node2 = Guid.NewGuid();
        const string role = "editors";

        await Repository.SaveManyAsync(
        [
            (node1, role, new[] { (VerbA, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants, false) }, null),
            (node2, role, new[] { (VerbB, PermissionState.Deny, PermissionScope.ThisNodeOnly, false) }, null),
        ]);

        var node1Results = await Repository.GetByNodeAndRoleAsync(node1, role);
        var node2Results = await Repository.GetByNodeAndRoleAsync(node2, role);

        Assert.Single(node1Results);
        Assert.Equal(VerbA, node1Results[0].Verb);
        Assert.Equal(PermissionState.Allow, node1Results[0].State);

        Assert.Single(node2Results);
        Assert.Equal(VerbB, node2Results[0].Verb);
        Assert.Equal(PermissionState.Deny, node2Results[0].State);
    }

    /// <summary>
    /// Proves atomicity: when the second pair in a batch fails at the database, the first pair
    /// writes nothing either, even though it was staged and would otherwise have succeeded alone.
    /// </summary>
    /// <remarks>
    /// The failure is engineered against a real database constraint rather than simulated: the
    /// second pair carries two rows sharing the same node, user group, verb and scope, which
    /// collides with the table's unique index. That only throws once EF flushes the pending inserts
    /// in <c>SaveChangesAsync</c> — after both pairs, including the valid first one, have been
    /// staged on the same context. A single flush for the whole batch inside one transaction is
    /// what rolls the first pair back too; a flush per pair would commit it first and fail this test.
    /// </remarks>
    [Fact]
    public async Task SaveManyAsync_FailureMidway_WritesNothing()
    {
        var node1 = Guid.NewGuid();
        var node2 = Guid.NewGuid();
        const string role = "editors";

        var batch = new (Guid NodeKey, string RoleAlias, IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries, string? ExpectedStamp)[]
        {
            (node1, role, new[] { (VerbA, PermissionState.Allow, PermissionScope.ThisNodeOnly, false) }, null),
            (node2, role, new[]
            {
                // Same node, user group, verb and scope twice: violates the unique index.
                (VerbB, PermissionState.Allow, PermissionScope.ThisNodeOnly, false),
                (VerbB, PermissionState.Deny, PermissionScope.ThisNodeOnly, false),
            }, null),
        };

        await Assert.ThrowsAsync<DbUpdateException>(() => Repository.SaveManyAsync(batch));

        Assert.Empty(await Repository.GetByNodeAndRoleAsync(node1, role));
        Assert.Empty(await Repository.GetByNodeAndRoleAsync(node2, role));
    }

    /// <summary>
    /// Proves the explicit transaction is load-bearing, not decorative: the deletes run immediately
    /// while the inserts stay staged, so a failure midway would leave a pair's previous entries
    /// deleted and nothing written in their place unless the transaction rolls the deletes back.
    /// </summary>
    /// <remarks>
    /// <c>SaveManyAsync_FailureMidway_WritesNothing</c> alone cannot catch a missing transaction,
    /// because its first pair has nothing stored to lose. This test seeds one, so the immediate
    /// delete has something to destroy.
    /// </remarks>
    [Fact]
    public async Task SaveManyAsync_FailureMidway_LeavesPreviouslyStoredEntriesIntact()
    {
        var node1 = Guid.NewGuid();
        var node2 = Guid.NewGuid();
        const string role = "editors";

        await Repository.SaveAsync(node1, role,
        [
            (VerbA, PermissionState.Allow, PermissionScope.ThisNodeOnly, false),
        ], (string?)null);

        var batch = new (Guid NodeKey, string RoleAlias, IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries, string? ExpectedStamp)[]
        {
            (node1, role, new[] { (VerbA, PermissionState.Deny, PermissionScope.ThisNodeOnly, false) }, null),
            (node2, role, new[]
            {
                (VerbB, PermissionState.Allow, PermissionScope.ThisNodeOnly, false),
                (VerbB, PermissionState.Deny, PermissionScope.ThisNodeOnly, false),
            }, null),
        };

        await Assert.ThrowsAsync<DbUpdateException>(() => Repository.SaveManyAsync(batch));

        var survivors = await Repository.GetByNodeAndRoleAsync(node1, role);
        Assert.Single(survivors);
        Assert.Equal(VerbA, survivors[0].Verb);
        Assert.Equal(PermissionState.Allow, survivors[0].State);
    }

    /// <summary>
    /// Proves the ordinary save: replacing an existing entry with the same verb and scope but a
    /// different state (flipping Allow to Deny) is the everyday shape of an editor save, and it
    /// collides with the unique index unless the delete for the old row provably reaches the
    /// database before the insert of the new one.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_ReplacesEntryWithSameVerbAndScope_Succeeds()
    {
        var nodeKey = Guid.NewGuid();
        const string role = "editors";

        await Repository.SaveAsync(nodeKey, role,
        [
            (VerbA, PermissionState.Allow, PermissionScope.ThisNodeOnly, false),
        ], (string?)null);

        await Repository.SaveManyAsync(
        [
            (nodeKey, role, new[] { (VerbA, PermissionState.Deny, PermissionScope.ThisNodeOnly, false) }, null),
        ]);

        var results = await Repository.GetByNodeAndRoleAsync(nodeKey, role);

        Assert.Single(results);
        Assert.Equal(PermissionState.Deny, results[0].State);
    }

    /// <summary>
    /// Builds a repository over the same in-memory database whose every SQL command is recorded, for
    /// tests that assert on the mechanism of a write rather than its outcome.
    /// </summary>
    /// <returns>The repository and the interceptor holding the statements it issues.</returns>
    private (INodePermissionRepository Repository, SqlCaptureInterceptor Sql) CreateInterceptedRepository()
    {
        var sql = new SqlCaptureInterceptor();
        var options = new DbContextOptionsBuilder<AdvancedPermissionsDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(sql)
            .Options;

        return (CreateRepository(new SingleConnectionDbContextFactory(options)), sql);
    }

    /// <summary>
    /// Protects the requirement that <c>SaveManyAsync</c> replaces a pair's entries with a predicate
    /// delete (<c>ExecuteDeleteAsync</c>) that runs before the inserts, and never by loading the rows
    /// and removing them through the change tracker.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The table's unique index means an ordinary save - flipping an entry from Allow to Deny while
    /// keeping its verb and scope - collides unless the old row's delete provably reaches the database
    /// before the new row's insert. EF Core's ordering of tracked removals against inserts has varied
    /// across versions.
    /// </para>
    /// <para>
    /// No outcome-based test can guard this: on the EF version in use, tracked removal happens to order
    /// correctly, so <c>SaveManyAsync_ReplacesEntryWithSameVerbAndScope_Succeeds</c> passes either way.
    /// Only the mechanism is observable, so this test records the SQL and asserts on its shape (see
    /// <see cref="SqlCaptureInterceptor.AssertPredicateDeleteThenInsert"/>). It is therefore SQLite-only:
    /// it asserts on the statement text SQLite receives and must not be run against another provider.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task SaveManyAsync_Replace_UsesPredicateDelete_NotTrackedRemoval()
    {
        var nodeKey = Guid.NewGuid();
        const string role = "editors";

        await Repository.SaveAsync(nodeKey, role,
        [
            (VerbA, PermissionState.Allow, PermissionScope.ThisNodeOnly, false),
        ], (string?)null);

        var (intercepted, sql) = CreateInterceptedRepository();
        await intercepted.SaveManyAsync(
        [
            (nodeKey, role, new[] { (VerbA, PermissionState.Deny, PermissionScope.ThisNodeOnly, false) }, null),
        ]);

        SqlCaptureInterceptor.AssertPredicateDeleteThenInsert(sql.Statements, "NodeKey", "RoleAlias");

        var results = await Repository.GetByNodeAndRoleAsync(nodeKey, role);
        Assert.Single(results);
        Assert.Equal(PermissionState.Deny, results[0].State);
    }

    /// <summary>
    /// The single-save twin of <c>SaveManyAsync_Replace_UsesPredicateDelete_NotTrackedRemoval</c>:
    /// <c>SaveAsync</c> has the same requirement for the same reason, and the same blind spot - an
    /// outcome-based test passes whichever mechanism is used. SQLite-only, for the same reason.
    /// </summary>
    [Fact]
    public async Task SaveAsync_Replace_UsesPredicateDelete_NotTrackedRemoval()
    {
        var nodeKey = Guid.NewGuid();
        const string role = "editors";

        await Repository.SaveAsync(nodeKey, role,
        [
            (VerbA, PermissionState.Allow, PermissionScope.ThisNodeOnly, false),
        ], (string?)null);

        var (intercepted, sql) = CreateInterceptedRepository();
        await intercepted.SaveAsync(nodeKey, role,
        [
            (VerbA, PermissionState.Deny, PermissionScope.ThisNodeOnly, false),
        ], (string?)null);

        SqlCaptureInterceptor.AssertPredicateDeleteThenInsert(sql.Statements, "NodeKey", "RoleAlias");

        var results = await Repository.GetByNodeAndRoleAsync(nodeKey, role);
        Assert.Single(results);
        Assert.Equal(PermissionState.Deny, results[0].State);
    }

    /// <summary>
    /// Proves the realistic multi-verb save: one verb's entry is kept identical while another verb's
    /// state is flipped. Both rows share the node and user group, so the unchanged entry's
    /// delete-then-reinsert has to succeed alongside the flipped one's within the same pair.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_MultiVerbPair_KeepsIdenticalEntryAndFlipsAnother()
    {
        var nodeKey = Guid.NewGuid();
        const string role = "editors";

        await Repository.SaveAsync(nodeKey, role,
        [
            (VerbA, PermissionState.Allow, PermissionScope.ThisNodeOnly, false),
            (VerbB, PermissionState.Allow, PermissionScope.ThisNodeOnly, false),
        ], (string?)null);

        await Repository.SaveManyAsync(
        [
            (nodeKey, role, new[]
            {
                (VerbA, PermissionState.Allow, PermissionScope.ThisNodeOnly, false),
                (VerbB, PermissionState.Deny, PermissionScope.ThisNodeOnly, false),
            }, null),
        ]);

        var results = await Repository.GetByNodeAndRoleAsync(nodeKey, role);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Verb == VerbA && r.State == PermissionState.Allow);
        Assert.Contains(results, r => r.Verb == VerbB && r.State == PermissionState.Deny);
    }

    /// <summary>
    /// Proves that an empty entry list inside a pair removes what was stored, which is how an editor
    /// reverts a node to "inherit" as part of a batch.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_EmptyEntriesForPair_RemovesExistingEntries()
    {
        var nodeKey = Guid.NewGuid();
        const string role = "editors";

        await Repository.SaveAsync(nodeKey, role,
        [
            (VerbA, PermissionState.Allow, PermissionScope.ThisNodeOnly, false),
        ], (string?)null);

        await Repository.SaveManyAsync(
        [
            (nodeKey, role, Array.Empty<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)>(), null),
        ]);

        Assert.Empty(await Repository.GetByNodeAndRoleAsync(nodeKey, role));
    }

    /// <summary>
    /// Proves the batch only replaces the pairs it names: another user group's entries on the same
    /// node, and the same user group's entries on another node, are left alone.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_LeavesOtherPairsUntouched()
    {
        var nodeKey = Guid.NewGuid();
        var otherNode = Guid.NewGuid();

        await Repository.SaveAsync(nodeKey, "writers",
        [
            (VerbA, PermissionState.Allow, PermissionScope.ThisNodeOnly, false),
        ], (string?)null);
        await Repository.SaveAsync(otherNode, "editors",
        [
            (VerbA, PermissionState.Allow, PermissionScope.ThisNodeOnly, false),
        ], (string?)null);

        await Repository.SaveManyAsync(
        [
            (nodeKey, "editors", new[] { (VerbB, PermissionState.Deny, PermissionScope.ThisNodeOnly, false) }, null),
        ]);

        Assert.Single(await Repository.GetByNodeAndRoleAsync(nodeKey, "writers"));
        Assert.Single(await Repository.GetByNodeAndRoleAsync(otherNode, "editors"));
        Assert.Single(await Repository.GetByNodeAndRoleAsync(nodeKey, "editors"));
    }

    /// <summary>
    /// Proves the upfront guard: two pairs in one batch for the same node and user group throw
    /// <see cref="ArgumentException"/> before any database work, rather than silently merging (or,
    /// with overlapping verbs, colliding on the unique index with a confusing exception). A
    /// well-behaved caller groups by node first, so this should never fire in practice — which is
    /// exactly why it must be loud rather than silent if it ever does.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_DuplicatePairForSameNodeAndRole_ThrowsArgumentException()
    {
        var nodeKey = Guid.NewGuid();
        const string role = "editors";

        var batch = new (Guid NodeKey, string RoleAlias, IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries, string? ExpectedStamp)[]
        {
            (nodeKey, role, new[] { (VerbA, PermissionState.Allow, PermissionScope.ThisNodeOnly, false) }, null),
            (nodeKey, role, new[] { (VerbB, PermissionState.Deny, PermissionScope.ThisNodeOnly, false) }, null),
        };

        await Assert.ThrowsAsync<ArgumentException>(() => Repository.SaveManyAsync(batch));

        // No database work should have happened before the guard fired.
        Assert.Empty(await Repository.GetByNodeAndRoleAsync(nodeKey, role));
    }

    /// <summary>
    /// Pins the invariant the batch save endpoint depends on: the stamp computed from what a client
    /// submitted must equal the stamp a subsequent read of the same entries computes. If this ever
    /// drifts — for example because a future change trims whitespace or normalises casing on the way
    /// into storage — the client's very next save carries a stamp that no longer matches what the
    /// server reads back, and every second save fails with a spurious conflict about a change nobody
    /// made. That trains people to click "Overwrite anyway" until the concurrency check is worse than
    /// useless.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_StampOfSubmittedEntries_MatchesStampOfStoredEntries()
    {
        var nodeKey = Guid.NewGuid();
        const string role = "editors";

        // A realistic submission: several verbs, a non-default scope, a priority override, and
        // state/scope spelled with the mixed casing a real client's JSON payload sends rather than
        // the exact casing the enums would round-trip through ToString().
        var submitted = new (string Verb, string State, string Scope, bool IsPriorityOverride)[]
        {
            (VerbA, "allow", "ThisNodeAndDescendants", false),
            (VerbB, "Deny", "thisnodeonly", true),
            (VerbC, "ALLOW", "DescendantsOnly", false),
        };

        var toSave = submitted
            .Select(e => (
                e.Verb,
                Enum.Parse<PermissionState>(e.State, ignoreCase: true),
                Enum.Parse<PermissionScope>(e.Scope, ignoreCase: true),
                e.IsPriorityOverride))
            .ToArray();

        await Repository.SaveManyAsync([(nodeKey, role, toSave, null)]);

        var stored = await Repository.GetByNodeAndRoleAsync(nodeKey, role);

        Assert.Equal(
            PermissionStamp.ComputeFromNames(submitted),
            PermissionStamp.Compute(stored));
    }
}
