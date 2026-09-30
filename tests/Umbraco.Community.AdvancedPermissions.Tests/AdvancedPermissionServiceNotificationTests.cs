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
/// against the content <see cref="AdvancedPermissionService"/>, which must publish
/// <see cref="AdvancedPermissionsChangedNotification"/>.
/// </summary>
public sealed class AdvancedPermissionServiceNotificationTests()
    : NodePermissionServiceNotificationTestsBase<AdvancedPermissionsChangedNotification>(
        Substitute.For<ILogger<AdvancedPermissionService>>())
{
    /// <summary>The repository substitute the service writes through.</summary>
    private readonly IAdvancedPermissionRepository _repository = Substitute.For<IAdvancedPermissionRepository>();

    /// <inheritdoc />
    protected override INodePermissionRepository Repository => _repository;

    /// <inheritdoc />
    protected override string VerbA => AdvancedPermissionsConstants.VerbRead;

    /// <inheritdoc />
    protected override string VerbB => AdvancedPermissionsConstants.VerbDelete;

    /// <inheritdoc />
    protected override INodePermissionService CreateService(AppCaches appCaches) =>
        new AdvancedPermissionService(
            _repository,
            Substitute.For<IPermissionResolver>(),
            Substitute.For<IUserService>(),
            new AdvancedPermissionCache(appCaches),
            EventAggregator,
            (ILogger<AdvancedPermissionService>)Logger);

    /// <inheritdoc />
    protected override (Guid NodeKey, string RoleAlias, IReadOnlyList<string> Verbs) Describe(
        AdvancedPermissionsChangedNotification notification) =>
        (notification.NodeKey, notification.RoleAlias, notification.Verbs);

    /// <summary>
    /// The base serves two families, so publishing the right notification type is not a given. The
    /// content service must never announce a change as a library element change, or the Library
    /// editors would refetch on a content save and the content editors would miss theirs.
    /// </summary>
    [Fact]
    public async Task SaveEntriesAsync_PublishesNoOtherFamilysNotification()
    {
        var sut = CreateService(AppCaches.NoCache);

        await sut.SaveEntriesAsync(
            Guid.NewGuid(), "editors", [(VerbA, PermissionState.Allow, PermissionScope.ThisNodeOnly, false)], (string?)null);

        await EventAggregator.DidNotReceive().PublishAsync(
            Arg.Any<ElementPermissionsChangedNotification>(), Arg.Any<CancellationToken>());
        await EventAggregator.DidNotReceive().PublishAsync(
            Arg.Any<DocTypePermissionsChangedNotification>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The same rule for a batch: every pair announces itself as a content change, and never as a
    /// library element or document-type change.
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
            Arg.Any<ElementPermissionsChangedNotification>(), Arg.Any<CancellationToken>());
        await EventAggregator.DidNotReceive().PublishAsync(
            Arg.Any<DocTypePermissionsChangedNotification>(), Arg.Any<CancellationToken>());
    }
}
