using Microsoft.EntityFrameworkCore;
using UmbracoAdvancedSecurity.Core.Interfaces;
using UmbracoAdvancedSecurity.Core.Models;
using UmbracoAdvancedSecurity.Data.Context;
using UmbracoAdvancedSecurity.Data.Entities;

namespace UmbracoAdvancedSecurity.Data.Repositories;

/// <summary>
/// Entity Framework Core implementation of <see cref="IAdvancedPermissionRepository"/>.
/// Uses a factory to create short-lived DbContext instances, making it safe for use in singleton services.
/// </summary>
/// <param name="dbContextFactory">
/// The factory used to create <see cref="AdvancedSecurityDbContext"/> instances.
/// </param>
public sealed class AdvancedPermissionRepository(IDbContextFactory<AdvancedSecurityDbContext> dbContextFactory)
    : IAdvancedPermissionRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<AdvancedPermissionEntry>> GetByNodeAndRoleAsync(
        Guid? nodeKey,
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
        Guid? nodeKey,
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
    public async Task<IReadOnlyList<AdvancedPermissionEntry>> GetByNodesAndRoleAsync(
        IEnumerable<Guid> nodeKeys,
        string roleAlias,
        CancellationToken cancellationToken = default)
    {
        var keyList = nodeKeys.Select(k => (Guid?)k).ToList();

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
        IEnumerable<Guid?> nodeKeys,
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
        Guid? nodeKey,
        string roleAlias,
        IEnumerable<(string Verb, PermissionState State, PermissionScope Scope)> entries,
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
            foreach (var (verb, state, scope) in newEntries)
            {
                db.Permissions.Add(new AdvancedPermissionEntity
                {
                    NodeKey = nodeKey,
                    RoleAlias = roleAlias,
                    Verb = verb,
                    State = state,
                    Scope = scope,
                });
            }

            await db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(
        Guid? nodeKey,
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

    /// <summary>
    /// Maps a <see cref="AdvancedPermissionEntity"/> to the domain model <see cref="AdvancedPermissionEntry"/>.
    /// </summary>
    /// <param name="entity">The entity to map.</param>
    /// <returns>The corresponding domain model.</returns>
    private static AdvancedPermissionEntry MapToDomain(AdvancedPermissionEntity entity) =>
        new(entity.Id, entity.NodeKey, entity.RoleAlias, entity.Verb, entity.State, entity.Scope);
}
