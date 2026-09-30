using Microsoft.EntityFrameworkCore;
using Umbraco.Community.AdvancedPermissions.Core.Exceptions;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;
using Umbraco.Community.AdvancedPermissions.Data.Context;

namespace Umbraco.Community.AdvancedPermissions.Data.Tests;

/// <summary>
/// Tests that a node-keyed repository implements the stamped <c>SaveAsync</c> overload itself,
/// rather than inheriting the interface's default implementation, and that its unstamped overload
/// still writes without a check. Written once and run for every family that shares the contract.
/// </summary>
/// <remarks>
/// <para>
/// The default implementation forwards to the unstamped member and ignores the stamp, so a
/// repository relying on it would compile, run and write successfully while performing no
/// concurrency check at all - the failure this suite exists to make impossible to introduce by
/// accident, for example by a refactor that deletes the override.
/// </para>
/// <para>
/// The implementation lives in the shared repository base, not in either concrete repository, so an
/// abstract base with one subclass per family is what proves both families inherit it.
/// </para>
/// </remarks>
public abstract class NodePermissionRepositoryStampedOverloadTestsBase : IAsyncLifetime
{
    /// <summary>The user group every test saves for.</summary>
    private const string Role = "editors";

    /// <summary>The in-memory database backing the repository under test.</summary>
    private InMemoryPermissionDatabase _database = null!;

    /// <summary>The repository under test, held as the interface every caller uses.</summary>
    private INodePermissionRepository _repository = null!;

    /// <summary>Gets a verb valid for the family under test.</summary>
    protected abstract string Verb { get; }

    /// <summary>Gets the concrete repository type under test.</summary>
    protected abstract Type RepositoryType { get; }

    /// <summary>Creates the concrete repository for the family under test.</summary>
    /// <param name="factory">A factory handing out contexts over the shared in-memory connection.</param>
    /// <returns>The repository to test.</returns>
    protected abstract INodePermissionRepository CreateRepository(IDbContextFactory<AdvancedPermissionsDbContext> factory);

    /// <summary>
    /// Saves through the family's concrete repository's unstamped overload. Done by the subclass,
    /// against its concrete type, because the unstamped member is obsolete on the interface and the
    /// concrete class is where the package's own unstamped implementation lives.
    /// </summary>
    /// <param name="nodeKey">The node key.</param>
    /// <param name="roleAlias">The user group alias.</param>
    /// <param name="entries">The replacement entries.</param>
    /// <returns>A task that completes when the save has finished.</returns>
    protected abstract Task SaveUnstampedAsync(
        Guid nodeKey,
        string roleAlias,
        IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> entries);

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        _database = await InMemoryPermissionDatabase.CreateAsync();
        _repository = CreateRepository(_database.Factory);
    }

    /// <inheritdoc />
    public async Task DisposeAsync() => await _database.DisposeAsync();

    /// <summary>Builds one replacement entry for the family's verb.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The entry tuple in the shape the repository accepts.</returns>
    private (string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride) Entry(PermissionState state) =>
        (Verb, state, PermissionScope.ThisNodeOnly, false);

    /// <summary>
    /// The interface's stamped overload must map to a method declared by the repository hierarchy.
    /// For an implementer that leaves it out the runtime maps it to the interface's own default,
    /// which is the case this test would catch.
    /// </summary>
    [Fact]
    public void StampedSaveAsync_IsImplementedByTheRepository_NotInheritedFromTheInterfaceDefault()
    {
        var map = RepositoryType.GetInterfaceMap(typeof(INodePermissionRepository));
        var index = Array.FindIndex(
            map.InterfaceMethods,
            m => m.Name == nameof(INodePermissionRepository.SaveAsync)
                 && m.GetParameters().Any(p => p.Name == "expectedStamp"));

        Assert.True(index >= 0, "The stamped SaveAsync overload was not found on the interface.");
        var target = map.TargetMethods[index];
        Assert.False(target.DeclaringType!.IsInterface, "The stamped overload maps to the interface default implementation.");
        Assert.True(target.DeclaringType.IsAssignableFrom(RepositoryType));
    }

    /// <summary>
    /// Behaviour, not just shape: called through the interface with a stamp that does not match
    /// what is stored, the repository must refuse and leave the stored entries alone. A default
    /// implementation would have written.
    /// </summary>
    [Fact]
    public async Task StampedSaveAsync_ThroughTheInterface_WithAStaleStamp_ThrowsAndWritesNothing()
    {
        var nodeKey = Guid.NewGuid();
        await _repository.SaveAsync(nodeKey, Role, [Entry(PermissionState.Allow)], (string?)null);

        await Assert.ThrowsAsync<PermissionConcurrencyException>(() =>
            _repository.SaveAsync(nodeKey, Role, [Entry(PermissionState.Deny)], "a-stamp-that-matches-nothing"));

        var stored = Assert.Single(await _repository.GetByNodeAndRoleAsync(nodeKey, Role));
        Assert.Equal(PermissionState.Allow, stored.State);
    }

    /// <summary>
    /// The package's own unstamped overload keeps the behaviour it always had: it writes, replacing
    /// what is there, with no concurrency check.
    /// </summary>
    [Fact]
    public async Task UnstampedSaveAsync_OnTheConcreteRepository_ReplacesEntriesWithoutAnyCheck()
    {
        var nodeKey = Guid.NewGuid();
        await SaveUnstampedAsync(nodeKey, Role, [Entry(PermissionState.Allow)]);

        await SaveUnstampedAsync(nodeKey, Role, [Entry(PermissionState.Deny)]);

        var stored = Assert.Single(await _repository.GetByNodeAndRoleAsync(nodeKey, Role));
        Assert.Equal(PermissionState.Deny, stored.State);
    }
}
