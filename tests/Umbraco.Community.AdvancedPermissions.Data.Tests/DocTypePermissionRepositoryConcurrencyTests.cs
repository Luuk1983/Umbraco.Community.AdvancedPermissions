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
/// The document-type counterpart of <see cref="AdvancedPermissionRepositoryConcurrencyTests"/>:
/// tests that <see cref="DocTypePermissionRepository"/> checks the expected stamp inside the write
/// transaction and throws <see cref="DocTypePermissionConcurrencyException"/> — writing nothing —
/// when a triple has moved.
/// </summary>
/// <remarks>
/// The unit of identity is the triple of node, user group and document type, so these tests keep two
/// triples that differ only by document type side by side: a check that compared on node and user
/// group alone would treat them as one set.
/// </remarks>
public sealed class DocTypePermissionRepositoryConcurrencyTests : IAsyncLifetime
{
    /// <summary>The user group every test saves for.</summary>
    private const string Role = "editors";

    /// <summary>The in-memory database backing the repository under test.</summary>
    private InMemoryPermissionDatabase _database = null!;

    /// <summary>The repository under test.</summary>
    private DocTypePermissionRepository _repository = null!;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        _database = await InMemoryPermissionDatabase.CreateAsync();
        _repository = new DocTypePermissionRepository(_database.Factory);
    }

    /// <inheritdoc />
    public async Task DisposeAsync() => await _database.DisposeAsync();

    /// <summary>Builds one replacement entry for the create-of-type verb.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The entry tuple in the shape the repository accepts.</returns>
    private static (string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride) Entry(PermissionState state) =>
        (AdvancedPermissionsConstants.VerbCreateOfType, state, PermissionScope.ThisNodeOnly, false);

    /// <summary>Reads the stamp of what is currently stored for a triple.</summary>
    /// <param name="nodeKey">The node key.</param>
    /// <param name="contentTypeKey">The document type key.</param>
    /// <returns>The stamp.</returns>
    private async Task<string> StoredStampAsync(Guid nodeKey, Guid contentTypeKey) =>
        PermissionStamp.ComputeForDocType(
            (await _repository.GetByRoleAndContentTypeAsync(Role, contentTypeKey)).Where(e => e.NodeKey == nodeKey));

    /// <summary>
    /// A stale triple refuses the whole batch and writes nothing, including for the triple whose own
    /// stamp was fresh.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_StoredEntriesNoLongerMatchExpectedStamp_ThrowsAndWritesNothing()
    {
        var nodeKey = Guid.NewGuid();
        var staleType = Guid.NewGuid();
        var freshType = Guid.NewGuid();
        await _repository.SaveAsync(nodeKey, Role, staleType, [Entry(PermissionState.Allow)]);
        var staleStamp = await StoredStampAsync(nodeKey, staleType);
        await _repository.SaveAsync(nodeKey, Role, staleType, [Entry(PermissionState.Deny)]);
        var freshStamp = await StoredStampAsync(nodeKey, freshType);

        await Assert.ThrowsAsync<DocTypePermissionConcurrencyException>(() => _repository.SaveManyAsync(
        [
            (nodeKey, Role, freshType, [Entry(PermissionState.Allow)], freshStamp),
            (nodeKey, Role, staleType, [Entry(PermissionState.Allow)], staleStamp),
        ]));

        Assert.Equal(PermissionState.Deny, Assert.Single(await _repository.GetByRoleAndContentTypeAsync(Role, staleType)).State);
        Assert.Empty(await _repository.GetByRoleAndContentTypeAsync(Role, freshType));
    }

    /// <summary>Two saves carrying the same stamp cannot both land: the second is refused.</summary>
    [Fact]
    public async Task SaveManyAsync_TwoSavesCarryingTheSameStamp_SecondIsRefused()
    {
        var nodeKey = Guid.NewGuid();
        var typeKey = Guid.NewGuid();
        await _repository.SaveAsync(nodeKey, Role, typeKey, [Entry(PermissionState.Allow)]);
        var sharedStamp = await StoredStampAsync(nodeKey, typeKey);

        await _repository.SaveManyAsync([(nodeKey, Role, typeKey, [Entry(PermissionState.Deny)], sharedStamp)]);

        await Assert.ThrowsAsync<DocTypePermissionConcurrencyException>(() =>
            _repository.SaveManyAsync([(nodeKey, Role, typeKey, [Entry(PermissionState.Allow)], sharedStamp)]));

        Assert.Equal(PermissionState.Deny, Assert.Single(await _repository.GetByRoleAndContentTypeAsync(Role, typeKey)).State);
    }

    /// <summary>A stamp that still matches, and the empty stamp on a first-ever save, let the write through.</summary>
    [Fact]
    public async Task SaveManyAsync_MatchingStampAndEmptyStamp_Write()
    {
        var nodeKey = Guid.NewGuid();
        var existingType = Guid.NewGuid();
        var newType = Guid.NewGuid();
        await _repository.SaveAsync(nodeKey, Role, existingType, [Entry(PermissionState.Allow)]);

        await _repository.SaveManyAsync(
        [
            (nodeKey, Role, existingType, [Entry(PermissionState.Deny)], await StoredStampAsync(nodeKey, existingType)),
            (nodeKey, Role, newType, [Entry(PermissionState.Allow)], PermissionStamp.ComputeForDocType([])),
        ]);

        Assert.Equal(PermissionState.Deny, Assert.Single(await _repository.GetByRoleAndContentTypeAsync(Role, existingType)).State);
        Assert.Single(await _repository.GetByRoleAndContentTypeAsync(Role, newType));
    }

    /// <summary>A null expected stamp skips the check and overwrites whatever is stored.</summary>
    [Fact]
    public async Task SaveManyAsync_NullStamp_SkipsTheCheckAndOverwrites()
    {
        var nodeKey = Guid.NewGuid();
        var typeKey = Guid.NewGuid();
        await _repository.SaveAsync(nodeKey, Role, typeKey, [Entry(PermissionState.Deny)]);

        await _repository.SaveManyAsync([(nodeKey, Role, typeKey, [Entry(PermissionState.Allow)], null)]);

        Assert.Equal(PermissionState.Allow, Assert.Single(await _repository.GetByRoleAndContentTypeAsync(Role, typeKey)).State);
    }

    /// <summary>
    /// The exception names every conflicted triple — node, user group, document type — with the
    /// entries stored now and their stamp, and leaves out the triple that did not conflict. Two
    /// conflicted triples share a node and user group and differ only by document type, so a
    /// two-part key would collapse them.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_Conflict_ExceptionCarriesCurrentEntriesAndStampForEveryConflictedTriple()
    {
        var nodeKey = Guid.NewGuid();
        var typeA = Guid.NewGuid();
        var typeB = Guid.NewGuid();
        var typeClean = Guid.NewGuid();
        await _repository.SaveAsync(nodeKey, Role, typeA, [Entry(PermissionState.Deny)]);
        await _repository.SaveAsync(nodeKey, Role, typeB, [Entry(PermissionState.Allow)]);
        var cleanStamp = await StoredStampAsync(nodeKey, typeClean);

        var thrown = await Assert.ThrowsAsync<DocTypePermissionConcurrencyException>(() => _repository.SaveManyAsync(
        [
            (nodeKey, Role, typeA, [], "stale-a"),
            (nodeKey, Role, typeClean, [], cleanStamp),
            (nodeKey, Role, typeB, [], "stale-b"),
        ]));

        Assert.Equal([typeA, typeB], thrown.Conflicts.Select(c => c.ContentTypeKey));
        Assert.All(thrown.Conflicts, c =>
        {
            Assert.Equal(nodeKey, c.NodeKey);
            Assert.Equal(Role, c.RoleAlias);
        });
        Assert.Equal(PermissionState.Deny, Assert.Single(thrown.Conflicts[0].CurrentEntries).State);
        Assert.Equal(await StoredStampAsync(nodeKey, typeA), thrown.Conflicts[0].CurrentStamp);
        Assert.Equal(PermissionState.Allow, Assert.Single(thrown.Conflicts[1].CurrentEntries).State);
        Assert.Equal(await StoredStampAsync(nodeKey, typeB), thrown.Conflicts[1].CurrentStamp);
    }

    /// <summary>
    /// Another node's entries for the same user group and document type must not be read as part of
    /// this triple: only the triple's own rows feed its stamp.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_OtherNodesEntriesForTheSameTypeDoNotAffectTheStamp()
    {
        var nodeA = Guid.NewGuid();
        var nodeB = Guid.NewGuid();
        var typeKey = Guid.NewGuid();
        await _repository.SaveAsync(nodeB, Role, typeKey, [Entry(PermissionState.Deny)]);
        var stampOfEmptyNodeA = PermissionStamp.ComputeForDocType([]);

        await _repository.SaveManyAsync([(nodeA, Role, typeKey, [Entry(PermissionState.Allow)], stampOfEmptyNodeA)]);

        Assert.Equal(2, (await _repository.GetByRoleAndContentTypeAsync(Role, typeKey)).Count);
    }

    /// <summary>The single-triple save carries the same check: a stale stamp is refused and nothing changes.</summary>
    [Fact]
    public async Task SaveAsync_StaleStamp_ThrowsAndLeavesStoredEntriesUntouched()
    {
        var nodeKey = Guid.NewGuid();
        var typeKey = Guid.NewGuid();
        await _repository.SaveAsync(nodeKey, Role, typeKey, [Entry(PermissionState.Allow)]);

        var thrown = await Assert.ThrowsAsync<DocTypePermissionConcurrencyException>(() => _repository.SaveAsync(
            nodeKey, Role, typeKey, [Entry(PermissionState.Deny)], "stale"));

        var conflict = Assert.Single(thrown.Conflicts);
        Assert.Equal((nodeKey, Role, typeKey), (conflict.NodeKey, conflict.RoleAlias, conflict.ContentTypeKey));
        Assert.Equal(PermissionState.Allow, Assert.Single(await _repository.GetByRoleAndContentTypeAsync(Role, typeKey)).State);
    }

    /// <summary>The single-triple save writes when the stamp still matches.</summary>
    [Fact]
    public async Task SaveAsync_MatchingStamp_Writes()
    {
        var nodeKey = Guid.NewGuid();
        var typeKey = Guid.NewGuid();
        await _repository.SaveAsync(nodeKey, Role, typeKey, [Entry(PermissionState.Allow)]);

        await _repository.SaveAsync(
            nodeKey, Role, typeKey, [Entry(PermissionState.Deny)], await StoredStampAsync(nodeKey, typeKey));

        Assert.Equal(PermissionState.Deny, Assert.Single(await _repository.GetByRoleAndContentTypeAsync(Role, typeKey)).State);
    }

    /// <summary>A null stamp on the single-triple save skips the check, so a caller written before stamps existed keeps working.</summary>
    [Fact]
    public async Task SaveAsync_NullStamp_SkipsTheCheckAndOverwrites()
    {
        var nodeKey = Guid.NewGuid();
        var typeKey = Guid.NewGuid();
        await _repository.SaveAsync(nodeKey, Role, typeKey, [Entry(PermissionState.Deny)]);

        await _repository.SaveAsync(nodeKey, Role, typeKey, [Entry(PermissionState.Allow)], null);

        Assert.Equal(PermissionState.Allow, Assert.Single(await _repository.GetByRoleAndContentTypeAsync(Role, typeKey)).State);
    }

    /// <summary>
    /// The single-triple save identifies a triple by node, user group AND document type: a stamp read
    /// for one document type does not vouch for a different document type at the same node.
    /// </summary>
    [Fact]
    public async Task SaveAsync_StampForAnotherContentType_IsRefused()
    {
        var nodeKey = Guid.NewGuid();
        var typeA = Guid.NewGuid();
        var typeB = Guid.NewGuid();
        await _repository.SaveAsync(nodeKey, Role, typeA, [Entry(PermissionState.Allow)]);
        await _repository.SaveAsync(nodeKey, Role, typeB, [Entry(PermissionState.Deny)]);
        var stampOfA = await StoredStampAsync(nodeKey, typeA);

        await Assert.ThrowsAsync<DocTypePermissionConcurrencyException>(() => _repository.SaveAsync(
            nodeKey, Role, typeB, [Entry(PermissionState.Allow)], stampOfA));

        Assert.Equal(PermissionState.Deny, Assert.Single(await _repository.GetByRoleAndContentTypeAsync(Role, typeB)).State);
    }

    /// <summary>
    /// The doc-type <c>SaveAsync</c> used to delete then insert outside any transaction, so a failing
    /// insert left the triple wiped. It now runs as a single-triple batch and the previous entries survive.
    /// </summary>
    [Fact]
    public async Task SaveAsync_FailedInsert_LeavesPreviouslyStoredEntriesIntact()
    {
        var nodeKey = Guid.NewGuid();
        var typeKey = Guid.NewGuid();
        await _repository.SaveAsync(nodeKey, Role, typeKey, [Entry(PermissionState.Allow)]);

        // Two rows sharing verb and scope collide on the unique index.
        await Assert.ThrowsAsync<DbUpdateException>(() => _repository.SaveAsync(
            nodeKey, Role, typeKey, [Entry(PermissionState.Allow), Entry(PermissionState.Deny)]));

        Assert.Equal(PermissionState.Allow, Assert.Single(await _repository.GetByRoleAndContentTypeAsync(Role, typeKey)).State);
    }

    /// <summary>
    /// The end-to-end race for a document-type triple, run for real: several writers, each on its own
    /// connection to a file-backed database, each carrying the same expected stamp. Whatever the
    /// interleaving, exactly one may win; the rest must be refused with the conflict exception.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the document-type counterpart of
    /// <c>AdvancedPermissionRepositoryConcurrencyTests.SaveAsync_ConcurrentWritersSharingOneStamp_ExactlyOneWins</c>,
    /// and it exists because this family had no such test at all. Every other test in this class
    /// proves the <em>mechanism</em> - the repository compares the stored entries with the stamp and
    /// refuses on a mismatch - but runs its writers one after another, so none of them can tell a
    /// write transaction that holds the check and the write together from one that lets another
    /// writer in between them. Weakening <c>DocTypePermissionRepository.SaveManyAsync</c>'s isolation
    /// level from Serializable to ReadUncommitted left the whole suite green; this test is what
    /// fails, with either a second winner or the database refusing a writer that had already read.
    /// </para>
    /// <para>
    /// Each writer writes the same verb under a different scope, so the outcome is unambiguous:
    /// after a round the single stored entry carries the winner's scope. Repeated over many rounds
    /// because a race that is merely unlikely to be lost on any one attempt needs volume to show.
    /// SQLite only - it proves what SQLite's writer serialisation guarantees, not SQL Server's locking.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task SaveManyAsync_ConcurrentWritersSharingOneStamp_ExactlyOneWins()
    {
        var path = Path.Combine(Path.GetTempPath(), $"advperm-doctype-race-{Guid.NewGuid():N}.db");
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

            var repository = new DocTypePermissionRepository(new SingleConnectionDbContextFactory(options));
            PermissionScope[] scopes =
            [
                PermissionScope.ThisNodeOnly,
                PermissionScope.ThisNodeAndDescendants,
                PermissionScope.DescendantsOnly,
            ];

            for (var round = 0; round < 10; round++)
            {
                var nodeKey = Guid.NewGuid();
                var typeKey = Guid.NewGuid();
                await repository.SaveAsync(nodeKey, Role, typeKey, [Entry(PermissionState.Allow)]);
                var sharedStamp = PermissionStamp.ComputeForDocType(
                    (await repository.GetByRoleAndContentTypeAsync(Role, typeKey)).Where(e => e.NodeKey == nodeKey));

                // Released together, so the writers genuinely overlap rather than queueing up.
                var start = new TaskCompletionSource();
                var writers = scopes
                    .Select(scope => Task.Run(async () =>
                    {
                        await start.Task;
                        try
                        {
                            await repository.SaveManyAsync(
                            [
                                (nodeKey, Role, typeKey,
                                    [(AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Deny, scope, false)],
                                    sharedStamp),
                            ]);
                            return (Won: true, Scope: scope);
                        }
                        catch (DocTypePermissionConcurrencyException)
                        {
                            return (Won: false, Scope: scope);
                        }
                    }))
                    .ToList();
                start.SetResult();
                var outcomes = await Task.WhenAll(writers);

                var winner = Assert.Single(outcomes, o => o.Won);
                var stored = (await repository.GetByRoleAndContentTypeAsync(Role, typeKey)).Where(e => e.NodeKey == nodeKey);
                Assert.Equal(winner.Scope, Assert.Single(stored).Scope);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }
}
