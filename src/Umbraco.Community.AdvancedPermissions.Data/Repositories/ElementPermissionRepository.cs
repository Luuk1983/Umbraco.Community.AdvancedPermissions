using Microsoft.EntityFrameworkCore;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Data.Context;
using Umbraco.Community.AdvancedPermissions.Data.Entities;

namespace Umbraco.Community.AdvancedPermissions.Data.Repositories;

/// <summary>
/// Entity Framework Core implementation of <see cref="IElementPermissionRepository"/> backed by the
/// <c>ElementPermission</c> table (library element and folder permissions). All behaviour comes from
/// <see cref="NodePermissionRepositoryBase{TEntity}"/>; this type only binds it to
/// <see cref="ElementPermissionEntity"/>.
/// </summary>
/// <param name="dbContextFactory">
/// The factory used to create <see cref="AdvancedPermissionsDbContext"/> instances.
/// </param>
public sealed class ElementPermissionRepository(IDbContextFactory<AdvancedPermissionsDbContext> dbContextFactory)
    : NodePermissionRepositoryBase<ElementPermissionEntity>(dbContextFactory), IElementPermissionRepository;
