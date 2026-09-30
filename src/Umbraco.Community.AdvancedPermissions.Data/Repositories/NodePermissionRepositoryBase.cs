using System.Data;
using Microsoft.EntityFrameworkCore;
using Umbraco.Community.AdvancedPermissions.Core.Concurrency;
using Umbraco.Community.AdvancedPermissions.Core.Exceptions;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;
using Umbraco.Community.AdvancedPermissions.Data.Context;
using Umbraco.Community.AdvancedPermissions.Data.Entities;

namespace Umbraco.Community.AdvancedPermissions.Data.Repositories;

/// <summary>
/// Entity Framework Core implementation of <see cref="INodePermissionRepository"/> shared by every
/// node-based permission target. Uses a factory to create short-lived DbContext instances, making it
/// safe for use in singleton services, and operates over <c>DbSet&lt;TEntity&gt;</c> so the same query
/// and save logic serves any table whose entity implements <see cref="INodePermissionEntity"/>.
/// </summary>
/// <typeparam name="TEntity">
/// The backing entity type (e.g. <see cref="AdvancedPermissionEntity"/> or
/// <see cref="ElementPermissionEntity"/>). Must implement <see cref="INodePermissionEntity"/> so its
/// node-key/role/verb/state/scope columns can be queried generically.
/// </typeparam>
/// <param name="dbContextFactory">
/// The factory used to create <see cref="AdvancedPermissionsDbContext"/> instances.
/// </param>
public abstract class NodePermissionRepositoryBase<TEntity>(
    IDbContextFactory<AdvancedPermissionsDbContext> dbContextFactory)
    : INodePermissionRepository
    where TEntity : class, INodePermissionEntity, new()
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<AdvancedPermissionEntry>> GetByNodeAndRoleAsync(
        Guid nodeKey,
        string roleAlias,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entities = await db.Set<TEntity>()
            .Where(p => p.NodeKey == nodeKey
                     && p.RoleAlias == roleAlias)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return entities.ConvertAll(MapToDomain);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AdvancedPermissionEntry>> GetByNodeAsync(
        Guid nodeKey,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entities = await db.Set<TEntity>()
            .Where(p => p.NodeKey == nodeKey)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return entities.ConvertAll(MapToDomain);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AdvancedPermissionEntry>> GetByRoleAsync(
        string roleAlias,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entities = await db.Set<TEntity>()
            .Where(p => p.RoleAlias == roleAlias)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return entities.ConvertAll(MapToDomain);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AdvancedPermissionEntry>> GetByNodesAsync(
        IEnumerable<Guid> nodeKeys,
        CancellationToken cancellationToken = default)
    {
        var keyList = nodeKeys.ToList();

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entities = await db.Set<TEntity>()
            .Where(p => keyList.Contains(p.NodeKey))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return entities.ConvertAll(MapToDomain);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AdvancedPermissionEntry>> GetByNodesAndRoleAsync(
        IEnumerable<Guid> nodeKeys,
        string roleAlias,
        CancellationToken cancellationToken = default)
    {
        var keyList = nodeKeys.ToList();

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entities = await db.Set<TEntity>()
            .Where(p => p.RoleAlias == roleAlias && keyList.Contains(p.NodeKey))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return entities.ConvertAll(MapToDomain);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AdvancedPermissionEntry>> GetByRolesAndNodesAsync(
        IEnumerable<string> roleAliases,
        IEnumerable<Guid> nodeKeys,
        CancellationToken cancellationToken = default)
    {
        var roleList = roleAliases.ToList();
        var nodeList = nodeKeys.ToList();

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entities = await db.Set<TEntity>()
            .Where(p => roleList.Contains(p.RoleAlias) && nodeList.Contains(p.NodeKey))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return entities.ConvertAll(MapToDomain);
    }

    /// <summary>
    /// Saves the entries for a node and user group with no concurrency check, by delegating to the
    /// stamped overload with a <see langword="null"/> stamp.
    /// </summary>
    /// <param name="nodeKey">The node key.</param>
    /// <param name="roleAlias">The user group alias.</param>
    /// <param name="entries">The replacement entries.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    // Not marked [Obsolete] here: this class is not the published contract, and the obsolete warning
    // fires on the interface member, which is where every caller is steered away from it.
    // Implementing an obsolete interface member does not itself raise a warning.
    public Task SaveAsync(
        Guid nodeKey,
        string roleAlias,
        IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> entries,
        CancellationToken cancellationToken = default) =>
        SaveAsync(nodeKey, roleAlias, entries, (string?)null, cancellationToken);

    /// <summary>
    /// Saves the entries for a node and user group, refusing the write if the stored entries no
    /// longer match <paramref name="expectedStamp"/>. This is the real implementation of the
    /// interface's stamped overload - it overrides the interface's default implementation, which
    /// would ignore the stamp.
    /// </summary>
    /// <param name="nodeKey">The node key.</param>
    /// <param name="roleAlias">The user group alias.</param>
    /// <param name="entries">The replacement entries.</param>
    /// <param name="expectedStamp">The stamp the writer read, or <see langword="null"/> to skip the check.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    public Task SaveAsync(
        Guid nodeKey,
        string roleAlias,
        IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> entries,
        string? expectedStamp,
        CancellationToken cancellationToken = default) =>
        // A single-pair batch, so there is exactly one copy of the delete-then-insert logic and it
        // is always atomic. This used to delete and then insert as two separate operations outside
        // any transaction: an insert that failed after the delete succeeded left the pair's
        // permissions wiped - not stale, gone.
        SaveManyAsync([(nodeKey, roleAlias, entries, expectedStamp)], cancellationToken);

    /// <inheritdoc />
    public async Task SaveManyAsync(
        IEnumerable<(Guid NodeKey, string RoleAlias, IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries, string? ExpectedStamp)> batch,
        CancellationToken cancellationToken = default)
    {
        var pairs = batch.ToList();

        // Guard against duplicate (NodeKey, RoleAlias) pairs before touching the database. Each
        // pair's delete runs immediately (see below) while its inserts stay staged, so a second
        // pair for the same node+role would not see the first pair's unflushed inserts: the two
        // sets would either silently merge (non-overlapping verbs) or collide on the unique index
        // with a confusing exception (overlapping verbs). A well-behaved caller groups by node
        // first, so this should never fire in practice — which is exactly why it must be loud.
        var seenPairs = new HashSet<(Guid NodeKey, string RoleAlias)>();
        foreach (var pair in pairs)
        {
            if (!seenPairs.Add((pair.NodeKey, pair.RoleAlias)))
            {
                throw new ArgumentException(
                    $"Batch contains more than one entry for node '{pair.NodeKey}' and role '{pair.RoleAlias}'. " +
                    "Each node+role pair must appear at most once in a single SaveManyAsync call.",
                    nameof(batch));
            }
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        // ISOLATION: this is what makes the stamp check atomic with the write. A transaction alone
        // is not enough. Under a provider's default isolation another transaction can still commit
        // between the read of the stored entries below and the delete that follows it, so "read,
        // compare, then write" would remain two steps with a gap between them, and two saves
        // carrying the same expected stamp could both get through. Serializable closes the gap.
        // What it does depends on the provider:
        //
        // * SQLite (verified: the repository concurrency tests run concurrent writers against a
        //   file database). There is one writer at a time, database-wide, and Microsoft.Data.Sqlite
        //   starts every transaction except ReadUncommitted with BEGIN IMMEDIATE, which takes that
        //   write lock up front - another connection's write is refused from the moment BEGIN
        //   returns, before this transaction has touched anything. A second saver therefore waits at
        //   BEGIN (up to the connection's command timeout, 30 s by default), then reads what the
        //   first left behind and, if it carried the same stamp, is refused. Asking for Serializable
        //   costs nothing extra here; it states the intent and would not silently weaken if a
        //   future provider default did.
        //
        // * SQL Server (from documented behaviour; NOT exercised by this repository's tests, which
        //   run on SQLite only). SERIALIZABLE takes key-range locks on what a query reads and holds
        //   them until commit. The read below filters on NodeKey and RoleAlias, a prefix of the
        //   unique index on these tables, so the range locked is exactly this pair's - including the
        //   gap where a row would be, which is what protects a first-ever save (an empty range) as
        //   well as an update. Another transaction cannot insert, change or delete within that range
        //   until this one commits.
        //
        // TRADE-OFF: serializable can deadlock. On SQL Server, two transactions that both read the
        // same range (shared range locks) and then both try to delete and insert in it (conflicting
        // locks) each wait on the other, and SQL Server kills one as the deadlock victim (error
        // 1205). That is accepted here because this is an admin permissions screen where two people
        // saving the same node for the same user group in the same instant is close to never, and
        // because the failure is loud and safe: the victim's transaction rolls back, so nothing is
        // written, and it surfaces as a retryable error - not as one save silently overwriting the
        // other, which is what weaker isolation risks. If contention ever proved real, the remedy
        // is to queue rather than deadlock: take the lock up front with an UPDLOCK, HOLDLOCK read,
        // which EF Core can only express as raw SQL.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        // Check every pair before writing any, so the exception lists all the conflicts rather than
        // just the first one hit, and so a refused batch has written nothing when it is thrown. A
        // null stamp skips the check for that pair (a forced save, or an older client) - and skips
        // the read too, so an unchecked save issues no SELECT at all.
        var conflicts = new List<PermissionConflict>();
        foreach (var (nodeKey, roleAlias, _, expectedStamp) in pairs)
        {
            if (expectedStamp is null)
            {
                continue;
            }

            var stored = (await db.Set<TEntity>()
                .Where(p => p.NodeKey == nodeKey && p.RoleAlias == roleAlias)
                .AsNoTracking()
                .ToListAsync(cancellationToken))
                .ConvertAll(MapToDomain);
            var currentStamp = PermissionStamp.Compute(stored);

            if (!string.Equals(currentStamp, expectedStamp, StringComparison.Ordinal))
            {
                conflicts.Add(new PermissionConflict(nodeKey, roleAlias, stored, currentStamp));
            }
        }

        if (conflicts.Count > 0)
        {
            // Leaving the method disposes the transaction, which rolls it back. Nothing has been
            // written yet in any case: every pair is checked before the first delete below.
            throw new PermissionConcurrencyException(conflicts);
        }

        foreach (var (nodeKey, roleAlias, entries, _) in pairs)
        {
            await ReplacePairAsync(db, nodeKey, roleAlias, entries, cancellationToken);
        }

        // The deletes above already executed immediately against the database; the inserts are
        // still staged on the change tracker. That split is exactly why the explicit transaction
        // is load-bearing rather than belt-and-braces: it is the only thing making each pair's
        // immediate delete and deferred insert — and every pair in the batch — atomic together.
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Replaces one node+role pair's entries on the given context: deletes the existing rows with a
    /// predicate <c>DELETE</c> that runs immediately, then stages the replacement rows for the caller
    /// to flush with <c>SaveChangesAsync</c>. The single place the replace semantics live, so
    /// <see cref="SaveManyAsync"/> (and <c>SaveAsync</c> through it) cannot drift.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The delete must be <c>ExecuteDeleteAsync</c> (a predicate delete that reaches the database
    /// straight away), not rows loaded and removed on the change tracker. The table has a unique index
    /// on node, user group, verb and scope, so the everyday editor save — replacing an entry with the
    /// same verb and scope but a different state — collides unless the delete provably precedes the
    /// insert, and EF Core's command ordering for tracked removal has varied across versions. On the
    /// EF version in use tracked removal happens to order correctly, so no test of the outcome can
    /// tell the two apart; the tests instead pin the mechanism, by asserting on the SQL issued
    /// (see <c>Replace_UsesPredicateDelete_NotTrackedRemoval</c> in the batch test base).
    /// </para>
    /// <para>
    /// The caller owns the transaction: the delete runs immediately while the inserts stay staged, so
    /// callers replacing more than one pair must wrap the whole sequence in a transaction to make it
    /// atomic.
    /// </para>
    /// </remarks>
    /// <param name="db">The context to delete and stage on.</param>
    /// <param name="nodeKey">The node the entries belong to.</param>
    /// <param name="roleAlias">The user group alias the entries belong to.</param>
    /// <param name="entries">The replacement entries. An empty sequence leaves the pair with no rows.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>A task that completes once the delete has executed and the inserts are staged.</returns>
    private static async Task ReplacePairAsync(
        AdvancedPermissionsDbContext db,
        Guid nodeKey,
        string roleAlias,
        IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> entries,
        CancellationToken cancellationToken)
    {
        await db.Set<TEntity>()
            .Where(p => p.NodeKey == nodeKey && p.RoleAlias == roleAlias)
            .ExecuteDeleteAsync(cancellationToken);

        foreach (var (verb, state, scope, isPriorityOverride) in entries)
        {
            db.Set<TEntity>().Add(new TEntity
            {
                Id = Guid.NewGuid(),
                NodeKey = nodeKey,
                RoleAlias = roleAlias,
                Verb = verb,
                State = state,
                Scope = scope,
                IsPriorityOverride = isPriorityOverride,
            });
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(
        Guid nodeKey,
        string roleAlias,
        string verb,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        await db.Set<TEntity>()
            .Where(p => p.NodeKey == nodeKey
                     && p.RoleAlias == roleAlias
                     && p.Verb == verb)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAllForNodeAsync(
        Guid nodeKey,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        await db.Set<TEntity>()
            .Where(p => p.NodeKey == nodeKey)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAllForRoleAsync(
        string roleAlias,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        await db.Set<TEntity>()
            .Where(p => p.RoleAlias == roleAlias)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>
    /// Maps a node-permission entity to the shared domain model <see cref="AdvancedPermissionEntry"/>.
    /// </summary>
    /// <param name="entity">The entity to map.</param>
    /// <returns>The corresponding domain model.</returns>
    private static AdvancedPermissionEntry MapToDomain(TEntity entity) =>
        new(entity.Id, entity.NodeKey, entity.RoleAlias, entity.Verb, entity.State, entity.Scope, entity.IsPriorityOverride);
}
