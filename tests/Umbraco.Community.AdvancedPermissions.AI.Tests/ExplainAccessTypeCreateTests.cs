using System.Text.Json;
using NSubstitute;
using Umbraco.AI.Core.Tools;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.AI.Services;
using Umbraco.Community.AdvancedPermissions.AI.Tools;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Tests;

/// <summary>
/// Unit tests for the <see cref="ExplainAspect.TypeCreate"/> dimension of the consolidated
/// <see cref="ExplainAccessTool"/>. Each scenario is invoked through the public
/// <see cref="IAITool.ExecuteAsync(object?, System.Threading.CancellationToken)"/> entry point so the
/// real base-class plumbing is exercised, and a real <see cref="PermissionPresenter"/> wraps mocked
/// Umbraco services so the friendly projection (content-type names, "Insert" action, "Not applicable"
/// state) is exercised end-to-end and the no-raw-identifiers guarantee can be asserted.
/// </summary>
public sealed class ExplainAccessTypeCreateTests
{
    /// <summary>The mocked node-level permission service (unused by the type-create aspect, but a tool dependency).</summary>
    private readonly IAdvancedPermissionService _permissions = Substitute.For<IAdvancedPermissionService>();

    /// <summary>The mocked path resolver the tool uses to build the root-to-parent key path.</summary>
    private readonly IContentPathResolver _pathResolver = Substitute.For<IContentPathResolver>();

    /// <summary>The mocked user group service backing the real presenter and the all-roles enumeration.</summary>
    private readonly IUserGroupService _userGroupService = Substitute.For<IUserGroupService>();

    /// <summary>The mocked entity service backing the real presenter and parent-node resolution.</summary>
    private readonly IEntityService _entityService = Substitute.For<IEntityService>();

    /// <summary>The mocked content-type service backing the real presenter and the candidate roster.</summary>
    private readonly IContentTypeService _contentTypeService = Substitute.For<IContentTypeService>();

    /// <summary>The mocked doc-type permission service the type-create aspect delegates resolution to.</summary>
    private readonly IDocTypePermissionService _docTypePermissions = Substitute.For<IDocTypePermissionService>();

    /// <summary>The mocked backoffice security accessor used to resolve the current user.</summary>
    private readonly IBackOfficeSecurityAccessor _securityAccessor = Substitute.For<IBackOfficeSecurityAccessor>();

    /// <summary>Builds the tests around a single editors group exposed by the user group service.</summary>
    public ExplainAccessTypeCreateTests()
    {
        var editors = Group("editors", "Editors");
        _userGroupService
            .GetAllAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns(new PagedModel<IUserGroup>(1, new[] { editors }));
    }

    /// <summary>The presenter under test, wired over the mocked services.</summary>
    private IPermissionPresenter Presenter => new PermissionPresenter(_userGroupService, _entityService, _contentTypeService);

    /// <summary>Builds the tool under test over the current mocks.</summary>
    /// <returns>A fresh tool instance.</returns>
    private ExplainAccessTool CreateTool() =>
        new(
            _permissions,
            _pathResolver,
            Presenter,
            _userGroupService,
            _securityAccessor,
            _docTypePermissions,
            _contentTypeService,
            _entityService);

    /// <summary>Builds a mocked user group exposing the given alias and display name.</summary>
    /// <param name="alias">The alias the group should report.</param>
    /// <param name="name">The display name the group should report.</param>
    /// <returns>A substitute user group.</returns>
    private static IUserGroup Group(string alias, string name)
    {
        var group = Substitute.For<IUserGroup>();
        group.Alias.Returns(alias);
        group.Name.Returns(name);
        return group;
    }

    /// <summary>Builds a mocked non-element content type exposing the given key, name, and root-allowed flag.</summary>
    /// <param name="key">The content type key.</param>
    /// <param name="name">The display name.</param>
    /// <param name="allowedAsRoot">Whether the type is allowed at the tree root.</param>
    /// <param name="isElement">Whether the type is an element type (excluded from candidates).</param>
    /// <returns>A substitute content type.</returns>
    private static IContentType ContentType(Guid key, string name, bool allowedAsRoot = true, bool isElement = false)
    {
        var ct = Substitute.For<IContentType>();
        ct.Key.Returns(key);
        ct.Name.Returns(name);
        ct.Alias.Returns(name.ToLowerInvariant());
        ct.Icon.Returns("icon-document");
        ct.IsElement.Returns(isElement);
        ct.AllowedAsRoot.Returns(allowedAsRoot);
        return ct;
    }

    /// <summary>Registers the given content types as the full content-type set, and by key lookups.</summary>
    /// <param name="contentTypes">The content types to expose.</param>
    private void SetupContentTypes(params IContentType[] contentTypes)
    {
        _contentTypeService.GetAll().Returns(contentTypes);
        foreach (var ct in contentTypes)
        {
            _contentTypeService.Get(ct.Key).Returns(ct);
        }
    }

    /// <summary>
    /// Configures the backoffice security accessor to report a current user with the given key. The user's
    /// group aliases are resolved inside the (mocked) doc-type permission service, so only the key matters
    /// here.
    /// </summary>
    /// <param name="userKey">The current user's key.</param>
    private void SetupCurrentUser(Guid userKey)
    {
        var user = Substitute.For<IUser>();
        user.Key.Returns(userKey);
        var security = Substitute.For<IBackOfficeSecurity>();
        security.CurrentUser.Returns(user);
        _securityAccessor.BackOfficeSecurity.Returns(security);
    }

    /// <summary>Builds an effective create permission with a single reasoning line.</summary>
    /// <param name="isAllowed">Whether creation is allowed.</param>
    /// <param name="role">The contributing role alias.</param>
    /// <param name="nodeKey">The node the contributing entry was set on.</param>
    /// <returns>An effective permission for the doc-type create verb.</returns>
    private static EffectivePermission CreatePerm(bool isAllowed, string role, Guid nodeKey) =>
        new(
            AdvancedPermissionsConstants.VerbCreateOfType,
            isAllowed,
            IsExplicit: true,
            Reasoning:
            [
                new PermissionReasoning(
                    role,
                    isAllowed ? PermissionState.Allow : PermissionState.Deny,
                    IsExplicit: true,
                    SourceNodeKey: nodeKey,
                    SourceScope: PermissionScope.ThisNodeAndDescendants,
                    IsFromGroupDefault: false),
            ]);

    /// <summary>Asserts that a serialized object leaks none of the raw identifiers.</summary>
    /// <param name="value">The object to serialize and inspect.</param>
    /// <param name="nodeKey">The node key whose GUID must not appear.</param>
    /// <param name="contentTypeKeys">Content-type keys whose GUIDs must not appear.</param>
    private static void AssertNoRawIdentifiers(object value, Guid nodeKey, params Guid[] contentTypeKeys)
    {
        var json = JsonSerializer.Serialize(value);
        Assert.DoesNotContain("$everyone", json);
        Assert.DoesNotContain("Umb.Document.", json);
        Assert.DoesNotContain("CreateOfType", json);
        Assert.DoesNotContain("ThisNodeAndDescendants", json);
        Assert.DoesNotContain(nodeKey.ToString(), json);
        foreach (var key in contentTypeKeys)
        {
            Assert.DoesNotContain(key.ToString(), json);
        }
    }

    /// <summary>
    /// TypeCreate with a focused content type that the current user MAY create returns a single friendly
    /// "Allowed" verdict naming the document type, with no raw identifiers.
    /// </summary>
    [Fact]
    public async Task TypeCreate_FocusedType_Allowed_ReturnsFriendlyVerdict()
    {
        var userKey = Guid.NewGuid();
        var nodeKey = Guid.NewGuid();
        var ctKey = Guid.NewGuid();
        var path = new[] { Guid.NewGuid(), nodeKey };
        SetupParentAllowing(nodeKey, "Campaigns", ctKey);
        SetupCurrentUser(userKey);
        SetupContentTypes(ContentType(ctKey, "Article"));

        _pathResolver.GetPathFromRoot(nodeKey).Returns(path);
        _docTypePermissions
            .ResolveCreateAsync(userKey, nodeKey, path, ctKey, Arg.Any<CancellationToken>())
            .Returns(CreatePerm(isAllowed: true, "editors", nodeKey));

        var result = await ((IAITool)CreateTool()).ExecuteAsync(
            new ExplainAccessArgs(ExplainSubject.CurrentUser, nodeKey, Aspect: ExplainAspect.TypeCreate, ContentTypeKey: ctKey),
            CancellationToken.None);

        var verdict = Assert.IsType<TypeCreateVerdict>(result);
        Assert.Equal("Article", verdict.DocumentType);
        Assert.Equal("Allowed", verdict.Result);
        AssertNoRawIdentifiers(verdict, nodeKey, ctKey);
        await _docTypePermissions.Received(1).ResolveCreateAsync(userKey, nodeKey, path, ctKey, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// TypeCreate with a focused content type that a ROLE may NOT create returns a single friendly
    /// "Denied" verdict with the contributing reason, and resolves via the role-based overload.
    /// </summary>
    [Fact]
    public async Task TypeCreate_FocusedType_DeniedForRole_ReturnsReason()
    {
        const string roleAlias = "editors";
        var nodeKey = Guid.NewGuid();
        var ctKey = Guid.NewGuid();
        var path = new[] { Guid.NewGuid(), nodeKey };
        SetupParentAllowing(nodeKey, "Campaigns", ctKey);
        SetupContentTypes(ContentType(ctKey, "Article"));

        _pathResolver.GetPathFromRoot(nodeKey).Returns(path);
        _docTypePermissions
            .ResolveCreateForRolesAsync(
                Arg.Is<IReadOnlyList<string>>(r => r.Contains(roleAlias) && r.Contains(AdvancedPermissionsConstants.EveryoneRoleAlias)),
                path,
                ctKey,
                Arg.Any<CancellationToken>())
            .Returns(CreatePerm(isAllowed: false, roleAlias, nodeKey));

        var result = await ((IAITool)CreateTool()).ExecuteAsync(
            new ExplainAccessArgs(ExplainSubject.Role, nodeKey, RoleAlias: roleAlias, Aspect: ExplainAspect.TypeCreate, ContentTypeKey: ctKey),
            CancellationToken.None);

        var verdict = Assert.IsType<TypeCreateVerdict>(result);
        Assert.Equal("Article", verdict.DocumentType);
        Assert.Equal("Denied", verdict.Result);
        Assert.NotEmpty(verdict.Reasons);
        Assert.Equal("Editors", verdict.Reasons[0].Role);
        AssertNoRawIdentifiers(verdict, nodeKey, ctKey);
    }

    /// <summary>
    /// TypeCreate with NO focused content type returns the per-type roster for the node: an allowed type,
    /// a permission-denied type, and a structurally "Not applicable" type (not in the parent's allowed
    /// children), each distinctly marked, with no raw identifiers.
    /// </summary>
    [Fact]
    public async Task TypeCreate_NoFocusedType_ReturnsRosterWithStructuralNotApplicable()
    {
        var userKey = Guid.NewGuid();
        var parentKey = Guid.NewGuid();
        var parentCtKey = Guid.NewGuid();
        var allowedKey = Guid.NewGuid();
        var deniedKey = Guid.NewGuid();
        var naKey = Guid.NewGuid();
        var path = new[] { Guid.NewGuid(), parentKey };
        SetupCurrentUser(userKey);

        // The parent node resolves (via the entity service) to a content type that allows only
        // 'allowedKey' and 'deniedKey' as children — 'naKey' is therefore structurally not applicable.
        SetupParentNode(parentKey, "Campaigns", parentCtAlias: "campaign");
        var parentCt = ContentType(parentCtKey, "Campaign");
        parentCt.AllowedContentTypes.Returns(new[]
        {
            new ContentTypeSort(allowedKey, 0, "allowed"),
            new ContentTypeSort(deniedKey, 1, "denied"),
        });
        _contentTypeService.Get("campaign").Returns(parentCt);

        SetupContentTypes(
            ContentType(allowedKey, "Article"),
            ContentType(deniedKey, "Landing Page"),
            ContentType(naKey, "Widget"));

        _pathResolver.GetPathFromRoot(parentKey).Returns(path);
        _docTypePermissions
            .ResolveCreateAsync(userKey, parentKey, path, allowedKey, Arg.Any<CancellationToken>())
            .Returns(CreatePerm(isAllowed: true, "editors", parentKey));
        _docTypePermissions
            .ResolveCreateAsync(userKey, parentKey, path, deniedKey, Arg.Any<CancellationToken>())
            .Returns(CreatePerm(isAllowed: false, "editors", parentKey));
        // naKey is structurally disallowed; even if the resolver would allow it, the verdict must be n/a.
        _docTypePermissions
            .ResolveCreateAsync(userKey, parentKey, path, naKey, Arg.Any<CancellationToken>())
            .Returns(CreatePerm(isAllowed: true, "editors", parentKey));

        var result = await ((IAITool)CreateTool()).ExecuteAsync(
            new ExplainAccessArgs(ExplainSubject.CurrentUser, parentKey, Aspect: ExplainAspect.TypeCreate),
            CancellationToken.None);

        var explanation = Assert.IsType<TypeCreateExplanation>(result);
        Assert.Equal("Campaigns", explanation.Node);

        var allowed = Assert.Single(explanation.DocumentTypes, d => d.DocumentType == "Article");
        Assert.Equal("Allowed", allowed.Result);

        var denied = Assert.Single(explanation.DocumentTypes, d => d.DocumentType == "Landing Page");
        Assert.Equal("Denied", denied.Result);

        var na = Assert.Single(explanation.DocumentTypes, d => d.DocumentType == "Widget");
        Assert.Equal("Not applicable", na.Result);

        AssertNoRawIdentifiers(explanation, parentKey, allowedKey, deniedKey, naKey, parentCtKey);
    }

    /// <summary>
    /// TypeCreate all-roles with a focused content type returns a friendly roster partitioning every
    /// assignable role into allowed/denied for creating that type, with no raw identifiers.
    /// </summary>
    [Fact]
    public async Task TypeCreate_AllRoles_FocusedType_PartitionsRoles()
    {
        var nodeKey = Guid.NewGuid();
        var ctKey = Guid.NewGuid();
        var path = new[] { Guid.NewGuid(), nodeKey };
        SetupParentAllowing(nodeKey, "Campaigns", ctKey);
        SetupContentTypes(ContentType(ctKey, "Article"));

        var editors = Group("editors", "Editors");
        var writers = Group("writers", "Writers");
        _userGroupService
            .GetAllAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns(new PagedModel<IUserGroup>(2, new[] { editors, writers }));

        _pathResolver.GetPathFromRoot(nodeKey).Returns(path);

        _docTypePermissions
            .ResolveCreateForRolesAsync(
                Arg.Is<IReadOnlyList<string>>(r => r.Contains("editors")),
                path,
                ctKey,
                Arg.Any<CancellationToken>())
            .Returns(CreatePerm(isAllowed: true, "editors", nodeKey));
        _docTypePermissions
            .ResolveCreateForRolesAsync(
                Arg.Is<IReadOnlyList<string>>(r => r.Contains("writers")),
                path,
                ctKey,
                Arg.Any<CancellationToken>())
            .Returns(CreatePerm(isAllowed: false, "writers", nodeKey));
        _docTypePermissions
            .ResolveCreateForRolesAsync(
                Arg.Is<IReadOnlyList<string>>(r => r.Count == 1 && r[0] == AdvancedPermissionsConstants.EveryoneRoleAlias),
                path,
                ctKey,
                Arg.Any<CancellationToken>())
            .Returns(CreatePerm(isAllowed: false, AdvancedPermissionsConstants.EveryoneRoleAlias, nodeKey));

        var result = await ((IAITool)CreateTool()).ExecuteAsync(
            new ExplainAccessArgs(ExplainSubject.AllRoles, nodeKey, Aspect: ExplainAspect.TypeCreate, ContentTypeKey: ctKey),
            CancellationToken.None);

        var roster = Assert.IsType<TypeCreateRoster>(result);
        Assert.Equal("Article", roster.DocumentType);
        Assert.Equal("Campaigns", roster.Node);
        Assert.True(roster.IsApplicable);
        Assert.Contains("Editors", roster.AllowedRoles);
        Assert.Contains("Writers", roster.DeniedRoles);
        Assert.Contains(AdvancedPermissionsConstants.EveryoneRoleDisplayName, roster.DeniedRoles);
        AssertNoRawIdentifiers(roster, nodeKey, ctKey);
    }

    /// <summary>
    /// TypeCreate with a focused content type missing the role alias for subject=role returns a friendly
    /// error and never touches the doc-type service.
    /// </summary>
    [Fact]
    public async Task TypeCreate_Role_MissingRoleAlias_ReturnsError()
    {
        var nodeKey = Guid.NewGuid();
        var ctKey = Guid.NewGuid();

        var result = await ((IAITool)CreateTool()).ExecuteAsync(
            new ExplainAccessArgs(ExplainSubject.Role, nodeKey, Aspect: ExplainAspect.TypeCreate, ContentTypeKey: ctKey),
            CancellationToken.None);

        Assert.IsType<AccessError>(result);
        await _docTypePermissions.DidNotReceive().ResolveCreateForRolesAsync(
            Arg.Any<IReadOnlyList<string>>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Configures the entity service so a parent node key resolves to a content entity exposing the given
    /// content-type alias, so the tool can derive the structural allowed-children set.
    /// </summary>
    /// <param name="key">The parent node key.</param>
    /// <param name="name">The parent node name.</param>
    /// <param name="parentCtAlias">The parent's content-type alias.</param>
    private void SetupParentNode(Guid key, string name, string parentCtAlias)
    {
        var entity = Substitute.For<IContentEntitySlim>();
        entity.Name.Returns(name);
        entity.ContentTypeAlias.Returns(parentCtAlias);
        _entityService.Get(key, UmbracoObjectTypes.Document).Returns(entity);
    }

    /// <summary>
    /// Configures a parent node whose content type structurally allows the supplied child content-type key,
    /// so a focused-type query treats that type as applicable (not structurally "Not applicable").
    /// </summary>
    /// <param name="parentNodeKey">The parent node key.</param>
    /// <param name="parentName">The parent node name.</param>
    /// <param name="allowedChildKey">The child content-type key the parent allows.</param>
    private void SetupParentAllowing(Guid parentNodeKey, string parentName, Guid allowedChildKey)
    {
        const string parentCtAlias = "parentType";
        SetupParentNode(parentNodeKey, parentName, parentCtAlias);

        var parentCt = ContentType(Guid.NewGuid(), "Parent Type");
        parentCt.AllowedContentTypes.Returns(new[] { new ContentTypeSort(allowedChildKey, 0, "child") });
        _contentTypeService.Get(parentCtAlias).Returns(parentCt);
    }
}
