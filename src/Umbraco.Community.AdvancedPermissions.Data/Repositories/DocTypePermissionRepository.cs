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
