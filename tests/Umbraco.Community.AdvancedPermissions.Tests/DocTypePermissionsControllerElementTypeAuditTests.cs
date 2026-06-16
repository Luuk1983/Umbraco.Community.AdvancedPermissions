using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.Controllers;
using Umbraco.Community.AdvancedPermissions.Controllers.Models;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests the element-type create audit endpoint that powers the Library Insert Viewer. Library
/// element-type create-filtering is section-global, so the audit resolves each library element type at
/// the virtual root and returns one row per type with the effective create decision and reasoning.
/// </summary>
public sealed class DocTypePermissionsControllerElementTypeAuditTests
{
    private readonly IDocTypePermissionService _docTypeService = Substitute.For<IDocTypePermissionService>();
    private readonly IDocTypePermissionRepository _repository = Substitute.For<IDocTypePermissionRepository>();
    private readonly IContentTypeService _contentTypeService = Substitute.For<IContentTypeService>();
    private readonly IEntityService _entityService = Substitute.For<IEntityService>();
    private readonly IUserService _userService = Substitute.For<IUserService>();
    private readonly DocTypePermissionsController _sut;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocTypePermissionsControllerElementTypeAuditTests"/> class.
    /// </summary>
    public DocTypePermissionsControllerElementTypeAuditTests()
    {
        _sut = new DocTypePermissionsController(
            _docTypeService, _repository, _contentTypeService, _entityService, _userService);
    }

    /// <summary>
    /// Role mode: returns one row per library element type (non-element types excluded), each carrying the
    /// resolver's effective decision. The audit is global, so every row is flagged as an allowed child.
    /// </summary>
    [Fact]
    public async Task ElementTypeAudit_RoleMode_ReturnsRowPerLibraryElementType_WithResolvedDecision()
    {
        var allowedType = Guid.NewGuid();
        var deniedType = Guid.NewGuid();
        // Build the stub content types into a local first — StubElementType configures the substitutes
        // with .Returns(), which would clobber NSubstitute's last-call context if done inside GetAll().Returns().
        var types = new[]
        {
            StubElementType(allowedType, "et1", "Element One"),
            StubElementType(deniedType, "et2", "Element Two"),
            StubNonElementType(Guid.NewGuid()),
        };
        _contentTypeService.GetAll().Returns(types);

        StubResolve(allowedType, isAllowed: true);
        StubResolve(deniedType, isAllowed: false);

        var result = await _sut.ElementTypeAudit(CancellationToken.None, userKey: null, roleAlias: "editors");

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<DocTypeAuditForNodeResponseModel>(ok.Value);
        Assert.Equal(AdvancedPermissionsConstants.VirtualRootNodeKey, body.NodeKey);
        Assert.Equal(2, body.Results.Count); // non-element type excluded

        var allowedRow = body.Results.Single(r => r.ContentTypeKey == allowedType);
        Assert.True(allowedRow.IsAllowed);
        Assert.True(allowedRow.IsInAllowedChildren); // global audit — always an allowed child

        var deniedRow = body.Results.Single(r => r.ContentTypeKey == deniedType);
        Assert.False(deniedRow.IsAllowed);
    }

    /// <summary>
    /// The audit must resolve against the canonical element-type verb (not the document create verb), so
    /// it reflects the Library element-type rules rather than content create-filtering.
    /// </summary>
    [Fact]
    public async Task ElementTypeAudit_ResolvesAgainstElementTypeVerb()
    {
        var typeKey = Guid.NewGuid();
        var types = new[] { StubElementType(typeKey, "et1", "Element One") };
        _contentTypeService.GetAll().Returns(types);
        StubResolve(typeKey, isAllowed: true);

        await _sut.ElementTypeAudit(CancellationToken.None, userKey: null, roleAlias: "editors");

        await _docTypeService.Received().ResolveCreateForRolesAsync(
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<IReadOnlyList<Guid>>(),
            typeKey,
            AdvancedPermissionsConstants.VerbElementCreateOfType,
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// User mode: the audited user's group aliases (plus <c>$everyone</c>) drive resolution.
    /// </summary>
    [Fact]
    public async Task ElementTypeAudit_UserMode_ResolvesForUserGroups()
    {
        var userKey = Guid.NewGuid();
        var typeKey = Guid.NewGuid();
        var group = Substitute.For<IReadOnlyUserGroup>();
        group.Alias.Returns("editors");
        var user = Substitute.For<IUser>();
        user.Groups.Returns([group]);
        _userService.GetAsync(userKey).Returns(user);

        var types = new[] { StubElementType(typeKey, "et1", "Element One") };
        _contentTypeService.GetAll().Returns(types);
        StubResolve(typeKey, isAllowed: true);

        var result = await _sut.ElementTypeAudit(CancellationToken.None, userKey, roleAlias: null);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<DocTypeAuditForNodeResponseModel>(ok.Value);
        Assert.Single(body.Results);
        await _docTypeService.Received().ResolveCreateForRolesAsync(
            Arg.Is<IReadOnlyList<string>>(r => r.Contains("editors") && r.Contains(AdvancedPermissionsConstants.EveryoneRoleAlias)),
            Arg.Any<IReadOnlyList<Guid>>(),
            typeKey,
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Supplying neither (or both) subject is a 400 — exactly one of userKey/roleAlias is required.
    /// </summary>
    [Fact]
    public async Task ElementTypeAudit_NoSubject_Returns400()
    {
        var result = await _sut.ElementTypeAudit(CancellationToken.None, userKey: null, roleAlias: null);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    private void StubResolve(Guid contentTypeKey, bool isAllowed) =>
        _docTypeService
            .ResolveCreateForRolesAsync(
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<IReadOnlyList<Guid>>(),
                contentTypeKey,
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new EffectivePermission(
                AdvancedPermissionsConstants.VerbElementCreateOfType, isAllowed, IsExplicit: false, Reasoning: []));

    private static IContentType StubElementType(Guid key, string alias, string name)
    {
        var ct = Substitute.For<IContentType>();
        ct.Key.Returns(key);
        ct.Alias.Returns(alias);
        ct.Name.Returns(name);
        ct.Icon.Returns("icon-block");
        ct.IsElement.Returns(true);
        ct.AllowedInLibrary.Returns(true);
        return ct;
    }

    private static IContentType StubNonElementType(Guid key)
    {
        var ct = Substitute.For<IContentType>();
        ct.Key.Returns(key);
        ct.Alias.Returns("doc");
        ct.Name.Returns("Document");
        ct.IsElement.Returns(false);
        ct.AllowedInLibrary.Returns(false);
        return ct;
    }
}
