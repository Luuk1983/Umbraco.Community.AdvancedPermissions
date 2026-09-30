using Microsoft.Extensions.Logging;
using NSubstitute;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.Caching;
using Umbraco.Community.AdvancedPermissions.Core.Exceptions;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;
using Umbraco.Community.AdvancedPermissions.Notifications;
using Umbraco.Community.AdvancedPermissions.Services;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests that <see cref="DocTypePermissionService"/> implements the stamped
/// <c>SaveEditorEntriesAsync</c> overload itself, rather than inheriting the interface's default
/// implementation, and that it lets a stale write's exception through untouched.
/// </summary>
/// <remarks>
/// The default implementation forwards to the unstamped member and ignores the stamp, so a service
/// relying on it would compile, run and save successfully while passing no stamp to the repository
/// — every save silently unchecked. These tests make that regression fail loudly.
/// </remarks>
public sealed class DocTypePermissionServiceStampedOverloadTests
{
    /// <summary>The repository the service writes through.</summary>
    private readonly IDocTypePermissionRepository _repository = Substitute.For<IDocTypePermissionRepository>();

    /// <summary>The aggregator the service publishes change notifications through.</summary>
    private readonly IEventAggregator _eventAggregator = Substitute.For<IEventAggregator>();

    /// <summary>The service under test, held as the interface every caller uses.</summary>
    private readonly IDocTypePermissionService _sut;

    /// <summary>Initialises the service under test with no-op caches and substituted dependencies.</summary>
    public DocTypePermissionServiceStampedOverloadTests()
    {
        _sut = new DocTypePermissionService(
            _repository,
            Substitute.For<IDocTypePermissionResolver>(),
            Substitute.For<IUserService>(),
            new DocTypePermissionCache(AppCaches.NoCache),
            _eventAggregator,
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
        var nodeKey = Guid.NewGuid();
        var contentTypeKey = Guid.NewGuid();

        await _sut.SaveEditorEntriesAsync(nodeKey, "editors", contentTypeKey, [], "the-stamp");

        await _repository.Received(1).SaveAsync(
            nodeKey,
            "editors",
            contentTypeKey,
            Arg.Any<IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)>>(),
            "the-stamp",
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The unstamped member still works, and reaches the repository with a null stamp — no check —
    /// so a caller written before the stamped overload existed behaves exactly as it did.
    /// </summary>
    [Fact]
    public async Task UnstampedSaveEditorEntriesAsync_PassesANullStampToTheRepository()
    {
        var nodeKey = Guid.NewGuid();
        var contentTypeKey = Guid.NewGuid();
        var service = new DocTypePermissionService(
            _repository,
            Substitute.For<IDocTypePermissionResolver>(),
            Substitute.For<IUserService>(),
            new DocTypePermissionCache(AppCaches.NoCache),
            _eventAggregator,
            Substitute.For<ILogger<DocTypePermissionService>>());

        await service.SaveEditorEntriesAsync(nodeKey, "editors", contentTypeKey, []);

        await _repository.Received(1).SaveAsync(
            nodeKey,
            "editors",
            contentTypeKey,
            Arg.Any<IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)>>(),
            (string?)null,
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// When the repository refuses a stale write, nothing was written, so nothing may be announced
    /// and the exception must reach the caller untouched: the controller turns it into the 409.
    /// </summary>
    [Fact]
    public async Task StampedSaveEditorEntriesAsync_RepositoryReportsConflict_PropagatesWithoutPublishing()
    {
        var nodeKey = Guid.NewGuid();
        var contentTypeKey = Guid.NewGuid();
        var conflict = new DocTypePermissionConcurrencyException(
            [new DocTypePermissionConflict(nodeKey, "editors", contentTypeKey, [], "current")]);
        _repository
            .SaveAsync(
                Arg.Any<Guid>(),
                Arg.Any<string>(),
                Arg.Any<Guid>(),
                Arg.Any<IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)>>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(conflict));

        var thrown = await Assert.ThrowsAsync<DocTypePermissionConcurrencyException>(() =>
            _sut.SaveEditorEntriesAsync(nodeKey, "editors", contentTypeKey, [], "stale"));

        Assert.Same(conflict, thrown);
        await _eventAggregator.DidNotReceiveWithAnyArgs().PublishAsync(
            Arg.Any<DocTypePermissionsChangedNotification>(), Arg.Any<CancellationToken>());
    }
}
