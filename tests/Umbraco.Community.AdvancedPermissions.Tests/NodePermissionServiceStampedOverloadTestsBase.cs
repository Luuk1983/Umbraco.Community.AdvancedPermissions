using NSubstitute;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests that a node-keyed service implements the stamped <c>SaveEntriesAsync</c> overload itself,
/// rather than inheriting the interface's default implementation, and that its unstamped overload
/// still saves - with no stamp. Written once and run for every family that shares the contract.
/// </summary>
/// <remarks>
/// <para>
/// The default implementation forwards to the unstamped member and ignores the stamp, so a service
/// relying on it would compile, run and save successfully while passing no stamp to the repository
/// - every save silently unchecked. These tests make that regression fail loudly.
/// </para>
/// <para>
/// The implementation lives in the shared service base, not in either concrete service, so an
/// abstract base with one subclass per family is what proves both families inherit it.
/// </para>
/// </remarks>
public abstract class NodePermissionServiceStampedOverloadTestsBase
{
    /// <summary>Gets the repository substitute the service under test writes through.</summary>
    protected abstract INodePermissionRepository Repository { get; }

    /// <summary>Gets the concrete service type under test.</summary>
    protected abstract Type ServiceType { get; }

    /// <summary>Gets a verb valid for the family under test.</summary>
    protected abstract string Verb { get; }

    /// <summary>Builds the service under test, held as the interface every caller uses.</summary>
    /// <returns>The service under test.</returns>
    protected abstract INodePermissionService CreateService();

    /// <summary>
    /// Saves through the family's concrete service's unstamped overload. Done by the subclass,
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

    /// <summary>
    /// The interface's stamped overload must map to a method declared by the service hierarchy. For
    /// an implementer that leaves it out the runtime maps it to the interface's own default, which
    /// is the case this test would catch.
    /// </summary>
    [Fact]
    public void StampedSaveEntriesAsync_IsImplementedByTheService_NotInheritedFromTheInterfaceDefault()
    {
        var map = ServiceType.GetInterfaceMap(typeof(INodePermissionService));
        var index = Array.FindIndex(
            map.InterfaceMethods,
            m => m.Name == nameof(INodePermissionService.SaveEntriesAsync)
                 && m.GetParameters().Any(p => p.Name == "expectedStamp"));

        Assert.True(index >= 0, "The stamped SaveEntriesAsync overload was not found on the interface.");
        var target = map.TargetMethods[index];
        Assert.False(target.DeclaringType!.IsInterface, "The stamped overload maps to the interface default implementation.");
        Assert.True(target.DeclaringType.IsAssignableFrom(ServiceType));
    }

    /// <summary>
    /// Behaviour, not just shape: called through the interface, the stamp must arrive at the
    /// repository, where the check is made. A default implementation would have dropped it.
    /// </summary>
    [Fact]
    public async Task StampedSaveEntriesAsync_ThroughTheInterface_PassesTheStampToTheRepository()
    {
        var sut = CreateService();
        var nodeKey = Guid.NewGuid();

        await sut.SaveEntriesAsync(nodeKey, "editors", [], "the-stamp");

        await Repository.Received(1).SaveAsync(
            nodeKey,
            "editors",
            Arg.Any<IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)>>(),
            "the-stamp",
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The package's own unstamped overload keeps saving, and reaches the repository through the
    /// stamped overload with no stamp - so it is the checked path with the check switched off, not a
    /// second write path.
    /// </summary>
    [Fact]
    public async Task UnstampedSaveEntriesAsync_OnTheConcreteService_SavesWithANullStamp()
    {
        var nodeKey = Guid.NewGuid();

        await SaveUnstampedAsync(nodeKey, "editors", [(Verb, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)]);

        await Repository.Received(1).SaveAsync(
            nodeKey,
            "editors",
            Arg.Any<IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)>>(),
            (string?)null,
            Arg.Any<CancellationToken>());
    }
}
