using Microsoft.EntityFrameworkCore;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;
using Umbraco.Community.AdvancedPermissions.Data.Context;
using Umbraco.Community.AdvancedPermissions.Data.Repositories;

namespace Umbraco.Community.AdvancedPermissions.Data.Tests;

/// <summary>
/// Runs the shared batch-save contract (<see cref="NodePermissionRepositoryBatchTestsBase"/>) against
/// the library <see cref="ElementPermissionRepository"/> and the <c>ElementPermission</c> table.
/// </summary>
/// <remarks>
/// The batch save is implemented once in <c>NodePermissionRepositoryBase</c>, not in either concrete
/// repository. These tests exist because a base-class method that only one family's tests exercise
/// can look covered while the other family has no protection at all — the failure would stay invisible
/// until two people edited element permissions at once.
/// </remarks>
public sealed class ElementPermissionRepositoryBatchTests : NodePermissionRepositoryBatchTestsBase
{
    /// <inheritdoc />
    protected override string VerbA => AdvancedPermissionsConstants.VerbElementRead;

    /// <inheritdoc />
    protected override string VerbB => AdvancedPermissionsConstants.VerbElementDelete;

    /// <inheritdoc />
    protected override string VerbC => AdvancedPermissionsConstants.VerbElementPublish;

    /// <inheritdoc />
    protected override INodePermissionRepository CreateRepository(IDbContextFactory<AdvancedPermissionsDbContext> factory) =>
        new ElementPermissionRepository(factory);

    /// <summary>
    /// Proves the batch lands in the <c>ElementPermission</c> table and never in the content
    /// <c>AdvancedPermission</c> table, so the two families sharing one implementation still keep
    /// separate storage.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_WritesToElementTableOnly()
    {
        var nodeKey = Guid.NewGuid();

        await Repository.SaveManyAsync(
        [
            (nodeKey, "editors", new[] { (VerbA, PermissionState.Allow, PermissionScope.ThisNodeOnly, false) }, null),
        ]);

        await using var db = new AdvancedPermissionsDbContext(Options);
        Assert.Equal(1, await db.ElementPermissions.CountAsync(p => p.NodeKey == nodeKey));
        Assert.Equal(0, await db.Permissions.CountAsync(p => p.NodeKey == nodeKey));
    }
}
