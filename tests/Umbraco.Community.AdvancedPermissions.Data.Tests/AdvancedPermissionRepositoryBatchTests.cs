using Microsoft.EntityFrameworkCore;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Data.Context;
using Umbraco.Community.AdvancedPermissions.Data.Repositories;

namespace Umbraco.Community.AdvancedPermissions.Data.Tests;

/// <summary>
/// Runs the shared batch-save contract (<see cref="NodePermissionRepositoryBatchTestsBase"/>) against
/// the content-node <see cref="AdvancedPermissionRepository"/> and the <c>AdvancedPermission</c> table.
/// </summary>
public sealed class AdvancedPermissionRepositoryBatchTests : NodePermissionRepositoryBatchTestsBase
{
    /// <inheritdoc />
    protected override string VerbA => AdvancedPermissionsConstants.VerbRead;

    /// <inheritdoc />
    protected override string VerbB => AdvancedPermissionsConstants.VerbDelete;

    /// <inheritdoc />
    protected override string VerbC => AdvancedPermissionsConstants.VerbPublish;

    /// <inheritdoc />
    protected override INodePermissionRepository CreateRepository(IDbContextFactory<AdvancedPermissionsDbContext> factory) =>
        new AdvancedPermissionRepository(factory);
}
