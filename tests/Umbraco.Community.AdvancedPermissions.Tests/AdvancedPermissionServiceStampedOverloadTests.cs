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
/// Runs the stamped-overload contract (<see cref="NodePermissionServiceStampedOverloadTestsBase"/>)
/// against the content <see cref="AdvancedPermissionService"/>.
/// </summary>
public sealed class AdvancedPermissionServiceStampedOverloadTests : NodePermissionServiceStampedOverloadTestsBase
{
    /// <summary>The repository substitute the service writes through.</summary>
    private readonly IAdvancedPermissionRepository _repository = Substitute.For<IAdvancedPermissionRepository>();

    /// <summary>The concrete service the unstamped overload is called on.</summary>
    private readonly AdvancedPermissionService _concrete;

    /// <summary>Initialises the service under test with no-op caches and substituted dependencies.</summary>
    public AdvancedPermissionServiceStampedOverloadTests()
    {
        _concrete = new AdvancedPermissionService(
            _repository,
            Substitute.For<IPermissionResolver>(),
            Substitute.For<IUserService>(),
            new AdvancedPermissionCache(AppCaches.NoCache),
            Substitute.For<IEventAggregator>(),
            Substitute.For<ILogger<AdvancedPermissionService>>());
    }

    /// <inheritdoc />
    protected override INodePermissionRepository Repository => _repository;

    /// <inheritdoc />
    protected override Type ServiceType => typeof(AdvancedPermissionService);

    /// <inheritdoc />
    protected override string Verb => AdvancedPermissionsConstants.VerbRead;

    /// <inheritdoc />
    protected override INodePermissionService CreateService() => _concrete;

    /// <inheritdoc />
    protected override Task SaveUnstampedAsync(
        Guid nodeKey,
        string roleAlias,
        IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> entries) =>
        _concrete.SaveEntriesAsync(nodeKey, roleAlias, entries);
}
