using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Umbraco.Community.AdvancedPermissions.Core.Concurrency;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Models;
using Umbraco.Community.AdvancedPermissions.Data.Context;
using Umbraco.Community.AdvancedPermissions.Data.Repositories;

namespace Umbraco.Community.AdvancedPermissions.Data.Tests;

/// <summary>
/// Integration tests for <see cref="AdvancedPermissionRepository.SaveManyAsync"/> using a SQLite
/// in-memory database. Mirrors the fixture pattern used by <see cref="AdvancedPermissionRepositoryTests"/>:
/// a single open <see cref="SqliteConnection"/> keeps the in-memory database alive for the test, and a
/// <see cref="SingleConnectionDbContextFactory"/> hands out short-lived contexts over that connection.
/// </summary>
/// <remarks>
/// The Permissions Editor lets a user change permissions across several content nodes and then hit
/// Save once. Before Task 1's <c>PermissionStamp</c> concurrency check can mean anything, the write
/// underneath it has to be all-or-nothing: a partial save (some nodes updated, some not) is a worse
/// outcome than either completing or refusing, because nothing afterwards can tell which half landed.
/// These tests prove that <c>SaveManyAsync</c> actually delivers that guarantee.
/// </remarks>
public sealed class AdvancedPermissionRepositoryBatchTests : IAsyncLifetime
{
    /// <summary>
    /// The open SQLite connection backing the in-memory database for the current test instance.
    /// </summary>
    private SqliteConnection _connection = null!;

    /// <summary>
    /// The repository under test, wired to <see cref="_connection"/> via a
    /// <see cref="SingleConnectionDbContextFactory"/>.
    /// </summary>
    private AdvancedPermissionRepository _repository = null!;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        // Keep a single connection open so the in-memory SQLite database persists
        // for the lifetime of the test.
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AdvancedPermissionsDbContext>()
            .UseSqlite(_connection)
            .Options;

        // Create the schema using EF Core's model, including the unique index that Test B relies on.
        await using var db = new AdvancedPermissionsDbContext(options);
        await db.Database.EnsureCreatedAsync();

        // Build a simple factory that reuses the same connection
        var factory = new SingleConnectionDbContextFactory(options);
        _repository = new AdvancedPermissionRepository(factory);
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

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

        await _repository.SaveManyAsync(
        [
            (node1, role, new[] { (AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants, false) }),
            (node2, role, new[] { (AdvancedPermissionsConstants.VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeOnly, false) }),
        ]);

        var node1Results = await _repository.GetByNodeAndRoleAsync(node1, role);
        var node2Results = await _repository.GetByNodeAndRoleAsync(node2, role);

        Assert.Single(node1Results);
        Assert.Equal(AdvancedPermissionsConstants.VerbRead, node1Results[0].Verb);
        Assert.Equal(PermissionState.Allow, node1Results[0].State);

        Assert.Single(node2Results);
        Assert.Equal(AdvancedPermissionsConstants.VerbDelete, node2Results[0].Verb);
        Assert.Equal(PermissionState.Deny, node2Results[0].State);
    }

    /// <summary>
    /// Proves atomicity: when the second pair in a batch fails at the database, the first pair
    /// writes nothing either, even though it was staged and would otherwise have succeeded on its own.
    /// </summary>
    /// <remarks>
    /// The failure is engineered against a real database constraint rather than simulated: the second
    /// pair's entries contain two rows sharing the same (NodeKey, RoleAlias, Verb, Scope) tuple, which
    /// collides with the <c>IX_AdvancedPermission_Unique</c> index defined in
    /// <see cref="AdvancedPermissionsDbContext"/>. SQLite enforces unique indexes (unlike, say, the
    /// <c>HasMaxLength(255)</c> constraint on the same columns, which SQLite's dynamic typing does not
    /// enforce), so this only throws once EF Core flushes the pending inserts via <c>SaveChangesAsync</c>
    /// — after both pairs, including the valid first one, have already been staged on the same
    /// <see cref="Microsoft.EntityFrameworkCore.DbContext"/>. A single flush for the whole batch is what
    /// makes the first pair's write roll back too: if <c>SaveManyAsync</c> instead called
    /// <c>SaveChangesAsync</c> once per pair, the first pair would have committed before the second one
    /// ever ran, and this test would fail — which is exactly the failure mode it exists to catch.
    /// </remarks>
    [Fact]
    public async Task SaveManyAsync_FailureMidway_WritesNothing()
    {
        var node1 = Guid.NewGuid();
        var node2 = Guid.NewGuid();
        const string role = "editors";

        var batch = new (Guid NodeKey, string RoleAlias, IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries)[]
        {
            (node1, role, new[]
            {
                (AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.ThisNodeOnly, false),
            }),
            (node2, role, new[]
            {
                // Two rows with the same NodeKey+RoleAlias+Verb+Scope: violates IX_AdvancedPermission_Unique.
                (AdvancedPermissionsConstants.VerbDelete, PermissionState.Allow, PermissionScope.ThisNodeOnly, false),
                (AdvancedPermissionsConstants.VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeOnly, false),
            }),
        };

        await Assert.ThrowsAsync<DbUpdateException>(() => _repository.SaveManyAsync(batch));

        var node1Results = await _repository.GetByNodeAndRoleAsync(node1, role);
        Assert.Empty(node1Results);
    }

    /// <summary>
    /// Proves the explicit transaction is load-bearing, not decorative: the per-pair deletes run
    /// immediately while the inserts stay staged, so a failure midway would leave a pair's previous
    /// entries deleted and nothing written in their place unless the transaction rolls the deletes back.
    /// </summary>
    /// <remarks>
    /// <c>SaveManyAsync_FailureMidway_WritesNothing</c> alone cannot catch a missing transaction: its
    /// first pair has nothing stored beforehand, so "wrote nothing" holds whether or not the batch is
    /// atomic. Deleting <c>BeginTransactionAsync</c> from the repository leaves that test green. This
    /// test seeds the first pair with an entry, so the immediate <c>ExecuteDeleteAsync</c> has
    /// something to destroy; without the transaction the seeded entry is gone after the failure, and
    /// with it the entry survives untouched. Do not weaken this test back to an empty first pair.
    /// </remarks>
    [Fact]
    public async Task SaveManyAsync_FailureMidway_LeavesPreviouslyStoredEntriesIntact()
    {
        var node1 = Guid.NewGuid();
        var node2 = Guid.NewGuid();
        const string role = "editors";

        await _repository.SaveAsync(node1, role,
        [
            (AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.ThisNodeOnly, false),
        ]);

        var batch = new (Guid NodeKey, string RoleAlias, IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries)[]
        {
            // Replaces the seeded entry: its delete executes immediately, its insert stays staged.
            (node1, role, new[]
            {
                (AdvancedPermissionsConstants.VerbRead, PermissionState.Deny, PermissionScope.ThisNodeOnly, false),
            }),
            (node2, role, new[]
            {
                // Two rows with the same NodeKey+RoleAlias+Verb+Scope: violates IX_AdvancedPermission_Unique.
                (AdvancedPermissionsConstants.VerbDelete, PermissionState.Allow, PermissionScope.ThisNodeOnly, false),
                (AdvancedPermissionsConstants.VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeOnly, false),
            }),
        };

        await Assert.ThrowsAsync<DbUpdateException>(() => _repository.SaveManyAsync(batch));

        var survivors = await _repository.GetByNodeAndRoleAsync(node1, role);
        Assert.Single(survivors);
        Assert.Equal(AdvancedPermissionsConstants.VerbRead, survivors[0].Verb);
        Assert.Equal(PermissionState.Allow, survivors[0].State);
    }

    /// <summary>
    /// Proves the ordinary save: replacing an existing entry with the same verb and scope but a
    /// different state (flipping Allow to Deny) is the everyday shape of a Permissions Editor save,
    /// and it collides with <c>IX_AdvancedPermission_Unique</c> unless the delete for the old row
    /// reaches the database before the insert of the new one. This case was previously uncovered.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_ReplacesEntryWithSameVerbAndScope_Succeeds()
    {
        var nodeKey = Guid.NewGuid();
        const string role = "editors";

        await _repository.SaveAsync(nodeKey, role,
        [
            (AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.ThisNodeOnly, false),
        ]);

        await _repository.SaveManyAsync(
        [
            (nodeKey, role, new[] { (AdvancedPermissionsConstants.VerbRead, PermissionState.Deny, PermissionScope.ThisNodeOnly, false) }),
        ]);

        var results = await _repository.GetByNodeAndRoleAsync(nodeKey, role);

        Assert.Single(results);
        Assert.Equal(PermissionState.Deny, results[0].State);
    }

    /// <summary>
    /// Proves the realistic multi-verb save: a pair whose replacement entries keep one verb's entry
    /// unchanged (same verb, scope and state) while flipping another verb's state. Both rows share the
    /// same node+role, so the unchanged entry's delete-then-reinsert has to succeed alongside the
    /// flipped entry's delete-then-reinsert within the same pair. This case was previously uncovered.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_MultiVerbPair_KeepsIdenticalEntryAndFlipsAnother()
    {
        var nodeKey = Guid.NewGuid();
        const string role = "editors";

        await _repository.SaveAsync(nodeKey, role,
        [
            (AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.ThisNodeOnly, false),
            (AdvancedPermissionsConstants.VerbDelete, PermissionState.Allow, PermissionScope.ThisNodeOnly, false),
        ]);

        await _repository.SaveManyAsync(
        [
            (nodeKey, role, new[]
            {
                (AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.ThisNodeOnly, false),
                (AdvancedPermissionsConstants.VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeOnly, false),
            }),
        ]);

        var results = await _repository.GetByNodeAndRoleAsync(nodeKey, role);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Verb == AdvancedPermissionsConstants.VerbRead && r.State == PermissionState.Allow);
        Assert.Contains(results, r => r.Verb == AdvancedPermissionsConstants.VerbDelete && r.State == PermissionState.Deny);
    }

    /// <summary>
    /// Proves the upfront guard: two pairs in one batch for the same (NodeKey, RoleAlias) throw
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

        var batch = new (Guid NodeKey, string RoleAlias, IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries)[]
        {
            (nodeKey, role, new[] { (AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.ThisNodeOnly, false) }),
            (nodeKey, role, new[] { (AdvancedPermissionsConstants.VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeOnly, false) }),
        };

        await Assert.ThrowsAsync<ArgumentException>(() => _repository.SaveManyAsync(batch));

        // No database work should have happened before the guard fired.
        var results = await _repository.GetByNodeAndRoleAsync(nodeKey, role);
        Assert.Empty(results);
    }

    /// <summary>
    /// Pins the invariant the batch save endpoint depends on: the stamp computed from what a client
    /// submitted must equal the stamp a subsequent read of the same entries computes. If this ever
    /// drifts — for example because a future change trims whitespace or normalises casing on the way
    /// into storage — the client's very next save carries a stamp that no longer matches what the
    /// server reads back, and every second save fails with a spurious 409: a conflict dialog about a
    /// change nobody made. That trains people to click "Overwrite anyway" until the concurrency check
    /// is worse than useless. Anyone who later adds trimming or case-normalisation to the repository
    /// needs this test to stop them.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_StampOfSubmittedEntries_MatchesStampOfStoredEntries()
    {
        var nodeKey = Guid.NewGuid();
        const string role = "editors";

        // A realistic submission: several verbs, a non-default scope, a priority override, and
        // state/scope spelled with the mixed casing a real client's JSON payload sends rather than
        // the exact casing PermissionState/PermissionScope would round-trip through ToString().
        var submitted = new (string Verb, string State, string Scope, bool IsPriorityOverride)[]
        {
            (AdvancedPermissionsConstants.VerbRead, "allow", "ThisNodeAndDescendants", false),
            (AdvancedPermissionsConstants.VerbDelete, "Deny", "thisnodeonly", true),
            (AdvancedPermissionsConstants.VerbPublish, "ALLOW", "DescendantsOnly", false),
        };

        var toSave = submitted
            .Select(e => (
                e.Verb,
                Enum.Parse<PermissionState>(e.State, ignoreCase: true),
                Enum.Parse<PermissionScope>(e.Scope, ignoreCase: true),
                e.IsPriorityOverride))
            .ToArray();

        await _repository.SaveManyAsync([(nodeKey, role, toSave)]);

        var stored = await _repository.GetByNodeAndRoleAsync(nodeKey, role);

        Assert.Equal(
            PermissionStamp.ComputeFromNames(submitted),
            PermissionStamp.Compute(stored));
    }
}
