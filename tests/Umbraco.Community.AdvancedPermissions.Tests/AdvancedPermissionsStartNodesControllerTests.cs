using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Umbraco.Community.AdvancedPermissions.Controllers;
using Umbraco.Community.AdvancedPermissions.Controllers.Models;
using Umbraco.Community.AdvancedPermissions.Services.StartNodes;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>Tests the HTTP contract of <see cref="AdvancedPermissionsStartNodesController"/>.</summary>
public sealed class AdvancedPermissionsStartNodesControllerTests
{
    /// <summary>The start-node service substitute.</summary>
    private readonly IStartNodeAccessService _startNodes = Substitute.For<IStartNodeAccessService>();

    /// <summary>The system under test.</summary>
    private readonly AdvancedPermissionsStartNodesController _sut;

    /// <summary>Initializes a new instance of the <see cref="AdvancedPermissionsStartNodesControllerTests"/> class.</summary>
    public AdvancedPermissionsStartNodesControllerTests() =>
        _sut = new AdvancedPermissionsStartNodesController(_startNodes);

    /// <summary>An unknown user is a 404.</summary>
    [Fact]
    public async Task GetForUser_UnknownUser_Returns404()
    {
        _startNodes.GetForUserAsync(Arg.Any<Guid>(), StartNodeTree.Content).Returns((StartNodeSnapshot?)null);

        Assert.IsType<NotFoundObjectResult>(await _sut.GetStartNodesForUser(Guid.NewGuid()));
    }

    /// <summary>An unknown user group is a 404.</summary>
    [Fact]
    public async Task GetForRole_UnknownGroup_Returns404()
    {
        _startNodes.GetForUserGroupAsync("nope", StartNodeTree.Content).Returns((StartNodeSnapshot?)null);

        Assert.IsType<NotFoundObjectResult>(await _sut.GetStartNodesForRole("nope"));
    }

    /// <summary>The snapshot maps to keys, names and string statuses; the root is flagged, not named.</summary>
    [Fact]
    public async Task GetForUser_MapsSnapshot()
    {
        var homeKey = Guid.NewGuid();
        var nodes = new Dictionary<int, StartNodeInfo>
        {
            [-1] = new(-1, Guid.Empty, string.Empty, "-1"),
            [10] = new(10, homeKey, "Home", "-1,10"),
        };
        var editors = new StartNodeSource(StartNodeSourceKind.UserGroup, "editors", "Editors", 10);
        var admin = new StartNodeSource(StartNodeSourceKind.UserGroup, "admin", "Administrators", -1);
        var snapshot = new StartNodeSnapshot(
            StartNodeTree.Content,
            StartNodeSubjectState.Restricted,
            [10],
            nodes,
            [
                new StartNodeSourceResult(editors, StartNodeSourceStatus.InUse, null),
                new StartNodeSourceResult(admin, StartNodeSourceStatus.ReplacedByUserStartNode, 10),
            ]);
        _startNodes.GetForUserAsync(Arg.Any<Guid>(), StartNodeTree.Content).Returns(snapshot);

        var ok = Assert.IsType<OkObjectResult>(await _sut.GetStartNodesForUser(Guid.NewGuid()));
        var body = Assert.IsType<StartNodesResponseModel>(ok.Value);

        Assert.Equal("Restricted", body.State);
        var startNode = Assert.Single(body.StartNodes);
        Assert.Equal(homeKey, startNode.Key);
        Assert.Equal("Home", startNode.Name);

        Assert.Equal("InUse", body.Sources[0].Status);
        Assert.Equal(homeKey, body.Sources[0].StartNodeKey);
        Assert.False(body.Sources[0].IsRoot);

        Assert.True(body.Sources[1].IsRoot);
        Assert.Null(body.Sources[1].StartNodeKey);
        Assert.Equal("ReplacedByUserStartNode", body.Sources[1].Status);
        Assert.Equal("Home", body.Sources[1].RelatedNodeName);
    }

    /// <summary>A root-access subject lists no start nodes; the state says it all.</summary>
    [Fact]
    public async Task GetForRole_RootAccess_ListsNoStartNodes()
    {
        var nodes = new Dictionary<int, StartNodeInfo> { [-1] = new(-1, Guid.Empty, string.Empty, "-1") };
        var source = new StartNodeSource(StartNodeSourceKind.UserGroup, "admin", "Administrators", -1);
        _startNodes.GetForUserGroupAsync("admin", StartNodeTree.Content).Returns(new StartNodeSnapshot(
            StartNodeTree.Content,
            StartNodeSubjectState.RootAccess, [-1], nodes, [new StartNodeSourceResult(source, StartNodeSourceStatus.InUse, null)]));

        var ok = Assert.IsType<OkObjectResult>(await _sut.GetStartNodesForRole("admin"));
        var body = Assert.IsType<StartNodesResponseModel>(ok.Value);

        Assert.Equal("RootAccess", body.State);
        Assert.Empty(body.StartNodes);
    }

    /// <summary>The Library endpoints read Library start nodes.</summary>
    [Fact]
    public async Task GetElementForUser_ReadsLibraryTree()
    {
        var userKey = Guid.NewGuid();
        var folderKey = Guid.NewGuid();
        var nodes = new Dictionary<int, StartNodeInfo>
        {
            [-1] = new(-1, Guid.Empty, string.Empty, "-1"),
            [60] = new(60, folderKey, "Shared", "-1,60"),
        };
        var source = new StartNodeSource(StartNodeSourceKind.UserGroup, "editors", "Editors", 60);
        _startNodes.GetForUserAsync(userKey, StartNodeTree.Library).Returns(new StartNodeSnapshot(
            StartNodeTree.Library, StartNodeSubjectState.Restricted, [60], nodes,
            [new StartNodeSourceResult(source, StartNodeSourceStatus.InUse, null)]));

        var ok = Assert.IsType<OkObjectResult>(await _sut.GetElementStartNodesForUser(userKey));
        var body = Assert.IsType<StartNodesResponseModel>(ok.Value);

        Assert.Equal("Restricted", body.State);
        Assert.Equal("Shared", Assert.Single(body.StartNodes).Name);
    }

    /// <summary>An unknown user group on the Library endpoint is a 404.</summary>
    [Fact]
    public async Task GetElementForRole_UnknownGroup_Returns404()
    {
        _startNodes.GetForUserGroupAsync("nope", StartNodeTree.Library).Returns((StartNodeSnapshot?)null);

        Assert.IsType<NotFoundObjectResult>(await _sut.GetElementStartNodesForRole("nope"));
    }
}
