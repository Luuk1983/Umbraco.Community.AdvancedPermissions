using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.Controllers;
using Umbraco.Community.AdvancedPermissions.Controllers.Models;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Services.StartNodes;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests that <c>GET doc-type-permissions/audit-for-node</c> reports whether the subject can reach the
/// parent node through its start nodes (issue #66). Creating under a node needs access to that node.
/// </summary>
public sealed class DocTypeAuditStartNodeTests
{
    /// <summary>The entity service substitute.</summary>
    private readonly IEntityService _entityService = Substitute.For<IEntityService>();

    /// <summary>The user service substitute.</summary>
    private readonly IUserService _userService = Substitute.For<IUserService>();

    /// <summary>The start-node service substitute.</summary>
    private readonly IStartNodeAccessService _startNodes = Substitute.For<IStartNodeAccessService>();

    /// <summary>The system under test.</summary>
    private readonly DocTypePermissionsController _sut;

    /// <summary>Initializes a new instance of the <see cref="DocTypeAuditStartNodeTests"/> class.</summary>
    public DocTypeAuditStartNodeTests()
    {
        // The audit needs the content-type list (and the root's allowed-as-root set) to be non-null;
        // an empty list yields zero rows, which is all these tests need.
        var contentTypeService = Substitute.For<IContentTypeService>();
        contentTypeService.GetAll().Returns(Array.Empty<IContentType>());

        _sut = new DocTypePermissionsController(
            Substitute.For<IDocTypePermissionService>(),
            Substitute.For<IDocTypePermissionRepository>(),
            contentTypeService,
            _entityService,
            _userService,
            _startNodes);
    }

    /// <summary>Auditing the content root classifies path <c>-1</c> for the user.</summary>
    [Fact]
    public async Task AuditForNode_ContentRoot_ClassifiesRootPath()
    {
        var user = Substitute.For<IUser>();
        var userKey = Guid.NewGuid();
        user.Key.Returns(userKey);
        user.Groups.Returns([]);
        _userService.GetAsync(userKey).Returns(user);
        var snapshot = StartNodeSnapshot.NotApplicable with { State = StartNodeSubjectState.Restricted };
        _startNodes.GetForUserAsync(userKey).Returns(snapshot);
        _startNodes.Classify(snapshot, "-1").Returns(StartNodeAccess.PathOnly);

        var result = await _sut.AuditForNode(CancellationToken.None, AdvancedPermissionsConstants.VirtualRootNodeKey, userKey);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("PathOnly", Assert.IsType<DocTypeAuditForNodeResponseModel>(ok.Value).StartNodeAccess);
    }

    /// <summary>Review focus 4: the All Users group is never blocked.</summary>
    [Fact]
    public async Task AuditForNode_Everyone_IsAccessible()
    {
        _startNodes.GetForUserGroupAsync(AdvancedPermissionsConstants.EveryoneRoleAlias).Returns(StartNodeSnapshot.NotApplicable);
        _startNodes.Classify(StartNodeSnapshot.NotApplicable, Arg.Any<string>()).Returns(StartNodeAccess.Accessible);

        var result = await _sut.AuditForNode(
            CancellationToken.None, AdvancedPermissionsConstants.VirtualRootNodeKey, null, AdvancedPermissionsConstants.EveryoneRoleAlias);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Accessible", Assert.IsType<DocTypeAuditForNodeResponseModel>(ok.Value).StartNodeAccess);
    }

    /// <summary>An unknown subject on the audit is never shown as blocked.</summary>
    [Fact]
    public async Task AuditForNode_UnknownGroup_IsAccessible()
    {
        _startNodes.GetForUserGroupAsync("ghosts").Returns((StartNodeSnapshot?)null);

        var result = await _sut.AuditForNode(
            CancellationToken.None, AdvancedPermissionsConstants.VirtualRootNodeKey, null, "ghosts");

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Accessible", Assert.IsType<DocTypeAuditForNodeResponseModel>(ok.Value).StartNodeAccess);
    }
}
