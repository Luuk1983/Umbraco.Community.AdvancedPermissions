using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.Controllers;
using Umbraco.Community.AdvancedPermissions.Controllers.Models;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests the HTTP-level contract of <see cref="AdvancedPermissionsEffectiveController"/>.
/// These tests specifically cover the "unknown node key" contract: the backoffice may
/// query this endpoint with transient keys (e.g. drafts that have been pre-assigned a
/// Guid but not yet saved), and the endpoint must not 404 in that case — it should
/// return 200 with an empty permissions list so the client cache doesn't enter a
/// purge-and-retry loop.
/// </summary>
public sealed class AdvancedPermissionsEffectiveControllerTests
{
    private readonly IAdvancedPermissionService _permissionService = Substitute.For<IAdvancedPermissionService>();
    private readonly IEntityService _entityService = Substitute.For<IEntityService>();
    private readonly AdvancedPermissionsEffectiveController _sut;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdvancedPermissionsEffectiveControllerTests"/> class.
    /// </summary>
    public AdvancedPermissionsEffectiveControllerTests()
    {
        _sut = new AdvancedPermissionsEffectiveController(_permissionService, _entityService);
    }

    // ─── GetEffectiveForUser ─────────────────────────────────────────────────

    /// <summary>
    /// When the node key does not resolve to an existing content node, the endpoint
    /// must return 200 OK with an empty permissions list rather than 404. This lets
    /// the backoffice query transient draft keys (pre-save Guids) without triggering
    /// error-handling code paths.
    /// </summary>
    [Fact]
    public async Task GetEffectiveForUser_UnknownNodeKey_Returns200OkWithEmptyPermissions()
    {
        // Arrange — entityService returns no paths, meaning the node does not exist
        var userKey = Guid.NewGuid();
        var unknownNodeKey = Guid.NewGuid();
        _entityService
            .GetAllPaths(UmbracoObjectTypes.Document, Arg.Any<Guid[]>())
            .Returns([]);

        // Act
        var result = await _sut.GetEffectiveForUser(CancellationToken.None, userKey, unknownNodeKey);

        // Assert — must NOT be 404; must be 200 with empty permissions list
        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<EffectivePermissionsResponseModel>(ok.Value);
        Assert.Equal(unknownNodeKey, body.NodeKey);
        Assert.Empty(body.Permissions);
    }

    /// <summary>
    /// Contract: when the node path can't be resolved there is nothing meaningful to
    /// ask the resolver. Skipping the call avoids pointless work and keeps the
    /// response trivially cacheable by the client.
    /// </summary>
    [Fact]
    public async Task GetEffectiveForUser_UnknownNodeKey_DoesNotCallPermissionService()
    {
        // Contract: if we can't resolve the node's path, there's nothing meaningful to
        // ask the resolver. Skipping the call avoids pointless work and makes the
        // response trivially cacheable by the client.
        var userKey = Guid.NewGuid();
        var unknownNodeKey = Guid.NewGuid();
        _entityService
            .GetAllPaths(UmbracoObjectTypes.Document, Arg.Any<Guid[]>())
            .Returns([]);

        await _sut.GetEffectiveForUser(CancellationToken.None, userKey, unknownNodeKey);

        await _permissionService
            .DidNotReceive()
            .ResolveAllAsync(
                Arg.Any<Guid>(),
                Arg.Any<Guid>(),
                Arg.Any<IReadOnlyList<Guid>>(),
                Arg.Any<IEnumerable<string>?>(),
                Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Happy path: when the node exists the endpoint returns the resolver's result
    /// mapped into the response model.
    /// </summary>
    [Fact]
    public async Task GetEffectiveForUser_KnownNodeKey_Returns200OkWithResolvedPermissions()
    {
        // Arrange — entityService returns a valid single-node path, resolver returns one verb
        var userKey = Guid.NewGuid();
        var nodeKey = Guid.NewGuid();
        var entity = StubEntity(42, nodeKey);
        _entityService
            .GetAllPaths(UmbracoObjectTypes.Document, Arg.Any<Guid[]>())
            .Returns([new TreeEntityPath { Id = 42, Path = "42" }]);
        _entityService
            .GetAll(UmbracoObjectTypes.Document, Arg.Any<int[]>())
            .Returns([entity]);

        var resolved = new Dictionary<string, EffectivePermission>
        {
            ["Umb.Document.Read"] = new(
                Verb: "Umb.Document.Read",
                IsAllowed: true,
                IsExplicit: true,
                Reasoning: []),
        };
        _permissionService
            .ResolveAllAsync(userKey, nodeKey, Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<IEnumerable<string>?>(), Arg.Any<CancellationToken>())
            .Returns(resolved);

        // Act
        var result = await _sut.GetEffectiveForUser(CancellationToken.None, userKey, nodeKey);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<EffectivePermissionsResponseModel>(ok.Value);
        Assert.Equal(nodeKey, body.NodeKey);
        Assert.Single(body.Permissions);
        Assert.Equal("Umb.Document.Read", body.Permissions[0].Verb);
        Assert.True(body.Permissions[0].IsAllowed);
    }

    // ─── GetEffectiveForRole ─────────────────────────────────────────────────

    /// <summary>
    /// By-role variant of the unknown-node contract — must also return 200 OK with an
    /// empty permissions list instead of 404.
    /// </summary>
    [Fact]
    public async Task GetEffectiveForRole_UnknownNodeKey_Returns200OkWithEmptyPermissions()
    {
        var unknownNodeKey = Guid.NewGuid();
        _entityService
            .GetAllPaths(UmbracoObjectTypes.Document, Arg.Any<Guid[]>())
            .Returns([]);

        var result = await _sut.GetEffectiveForRole(CancellationToken.None, "admin", unknownNodeKey);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<EffectivePermissionsResponseModel>(ok.Value);
        Assert.Equal(unknownNodeKey, body.NodeKey);
        Assert.Empty(body.Permissions);
    }

    /// <summary>
    /// By-role variant: when the node can't be resolved, the resolver must not be
    /// invoked.
    /// </summary>
    [Fact]
    public async Task GetEffectiveForRole_UnknownNodeKey_DoesNotCallPermissionService()
    {
        var unknownNodeKey = Guid.NewGuid();
        _entityService
            .GetAllPaths(UmbracoObjectTypes.Document, Arg.Any<Guid[]>())
            .Returns([]);

        await _sut.GetEffectiveForRole(CancellationToken.None, "admin", unknownNodeKey);

        await _permissionService
            .DidNotReceive()
            .ResolveForRoleAsync(
                Arg.Any<string>(),
                Arg.Any<Guid>(),
                Arg.Any<IReadOnlyList<Guid>>(),
                Arg.Any<IEnumerable<string>?>(),
                Arg.Any<CancellationToken>());
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static IEntitySlim StubEntity(int id, Guid key)
    {
        var e = Substitute.For<IEntitySlim>();
        e.Id.Returns(id);
        e.Key.Returns(key);
        return e;
    }
}
