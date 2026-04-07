using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using LP.Umbraco.AdvancedPermissions.Core.Constants;
using LP.Umbraco.AdvancedPermissions.Core.Models;
using LP.Umbraco.AdvancedPermissions.Data.Context;
using LP.Umbraco.AdvancedPermissions.Data.Repositories;

namespace LP.Umbraco.AdvancedPermissions.Data.Tests;

/// <summary>
/// Integration tests for <see cref="AdvancedPermissionRepository"/> using a SQLite in-memory database.
/// Each test class instance gets a fresh, isolated database; the shared SQLite connection keeps
/// the in-memory database alive for the lifetime of the test.
/// </summary>
public sealed class AdvancedPermissionRepositoryTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AdvancedPermissionRepository _repository = null!;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        // Keep a single connection open so the in-memory SQLite database persists
        // for the lifetime of the test.
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AdvancedPermissionsDbContext>()
            .UseSqlite(_connection)
            .Options;

        // Create the schema using EF Core's model
        await using var db = new AdvancedPermissionsDbContext(options);
        await db.Database.EnsureCreatedAsync();

        // Build a simple factory that reuses the same connection
        var factory = new SingleConnectionDbContextFactory(options);
        _repository = new AdvancedPermissionRepository(factory);
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    // -------------------------------------------------------------------------
    // SaveAsync + GetByNodeAndRoleAsync
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that entries saved for a node+role can be retrieved by the same node+role.
    /// </summary>
    [Fact]
    public async Task SaveAsync_StoresEntries_GetByNodeAndRoleAsync_ReturnsThem()
    {
        var nodeKey = Guid.NewGuid();
        const string role = "editors";

        await _repository.SaveAsync(nodeKey, role,
        [
            (AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants),
            (AdvancedPermissionsConstants.VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeOnly),
        ]);

        var results = await _repository.GetByNodeAndRoleAsync(nodeKey, role);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r =>
            r.Verb == AdvancedPermissionsConstants.VerbRead &&
            r.State == PermissionState.Allow &&
            r.Scope == PermissionScope.ThisNodeAndDescendants);
        Assert.Contains(results, r =>
            r.Verb == AdvancedPermissionsConstants.VerbDelete &&
            r.State == PermissionState.Deny &&
            r.Scope == PermissionScope.ThisNodeOnly);
    }

    /// <summary>
    /// Verifies that calling SaveAsync a second time for the same node+role replaces the existing entries.
    /// </summary>
    [Fact]
    public async Task SaveAsync_ReplacesExistingEntries_WhenCalledAgainForSameNodeAndRole()
    {
        var nodeKey = Guid.NewGuid();
        const string role = "editors";

        await _repository.SaveAsync(nodeKey, role,
        [
            (AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants),
            (AdvancedPermissionsConstants.VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeOnly),
        ]);

        // Second save with only one entry — should remove the delete entry
        await _repository.SaveAsync(nodeKey, role,
        [
            (AdvancedPermissionsConstants.VerbCreate, PermissionState.Allow, PermissionScope.DescendantsOnly),
        ]);

        var results = await _repository.GetByNodeAndRoleAsync(nodeKey, role);

        Assert.Single(results);
        Assert.Equal(AdvancedPermissionsConstants.VerbCreate, results[0].Verb);
    }

    /// <summary>
    /// Verifies that saving an empty entry list removes all existing entries for the node+role.
    /// </summary>
    [Fact]
    public async Task SaveAsync_WithEmptyEntries_RemovesAllExistingEntriesForNodeAndRole()
    {
        var nodeKey = Guid.NewGuid();
        const string role = "editors";

        await _repository.SaveAsync(nodeKey, role,
        [
            (AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants),
        ]);

        await _repository.SaveAsync(nodeKey, role, []);

        var results = await _repository.GetByNodeAndRoleAsync(nodeKey, role);
        Assert.Empty(results);
    }

    /// <summary>
    /// Verifies that saving entries for one role does not affect entries stored for another role on the same node.
    /// </summary>
    [Fact]
    public async Task SaveAsync_PreservesEntriesForDifferentRole_WhenSavingForOneRole()
    {
        var nodeKey = Guid.NewGuid();

        await _repository.SaveAsync(nodeKey, "editors",
        [
            (AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants),
        ]);

        await _repository.SaveAsync(nodeKey, "writers",
        [
            (AdvancedPermissionsConstants.VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeOnly),
        ]);

        // Overwrite editors only — writers should remain untouched
        await _repository.SaveAsync(nodeKey, "editors", []);

        var editorsResults = await _repository.GetByNodeAndRoleAsync(nodeKey, "editors");
        var writersResults = await _repository.GetByNodeAndRoleAsync(nodeKey, "writers");

        Assert.Empty(editorsResults);
        Assert.Single(writersResults);
    }

    /// <summary>
    /// Verifies that two entries for the same verb with different scopes (the "different for node vs descendants"
    /// pattern) can be stored and retrieved correctly.
    /// </summary>
    [Fact]
    public async Task SaveAsync_DualEntriesForSameVerb_StoredAndRetrievedCorrectly()
    {
        var nodeKey = Guid.NewGuid();
        const string role = "editors";

        await _repository.SaveAsync(nodeKey, role,
        [
            (AdvancedPermissionsConstants.VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeOnly),
            (AdvancedPermissionsConstants.VerbDelete, PermissionState.Allow, PermissionScope.DescendantsOnly),
        ]);

        var results = await _repository.GetByNodeAndRoleAsync(nodeKey, role);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r =>
            r.Verb == AdvancedPermissionsConstants.VerbDelete &&
            r.State == PermissionState.Deny &&
            r.Scope == PermissionScope.ThisNodeOnly);
        Assert.Contains(results, r =>
            r.Verb == AdvancedPermissionsConstants.VerbDelete &&
            r.State == PermissionState.Allow &&
            r.Scope == PermissionScope.DescendantsOnly);
    }

    // -------------------------------------------------------------------------
    // GetByNodeAsync
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that GetByNodeAsync returns entries for all roles at the given node.
    /// </summary>
    [Fact]
    public async Task GetByNodeAsync_ReturnsEntriesForAllRolesAtNode()
    {
        var nodeKey = Guid.NewGuid();

        await _repository.SaveAsync(nodeKey, "editors",
        [
            (AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants),
        ]);

        await _repository.SaveAsync(nodeKey, "writers",
        [
            (AdvancedPermissionsConstants.VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeOnly),
        ]);

        // Entry for a different node — should NOT appear
        await _repository.SaveAsync(Guid.NewGuid(), "editors",
        [
            (AdvancedPermissionsConstants.VerbCreate, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants),
        ]);

        var results = await _repository.GetByNodeAsync(nodeKey);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.RoleAlias == "editors" && r.Verb == AdvancedPermissionsConstants.VerbRead);
        Assert.Contains(results, r => r.RoleAlias == "writers" && r.Verb == AdvancedPermissionsConstants.VerbDelete);
    }

    /// <summary>
    /// Verifies that GetByNodeAsync with the VirtualRootNodeKey returns virtual-root entries only,
    /// not node-specific ones.
    /// </summary>
    [Fact]
    public async Task GetByNodeAsync_WithVirtualRootKey_ReturnsVirtualRootEntries()
    {
        await _repository.SaveAsync(AdvancedPermissionsConstants.VirtualRootNodeKey, AdvancedPermissionsConstants.EveryoneRoleAlias,
        [
            (AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants),
        ]);

        await _repository.SaveAsync(Guid.NewGuid(), "editors",
        [
            (AdvancedPermissionsConstants.VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeOnly),
        ]);

        var results = await _repository.GetByNodeAsync(AdvancedPermissionsConstants.VirtualRootNodeKey);

        Assert.Single(results);
        Assert.Equal(AdvancedPermissionsConstants.EveryoneRoleAlias, results[0].RoleAlias);
    }

    /// <summary>
    /// Verifies that GetByNodeAsync returns an empty list when no entries exist for the node.
    /// </summary>
    [Fact]
    public async Task GetByNodeAsync_ReturnsEmpty_WhenNoEntriesExistForNode()
    {
        var results = await _repository.GetByNodeAsync(Guid.NewGuid());
        Assert.Empty(results);
    }

    // -------------------------------------------------------------------------
    // GetByRoleAsync
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that GetByRoleAsync returns all entries for the specified role across all nodes.
    /// </summary>
    [Fact]
    public async Task GetByRoleAsync_ReturnsAllEntriesForRole_AcrossAllNodes()
    {
        var node1 = Guid.NewGuid();
        var node2 = Guid.NewGuid();

        await _repository.SaveAsync(node1, "editors",
        [
            (AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants),
        ]);

        await _repository.SaveAsync(node2, "editors",
        [
            (AdvancedPermissionsConstants.VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeOnly),
        ]);

        // Different role — should NOT appear
        await _repository.SaveAsync(node1, "writers",
        [
            (AdvancedPermissionsConstants.VerbCreate, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants),
        ]);

        var results = await _repository.GetByRoleAsync("editors");

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal("editors", r.RoleAlias));
        Assert.Contains(results, r => r.NodeKey == node1);
        Assert.Contains(results, r => r.NodeKey == node2);
    }

    /// <summary>
    /// Verifies that GetByRoleAsync returns an empty list when the role has no entries.
    /// </summary>
    [Fact]
    public async Task GetByRoleAsync_ReturnsEmpty_WhenRoleHasNoEntries()
    {
        var results = await _repository.GetByRoleAsync("nonexistent-role");
        Assert.Empty(results);
    }

    // -------------------------------------------------------------------------
    // GetByRolesAndNodesAsync
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that GetByRolesAndNodesAsync returns only entries matching both the role and node filters.
    /// </summary>
    [Fact]
    public async Task GetByRolesAndNodesAsync_ReturnsMatchingEntries()
    {
        var node1 = Guid.NewGuid();
        var node2 = Guid.NewGuid();
        var node3 = Guid.NewGuid();

        await _repository.SaveAsync(node1, "editors",
        [
            (AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants),
        ]);

        await _repository.SaveAsync(node2, "writers",
        [
            (AdvancedPermissionsConstants.VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeOnly),
        ]);

        // Editors at node3 — excluded because node3 is not in the filter
        await _repository.SaveAsync(node3, "editors",
        [
            (AdvancedPermissionsConstants.VerbCreate, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants),
        ]);

        // Query for editors at node1 and node2 only
        var results = await _repository.GetByRolesAndNodesAsync(["editors"], [node1, node2]);

        Assert.Single(results);
        Assert.Equal(node1, results[0].NodeKey);
        Assert.Equal("editors", results[0].RoleAlias);
    }

    /// <summary>
    /// Verifies that GetByRolesAndNodesAsync includes virtual-root entries when VirtualRootNodeKey is in the node key list.
    /// </summary>
    [Fact]
    public async Task GetByRolesAndNodesAsync_WithVirtualRootKey_IncludesVirtualRootEntries()
    {
        var node1 = Guid.NewGuid();

        await _repository.SaveAsync(AdvancedPermissionsConstants.VirtualRootNodeKey, "editors",
        [
            (AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants),
        ]);

        await _repository.SaveAsync(node1, "editors",
        [
            (AdvancedPermissionsConstants.VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeOnly),
        ]);

        var results = await _repository.GetByRolesAndNodesAsync(["editors"], [AdvancedPermissionsConstants.VirtualRootNodeKey, node1]);

        Assert.Equal(2, results.Count);
    }

    // -------------------------------------------------------------------------
    // DeleteAsync
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that DeleteAsync removes only the specified verb entry, leaving others intact.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_RemovesSpecificVerbEntry()
    {
        var nodeKey = Guid.NewGuid();
        const string role = "editors";

        await _repository.SaveAsync(nodeKey, role,
        [
            (AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants),
            (AdvancedPermissionsConstants.VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeOnly),
        ]);

        await _repository.DeleteAsync(nodeKey, role, AdvancedPermissionsConstants.VerbDelete);

        var results = await _repository.GetByNodeAndRoleAsync(nodeKey, role);

        Assert.Single(results);
        Assert.Equal(AdvancedPermissionsConstants.VerbRead, results[0].Verb);
    }

    /// <summary>
    /// Verifies that DeleteAsync does not throw when the specified entry does not exist.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_IsIdempotent_WhenEntryDoesNotExist()
    {
        // Should not throw when the entry is already absent
        await _repository.DeleteAsync(Guid.NewGuid(), "editors", AdvancedPermissionsConstants.VerbRead);
    }

    /// <summary>
    /// Verifies that DeleteAsync removes both entries when two entries share the same verb (dual-scope pattern).
    /// </summary>
    [Fact]
    public async Task DeleteAsync_RemovesBothDualEntries_WhenBothShareSameVerb()
    {
        var nodeKey = Guid.NewGuid();
        const string role = "editors";

        await _repository.SaveAsync(nodeKey, role,
        [
            (AdvancedPermissionsConstants.VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeOnly),
            (AdvancedPermissionsConstants.VerbDelete, PermissionState.Allow, PermissionScope.DescendantsOnly),
        ]);

        await _repository.DeleteAsync(nodeKey, role, AdvancedPermissionsConstants.VerbDelete);

        var results = await _repository.GetByNodeAndRoleAsync(nodeKey, role);
        Assert.Empty(results);
    }

    // -------------------------------------------------------------------------
    // DeleteAllForNodeAsync
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that DeleteAllForNodeAsync removes all entries for the node across all roles.
    /// </summary>
    [Fact]
    public async Task DeleteAllForNodeAsync_RemovesAllEntriesForNode_AcrossAllRoles()
    {
        var nodeKey = Guid.NewGuid();

        await _repository.SaveAsync(nodeKey, "editors",
        [
            (AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants),
        ]);

        await _repository.SaveAsync(nodeKey, "writers",
        [
            (AdvancedPermissionsConstants.VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeOnly),
        ]);

        await _repository.DeleteAllForNodeAsync(nodeKey);

        var results = await _repository.GetByNodeAsync(nodeKey);
        Assert.Empty(results);
    }

    /// <summary>
    /// Verifies that DeleteAllForNodeAsync leaves entries for other nodes untouched.
    /// </summary>
    [Fact]
    public async Task DeleteAllForNodeAsync_PreservesEntriesForOtherNodes()
    {
        var nodeToDelete = Guid.NewGuid();
        var nodeToKeep = Guid.NewGuid();

        await _repository.SaveAsync(nodeToDelete, "editors",
        [
            (AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants),
        ]);

        await _repository.SaveAsync(nodeToKeep, "editors",
        [
            (AdvancedPermissionsConstants.VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeOnly),
        ]);

        await _repository.DeleteAllForNodeAsync(nodeToDelete);

        var remainingResults = await _repository.GetByNodeAsync(nodeToKeep);
        Assert.Single(remainingResults);
    }

    /// <summary>
    /// Verifies that DeleteAllForNodeAsync does not throw when the node has no entries.
    /// </summary>
    [Fact]
    public async Task DeleteAllForNodeAsync_IsIdempotent_WhenNodeHasNoEntries()
    {
        // Should not throw when node has no entries
        await _repository.DeleteAllForNodeAsync(Guid.NewGuid());
    }

    // -------------------------------------------------------------------------
    // DeleteAllForRoleAsync
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that DeleteAllForRoleAsync removes all entries for the role across all nodes.
    /// </summary>
    [Fact]
    public async Task DeleteAllForRoleAsync_RemovesAllEntriesForRole_AcrossAllNodes()
    {
        var nodeKey1 = Guid.NewGuid();
        var nodeKey2 = Guid.NewGuid();

        await _repository.SaveAsync(nodeKey1, "editors",
        [
            (AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants),
        ]);

        await _repository.SaveAsync(nodeKey2, "editors",
        [
            (AdvancedPermissionsConstants.VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeOnly),
        ]);

        await _repository.SaveAsync(nodeKey1, "writers",
        [
            (AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants),
        ]);

        await _repository.DeleteAllForRoleAsync("editors");

        var editorsResults = await _repository.GetByRoleAsync("editors");
        Assert.Empty(editorsResults);

        var writersResults = await _repository.GetByRoleAsync("writers");
        Assert.Single(writersResults);
    }

    /// <summary>
    /// Verifies that DeleteAllForRoleAsync leaves entries for other roles untouched.
    /// </summary>
    [Fact]
    public async Task DeleteAllForRoleAsync_PreservesEntriesForOtherRoles()
    {
        var nodeKey = Guid.NewGuid();

        await _repository.SaveAsync(nodeKey, "editors",
        [
            (AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants),
        ]);

        await _repository.SaveAsync(nodeKey, "writers",
        [
            (AdvancedPermissionsConstants.VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeOnly),
        ]);

        await _repository.DeleteAllForRoleAsync("editors");

        var remainingResults = await _repository.GetByNodeAndRoleAsync(nodeKey, "writers");
        Assert.Single(remainingResults);
    }

    /// <summary>
    /// Verifies that DeleteAllForRoleAsync does not throw when the role has no entries.
    /// </summary>
    [Fact]
    public async Task DeleteAllForRoleAsync_IsIdempotent_WhenRoleHasNoEntries()
    {
        // Should not throw when role has no entries
        await _repository.DeleteAllForRoleAsync("nonexistent-role");
    }

    // -------------------------------------------------------------------------
    // Domain model mapping
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that all fields on a stored entry are correctly mapped to the domain model on retrieval.
    /// </summary>
    [Fact]
    public async Task SaveAndGet_PreservesAllFieldsCorrectly()
    {
        var nodeKey = Guid.NewGuid();
        const string role = AdvancedPermissionsConstants.EveryoneRoleAlias;

        await _repository.SaveAsync(nodeKey, role,
        [
            (AdvancedPermissionsConstants.VerbPublish, PermissionState.Deny, PermissionScope.DescendantsOnly),
        ]);

        var results = await _repository.GetByNodeAndRoleAsync(nodeKey, role);

        Assert.Single(results);
        var entry = results[0];
        Assert.NotEqual(Guid.Empty, entry.Id);
        Assert.Equal(nodeKey, entry.NodeKey);
        Assert.Equal(role, entry.RoleAlias);
        Assert.Equal(AdvancedPermissionsConstants.VerbPublish, entry.Verb);
        Assert.Equal(PermissionState.Deny, entry.State);
        Assert.Equal(PermissionScope.DescendantsOnly, entry.Scope);
    }
}

/// <summary>
/// An <see cref="IDbContextFactory{TContext}"/> that creates <see cref="AdvancedPermissionsDbContext"/>
/// instances from pre-configured options. Used in tests to reuse a shared SQLite in-memory connection.
/// </summary>
/// <param name="options">The pre-configured DbContext options to use for each created context.</param>
internal sealed class SingleConnectionDbContextFactory(DbContextOptions<AdvancedPermissionsDbContext> options)
    : IDbContextFactory<AdvancedPermissionsDbContext>
{
    /// <inheritdoc />
    public AdvancedPermissionsDbContext CreateDbContext() => new(options);
}
