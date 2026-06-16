using Microsoft.EntityFrameworkCore;
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
        await db.Set<TEntity>()
            .Where(p => p.NodeKey == nodeKey && p.RoleAlias == roleAlias)
            .ExecuteDeleteAsync(cancellationToken);

        // Add the new entries
        if (newEntries.Count > 0)
        {
            foreach (var (verb, state, scope, isPriorityOverride) in newEntries)
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

            await db.SaveChangesAsync(cancellationToken);
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
