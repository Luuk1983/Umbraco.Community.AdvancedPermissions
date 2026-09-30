using Microsoft.EntityFrameworkCore;
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

    /// <inheritdoc />
    public async Task SaveAsync(
        Guid nodeKey,
        string roleAlias,
        Guid contentTypeKey,
        IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> entries,
        CancellationToken cancellationToken = default)
    {
        var newEntries = entries.ToList();

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Remove all existing entries for this triple in a single DELETE statement
        await db.DocTypePermissions
            .Where(p => p.NodeKey == nodeKey
                     && p.RoleAlias == roleAlias
                     && p.ContentTypeKey == contentTypeKey)
            .ExecuteDeleteAsync(cancellationToken);

        if (newEntries.Count > 0)
        {
            foreach (var (verb, state, scope, isPriorityOverride) in newEntries)
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

            await db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task SaveManyAsync(
        IEnumerable<(Guid NodeKey, string RoleAlias, Guid ContentTypeKey, IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries)> batch,
        CancellationToken cancellationToken = default)
    {
        var triples = batch.ToList();

        // Guard against duplicate (NodeKey, RoleAlias, ContentTypeKey) triples before touching the
        // database. Each triple's delete runs immediately (see below) while its inserts stay
        // staged, so a second triple for the same node+role+content-type would not see the first
        // triple's unflushed inserts — mirrors AdvancedPermissionRepository.SaveManyAsync's guard.
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
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        foreach (var (nodeKey, roleAlias, contentTypeKey, entries) in triples)
        {
            // Remove all existing entries for this triple in a single DELETE statement, executed
            // immediately — the same mechanism SaveAsync uses. This makes the delete provably
            // precede the insert of the replacement rows below: without it, replacing an entry with
            // the same verb+scope but a different state would depend on EF Core's internal
            // command-batch ordering to avoid colliding with the unique index.
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

        // The deletes above already executed immediately against the database; the inserts are
        // still staged on the change tracker. That split is exactly why the explicit transaction is
        // load-bearing rather than belt-and-braces: it is the only thing making each triple's
        // immediate delete and deferred insert — and every triple in the batch — atomic together.
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
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
