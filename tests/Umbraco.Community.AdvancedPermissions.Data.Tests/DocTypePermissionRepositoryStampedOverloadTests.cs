using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Exceptions;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;
using Umbraco.Community.AdvancedPermissions.Data.Repositories;

namespace Umbraco.Community.AdvancedPermissions.Data.Tests;

/// <summary>
/// Tests that <see cref="DocTypePermissionRepository"/> implements the stamped <c>SaveAsync</c>
/// overload itself, rather than inheriting the interface's default implementation, and that its
/// unstamped overload still writes without a check.
/// </summary>
/// <remarks>
/// The default implementation forwards to the unstamped member and ignores the stamp, so a
/// repository relying on it would compile, run and write successfully while performing no
/// concurrency check at all - the failure this suite exists to make impossible to introduce by
/// accident, for example by a refactor that deletes the override.
/// </remarks>
public sealed class DocTypePermissionRepositoryStampedOverloadTests : IAsyncLifetime
{
    /// <summary>The user group every test saves for.</summary>
    private const string Role = "editors";

    /// <summary>The in-memory database backing the repository under test.</summary>
    private InMemoryPermissionDatabase _database = null!;

    /// <summary>The concrete repository under test.</summary>
    private DocTypePermissionRepository _concrete = null!;

    /// <summary>The repository under test, held as the interface every caller uses.</summary>
    private IDocTypePermissionRepository _repository = null!;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        _database = await InMemoryPermissionDatabase.CreateAsync();
        _concrete = new DocTypePermissionRepository(_database.Factory);
        _repository = _concrete;
    }

    /// <inheritdoc />
    public async Task DisposeAsync() => await _database.DisposeAsync();

    /// <summary>Builds one replacement entry for the create-of-type verb.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The entry tuple in the shape the repository accepts.</returns>
    private static (string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride) Entry(PermissionState state) =>
        (AdvancedPermissionsConstants.VerbCreateOfType, state, PermissionScope.ThisNodeOnly, false);

    /// <summary>
    /// The interface's stamped overload must map to a method declared by the repository. For an
    /// implementer that leaves it out the runtime maps it to the interface's own default, which is
    /// the case this test would catch.
    /// </summary>
    [Fact]
    public void StampedSaveAsync_IsImplementedByTheRepository_NotInheritedFromTheInterfaceDefault()
    {
        var map = typeof(DocTypePermissionRepository).GetInterfaceMap(typeof(IDocTypePermissionRepository));
        var index = Array.FindIndex(
            map.InterfaceMethods,
            m => m.Name == nameof(IDocTypePermissionRepository.SaveAsync)
                 && m.GetParameters().Any(p => p.Name == "expectedStamp"));

        Assert.True(index >= 0, "The stamped SaveAsync overload was not found on the interface.");
        Assert.Equal(typeof(DocTypePermissionRepository), map.TargetMethods[index].DeclaringType);
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
        var typeKey = Guid.NewGuid();
        await _repository.SaveAsync(nodeKey, Role, typeKey, [Entry(PermissionState.Allow)], (string?)null);

        await Assert.ThrowsAsync<DocTypePermissionConcurrencyException>(() =>
            _repository.SaveAsync(nodeKey, Role, typeKey, [Entry(PermissionState.Deny)], "a-stamp-that-matches-nothing"));

        var stored = Assert.Single(await _repository.GetByRoleAndContentTypeAsync(Role, typeKey));
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
        var typeKey = Guid.NewGuid();
        await _concrete.SaveAsync(nodeKey, Role, typeKey, [Entry(PermissionState.Allow)]);

        await _concrete.SaveAsync(nodeKey, Role, typeKey, [Entry(PermissionState.Deny)]);

        var stored = Assert.Single(await _repository.GetByRoleAndContentTypeAsync(Role, typeKey));
        Assert.Equal(PermissionState.Deny, stored.State);
    }
}
