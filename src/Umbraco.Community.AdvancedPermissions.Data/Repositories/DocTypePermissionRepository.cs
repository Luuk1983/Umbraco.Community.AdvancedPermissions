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
/// EF Core implementation of <see cref="IDocTypePermissionRepository"/>. Mirrors the structure
/// of <see cref="AdvancedPermissionRepository"/> but with an extra <c>ContentTypeKey</c> dimension
/// on the save and query operations.
/// </summary>
/// <param name="dbContextFactory">
/// The factory used to create <see cref="AdvancedPermissionsDbContext"/> instances.
/// </param>
public sealed class DocTypePermissionRepository(IDbContextFactory<AdvancedPermissionsDbContext> dbContextFactory)
    : IDocTypePermissionRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<DocTypePermissionEntry>> GetByRoleAsync(
        string roleAlias,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entities = await db.DocTypePermissions
            .Where(p => p.RoleAlias == roleAlias)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return entities.ConvertAll(MapToDomain);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocTypePermissionEntry>> GetByRoleAndContentTypeAsync(
        string roleAlias,
        Guid contentTypeKey,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entities = await db.DocTypePermissions
            .Where(p => p.RoleAlias == roleAlias && p.ContentTypeKey == contentTypeKey)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return entities.ConvertAll(MapToDomain);
    }

    /// <summary>
    /// Saves the entries for a node, user group and document type triple with no concurrency check,
    /// by delegating to the stamped overload with a <see langword="null"/> stamp.
    /// </summary>
    /// <param name="nodeKey">The node key.</param>
    /// <param name="roleAlias">The user group alias.</param>
    /// <param name="contentTypeKey">The document type key.</param>
    /// <param name="entries">The replacement entries.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    // Not marked [Obsolete] here: this class is not the published contract, and the obsolete warning
    // fires on the interface member, which is where every caller is steered away from it.
    // Implementing an obsolete interface member does not itself raise a warning.
    public Task SaveAsync(
        Guid nodeKey,
        string roleAlias,
        Guid contentTypeKey,
        IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> entries,
        CancellationToken cancellationToken = default) =>
        SaveAsync(nodeKey, roleAlias, contentTypeKey, entries, (string?)null, cancellationToken);

    /// <summary>
    /// Saves the entries for a node, user group and document type triple, refusing the write if the
    /// stored entries no longer match <paramref name="expectedStamp"/>. This is the real
    /// implementation of the interface's stamped overload - it overrides the interface's default
    /// implementation, which would ignore the stamp.
    /// </summary>
    /// <param name="nodeKey">The node key.</param>
    /// <param name="roleAlias">The user group alias.</param>
    /// <param name="contentTypeKey">The document type key.</param>
    /// <param name="entries">The replacement entries.</param>
    /// <param name="expectedStamp">The stamp the writer read, or <see langword="null"/> to skip the check.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    public Task SaveAsync(
        Guid nodeKey,
        string roleAlias,
        Guid contentTypeKey,
        IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> entries,
        string? expectedStamp,
        CancellationToken cancellationToken = default) =>
        // A single-triple batch, so there is exactly one copy of the delete-then-insert logic and it
        // is always atomic. This used to delete and then insert as two separate operations outside
        // any transaction: an insert that failed after the delete succeeded left the triple's
        // permissions wiped - not stale, gone.
        SaveManyAsync([(nodeKey, roleAlias, contentTypeKey, entries, expectedStamp)], cancellationToken);

    /// <inheritdoc />
    public async Task SaveManyAsync(
        IEnumerable<(Guid NodeKey, string RoleAlias, Guid ContentTypeKey, IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries, string? ExpectedStamp)> batch,
        CancellationToken cancellationToken = default)
    {
        var triples = batch.ToList();

        // Guard against duplicate (NodeKey, RoleAlias, ContentTypeKey) triples before touching the
        // database. Each triple's delete runs immediately (see below) while its inserts stay
        // staged, so a second triple for the same node+role+content-type would not see the first
        // triple's unflushed inserts — mirrors NodePermissionRepositoryBase.SaveManyAsync's guard.
        var seenTriples = new HashSet<(Guid NodeKey, string RoleAlias, Guid ContentTypeKey)>();
        foreach (var triple in triples)
        {
            if (!seenTriples.Add((triple.NodeKey, triple.RoleAlias, triple.ContentTypeKey)))
            {
                throw new ArgumentException(
                    $"Batch contains more than one entry for node '{triple.NodeKey}', role '{triple.RoleAlias}' " +
                    $"and content type '{triple.ContentTypeKey}'. Each triple must appear at most once in a " +
                    "single SaveManyAsync call.",
                    nameof(batch));
            }
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Serializable, so the expected-stamp check below and the delete that follows it are one
        // atomic operation rather than two steps with a gap another writer can slip into. The
        // provider-by-provider reasoning - SQLite's up-front write lock, SQL Server's key-range
        // locks, and the accepted deadlock trade-off - is set out on
        // NodePermissionRepositoryBase.SaveManyAsync and applies here unchanged. The range read
        // below filters on NodeKey, RoleAlias and ContentTypeKey, all of which lead the unique
        // index, so on SQL Server it locks exactly this triple's range.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        // Check every triple before writing any, so the exception lists all the conflicts and a
        // refused batch has written nothing when it is thrown. A null stamp skips the check and
        // the read.
        var conflicts = new List<DocTypePermissionConflict>();
        foreach (var (nodeKey, roleAlias, contentTypeKey, _, expectedStamp) in triples)
        {
            if (expectedStamp is null)
            {
                continue;
            }

            var stored = (await db.DocTypePermissions
                .Where(p => p.NodeKey == nodeKey && p.RoleAlias == roleAlias && p.ContentTypeKey == contentTypeKey)
                .AsNoTracking()
                .ToListAsync(cancellationToken))
                .ConvertAll(MapToDomain);
            var currentStamp = PermissionStamp.ComputeForDocType(stored);

            if (!string.Equals(currentStamp, expectedStamp, StringComparison.Ordinal))
            {
                conflicts.Add(new DocTypePermissionConflict(nodeKey, roleAlias, contentTypeKey, stored, currentStamp));
            }
        }

        if (conflicts.Count > 0)
        {
            // Leaving the method disposes the transaction, which rolls it back. Nothing has been
            // written yet in any case: every triple is checked before the first delete below.
            throw new DocTypePermissionConcurrencyException(conflicts);
        }

        foreach (var (nodeKey, roleAlias, contentTypeKey, entries, _) in triples)
        {
            await ReplaceTripleAsync(db, nodeKey, roleAlias, contentTypeKey, entries, cancellationToken);
        }

        // The deletes above already executed immediately against the database; the inserts are
        // still staged on the change tracker. That split is exactly why the explicit transaction is
        // load-bearing rather than belt-and-braces: it is the only thing making each triple's
        // immediate delete and deferred insert — and every triple in the batch — atomic together.
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Replaces one node+role+content-type triple's entries on the given context: deletes the existing
    /// rows with a predicate <c>DELETE</c> that runs immediately, then stages the replacement rows for
    /// the caller to flush with <c>SaveChangesAsync</c>. The single place the replace semantics live,
    /// so <see cref="SaveManyAsync"/> (and <c>SaveAsync</c> through it) cannot drift.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The delete must be <c>ExecuteDeleteAsync</c> (a predicate delete that reaches the database
    /// straight away), not rows loaded and removed on the change tracker. The table has a unique index,
    /// so replacing an entry with the same verb and scope but a different state collides unless the
    /// delete provably precedes the insert, and EF Core's command ordering for tracked removal has
    /// varied across versions. On the EF version in use tracked removal happens to order correctly, so
    /// no test of the outcome can tell the two apart; the tests instead pin the mechanism, by asserting
    /// on the SQL issued (see <c>Replace_UsesPredicateDelete_NotTrackedRemoval</c>).
    /// </para>
    /// <para>
    /// The caller owns the transaction: the delete runs immediately while the inserts stay staged, so
    /// callers replacing more than one triple must wrap the whole sequence in a transaction to make it
    /// atomic.
    /// </para>
    /// </remarks>
    /// <param name="db">The context to delete and stage on.</param>
    /// <param name="nodeKey">The node the entries belong to.</param>
    /// <param name="roleAlias">The user group alias the entries belong to.</param>
    /// <param name="contentTypeKey">The document or element type the entries belong to.</param>
    /// <param name="entries">The replacement entries. An empty sequence leaves the triple with no rows.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>A task that completes once the delete has executed and the inserts are staged.</returns>
    private static async Task ReplaceTripleAsync(
        AdvancedPermissionsDbContext db,
        Guid nodeKey,
        string roleAlias,
        Guid contentTypeKey,
        IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> entries,
        CancellationToken cancellationToken)
    {
        await db.DocTypePermissions
            .Where(p => p.NodeKey == nodeKey && p.RoleAlias == roleAlias && p.ContentTypeKey == contentTypeKey)
            .ExecuteDeleteAsync(cancellationToken);

        foreach (var (verb, state, scope, isPriorityOverride) in entries)
        {
            db.DocTypePermissions.Add(new DocTypePermissionEntity
            {
                Id = Guid.NewGuid(),
                NodeKey = nodeKey,
                ContentTypeKey = contentTypeKey,
                RoleAlias = roleAlias,
                Verb = verb,
                State = state,
                Scope = scope,
                IsPriorityOverride = isPriorityOverride,
            });
        }
    }

    /// <inheritdoc />
    public async Task DeleteAllForNodeAsync(
        Guid nodeKey,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        await db.DocTypePermissions
            .Where(p => p.NodeKey == nodeKey)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAllForContentTypeAsync(
        Guid contentTypeKey,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        await db.DocTypePermissions
            .Where(p => p.ContentTypeKey == contentTypeKey)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAllForRoleAsync(
        string roleAlias,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        await db.DocTypePermissions
            .Where(p => p.RoleAlias == roleAlias)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocTypePermissionEntry>> GetByContentTypeAndNodesAsync(
        Guid contentTypeKey,
        IEnumerable<Guid> nodeKeys,
        CancellationToken cancellationToken = default)
    {
        var keyList = nodeKeys.ToList();

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entities = await db.DocTypePermissions
            .Where(p => p.ContentTypeKey == contentTypeKey && keyList.Contains(p.NodeKey))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return entities.ConvertAll(MapToDomain);
    }

    /// <summary>
    /// Maps an entity to the domain record.
    /// </summary>
    /// <param name="entity">The entity to map.</param>
    /// <returns>The corresponding domain record.</returns>
    private static DocTypePermissionEntry MapToDomain(DocTypePermissionEntity entity) =>
        new(entity.Id, entity.NodeKey, entity.ContentTypeKey, entity.RoleAlias, entity.Verb, entity.State, entity.Scope, entity.IsPriorityOverride);
}
