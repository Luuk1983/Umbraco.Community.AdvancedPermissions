using Microsoft.Extensions.Logging;
using NSubstitute;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.Caching;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;
using Umbraco.Community.AdvancedPermissions.Services;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests that <see cref="DocTypePermissionService"/> implements the stamped
/// <c>SaveEditorEntriesAsync</c> overload itself, rather than inheriting the interface's default
/// implementation, and that its unstamped overload still saves - with no stamp.
/// </summary>
/// <remarks>
/// The default implementation forwards to the unstamped member and ignores the stamp, so a service
/// relying on it would compile, run and save successfully while passing no stamp to the repository
/// - every save silently unchecked. These tests make that regression fail loudly.
/// </remarks>
public sealed class DocTypePermissionServiceStampedOverloadTests
{
    /// <summary>The repository substitute the service writes through.</summary>
    private readonly IDocTypePermissionRepository _repository = Substitute.For<IDocTypePermissionRepository>();

    /// <summary>The service under test, held as the concrete type so the unstamped overload is reachable.</summary>
    private readonly DocTypePermissionService _concrete;

    /// <summary>Initialises the service under test with no-op caches and substituted dependencies.</summary>
    public DocTypePermissionServiceStampedOverloadTests()
    {
        _concrete = new DocTypePermissionService(
            _repository,
            Substitute.For<IDocTypePermissionResolver>(),
            Substitute.For<IUserService>(),
            Substitute.For<IContentTypeService>(),
            new DocTypePermissionCache(AppCaches.NoCache),
            Substitute.For<IEventAggregator>(),
            Substitute.For<ILogger<DocTypePermissionService>>());
    }

    /// <summary>
    /// The interface's stamped overload must map to a method declared by the service. For an
    /// implementer that leaves it out the runtime maps it to the interface's own default, which is
    /// the case this test would catch.
    /// </summary>
    [Fact]
    public void StampedSaveEditorEntriesAsync_IsImplementedByTheService_NotInheritedFromTheInterfaceDefault()
    {
        var map = typeof(DocTypePermissionService).GetInterfaceMap(typeof(IDocTypePermissionService));
        var index = Array.FindIndex(
            map.InterfaceMethods,
            m => m.Name == nameof(IDocTypePermissionService.SaveEditorEntriesAsync)
                 && m.GetParameters().Any(p => p.Name == "expectedStamp"));

        Assert.True(index >= 0, "The stamped SaveEditorEntriesAsync overload was not found on the interface.");
        Assert.Equal(typeof(DocTypePermissionService), map.TargetMethods[index].DeclaringType);
    }

    /// <summary>
    /// Behaviour, not just shape: called through the interface, the stamp must arrive at the
    /// repository, where the check is made. A default implementation would have dropped it.
    /// </summary>
    [Fact]
    public async Task StampedSaveEditorEntriesAsync_ThroughTheInterface_PassesTheStampToTheRepository()
    {
        IDocTypePermissionService sut = _concrete;
        var nodeKey = Guid.NewGuid();
        var typeKey = Guid.NewGuid();

        await sut.SaveEditorEntriesAsync(nodeKey, "editors", typeKey, [], "the-stamp");

        await _repository.Received(1).SaveAsync(
            nodeKey,
            "editors",
            typeKey,
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
    public async Task UnstampedSaveEditorEntriesAsync_OnTheConcreteService_SavesWithANullStamp()
    {
        var nodeKey = Guid.NewGuid();
        var typeKey = Guid.NewGuid();

        await _concrete.SaveEditorEntriesAsync(
            nodeKey,
            "editors",
            typeKey,
            [(AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)]);

        await _repository.Received(1).SaveAsync(
            nodeKey,
            "editors",
            typeKey,
            Arg.Any<IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)>>(),
            (string?)null,
            Arg.Any<CancellationToken>());
    }
}
