using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.Controllers;
using Umbraco.Community.AdvancedPermissions.Controllers.Models;
using Umbraco.Community.AdvancedPermissions.Abstractions.Constants;
using Umbraco.Community.AdvancedPermissions.Abstractions.Interfaces;
using Umbraco.Community.AdvancedPermissions.Abstractions.Models;
using Umbraco.Community.AdvancedPermissions.Services.StartNodes;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests that the Library effective endpoints report whether the subject can reach the node through
/// its Library start nodes (issue #66).
/// </summary>
public sealed class ElementPermissionsEffectiveControllerStartNodeTests
{
    /// <summary>The element permission service substitute.</summary>
    private readonly IElementNodePermissionService _permissionService = Substitute.For<IElementNodePermissionService>();

    /// <summary>The entity service substitute.</summary>
    private readonly IEntityService _entityService = Substitute.For<IEntityService>();

    /// <summary>The start-node service substitute.</summary>
    private readonly IStartNodeAccessService _startNodes = Substitute.For<IStartNodeAccessService>();

    /// <summary>The system under test.</summary>
    private readonly ElementPermissionsEffectiveController _sut;

    /// <summary>A restricted Library scope used as the classification input.</summary>
    private readonly StartNodeScope _restricted =
        StartNodeScope.NotApplicableFor(StartNodeTree.Library) with { State = StartNodeSubjectState.Restricted };

    /// <summary>Initializes a new instance of the <see cref="ElementPermissionsEffectiveControllerStartNodeTests"/> class.</summary>
    public ElementPermissionsEffectiveControllerStartNodeTests() =>
        _sut = new ElementPermissionsEffectiveController(
            _permissionService, _entityService, _startNodes);

    /// <summary>A known element carries the Library classification for the user.</summary>
    [Fact]
    public async Task GetEffectiveForUser_KnownElement_CarriesLibraryClassification()
    {
        var userKey = Guid.NewGuid();
        var nodeKey = SeedElement(123, "-1,60,123");
        _startNodes.GetScopeForUserAsync(userKey, StartNodeTree.Library).Returns(_restricted);
        _startNodes.Classify(_restricted, "-1,60,123").Returns(StartNodeAccess.NotVisible);

        var ok = Assert.IsType<OkObjectResult>(await _sut.GetEffectiveForUser(CancellationToken.None, userKey, nodeKey));

        Assert.Equal("NotVisible", Assert.IsType<EffectivePermissionsResponseModel>(ok.Value).StartNodeAccess);
    }

    /// <summary>The virtual root classifies the Library root path <c>-1</c>.</summary>
    [Fact]
    public async Task GetEffectiveForRole_VirtualRoot_ClassifiesLibraryRoot()
    {
        _startNodes.GetScopeForUserGroupAsync("editors", StartNodeTree.Library).Returns(_restricted);
        _startNodes.Classify(_restricted, "-1").Returns(StartNodeAccess.PathOnly);
        _permissionService
            .ResolveForRoleAsync("editors", AdvancedPermissionsConstants.VirtualRootNodeKey, Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<IEnumerable<string>?>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, EffectivePermission>());

        var ok = Assert.IsType<OkObjectResult>(
            await _sut.GetEffectiveForRole(CancellationToken.None, "editors", AdvancedPermissionsConstants.VirtualRootNodeKey));

        Assert.Equal("PathOnly", Assert.IsType<EffectivePermissionsResponseModel>(ok.Value).StartNodeAccess);
    }

    /// <summary>Review focus 4: an unknown user is never shown as blocked.</summary>
    [Fact]
    public async Task GetEffectiveForUser_UnknownUser_IsAccessible()
    {
        var nodeKey = SeedElement(123, "-1,60,123");
        _startNodes.GetScopeForUserAsync(Arg.Any<Guid>(), StartNodeTree.Library).Returns((StartNodeScope?)null);

        var ok = Assert.IsType<OkObjectResult>(await _sut.GetEffectiveForUser(CancellationToken.None, Guid.NewGuid(), nodeKey));

        Assert.Equal("Accessible", Assert.IsType<EffectivePermissionsResponseModel>(ok.Value).StartNodeAccess);
    }

    /// <summary>Review focus 4: the All Users group is never shown as blocked.</summary>
    [Fact]
    public async Task GetEffectiveForRole_Everyone_IsAccessible()
    {
        var nodeKey = SeedElement(123, "-1,60,123");
        var notApplicable = StartNodeScope.NotApplicableFor(StartNodeTree.Library);
        _startNodes.GetScopeForUserGroupAsync(AdvancedPermissionsConstants.EveryoneRoleAlias, StartNodeTree.Library).Returns(notApplicable);
        _startNodes.Classify(notApplicable, Arg.Any<string>()).Returns(StartNodeAccess.Accessible);

        var ok = Assert.IsType<OkObjectResult>(
            await _sut.GetEffectiveForRole(CancellationToken.None, AdvancedPermissionsConstants.EveryoneRoleAlias, nodeKey));

        Assert.Equal("Accessible", Assert.IsType<EffectivePermissionsResponseModel>(ok.Value).StartNodeAccess);
    }

    /// <summary>An unknown Library node is reported as not visible.</summary>
    [Fact]
    public async Task GetEffectiveForUser_UnknownNode_IsNotVisible()
    {
        _entityService.GetAllPaths(Arg.Any<UmbracoObjectTypes>(), Arg.Any<Guid[]>()).Returns([]);

        var ok = Assert.IsType<OkObjectResult>(await _sut.GetEffectiveForUser(CancellationToken.None, Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal("NotVisible", Assert.IsType<EffectivePermissionsResponseModel>(ok.Value).StartNodeAccess);
    }

    /// <summary>
    /// The element's path is read once per row: the path used to resolve the permissions is reused to
    /// classify the element against the subject's Library start nodes.
    /// </summary>
    [Fact]
    public async Task GetEffectiveForUser_KnownElement_ReadsPathOnce()
    {
        var userKey = Guid.NewGuid();
        var nodeKey = SeedElement(123, "-1,60,123");
        _startNodes.GetScopeForUserAsync(userKey, StartNodeTree.Library).Returns(_restricted);

        await _sut.GetEffectiveForUser(CancellationToken.None, userKey, nodeKey);

        _entityService.Received(1).GetAllPaths(UmbracoObjectTypes.Element, Arg.Any<Guid[]>());
    }

    /// <summary>Seeds one element (not a folder) inside folder 60, and an empty resolver answer.</summary>
    /// <param name="id">The element id.</param>
    /// <param name="path">The element path.</param>
    /// <returns>The element key.</returns>
    private Guid SeedElement(int id, string path)
    {
        var nodeKey = Guid.NewGuid();
        // Build every stub entity first: configuring a substitute inside a .Returns(...) argument
        // corrupts NSubstitute's last-call tracking.
        var folder = Substitute.For<IEntitySlim>();
        folder.Id.Returns(60);
        folder.Key.Returns(Guid.NewGuid());
        var element = Substitute.For<IEntitySlim>();
        element.Id.Returns(id);
        element.Key.Returns(nodeKey);
        _entityService.GetAllPaths(UmbracoObjectTypes.Element, Arg.Any<Guid[]>())
            .Returns([new TreeEntityPath { Id = id, Key = nodeKey, Path = path }]);
        _entityService.GetAll(Arg.Any<IEnumerable<UmbracoObjectTypes>>(), Arg.Any<int[]>()).Returns([folder, element]);
        _permissionService
            .ResolveAllAsync(Arg.Any<Guid>(), nodeKey, Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<IEnumerable<string>?>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, EffectivePermission>());
        _permissionService
            .ResolveForRoleAsync(Arg.Any<string>(), nodeKey, Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<IEnumerable<string>?>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, EffectivePermission>());
        return nodeKey;
    }
}
