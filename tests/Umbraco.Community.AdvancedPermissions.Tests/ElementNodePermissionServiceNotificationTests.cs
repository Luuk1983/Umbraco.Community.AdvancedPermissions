using Microsoft.Extensions.Logging;
using NSubstitute;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.Caching;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;
using Umbraco.Community.AdvancedPermissions.Notifications;
using Umbraco.Community.AdvancedPermissions.Services;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Runs the shared write-invalidate-publish contract (<see cref="NodePermissionServiceNotificationTestsBase{TNotification}"/>)
/// against the library <see cref="ElementNodePermissionService"/>, which must publish
/// <see cref="ElementPermissionsChangedNotification"/>.
/// </summary>
/// <remarks>
/// The behaviour lives in the shared service base, not in either concrete service. If it were placed
/// in the content service instead, every content test would still pass while element permissions had
/// no notifications and no ordering guarantee — invisible until two people edited the Library at once.
/// These tests are what would catch it.
/// </remarks>
public sealed class ElementNodePermissionServiceNotificationTests()
    : NodePermissionServiceNotificationTestsBase<ElementPermissionsChangedNotification>(
        Substitute.For<ILogger<ElementNodePermissionService>>())
{
    /// <summary>The repository substitute the service writes through.</summary>
    private readonly IElementPermissionRepository _repository = Substitute.For<IElementPermissionRepository>();

    /// <inheritdoc />
    protected override INodePermissionRepository Repository => _repository;

    /// <inheritdoc />
    protected override string VerbA => AdvancedPermissionsConstants.VerbElementRead;

    /// <inheritdoc />
    protected override string VerbB => AdvancedPermissionsConstants.VerbElementDelete;

    /// <inheritdoc />
    protected override INodePermissionService CreateService(AppCaches appCaches) =>
        new ElementNodePermissionService(
            _repository,
            Substitute.For<IPermissionResolver>(),
            Substitute.For<IUserService>(),
            new ElementPermissionCache(appCaches),
            EventAggregator,
            (ILogger<ElementNodePermissionService>)Logger);

    /// <inheritdoc />
    protected override (Guid NodeKey, string RoleAlias, IReadOnlyList<string> Verbs) Describe(
        ElementPermissionsChangedNotification notification) =>
        (notification.NodeKey, notification.RoleAlias, notification.Verbs);

    /// <summary>
    /// The base serves two families, so publishing the right notification type is not a given. The
    /// element service must never announce a change as a content change, or the content editors would
    /// refetch on a library save and the Library editors would miss theirs.
    /// </summary>
    [Fact]
    public async Task SaveEntriesAsync_PublishesNoOtherFamilysNotification()
    {
        var sut = CreateService(AppCaches.NoCache);

        await sut.SaveEntriesAsync(
            Guid.NewGuid(), "editors", [(VerbA, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], (string?)null);

        await EventAggregator.DidNotReceive().PublishAsync(
            Arg.Any<AdvancedPermissionsChangedNotification>(), Arg.Any<CancellationToken>());
        await EventAggregator.DidNotReceive().PublishAsync(
            Arg.Any<DocTypePermissionsChangedNotification>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The same rule for a batch: every pair announces itself as an element change.
    /// </summary>
    [Fact]
    public async Task SaveManyAsync_PublishesNoOtherFamilysNotification()
    {
        var sut = CreateService(AppCaches.NoCache);

        await sut.SaveManyAsync(
        [
            (Guid.NewGuid(), "editors", [(VerbA, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], null),
        ]);

        await EventAggregator.DidNotReceive().PublishAsync(
            Arg.Any<AdvancedPermissionsChangedNotification>(), Arg.Any<CancellationToken>());
        await EventAggregator.DidNotReceive().PublishAsync(
            Arg.Any<DocTypePermissionsChangedNotification>(), Arg.Any<CancellationToken>());
    }
}
