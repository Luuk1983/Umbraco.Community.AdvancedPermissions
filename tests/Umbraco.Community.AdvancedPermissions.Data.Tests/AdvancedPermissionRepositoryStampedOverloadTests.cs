using Microsoft.EntityFrameworkCore;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;
using Umbraco.Community.AdvancedPermissions.Data.Context;
using Umbraco.Community.AdvancedPermissions.Data.Repositories;

namespace Umbraco.Community.AdvancedPermissions.Data.Tests;

/// <summary>
/// Runs the stamped-overload contract (<see cref="NodePermissionRepositoryStampedOverloadTestsBase"/>)
/// against the content <see cref="AdvancedPermissionRepository"/>.
/// </summary>
public sealed class AdvancedPermissionRepositoryStampedOverloadTests : NodePermissionRepositoryStampedOverloadTestsBase
{
    /// <summary>The concrete repository the unstamped overload is called on.</summary>
    private AdvancedPermissionRepository _concrete = null!;

    /// <inheritdoc />
    protected override string Verb => AdvancedPermissionsConstants.VerbRead;

    /// <inheritdoc />
    protected override Type RepositoryType => typeof(AdvancedPermissionRepository);

    /// <inheritdoc />
    protected override INodePermissionRepository CreateRepository(IDbContextFactory<AdvancedPermissionsDbContext> factory)
    {
        _concrete = new AdvancedPermissionRepository(factory);
        return _concrete;
    }

    /// <inheritdoc />
    protected override Task SaveUnstampedAsync(
        Guid nodeKey,
        string roleAlias,
        IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> entries) =>
        _concrete.SaveAsync(nodeKey, roleAlias, entries);
}
