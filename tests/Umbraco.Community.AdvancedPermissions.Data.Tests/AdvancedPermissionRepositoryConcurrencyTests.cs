using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Umbraco.Community.AdvancedPermissions.Core.Concurrency;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Exceptions;
using Umbraco.Community.AdvancedPermissions.Core.Models;
using Umbraco.Community.AdvancedPermissions.Data.Context;
using Umbraco.Community.AdvancedPermissions.Data.Repositories;

namespace Umbraco.Community.AdvancedPermissions.Data.Tests;

/// <summary>
/// Tests that <see cref="AdvancedPermissionRepository"/> makes the stamp check and the write one
/// atomic operation: it refuses a stale write by throwing <see cref="PermissionConcurrencyException"/>
/// from inside the write transaction, and writes nothing when it does.
/// </summary>
/// <remarks>
/// <para>
/// The race this exists to close cannot be reproduced deterministically in-process, so the
/// <em>mechanism</em> is tested instead: the repository compares the stored entries with the
/// expected stamp itself, and refuses on a mismatch. A separate test then runs genuinely concurrent
/// writers against a file-backed database, each on its own connection, and asserts that of several
/// saves carrying the same stamp exactly one wins.
/// </para>
/// <para>
/// SQLite only. What these tests prove about isolation is what SQLite does — it serialises writers.
/// They say nothing about SQL Server's range locks, which are documented on the repository and were
/// not exercised here.
/// </para>
/// </remarks>
public sealed class AdvancedPermissionRepositoryConcurrencyTests : IAsyncLifetime
{
    /// <summary>The user group every test saves for.</summary>
    private const string Role = "editors";

    /// <summary>The in-memory database backing the repository under test.</summary>
    private InMemoryPermissionDatabase _database = null!;

    /// <summary>The repository under test.</summary>
    private AdvancedPermissionRepository _repository = null!;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        _database = await InMemoryPermissionDatabase.CreateAsync();
        _repository = new AdvancedPermissionRepository(_database.Factory);
    }

    /// <inheritdoc />
    public async Task DisposeAsync() => await _database.DisposeAsync();

    /// <summary>Builds one replacement entry, at the scope every test here uses.</summary>
    /// <param name="verb">The verb.</param>
    /// <param name="state">The state.</param>
    /// <returns>The entry tuple in the shape the repository accepts.</returns>
    private static (string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride) Entry(
        string verb, PermissionState state) =>
        (verb, state, PermissionScope.ThisNodeOnly, false);

    /// <summary>Reads the stamp of what is currently stored for a pair — what a client would have been handed.</summary>
    /// <param name="nodeKey">The node key.</param>
    /// <returns>The stamp.</returns>
    private async Task<string> StoredStampAsync(Guid nodeKey) =>
        PermissionStamp.Compute(await _repository.GetByNodeAndRoleAsync(nodeKey, Role));

    /// <summary>
    /// The core mechanism: when a pair's stored entries no longer match the expected stamp, the
    /// write throws and writes nothing — not for that pair, and not for any other pair in the batch,
    /// including one whose own stamp was perfectly fresh.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_StoredEntriesNoLongerMatchExpectedStamp_ThrowsAndWritesNothing()
    {
        var stale = Guid.NewGuid();
        var fresh = Guid.NewGuid();
        await _repository.SaveAsync(stale, Role, [Entry(AdvancedPermissionsConstants.VerbRead, PermissionState.Allow)]);
        var staleStamp = await StoredStampAsync(stale);

        // Somebody else changes the stale pair after the client read it.
        await _repository.SaveAsync(stale, Role, [Entry(AdvancedPermissionsConstants.VerbRead, PermissionState.Deny)]);
        var freshStamp = await StoredStampAsync(fresh);

        await Assert.ThrowsAsync<PermissionConcurrencyException>(() => _repository.SaveManyAsync(
        [
            (fresh, Role, [Entry(AdvancedPermissionsConstants.VerbDelete, PermissionState.Allow)], freshStamp),
            (stale, Role, [Entry(AdvancedPermissionsConstants.VerbRead, PermissionState.Allow)], staleStamp),
        ]));

        var staleNow = await _repository.GetByNodeAndRoleAsync(stale, Role);
        Assert.Single(staleNow);
        Assert.Equal(PermissionState.Deny, staleNow[0].State);
        Assert.Empty(await _repository.GetByNodeAndRoleAsync(fresh, Role));
    }

    /// <summary>
    /// The stamp is the write's own precondition, so two saves carrying the same expected stamp
    /// cannot both land: the first changes the stored entries, and the second — arriving with the
    /// stamp the first has just made stale — is refused. This is the sequential shape of the race;
    /// the concurrent shape is covered by a separate test.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_TwoSavesCarryingTheSameStamp_SecondIsRefused()
    {
        var nodeKey = Guid.NewGuid();
        await _repository.SaveAsync(nodeKey, Role, [Entry(AdvancedPermissionsConstants.VerbRead, PermissionState.Allow)]);
        var sharedStamp = await StoredStampAsync(nodeKey);

        await _repository.SaveManyAsync(
            [(nodeKey, Role, [Entry(AdvancedPermissionsConstants.VerbRead, PermissionState.Deny)], sharedStamp)]);

        await Assert.ThrowsAsync<PermissionConcurrencyException>(() => _repository.SaveManyAsync(
            [(nodeKey, Role, [Entry(AdvancedPermissionsConstants.VerbDelete, PermissionState.Deny)], sharedStamp)]));

        var stored = await _repository.GetByNodeAndRoleAsync(nodeKey, Role);
        Assert.Single(stored);
        Assert.Equal(AdvancedPermissionsConstants.VerbRead, stored[0].Verb);
        Assert.Equal(PermissionState.Deny, stored[0].State);
    }

    /// <summary>A stamp that still matches lets the write through.</summary>
    [Fact]
    public async Task SaveManyAsync_MatchingStamp_Writes()
    {
        var nodeKey = Guid.NewGuid();
        await _repository.SaveAsync(nodeKey, Role, [Entry(AdvancedPermissionsConstants.VerbRead, PermissionState.Allow)]);

        await _repository.SaveManyAsync(
            [(nodeKey, Role, [Entry(AdvancedPermissionsConstants.VerbRead, PermissionState.Deny)], await StoredStampAsync(nodeKey))]);

        var stored = await _repository.GetByNodeAndRoleAsync(nodeKey, Role);
        Assert.Equal(PermissionState.Deny, Assert.Single(stored).State);
    }

    /// <summary>
    /// A first-ever save carries the empty stamp and must not be mistaken for a conflict: nothing is
    /// stored, so the stored entries hash to exactly that value.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_NothingStoredAndEmptyStamp_Writes()
    {
        var nodeKey = Guid.NewGuid();

        await _repository.SaveManyAsync(
            [(nodeKey, Role, [Entry(AdvancedPermissionsConstants.VerbRead, PermissionState.Allow)], PermissionStamp.Compute([]))]);

        Assert.Single(await _repository.GetByNodeAndRoleAsync(nodeKey, Role));
    }

    /// <summary>
    /// A null expected stamp skips the check — how a forced save and an older client behave — so the
    /// write overwrites whatever is stored, however different it is.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_NullStamp_SkipsTheCheckAndOverwrites()
    {
        var nodeKey = Guid.NewGuid();
        await _repository.SaveAsync(nodeKey, Role, [Entry(AdvancedPermissionsConstants.VerbRead, PermissionState.Deny)]);

        await _repository.SaveManyAsync(
            [(nodeKey, Role, [Entry(AdvancedPermissionsConstants.VerbDelete, PermissionState.Allow)], null)]);

        var stored = await _repository.GetByNodeAndRoleAsync(nodeKey, Role);
        Assert.Equal(AdvancedPermissionsConstants.VerbDelete, Assert.Single(stored).Verb);
    }

    /// <summary>
    /// The exception carries what the controller needs to build the 409: for every conflicted pair,
    /// the node, user group, the entries stored right now and the stamp of them. Pairs that did not
    /// conflict are not listed, and the conflicts come in batch order.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_Conflict_ExceptionCarriesCurrentEntriesAndStampForEveryConflictedPair()
    {
        var first = Guid.NewGuid();
        var clean = Guid.NewGuid();
        var second = Guid.NewGuid();
        await _repository.SaveAsync(first, Role, [Entry(AdvancedPermissionsConstants.VerbRead, PermissionState.Deny)]);
        await _repository.SaveAsync(second, Role,
        [
            Entry(AdvancedPermissionsConstants.VerbRead, PermissionState.Allow),
            Entry(AdvancedPermissionsConstants.VerbDelete, PermissionState.Deny),
        ]);
        var cleanStamp = await StoredStampAsync(clean);

        var thrown = await Assert.ThrowsAsync<PermissionConcurrencyException>(() => _repository.SaveManyAsync(
        [
            (first, Role, [], "stale-1"),
            (clean, Role, [], cleanStamp),
            (second, Role, [], "stale-2"),
        ]));

        Assert.Equal(2, thrown.Conflicts.Count);

        var firstConflict = thrown.Conflicts[0];
        Assert.Equal(first, firstConflict.NodeKey);
        Assert.Equal(Role, firstConflict.RoleAlias);
        Assert.Equal(PermissionState.Deny, Assert.Single(firstConflict.CurrentEntries).State);
        Assert.Equal(await StoredStampAsync(first), firstConflict.CurrentStamp);

        var secondConflict = thrown.Conflicts[1];
        Assert.Equal(second, secondConflict.NodeKey);
        Assert.Equal(2, secondConflict.CurrentEntries.Count);
        Assert.Equal(await StoredStampAsync(second), secondConflict.CurrentStamp);
    }

    /// <summary>
    /// A stale stamp against a pair with nothing stored (somebody deleted it) is still a conflict,
    /// reported with an empty entry list and the empty stamp, so the client can show "now empty".
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_StaleStampAgainstNothingStored_ConflictsWithEmptyEntries()
    {
        var nodeKey = Guid.NewGuid();

        var thrown = await Assert.ThrowsAsync<PermissionConcurrencyException>(() => _repository.SaveManyAsync(
            [(nodeKey, Role, [Entry(AdvancedPermissionsConstants.VerbRead, PermissionState.Allow)], "a-stamp-of-something-since-removed")]));

        var conflict = Assert.Single(thrown.Conflicts);
        Assert.Empty(conflict.CurrentEntries);
        Assert.Equal(PermissionStamp.Compute([]), conflict.CurrentStamp);
        Assert.Empty(await _repository.GetByNodeAndRoleAsync(nodeKey, Role));
    }

    /// <summary>The single-pair save carries the same check: a stale stamp is refused and nothing changes.</summary>
    [Fact]
    public async Task SaveAsync_StaleStamp_ThrowsAndLeavesStoredEntriesUntouched()
    {
        var nodeKey = Guid.NewGuid();
        await _repository.SaveAsync(nodeKey, Role, [Entry(AdvancedPermissionsConstants.VerbRead, PermissionState.Allow)]);

        await Assert.ThrowsAsync<PermissionConcurrencyException>(() => _repository.SaveAsync(
            nodeKey, Role, [Entry(AdvancedPermissionsConstants.VerbRead, PermissionState.Deny)], "stale"));

        Assert.Equal(PermissionState.Allow, Assert.Single(await _repository.GetByNodeAndRoleAsync(nodeKey, Role)).State);
    }

    /// <summary>The single-pair save writes when the stamp still matches.</summary>
    [Fact]
    public async Task SaveAsync_MatchingStamp_Writes()
    {
        var nodeKey = Guid.NewGuid();
        await _repository.SaveAsync(nodeKey, Role, [Entry(AdvancedPermissionsConstants.VerbRead, PermissionState.Allow)]);

        await _repository.SaveAsync(
            nodeKey, Role, [Entry(AdvancedPermissionsConstants.VerbRead, PermissionState.Deny)], await StoredStampAsync(nodeKey));

        Assert.Equal(PermissionState.Deny, Assert.Single(await _repository.GetByNodeAndRoleAsync(nodeKey, Role)).State);
    }

    /// <summary>
    /// <c>SaveAsync</c> used to delete and then insert outside any transaction, so a failing insert
    /// left the pair's permissions wiped — not stale, gone. It now runs as a single-pair batch, so
    /// the delete is rolled back with the failed insert and the previous entries survive.
    /// </summary>
    /// <remarks>
    /// The failure is a real constraint violation: two replacement rows sharing a verb and scope
    /// collide on <c>IX_AdvancedPermission_Unique</c>. Seeding the pair first is what makes the test
    /// able to fail — with nothing stored, "left intact" and "wiped" look the same.
    /// </remarks>
    [Fact]
    public async Task SaveAsync_FailedInsert_LeavesPreviouslyStoredEntriesIntact()
    {
        var nodeKey = Guid.NewGuid();
        await _repository.SaveAsync(nodeKey, Role, [Entry(AdvancedPermissionsConstants.VerbRead, PermissionState.Allow)]);

        await Assert.ThrowsAsync<DbUpdateException>(() => _repository.SaveAsync(
            nodeKey,
            Role,
            [
                Entry(AdvancedPermissionsConstants.VerbDelete, PermissionState.Allow),
                Entry(AdvancedPermissionsConstants.VerbDelete, PermissionState.Deny),
            ]));

        var survivors = await _repository.GetByNodeAndRoleAsync(nodeKey, Role);
        Assert.Equal(AdvancedPermissionsConstants.VerbRead, Assert.Single(survivors).Verb);
        Assert.Equal(PermissionState.Allow, survivors[0].State);
    }

    /// <summary>
    /// The end-to-end race, run for real: several writers, each on its own connection to a
    /// file-backed database, each carrying the same expected stamp. Whatever the interleaving,
    /// exactly one may win; the rest must be refused with the conflict exception. With a check made
    /// before the write and the write as a separate operation, more than one would get through.
    /// </summary>
    /// <remarks>
    /// Each writer writes a different verb, so the outcome is unambiguous: after the round the
    /// stored entries belong to exactly one writer. Repeated over many rounds because a race that
    /// is merely unlikely to lose on any one attempt needs volume to show. SQLite only — it proves
    /// what SQLite's writer serialisation guarantees, not SQL Server's locking.
    /// </remarks>
    [Fact]
    public async Task SaveAsync_ConcurrentWritersSharingOneStamp_ExactlyOneWins()
    {
        var path = Path.Combine(Path.GetTempPath(), $"advperm-race-{Guid.NewGuid():N}.db");
        // The connection-string flavour Umbraco itself uses for SQLite (shared cache, pooled), so
        // the test exercises the locking behaviour a real site has rather than a private-cache one.
        var options = new DbContextOptionsBuilder<AdvancedPermissionsDbContext>()
            .UseSqlite($"Data Source={path};Cache=Shared;Foreign Keys=True;Pooling=True")
            .Options;

        try
        {
            await using (var setup = new AdvancedPermissionsDbContext(options))
            {
                await setup.Database.EnsureCreatedAsync();
            }

            var repository = new AdvancedPermissionRepository(new SingleConnectionDbContextFactory(options));
            string[] verbs =
            [
                AdvancedPermissionsConstants.VerbRead,
                AdvancedPermissionsConstants.VerbDelete,
                AdvancedPermissionsConstants.VerbPublish,
            ];

            for (var round = 0; round < 10; round++)
            {
                var nodeKey = Guid.NewGuid();
                await repository.SaveAsync(nodeKey, Role, [Entry(AdvancedPermissionsConstants.VerbRead, PermissionState.Allow)]);
                var sharedStamp = PermissionStamp.Compute(await repository.GetByNodeAndRoleAsync(nodeKey, Role));

                // Released together, so the writers genuinely overlap rather than queueing up.
                var start = new TaskCompletionSource();
                var writers = verbs
                    .Select(verb => Task.Run(async () =>
                    {
                        await start.Task;
                        try
                        {
                            await repository.SaveAsync(nodeKey, Role, [Entry(verb, PermissionState.Deny)], sharedStamp);
                            return (Won: true, Verb: verb);
                        }
                        catch (PermissionConcurrencyException)
                        {
                            return (Won: false, Verb: verb);
                        }
                    }))
                    .ToList();
                start.SetResult();
                var outcomes = await Task.WhenAll(writers);

                var winner = Assert.Single(outcomes, o => o.Won);
                var stored = await repository.GetByNodeAndRoleAsync(nodeKey, Role);
                Assert.Equal(winner.Verb, Assert.Single(stored).Verb);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }
}
