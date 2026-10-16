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
/// Also covers the element-type audit (Library Insert Viewer), whose verdict is per subject.
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
        var scope = StartNodeScope.NotApplicableFor(StartNodeTree.Content) with { State = StartNodeSubjectState.Restricted };
        _startNodes.GetScopeForUserAsync(userKey, StartNodeTree.Content).Returns(scope);
        _startNodes.Classify(scope, "-1").Returns(StartNodeAccess.PathOnly);

        var result = await _sut.AuditForNode(CancellationToken.None, AdvancedPermissionsConstants.VirtualRootNodeKey, userKey);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("PathOnly", Assert.IsType<DocTypeAuditForNodeResponseModel>(ok.Value).StartNodeAccess);
    }

    /// <summary>Review focus 4: the All Users group is never blocked.</summary>
    [Fact]
    public async Task AuditForNode_Everyone_IsAccessible()
    {
        var notApplicable = StartNodeScope.NotApplicableFor(StartNodeTree.Content);
        _startNodes.GetScopeForUserGroupAsync(AdvancedPermissionsConstants.EveryoneRoleAlias, StartNodeTree.Content).Returns(notApplicable);
        _startNodes.Classify(notApplicable, Arg.Any<string>()).Returns(StartNodeAccess.Accessible);

        var result = await _sut.AuditForNode(
            CancellationToken.None, AdvancedPermissionsConstants.VirtualRootNodeKey, null, AdvancedPermissionsConstants.EveryoneRoleAlias);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Accessible", Assert.IsType<DocTypeAuditForNodeResponseModel>(ok.Value).StartNodeAccess);
    }

    /// <summary>An unknown subject on the audit is never shown as blocked.</summary>
    [Fact]
    public async Task AuditForNode_UnknownGroup_IsAccessible()
    {
        _startNodes.GetScopeForUserGroupAsync("ghosts", StartNodeTree.Content).Returns((StartNodeScope?)null);

        var result = await _sut.AuditForNode(
            CancellationToken.None, AdvancedPermissionsConstants.VirtualRootNodeKey, null, "ghosts");

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Accessible", Assert.IsType<DocTypeAuditForNodeResponseModel>(ok.Value).StartNodeAccess);
    }

    /// <summary>
    /// Review focus 2: a subject without any Library access is blocked on the whole element-type
    /// audit; creation is section-global, so the verdict is per subject.
    /// </summary>
    [Fact]
    public async Task ElementTypeAudit_NoLibraryAccess_IsNotVisible()
    {
        var noAccess = StartNodeScope.NotApplicableFor(StartNodeTree.Library) with { State = StartNodeSubjectState.NoAccess };
        _startNodes.GetScopeForUserGroupAsync("editors", StartNodeTree.Library).Returns(noAccess);

        var ok = Assert.IsType<OkObjectResult>(await _sut.ElementTypeAudit(CancellationToken.None, null, "editors"));

        Assert.Equal("NotVisible", Assert.IsType<DocTypeAuditForNodeResponseModel>(ok.Value).StartNodeAccess);
    }

    /// <summary>A restricted subject can still create inside its folders, so the audit is not blocked.</summary>
    [Fact]
    public async Task ElementTypeAudit_RestrictedLibrary_IsAccessible()
    {
        var restricted = StartNodeScope.NotApplicableFor(StartNodeTree.Library) with { State = StartNodeSubjectState.Restricted };
        _startNodes.GetScopeForUserGroupAsync("editors", StartNodeTree.Library).Returns(restricted);

        var ok = Assert.IsType<OkObjectResult>(await _sut.ElementTypeAudit(CancellationToken.None, null, "editors"));

        Assert.Equal("Accessible", Assert.IsType<DocTypeAuditForNodeResponseModel>(ok.Value).StartNodeAccess);
    }

    /// <summary>Review focus 4: an unknown user group on the element-type audit is never blocked.</summary>
    [Fact]
    public async Task ElementTypeAudit_UnknownGroup_IsAccessible()
    {
        _startNodes.GetScopeForUserGroupAsync("ghosts", StartNodeTree.Library).Returns((StartNodeScope?)null);

        var ok = Assert.IsType<OkObjectResult>(await _sut.ElementTypeAudit(CancellationToken.None, null, "ghosts"));

        Assert.Equal("Accessible", Assert.IsType<DocTypeAuditForNodeResponseModel>(ok.Value).StartNodeAccess);
    }

    /// <summary>
    /// Auditing a real parent node reads its path once: the path used to resolve the rows is reused to
    /// classify the node against the subject's start nodes.
    /// </summary>
    [Fact]
    public async Task AuditForNode_KnownNode_ReadsNodePathOnce()
    {
        var nodeKey = Guid.NewGuid();
        // Build the stub entity first: configuring a substitute inside a .Returns(...) argument
        // corrupts NSubstitute's last-call tracking.
        var entity = Substitute.For<Umbraco.Cms.Core.Models.Entities.IEntitySlim>();
        entity.Id.Returns(42);
        entity.Key.Returns(nodeKey);
        _entityService.GetAllPaths(UmbracoObjectTypes.Document, Arg.Any<Guid[]>())
            .Returns([new Umbraco.Cms.Core.Models.Entities.TreeEntityPath { Id = 42, Key = nodeKey, Path = "-1,42" }]);
        _entityService.GetAll(UmbracoObjectTypes.Document, Arg.Any<int[]>()).Returns([entity]);
        var scope = StartNodeScope.NotApplicableFor(StartNodeTree.Content) with { State = StartNodeSubjectState.Restricted };
        _startNodes.GetScopeForUserGroupAsync("editors", StartNodeTree.Content).Returns(scope);
        _startNodes.Classify(scope, "-1,42").Returns(StartNodeAccess.PathOnly);

        var ok = Assert.IsType<OkObjectResult>(await _sut.AuditForNode(CancellationToken.None, nodeKey, null, "editors"));

        Assert.Equal("PathOnly", Assert.IsType<DocTypeAuditForNodeResponseModel>(ok.Value).StartNodeAccess);
        _entityService.Received(1).GetAllPaths(UmbracoObjectTypes.Document, Arg.Any<Guid[]>());
    }
}
