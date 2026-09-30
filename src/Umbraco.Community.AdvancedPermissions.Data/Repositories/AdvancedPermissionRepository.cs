using Microsoft.EntityFrameworkCore;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;
using Umbraco.Community.AdvancedPermissions.Data.Context;
using Umbraco.Community.AdvancedPermissions.Data.Entities;

namespace Umbraco.Community.AdvancedPermissions.Data.Repositories;

/// <summary>
/// Entity Framework Core implementation of <see cref="IAdvancedPermissionRepository"/>.
/// Uses a factory to create short-lived DbContext instances, making it safe for use in singleton services.
/// </summary>
/// <param name="dbContextFactory">
/// The factory used to create <see cref="AdvancedPermissionsDbContext"/> instances.
/// </param>
public sealed class AdvancedPermissionRepository(IDbContextFactory<AdvancedPermissionsDbContext> dbContextFactory)
    : IAdvancedPermissionRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<AdvancedPermissionEntry>> GetByNodeAndRoleAsync(
        Guid nodeKey,
        string roleAlias,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entities = await db.Permissions
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

        var entities = await db.Permissions
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

        var entities = await db.Permissions
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

        var entities = await db.Permissions
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

        var entities = await db.Permissions
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

        var entities = await db.Permissions
            .Where(p => roleList.Contains(p.RoleAlias) && nodeList.Contains(p.NodeKey))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return entities.ConvertAll(MapToDomain);
    }

    /// <inheritdoc />
    public async Task SaveAsync(
        Guid nodeKey,
        string roleAlias,
        IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> entries,
        CancellationToken cancellationToken = default)
    {
        var newEntries = entries.ToList();

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Remove all existing entries for this node+role combination in a single DELETE statement
        await db.Permissions
            .Where(p => p.NodeKey == nodeKey && p.RoleAlias == roleAlias)
            .ExecuteDeleteAsync(cancellationToken);

        // Add the new entries
        if (newEntries.Count > 0)
        {
            foreach (var (verb, state, scope, isPriorityOverride) in newEntries)
            {
                db.Permissions.Add(new AdvancedPermissionEntity
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

            await db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task SaveManyAsync(
        IEnumerable<(Guid NodeKey, string RoleAlias, IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries)> batch,
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
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        foreach (var (nodeKey, roleAlias, entries) in pairs)
        {
            // Remove all existing entries for this node+role combination in a single DELETE
            // statement, executed immediately — the same mechanism SaveAsync uses. This makes
            // the delete provably precede the insert of the replacement rows below: without it,
            // replacing an entry with the same verb+scope but a different state (the everyday
            // Permissions Editor save) would depend on EF Core's internal command-batch ordering
            // to avoid colliding with IX_AdvancedPermission_Unique.
            await db.Permissions
                .Where(p => p.NodeKey == nodeKey && p.RoleAlias == roleAlias)
                .ExecuteDeleteAsync(cancellationToken);

            foreach (var (verb, state, scope, isPriorityOverride) in entries)
            {
                db.Permissions.Add(new AdvancedPermissionEntity
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

        // The deletes above already executed immediately against the database; the inserts are
        // still staged on the change tracker. That split is exactly why the explicit transaction
        // is load-bearing rather than belt-and-braces: it is the only thing making each pair's
        // immediate delete and deferred insert — and every pair in the batch — atomic together.
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(
        Guid nodeKey,
        string roleAlias,
        string verb,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        await db.Permissions
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

        await db.Permissions
            .Where(p => p.NodeKey == nodeKey)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAllForRoleAsync(
        string roleAlias,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        await db.Permissions
            .Where(p => p.RoleAlias == roleAlias)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>
    /// Maps a <see cref="AdvancedPermissionEntity"/> to the domain model <see cref="AdvancedPermissionEntry"/>.
    /// </summary>
    /// <param name="entity">The entity to map.</param>
    /// <returns>The corresponding domain model.</returns>
    private static AdvancedPermissionEntry MapToDomain(AdvancedPermissionEntity entity) =>
        new(entity.Id, entity.NodeKey, entity.RoleAlias, entity.Verb, entity.State, entity.Scope, entity.IsPriorityOverride);
}
