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
/// Unit tests for the consolidated <see cref="ExplainAccessTool"/>. Each scenario is invoked through the
/// public <see cref="IAITool.ExecuteAsync(object?, System.Threading.CancellationToken)"/> entry point so
/// the real <see cref="AIToolBase{TArgs}"/> base-class plumbing (argument cast + dispatch) is exercised.
/// The tool wraps a real <see cref="PermissionPresenter"/> over mocked Umbraco services so the friendly
/// mapping is exercised end-to-end and the no-raw-identifiers guarantee can be asserted.
/// </summary>
public sealed class ExplainAccessToolTests
{
    /// <summary>The mocked permission service the tool delegates resolution to.</summary>
    private readonly IAdvancedPermissionService _permissions = Substitute.For<IAdvancedPermissionService>();

    /// <summary>The mocked path resolver the tool uses to build the root-to-node key path.</summary>
    private readonly IContentPathResolver _pathResolver = Substitute.For<IContentPathResolver>();

    /// <summary>The mocked user group service backing the real presenter and role enumeration.</summary>
    private readonly IUserGroupService _userGroupService = Substitute.For<IUserGroupService>();

    /// <summary>The mocked entity service backing the real presenter.</summary>
    private readonly IEntityService _entityService = Substitute.For<IEntityService>();

    /// <summary>The mocked content-type service backing the real presenter and the type-create aspect.</summary>
    private readonly IContentTypeService _contentTypeService = Substitute.For<IContentTypeService>();

    /// <summary>The mocked doc-type permission service the tool delegates type-create resolution to.</summary>
    private readonly IDocTypePermissionService _docTypePermissions = Substitute.For<IDocTypePermissionService>();

    /// <summary>The mocked backoffice security accessor used to resolve the current user.</summary>
    private readonly IBackOfficeSecurityAccessor _securityAccessor = Substitute.For<IBackOfficeSecurityAccessor>();

    /// <summary>Builds the tests around a single editors group exposed by the user group service.</summary>
    public ExplainAccessToolTests()
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

    /// <summary>Configures the entity service so a node key resolves to a content name.</summary>
    /// <param name="key">The node key.</param>
    /// <param name="name">The name to report.</param>
    private void SetupNode(Guid key, string name)
    {
        var entity = Substitute.For<IEntitySlim>();
        entity.Name.Returns(name);
        _entityService.Get(key, UmbracoObjectTypes.Document).Returns(entity);
    }

    /// <summary>Configures the backoffice security accessor to report a current user with the given key.</summary>
    /// <param name="userKey">The current user's key.</param>
    private void SetupCurrentUser(Guid userKey)
    {
        var user = Substitute.For<IUser>();
        user.Key.Returns(userKey);
        var security = Substitute.For<IBackOfficeSecurity>();
        security.CurrentUser.Returns(user);
        _securityAccessor.BackOfficeSecurity.Returns(security);
    }

    /// <summary>Builds an effective permission with a two-line reasoning chain (override + base).</summary>
    /// <param name="verb">The verb the permission applies to.</param>
    /// <param name="isAllowed">Whether the permission is allowed.</param>
    /// <param name="role">The contributing role alias.</param>
    /// <param name="nodeKey">The node the contributing entry was set on.</param>
    /// <returns>An effective permission with two reasoning lines.</returns>
    private static EffectivePermission Perm(string verb, bool isAllowed, string role, Guid nodeKey) =>
        new(
            verb,
            isAllowed,
            IsExplicit: true,
            Reasoning:
            [
                new PermissionReasoning(
                    role,
                    isAllowed ? PermissionState.Allow : PermissionState.Deny,
                    IsExplicit: true,
                    SourceNodeKey: nodeKey,
                    SourceScope: PermissionScope.ThisNodeOnly,
                    IsFromGroupDefault: false),
                new PermissionReasoning(
                    AdvancedPermissionsConstants.EveryoneRoleAlias,
                    PermissionState.Allow,
                    IsExplicit: false,
                    SourceNodeKey: nodeKey,
                    SourceScope: PermissionScope.ThisNodeAndDescendants,
                    IsFromGroupDefault: false),
            ]);

    /// <summary>Asserts that a serialized object leaks none of the raw identifiers.</summary>
    /// <param name="value">The object to serialize and inspect.</param>
    /// <param name="nodeKey">The node key whose GUID must not appear.</param>
    private static void AssertNoRawIdentifiers(object value, Guid nodeKey)
    {
        var json = JsonSerializer.Serialize(value);
        Assert.DoesNotContain("$everyone", json);
        Assert.DoesNotContain("Umb.Document.", json);
        Assert.DoesNotContain("ThisNodeOnly", json);
        Assert.DoesNotContain("ThisNodeAndDescendants", json);
        Assert.DoesNotContain("DescendantsOnly", json);
        Assert.DoesNotContain(nodeKey.ToString(), json);
    }

    /// <summary>
    /// Subject=User with no verb returns a friendly <see cref="AccessExplanation"/> over all verbs and
    /// resolves via <see cref="IAdvancedPermissionService.ResolveAllAsync"/>.
    /// </summary>
    [Fact]
    public async Task User_NoVerb_ReturnsFriendlyExplanation()
    {
        var userKey = Guid.NewGuid();
        var nodeKey = Guid.NewGuid();
        var path = new[] { Guid.NewGuid(), nodeKey };
        SetupNode(nodeKey, "Homepage");

        var resolved = new Dictionary<string, EffectivePermission>
        {
            ["Umb.Document.Read"] = Perm("Umb.Document.Read", isAllowed: true, "editors", nodeKey),
            ["Umb.Document.Delete"] = Perm("Umb.Document.Delete", isAllowed: false, AdvancedPermissionsConstants.EveryoneRoleAlias, nodeKey),
        };

        _pathResolver.GetPathFromRoot(nodeKey).Returns(path);
        _permissions
            .ResolveAllAsync(userKey, nodeKey, path, null, Arg.Any<CancellationToken>())
            .Returns(resolved);

        var result = await ((IAITool)CreateTool()).ExecuteAsync(
            new ExplainAccessArgs(ExplainSubject.User, nodeKey, UserKey: userKey), CancellationToken.None);

        var explanation = Assert.IsType<AccessExplanation>(result);
        Assert.Equal("Homepage", explanation.Node);
        Assert.Contains(explanation.Permissions, p => p.Action == "Read" && p.Result == "Allowed");
        Assert.Contains(explanation.Permissions, p => p.Action == "Delete" && p.Result == "Denied");
        AssertNoRawIdentifiers(explanation, nodeKey);

        await _permissions.Received(1).ResolveAllAsync(userKey, nodeKey, path, null, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Subject=User with a verb returns a single friendly <see cref="AccessVerdict"/> and resolves via
    /// <see cref="IAdvancedPermissionService.ResolveAsync"/>.
    /// </summary>
    [Fact]
    public async Task User_WithVerb_ReturnsFriendlyVerdict()
    {
        var userKey = Guid.NewGuid();
        var nodeKey = Guid.NewGuid();
        var path = new[] { Guid.NewGuid(), nodeKey };
        const string verb = "Umb.Document.Publish";
        SetupNode(nodeKey, "Homepage");

        _pathResolver.GetPathFromRoot(nodeKey).Returns(path);
        _permissions
            .ResolveAsync(userKey, nodeKey, path, verb, Arg.Any<CancellationToken>())
            .Returns(Perm(verb, isAllowed: false, "editors", nodeKey));

        var result = await ((IAITool)CreateTool()).ExecuteAsync(
            new ExplainAccessArgs(ExplainSubject.User, nodeKey, UserKey: userKey, Verb: verb), CancellationToken.None);

        var verdict = Assert.IsType<AccessVerdict>(result);
        Assert.Equal("Publish", verdict.Action);
        Assert.Equal("Denied", verdict.Result);
        Assert.Equal("Editors", verdict.Reasons[0].Role);
        AssertNoRawIdentifiers(verdict, nodeKey);

        await _permissions.Received(1).ResolveAsync(userKey, nodeKey, path, verb, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Subject=User missing the user key returns a friendly <see cref="AccessError"/> and never throws or
    /// touches the permission service.
    /// </summary>
    [Fact]
    public async Task User_MissingUserKey_ReturnsError()
    {
        var nodeKey = Guid.NewGuid();

        var result = await ((IAITool)CreateTool()).ExecuteAsync(
            new ExplainAccessArgs(ExplainSubject.User, nodeKey), CancellationToken.None);

        var error = Assert.IsType<AccessError>(result);
        Assert.False(string.IsNullOrWhiteSpace(error.Error));
        await _permissions.DidNotReceive().ResolveAllAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<IEnumerable<string>?>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Subject=CurrentUser resolves the user from the backoffice security accessor and explains as for a user.
    /// </summary>
    [Fact]
    public async Task CurrentUser_ResolvesFromSecurityAccessor()
    {
        var userKey = Guid.NewGuid();
        var nodeKey = Guid.NewGuid();
        var path = new[] { Guid.NewGuid(), nodeKey };
        SetupNode(nodeKey, "Homepage");
        SetupCurrentUser(userKey);

        var resolved = new Dictionary<string, EffectivePermission>
        {
            ["Umb.Document.Publish"] = Perm("Umb.Document.Publish", isAllowed: false, "editors", nodeKey),
        };
        _pathResolver.GetPathFromRoot(nodeKey).Returns(path);
        _permissions
            .ResolveAllAsync(userKey, nodeKey, path, null, Arg.Any<CancellationToken>())
            .Returns(resolved);

        var result = await ((IAITool)CreateTool()).ExecuteAsync(
            new ExplainAccessArgs(ExplainSubject.CurrentUser, nodeKey), CancellationToken.None);

        var explanation = Assert.IsType<AccessExplanation>(result);
        Assert.Contains(explanation.Permissions, p => p.Action == "Publish" && p.Result == "Denied");
        AssertNoRawIdentifiers(explanation, nodeKey);
        await _permissions.Received(1).ResolveAllAsync(userKey, nodeKey, path, null, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Subject=CurrentUser with no authenticated user returns a friendly <see cref="AccessError"/> and never throws.
    /// </summary>
    [Fact]
    public async Task CurrentUser_NoAuthenticatedUser_ReturnsError()
    {
        var nodeKey = Guid.NewGuid();
        _securityAccessor.BackOfficeSecurity.Returns((IBackOfficeSecurity?)null);

        var result = await ((IAITool)CreateTool()).ExecuteAsync(
            new ExplainAccessArgs(ExplainSubject.CurrentUser, nodeKey), CancellationToken.None);

        var error = Assert.IsType<AccessError>(result);
        Assert.False(string.IsNullOrWhiteSpace(error.Error));
    }

    /// <summary>
    /// Subject=Role with a verb resolves via <see cref="IAdvancedPermissionService.ResolveForRoleAsync"/>
    /// with a single-element verb set and returns a single friendly verdict.
    /// </summary>
    [Fact]
    public async Task Role_WithVerb_ReturnsFriendlyVerdict()
    {
        const string roleAlias = "editors";
        var nodeKey = Guid.NewGuid();
        var path = new[] { Guid.NewGuid(), nodeKey };
        const string verb = "Umb.Document.Publish";
        SetupNode(nodeKey, "News");

        _pathResolver.GetPathFromRoot(nodeKey).Returns(path);
        _permissions
            .ResolveForRoleAsync(
                roleAlias,
                nodeKey,
                path,
                Arg.Is<IEnumerable<string>>(v => v != null && v.SequenceEqual(new[] { verb })),
                Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, EffectivePermission> { [verb] = Perm(verb, isAllowed: false, roleAlias, nodeKey) });

        var result = await ((IAITool)CreateTool()).ExecuteAsync(
            new ExplainAccessArgs(ExplainSubject.Role, nodeKey, RoleAlias: roleAlias, Verb: verb), CancellationToken.None);

        var verdict = Assert.IsType<AccessVerdict>(result);
        Assert.Equal("Publish", verdict.Action);
        Assert.Equal("Denied", verdict.Result);
        AssertNoRawIdentifiers(verdict, nodeKey);
    }

    /// <summary>
    /// Subject=Role with no verb resolves all verbs and returns a friendly explanation.
    /// </summary>
    [Fact]
    public async Task Role_NoVerb_ReturnsFriendlyExplanation()
    {
        const string roleAlias = "editors";
        var nodeKey = Guid.NewGuid();
        var path = new[] { Guid.NewGuid(), nodeKey };
        SetupNode(nodeKey, "News");

        _pathResolver.GetPathFromRoot(nodeKey).Returns(path);
        _permissions
            .ResolveForRoleAsync(roleAlias, nodeKey, path, null, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, EffectivePermission>
            {
                ["Umb.Document.Read"] = Perm("Umb.Document.Read", isAllowed: true, roleAlias, nodeKey),
            });

        var result = await ((IAITool)CreateTool()).ExecuteAsync(
            new ExplainAccessArgs(ExplainSubject.Role, nodeKey, RoleAlias: roleAlias), CancellationToken.None);

        var explanation = Assert.IsType<AccessExplanation>(result);
        Assert.Equal("News", explanation.Node);
        Assert.Contains(explanation.Permissions, p => p.Action == "Read" && p.Result == "Allowed");
        AssertNoRawIdentifiers(explanation, nodeKey);
        await _permissions.Received(1).ResolveForRoleAsync(roleAlias, nodeKey, path, null, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Subject=Role missing the role alias returns a friendly <see cref="AccessError"/> and never throws.
    /// </summary>
    [Fact]
    public async Task Role_MissingRoleAlias_ReturnsError()
    {
        var nodeKey = Guid.NewGuid();

        var result = await ((IAITool)CreateTool()).ExecuteAsync(
            new ExplainAccessArgs(ExplainSubject.Role, nodeKey), CancellationToken.None);

        var error = Assert.IsType<AccessError>(result);
        Assert.False(string.IsNullOrWhiteSpace(error.Error));
    }

    /// <summary>
    /// Subject=AllRoles with a verb enumerates every assignable role (groups plus All Users) and
    /// partitions them into allowed/denied friendly rosters with no raw identifiers.
    /// </summary>
    [Fact]
    public async Task AllRoles_WithVerb_PartitionsRolesByAllowState()
    {
        var nodeKey = Guid.NewGuid();
        var path = new[] { Guid.NewGuid(), nodeKey };
        const string verb = "Umb.Document.Publish";
        SetupNode(nodeKey, "Campaign");

        var editors = Group("editors", "Editors");
        var writers = Group("writers", "Writers");
        _userGroupService
            .GetAllAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns(new PagedModel<IUserGroup>(2, new[] { editors, writers }));

        _pathResolver.GetPathFromRoot(nodeKey).Returns(path);

        _permissions
            .ResolveForRoleAsync("editors", nodeKey, path, Arg.Any<IEnumerable<string>?>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, EffectivePermission> { [verb] = Perm(verb, isAllowed: true, "editors", nodeKey) });
        _permissions
            .ResolveForRoleAsync("writers", nodeKey, path, Arg.Any<IEnumerable<string>?>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, EffectivePermission> { [verb] = Perm(verb, isAllowed: false, "writers", nodeKey) });
        _permissions
            .ResolveForRoleAsync(AdvancedPermissionsConstants.EveryoneRoleAlias, nodeKey, path, Arg.Any<IEnumerable<string>?>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, EffectivePermission> { [verb] = Perm(verb, isAllowed: false, AdvancedPermissionsConstants.EveryoneRoleAlias, nodeKey) });

        var result = await ((IAITool)CreateTool()).ExecuteAsync(
            new ExplainAccessArgs(ExplainSubject.AllRoles, nodeKey, Verb: verb), CancellationToken.None);

        var roster = Assert.IsType<AccessRoster>(result);
        Assert.Equal("Publish", roster.Action);
        Assert.Equal("Campaign", roster.Node);
        Assert.Contains("Editors", roster.AllowedRoles);
        Assert.Contains("Writers", roster.DeniedRoles);
        Assert.Contains(AdvancedPermissionsConstants.EveryoneRoleDisplayName, roster.DeniedRoles);
        Assert.DoesNotContain("Editors", roster.DeniedRoles);

        var json = JsonSerializer.Serialize(roster);
        Assert.DoesNotContain("$everyone", json);
        Assert.DoesNotContain("Umb.Document.", json);
        Assert.DoesNotContain(nodeKey.ToString(), json);
    }

    /// <summary>
    /// Subject=AllRoles with no verb returns a per-action roster report covering every evaluated action.
    /// </summary>
    [Fact]
    public async Task AllRoles_NoVerb_ReturnsPerActionRosters()
    {
        var nodeKey = Guid.NewGuid();
        var path = new[] { Guid.NewGuid(), nodeKey };
        SetupNode(nodeKey, "Campaign");

        _pathResolver.GetPathFromRoot(nodeKey).Returns(path);

        // editors: can read, cannot delete.
        _permissions
            .ResolveForRoleAsync("editors", nodeKey, path, Arg.Any<IEnumerable<string>?>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, EffectivePermission>
            {
                ["Umb.Document.Read"] = Perm("Umb.Document.Read", isAllowed: true, "editors", nodeKey),
                ["Umb.Document.Delete"] = Perm("Umb.Document.Delete", isAllowed: false, "editors", nodeKey),
            });
        // $everyone: cannot read, cannot delete.
        _permissions
            .ResolveForRoleAsync(AdvancedPermissionsConstants.EveryoneRoleAlias, nodeKey, path, Arg.Any<IEnumerable<string>?>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, EffectivePermission>
            {
                ["Umb.Document.Read"] = Perm("Umb.Document.Read", isAllowed: false, AdvancedPermissionsConstants.EveryoneRoleAlias, nodeKey),
                ["Umb.Document.Delete"] = Perm("Umb.Document.Delete", isAllowed: false, AdvancedPermissionsConstants.EveryoneRoleAlias, nodeKey),
            });

        var result = await ((IAITool)CreateTool()).ExecuteAsync(
            new ExplainAccessArgs(ExplainSubject.AllRoles, nodeKey), CancellationToken.None);

        var report = Assert.IsType<AccessRosterReport>(result);
        Assert.Equal("Campaign", report.Node);
        var read = Assert.Single(report.Actions, a => a.Action == "Read");
        Assert.Contains("Editors", read.AllowedRoles);
        Assert.Contains(AdvancedPermissionsConstants.EveryoneRoleDisplayName, read.DeniedRoles);
        var delete = Assert.Single(report.Actions, a => a.Action == "Delete");
        Assert.Contains("Editors", delete.DeniedRoles);

        var json = JsonSerializer.Serialize(report);
        Assert.DoesNotContain("$everyone", json);
        Assert.DoesNotContain("Umb.Document.", json);
        Assert.DoesNotContain(nodeKey.ToString(), json);
    }

    /// <summary>
    /// Concise format trims each verdict to at most one summarized reason, while Detailed keeps the full
    /// reasoning chain.
    /// </summary>
    [Fact]
    public async Task ResponseFormat_ConciseTrimsReasons_DetailedKeepsChain()
    {
        var userKey = Guid.NewGuid();
        var nodeKey = Guid.NewGuid();
        var path = new[] { Guid.NewGuid(), nodeKey };
        const string verb = "Umb.Document.Publish";
        SetupNode(nodeKey, "Homepage");

        _pathResolver.GetPathFromRoot(nodeKey).Returns(path);
        _permissions
            .ResolveAsync(userKey, nodeKey, path, verb, Arg.Any<CancellationToken>())
            .Returns(Perm(verb, isAllowed: false, "editors", nodeKey));

        var concise = await ((IAITool)CreateTool()).ExecuteAsync(
            new ExplainAccessArgs(ExplainSubject.User, nodeKey, UserKey: userKey, Verb: verb, ResponseFormat: ExplainResponseFormat.Concise),
            CancellationToken.None);
        var conciseVerdict = Assert.IsType<AccessVerdict>(concise);
        Assert.True(conciseVerdict.Reasons.Count <= 1);

        // Reset the substitute return for clarity (same value) and request detailed.
        _permissions
            .ResolveAsync(userKey, nodeKey, path, verb, Arg.Any<CancellationToken>())
            .Returns(Perm(verb, isAllowed: false, "editors", nodeKey));

        var detailed = await ((IAITool)CreateTool()).ExecuteAsync(
            new ExplainAccessArgs(ExplainSubject.User, nodeKey, UserKey: userKey, Verb: verb, ResponseFormat: ExplainResponseFormat.Detailed),
            CancellationToken.None);
        var detailedVerdict = Assert.IsType<AccessVerdict>(detailed);
        Assert.Equal(2, detailedVerdict.Reasons.Count);
    }
}
