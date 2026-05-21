using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Models;
using Umbraco.Community.AdvancedPermissions.Data.Context;
using Umbraco.Community.AdvancedPermissions.Data.Repositories;

namespace Umbraco.Community.AdvancedPermissions.Data.Tests;

/// <summary>
/// Integration tests for <see cref="DocTypePermissionRepository"/> using a SQLite in-memory database.
/// Mirrors the structure of <see cref="AdvancedPermissionRepositoryTests"/>.
/// </summary>
public sealed class DocTypePermissionRepositoryTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private DocTypePermissionRepository _repository = null!;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AdvancedPermissionsDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using var db = new AdvancedPermissionsDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var factory = new SingleConnectionDbContextFactory(options);
        _repository = new DocTypePermissionRepository(factory);
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    // ───────────────────────────────────────────────────────────────────────
    // SaveAsync + GetByRoleAsync round-trip
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that entries saved for a (node, role, content-type) triple can be retrieved.
    /// </summary>
    [Fact]
    public async Task SaveAsync_StoresEntries_GetByRoleAsync_ReturnsThem()
    {
        var nodeKey = Guid.NewGuid();
        var contentTypeKey = Guid.NewGuid();
        const string role = "editors";

        await _repository.SaveAsync(nodeKey, role, contentTypeKey,
        [
            (AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Deny, PermissionScope.ThisNodeAndDescendants),
        ]);

        var results = await _repository.GetByRoleAsync(role);

        Assert.Single(results);
        Assert.Equal(nodeKey, results[0].NodeKey);
        Assert.Equal(contentTypeKey, results[0].ContentTypeKey);
        Assert.Equal(PermissionState.Deny, results[0].State);
    }

    /// <summary>
    /// Verifies that calling SaveAsync again for the same triple replaces existing entries.
    /// </summary>
    [Fact]
    public async Task SaveAsync_ReplacesExisting_WhenCalledAgainForSameTriple()
    {
        var nodeKey = Guid.NewGuid();
        var contentTypeKey = Guid.NewGuid();
        const string role = "editors";

        await _repository.SaveAsync(nodeKey, role, contentTypeKey,
        [
            (AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Deny, PermissionScope.ThisNodeAndDescendants),
        ]);

        // Replace with Allow at a different scope
        await _repository.SaveAsync(nodeKey, role, contentTypeKey,
        [
            (AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Allow, PermissionScope.ThisNodeOnly),
        ]);

        var results = await _repository.GetByRoleAndContentTypeAsync(role, contentTypeKey);

        Assert.Single(results);
        Assert.Equal(PermissionState.Allow, results[0].State);
        Assert.Equal(PermissionScope.ThisNodeOnly, results[0].Scope);
    }

    /// <summary>
    /// Verifies that saving with an empty entry list removes all existing entries for the triple.
    /// </summary>
    [Fact]
    public async Task SaveAsync_WithEmptyEntries_RemovesAllForTriple()
    {
        var nodeKey = Guid.NewGuid();
        var contentTypeKey = Guid.NewGuid();
        const string role = "editors";

        await _repository.SaveAsync(nodeKey, role, contentTypeKey,
        [
            (AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Deny, PermissionScope.ThisNodeAndDescendants),
        ]);

        await _repository.SaveAsync(nodeKey, role, contentTypeKey, []);

        var results = await _repository.GetByRoleAndContentTypeAsync(role, contentTypeKey);
        Assert.Empty(results);
    }

    /// <summary>
    /// Verifies that saving for one (role, type) triple does not affect entries for a different type.
    /// </summary>
    [Fact]
    public async Task SaveAsync_DoesNotAffectOtherContentTypes()
    {
        var nodeKey = Guid.NewGuid();
        var typeA = Guid.NewGuid();
        var typeB = Guid.NewGuid();
        const string role = "editors";

        await _repository.SaveAsync(nodeKey, role, typeA,
        [
            (AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Deny, PermissionScope.ThisNodeAndDescendants),
        ]);

        await _repository.SaveAsync(nodeKey, role, typeB,
        [
            (AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants),
        ]);

        await _repository.SaveAsync(nodeKey, role, typeA, []);

        var typeAResults = await _repository.GetByRoleAndContentTypeAsync(role, typeA);
        var typeBResults = await _repository.GetByRoleAndContentTypeAsync(role, typeB);

        Assert.Empty(typeAResults);
        Assert.Single(typeBResults);
    }

    // ───────────────────────────────────────────────────────────────────────
    // GetByRoleAndContentTypeAsync filtering
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that GetByRoleAndContentTypeAsync returns only entries matching both filters.
    /// </summary>
    [Fact]
    public async Task GetByRoleAndContentTypeAsync_FiltersCorrectly()
    {
        var node1 = Guid.NewGuid();
        var node2 = Guid.NewGuid();
        var newsType = Guid.NewGuid();
        var faqType = Guid.NewGuid();

        await _repository.SaveAsync(node1, "editors", newsType,
        [
            (AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Deny, PermissionScope.ThisNodeAndDescendants),
        ]);

        await _repository.SaveAsync(node2, "editors", newsType,
        [
            (AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Allow, PermissionScope.ThisNodeOnly),
        ]);

        // Different type — must not appear
        await _repository.SaveAsync(node1, "editors", faqType,
        [
            (AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Deny, PermissionScope.ThisNodeAndDescendants),
        ]);

        // Different role — must not appear
        await _repository.SaveAsync(node1, "writers", newsType,
        [
            (AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Deny, PermissionScope.ThisNodeAndDescendants),
        ]);

        var results = await _repository.GetByRoleAndContentTypeAsync("editors", newsType);

        Assert.Equal(2, results.Count);
        Assert.All(results, r =>
        {
            Assert.Equal("editors", r.RoleAlias);
            Assert.Equal(newsType, r.ContentTypeKey);
        });
    }

    // ───────────────────────────────────────────────────────────────────────
    // Cleanup operations
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that DeleteAllForNodeAsync removes entries scoped to that node only.
    /// </summary>
    [Fact]
    public async Task DeleteAllForNodeAsync_RemovesEntriesForThatNode()
    {
        var nodeToDelete = Guid.NewGuid();
        var nodeToKeep = Guid.NewGuid();
        var contentTypeKey = Guid.NewGuid();

        await _repository.SaveAsync(nodeToDelete, "editors", contentTypeKey,
        [
            (AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Deny, PermissionScope.ThisNodeAndDescendants),
        ]);

        await _repository.SaveAsync(nodeToKeep, "editors", contentTypeKey,
        [
            (AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants),
        ]);

        await _repository.DeleteAllForNodeAsync(nodeToDelete);

        var results = await _repository.GetByRoleAsync("editors");
        Assert.Single(results);
        Assert.Equal(nodeToKeep, results[0].NodeKey);
    }

    /// <summary>
    /// Verifies that DeleteAllForContentTypeAsync removes entries for that doc-type only.
    /// </summary>
    [Fact]
    public async Task DeleteAllForContentTypeAsync_RemovesEntriesForThatType()
    {
        var nodeKey = Guid.NewGuid();
        var typeToDelete = Guid.NewGuid();
        var typeToKeep = Guid.NewGuid();

        await _repository.SaveAsync(nodeKey, "editors", typeToDelete,
        [
            (AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Deny, PermissionScope.ThisNodeAndDescendants),
        ]);

        await _repository.SaveAsync(nodeKey, "editors", typeToKeep,
        [
            (AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants),
        ]);

        await _repository.DeleteAllForContentTypeAsync(typeToDelete);

        var results = await _repository.GetByRoleAsync("editors");
        Assert.Single(results);
        Assert.Equal(typeToKeep, results[0].ContentTypeKey);
    }

    /// <summary>
    /// Verifies that DeleteAllForRoleAsync removes entries for the given role only.
    /// </summary>
    [Fact]
    public async Task DeleteAllForRoleAsync_RemovesEntriesForThatRole()
    {
        var nodeKey = Guid.NewGuid();
        var contentTypeKey = Guid.NewGuid();

        await _repository.SaveAsync(nodeKey, "editors", contentTypeKey,
        [
            (AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Deny, PermissionScope.ThisNodeAndDescendants),
        ]);

        await _repository.SaveAsync(nodeKey, "writers", contentTypeKey,
        [
            (AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants),
        ]);

        await _repository.DeleteAllForRoleAsync("editors");

        var editorsResults = await _repository.GetByRoleAsync("editors");
        var writersResults = await _repository.GetByRoleAsync("writers");

        Assert.Empty(editorsResults);
        Assert.Single(writersResults);
    }

    /// <summary>
    /// Verifies that the delete operations are idempotent (no error when nothing matches).
    /// </summary>
    [Fact]
    public async Task DeleteOperations_AreIdempotent_WhenNothingMatches()
    {
        await _repository.DeleteAllForNodeAsync(Guid.NewGuid());
        await _repository.DeleteAllForContentTypeAsync(Guid.NewGuid());
        await _repository.DeleteAllForRoleAsync("non-existent-role");
    }

    // ───────────────────────────────────────────────────────────────────────
    // Field preservation
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies all fields round-trip correctly through save/retrieve.
    /// </summary>
    [Fact]
    public async Task SaveAndGet_PreservesAllFields()
    {
        var nodeKey = AdvancedPermissionsConstants.VirtualRootNodeKey;
        var contentTypeKey = Guid.NewGuid();
        const string role = AdvancedPermissionsConstants.EveryoneRoleAlias;

        await _repository.SaveAsync(nodeKey, role, contentTypeKey,
        [
            (AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Deny, PermissionScope.DescendantsOnly),
        ]);

        var results = await _repository.GetByRoleAndContentTypeAsync(role, contentTypeKey);

        Assert.Single(results);
        var entry = results[0];
        Assert.NotEqual(Guid.Empty, entry.Id);
        Assert.Equal(nodeKey, entry.NodeKey);
        Assert.Equal(contentTypeKey, entry.ContentTypeKey);
        Assert.Equal(role, entry.RoleAlias);
        Assert.Equal(AdvancedPermissionsConstants.VerbCreateOfType, entry.Verb);
        Assert.Equal(PermissionState.Deny, entry.State);
        Assert.Equal(PermissionScope.DescendantsOnly, entry.Scope);
    }
}
