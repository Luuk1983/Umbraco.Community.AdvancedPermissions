using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Models;
using Umbraco.Community.AdvancedPermissions.Data.Context;
using Umbraco.Community.AdvancedPermissions.Data.Repositories;

namespace Umbraco.Community.AdvancedPermissions.Data.Tests;

/// <summary>
/// Integration tests for <see cref="ElementPermissionRepository"/> using a SQLite in-memory database.
/// The full query/save behaviour is covered by <see cref="AdvancedPermissionRepositoryTests"/> (both
/// derive from the same <see cref="NodePermissionRepositoryBase{TEntity}"/>); these tests confirm the
/// element repository is correctly bound to its own <c>ElementPermission</c> table and is isolated
/// from the content <c>AdvancedPermission</c> table.
/// </summary>
public sealed class ElementPermissionRepositoryTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private DbContextOptions<AdvancedPermissionsDbContext> _options = null!;
    private ElementPermissionRepository _repository = null!;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        _options = new DbContextOptionsBuilder<AdvancedPermissionsDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using var db = new AdvancedPermissionsDbContext(_options);
        await db.Database.EnsureCreatedAsync();

        var factory = new SingleConnectionDbContextFactory(_options);
        _repository = new ElementPermissionRepository(factory);
    }

    /// <inheritdoc />
    public async Task DisposeAsync() => await _connection.DisposeAsync();

    /// <summary>
    /// Verifies a full save/get round-trip against the element table, preserving every field.
    /// </summary>
    [Fact]
    public async Task SaveAsync_StoresElementEntries_GetByNodeAndRoleAsync_ReturnsThem()
    {
        var nodeKey = Guid.NewGuid();
        const string role = "editors";

        await _repository.SaveAsync(nodeKey, role,
        [
            (AdvancedPermissionsConstants.VerbElementRead, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants, false),
            (AdvancedPermissionsConstants.VerbElementDelete, PermissionState.Deny, PermissionScope.ThisNodeOnly, true),
        ]);

        var results = await _repository.GetByNodeAndRoleAsync(nodeKey, role);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r =>
            r.Verb == AdvancedPermissionsConstants.VerbElementRead &&
            r.State == PermissionState.Allow &&
            r.Scope == PermissionScope.ThisNodeAndDescendants &&
            !r.IsPriorityOverride);
        Assert.Contains(results, r =>
            r.Verb == AdvancedPermissionsConstants.VerbElementDelete &&
            r.State == PermissionState.Deny &&
            r.Scope == PermissionScope.ThisNodeOnly &&
            r.IsPriorityOverride);
    }

    /// <summary>
    /// Verifies that canonical container verbs (mapped from <c>Umb.ElementContainer.*</c> by the
    /// enforcement adapter) round-trip correctly when stored on a folder node.
    /// </summary>
    [Fact]
    public async Task SaveAsync_StoresCanonicalVerbsForFolderNodes()
    {
        var folderKey = Guid.NewGuid();
        const string role = "editors";

        // The folder adapter maps Umb.ElementContainer.* to the canonical Umb.Element.* verbs before
        // storing; the repository only ever sees canonical verbs.
        await _repository.SaveAsync(folderKey, role,
        [
            (AdvancedPermissionsConstants.VerbElementRead, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants, false),
        ]);

        var results = await _repository.GetByNodeAndRoleAsync(folderKey, role);

        Assert.Single(results);
        Assert.Equal(AdvancedPermissionsConstants.VerbElementRead, results[0].Verb);
    }

    /// <summary>
    /// Verifies virtual-root element entries (the role default) are retrievable.
    /// </summary>
    [Fact]
    public async Task GetByNodeAsync_WithVirtualRootKey_ReturnsVirtualRootElementEntries()
    {
        await _repository.SaveAsync(
            AdvancedPermissionsConstants.VirtualRootNodeKey,
            AdvancedPermissionsConstants.EveryoneRoleAlias,
            [(AdvancedPermissionsConstants.VerbElementRead, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants, false)]);

        var results = await _repository.GetByNodeAsync(AdvancedPermissionsConstants.VirtualRootNodeKey);

        Assert.Single(results);
        Assert.Equal(AdvancedPermissionsConstants.EveryoneRoleAlias, results[0].RoleAlias);
    }

    /// <summary>
    /// Verifies cleanup helpers operate against the element table.
    /// </summary>
    [Fact]
    public async Task DeleteAllForNodeAsync_RemovesAllElementEntriesForNode()
    {
        var nodeKey = Guid.NewGuid();
        await _repository.SaveAsync(nodeKey, "editors",
            [(AdvancedPermissionsConstants.VerbElementRead, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)]);
        await _repository.SaveAsync(nodeKey, "writers",
            [(AdvancedPermissionsConstants.VerbElementUpdate, PermissionState.Deny, PermissionScope.ThisNodeOnly, false)]);

        await _repository.DeleteAllForNodeAsync(nodeKey);

        Assert.Empty(await _repository.GetByNodeAsync(nodeKey));
    }

    /// <summary>
    /// The core element-specific guarantee: element and content permissions live in separate tables.
    /// Saving element entries must not appear through the content repository, and vice versa, even for
    /// the same node key and role.
    /// </summary>
    [Fact]
    public async Task ElementAndContentEntries_AreStoredInSeparateTables()
    {
        var sharedKey = Guid.NewGuid();
        const string role = "editors";

        var contentRepo = new AdvancedPermissionRepository(new SingleConnectionDbContextFactory(_options));

        await _repository.SaveAsync(sharedKey, role,
            [(AdvancedPermissionsConstants.VerbElementRead, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)]);
        await contentRepo.SaveAsync(sharedKey, role,
            [(AdvancedPermissionsConstants.VerbRead, PermissionState.Deny, PermissionScope.ThisNodeOnly, false)]);

        var elementEntries = await _repository.GetByNodeAndRoleAsync(sharedKey, role);
        var contentEntries = await contentRepo.GetByNodeAndRoleAsync(sharedKey, role);

        // Each repository sees only its own table's row — no cross-contamination.
        Assert.Single(elementEntries);
        Assert.Equal(AdvancedPermissionsConstants.VerbElementRead, elementEntries[0].Verb);
        Assert.Equal(PermissionState.Allow, elementEntries[0].State);

        Assert.Single(contentEntries);
        Assert.Equal(AdvancedPermissionsConstants.VerbRead, contentEntries[0].Verb);
        Assert.Equal(PermissionState.Deny, contentEntries[0].State);

        // Deleting all element entries for the node must leave the content entry untouched.
        await _repository.DeleteAllForNodeAsync(sharedKey);
        Assert.Empty(await _repository.GetByNodeAndRoleAsync(sharedKey, role));
        Assert.Single(await contentRepo.GetByNodeAndRoleAsync(sharedKey, role));
    }
}
