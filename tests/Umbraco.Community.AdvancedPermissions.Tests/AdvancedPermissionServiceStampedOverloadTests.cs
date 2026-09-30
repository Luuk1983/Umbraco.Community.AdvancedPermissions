using Microsoft.Extensions.Logging;
using NSubstitute;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.Caching;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;
using Umbraco.Community.AdvancedPermissions.Services;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests that <see cref="AdvancedPermissionService"/> implements the stamped
/// <c>SaveEntriesAsync</c> overload itself, rather than inheriting the interface's default
/// implementation.
/// </summary>
/// <remarks>
/// The default implementation forwards to the unstamped member and ignores the stamp, so a service
/// relying on it would compile, run and save successfully while passing no stamp to the repository
/// — every save silently unchecked. These tests make that regression fail loudly.
/// </remarks>
public sealed class AdvancedPermissionServiceStampedOverloadTests
{
    /// <summary>The repository the service writes through.</summary>
    private readonly IAdvancedPermissionRepository _repository = Substitute.For<IAdvancedPermissionRepository>();

    /// <summary>The service under test, held as the interface every caller uses.</summary>
    private readonly IAdvancedPermissionService _sut;

    /// <summary>Initialises the service under test with no-op caches and substituted dependencies.</summary>
    public AdvancedPermissionServiceStampedOverloadTests()
    {
        _sut = new AdvancedPermissionService(
            _repository,
            Substitute.For<IPermissionResolver>(),
            Substitute.For<IUserService>(),
            new AdvancedPermissionCache(AppCaches.NoCache),
            Substitute.For<IEventAggregator>(),
            Substitute.For<ILogger<AdvancedPermissionService>>());
    }

    /// <summary>
    /// The interface's stamped overload must map to a method declared by the service. For an
    /// implementer that leaves it out the runtime maps it to the interface's own default, which is
    /// the case this test would catch.
    /// </summary>
    [Fact]
    public void StampedSaveEntriesAsync_IsImplementedByTheService_NotInheritedFromTheInterfaceDefault()
    {
        var map = typeof(AdvancedPermissionService).GetInterfaceMap(typeof(IAdvancedPermissionService));
        var index = Array.FindIndex(
            map.InterfaceMethods,
            m => m.Name == nameof(IAdvancedPermissionService.SaveEntriesAsync)
                 && m.GetParameters().Any(p => p.Name == "expectedStamp"));

        Assert.True(index >= 0, "The stamped SaveEntriesAsync overload was not found on the interface.");
        Assert.Equal(typeof(AdvancedPermissionService), map.TargetMethods[index].DeclaringType);
    }

    /// <summary>
    /// Behaviour, not just shape: called through the interface, the stamp must arrive at the
    /// repository, where the check is made. A default implementation would have dropped it.
    /// </summary>
    [Fact]
    public async Task StampedSaveEntriesAsync_ThroughTheInterface_PassesTheStampToTheRepository()
    {
        var nodeKey = Guid.NewGuid();

        await _sut.SaveEntriesAsync(nodeKey, "editors", [], "the-stamp");

        await _repository.Received(1).SaveAsync(
            nodeKey,
            "editors",
            Arg.Any<IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)>>(),
            "the-stamp",
            Arg.Any<CancellationToken>());
    }
}
