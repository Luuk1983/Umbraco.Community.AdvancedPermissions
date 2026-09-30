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
/// against the library <see cref="ElementNodePermissionService"/>, so the shared implementation is
/// proven for both families rather than for whichever one was tested first.
/// </summary>
public sealed class ElementNodePermissionServiceStampedOverloadTests : NodePermissionServiceStampedOverloadTestsBase
{
    /// <summary>The repository substitute the service writes through.</summary>
    private readonly IElementPermissionRepository _repository = Substitute.For<IElementPermissionRepository>();

    /// <summary>The concrete service the unstamped overload is called on.</summary>
    private readonly ElementNodePermissionService _concrete;

    /// <summary>Initialises the service under test with no-op caches and substituted dependencies.</summary>
    public ElementNodePermissionServiceStampedOverloadTests()
    {
        _concrete = new ElementNodePermissionService(
            _repository,
            Substitute.For<IPermissionResolver>(),
            Substitute.For<IUserService>(),
            new ElementPermissionCache(AppCaches.NoCache),
            Substitute.For<IEventAggregator>(),
            Substitute.For<ILogger<ElementNodePermissionService>>());
    }

    /// <inheritdoc />
    protected override INodePermissionRepository Repository => _repository;

    /// <inheritdoc />
    protected override Type ServiceType => typeof(ElementNodePermissionService);

    /// <inheritdoc />
    protected override string Verb => AdvancedPermissionsConstants.VerbElementRead;

    /// <inheritdoc />
    protected override INodePermissionService CreateService() => _concrete;

    /// <inheritdoc />
    protected override Task SaveUnstampedAsync(
        Guid nodeKey,
        string roleAlias,
        IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> entries) =>
        _concrete.SaveEntriesAsync(nodeKey, roleAlias, entries);
}
