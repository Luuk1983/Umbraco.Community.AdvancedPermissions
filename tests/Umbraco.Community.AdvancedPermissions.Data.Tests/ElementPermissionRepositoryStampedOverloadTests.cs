using Microsoft.EntityFrameworkCore;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;
using Umbraco.Community.AdvancedPermissions.Data.Context;
using Umbraco.Community.AdvancedPermissions.Data.Repositories;

namespace Umbraco.Community.AdvancedPermissions.Data.Tests;

/// <summary>
/// Runs the stamped-overload contract (<see cref="NodePermissionRepositoryStampedOverloadTestsBase"/>)
/// against the library <see cref="ElementPermissionRepository"/>, so the shared implementation is
/// proven for both families rather than for whichever one was tested first.
/// </summary>
public sealed class ElementPermissionRepositoryStampedOverloadTests : NodePermissionRepositoryStampedOverloadTestsBase
{
    /// <summary>The concrete repository the unstamped overload is called on.</summary>
    private ElementPermissionRepository _concrete = null!;

    /// <inheritdoc />
    protected override string Verb => AdvancedPermissionsConstants.VerbElementRead;

    /// <inheritdoc />
    protected override Type RepositoryType => typeof(ElementPermissionRepository);

    /// <inheritdoc />
    protected override INodePermissionRepository CreateRepository(IDbContextFactory<AdvancedPermissionsDbContext> factory)
    {
        _concrete = new ElementPermissionRepository(factory);
        return _concrete;
    }

    /// <inheritdoc />
    protected override Task SaveUnstampedAsync(
        Guid nodeKey,
        string roleAlias,
        IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> entries) =>
        _concrete.SaveAsync(nodeKey, roleAlias, entries);
}
