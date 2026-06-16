using Microsoft.EntityFrameworkCore;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Data.Context;
using Umbraco.Community.AdvancedPermissions.Data.Entities;

namespace Umbraco.Community.AdvancedPermissions.Data.Repositories;

/// <summary>
/// Entity Framework Core implementation of <see cref="IAdvancedPermissionRepository"/> backed by the
/// <c>AdvancedPermission</c> table (content-node permissions). All behaviour comes from
/// <see cref="NodePermissionRepositoryBase{TEntity}"/>; this type only binds it to
/// <see cref="AdvancedPermissionEntity"/>.
/// </summary>
/// <param name="dbContextFactory">
/// The factory used to create <see cref="AdvancedPermissionsDbContext"/> instances.
/// </param>
public sealed class AdvancedPermissionRepository(IDbContextFactory<AdvancedPermissionsDbContext> dbContextFactory)
    : NodePermissionRepositoryBase<AdvancedPermissionEntity>(dbContextFactory), IAdvancedPermissionRepository;
