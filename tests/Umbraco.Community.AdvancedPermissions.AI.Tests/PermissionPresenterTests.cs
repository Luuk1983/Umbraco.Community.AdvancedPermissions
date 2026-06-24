using NSubstitute;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.AI.Services;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Tests;

/// <summary>
/// Unit tests for <see cref="PermissionPresenter"/>. These verify that every raw identifier
/// (role alias, verb, scope, state, node key) is mapped to a friendly, editor-facing label,
/// so the LLM never sees <c>$everyone</c>, <c>Umb.Document.*</c>, enum names, or raw GUIDs.
/// </summary>
public sealed class PermissionPresenterTests
{
    /// <summary>The mocked user group service used to resolve group aliases to display names.</summary>
    private readonly IUserGroupService _userGroupService = Substitute.For<IUserGroupService>();

    /// <summary>The mocked entity service used to resolve content node keys to names.</summary>
    private readonly IEntityService _entityService = Substitute.For<IEntityService>();

    /// <summary>The mocked content-type service used to resolve document type keys to names.</summary>
    private readonly IContentTypeService _contentTypeService = Substitute.For<IContentTypeService>();

    /// <summary>Builds a substitute user group with the given alias and name.</summary>
    /// <param name="alias">The alias the group reports.</param>
    /// <param name="name">The display name the group reports.</param>
    /// <returns>The substitute user group.</returns>
    private static IUserGroup Group(string alias, string name)
    {
        var group = Substitute.For<IUserGroup>();
        group.Alias.Returns(alias);
        group.Name.Returns(name);
        return group;
    }

    /// <summary>Configures the user group service to return the supplied groups in a single page.</summary>
    /// <param name="groups">The groups to expose.</param>
    private void SetupGroups(params IUserGroup[] groups) =>
        _userGroupService
            .GetAllAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns(new PagedModel<IUserGroup>(groups.Length, groups));

    /// <summary>Configures the entity service so the given node key resolves to the given name.</summary>
    /// <param name="key">The node key.</param>
    /// <param name="name">The node name to report.</param>
    private void SetupNode(Guid key, string name)
    {
        var entity = Substitute.For<IEntitySlim>();
        entity.Name.Returns(name);
        _entityService.Get(key, UmbracoObjectTypes.Document).Returns(entity);
    }

    /// <summary>Creates the presenter under test with no groups configured by default.</summary>
    /// <returns>The presenter.</returns>
    private PermissionPresenter CreateSut()
    {
        if (!_userGroupService.ReceivedCalls().Any())
        {
            SetupGroups();
        }

        return new PermissionPresenter(_userGroupService, _entityService, _contentTypeService);
    }

    /// <summary>The <c>$everyone</c> alias maps to the "All Users" display name.</summary>
    [Fact]
    public async Task RoleDisplayName_Everyone_MapsToAllUsers()
    {
        var sut = CreateSut();
        var name = await sut.GetRoleDisplayNameAsync(AdvancedPermissionsConstants.EveryoneRoleAlias);
        Assert.Equal(AdvancedPermissionsConstants.EveryoneRoleDisplayName, name);
        Assert.Equal("All Users", name);
    }

    /// <summary>A known group alias maps to the group's name.</summary>
    [Fact]
    public async Task RoleDisplayName_KnownGroup_MapsToName()
    {
        SetupGroups(Group("editors", "Editors"));
        var sut = new PermissionPresenter(_userGroupService, _entityService, _contentTypeService);

        var name = await sut.GetRoleDisplayNameAsync("editors");

        Assert.Equal("Editors", name);
    }

    /// <summary>An unknown alias falls back to the alias itself.</summary>
    [Fact]
    public async Task RoleDisplayName_UnknownGroup_FallsBackToAlias()
    {
        SetupGroups(Group("editors", "Editors"));
        var sut = new PermissionPresenter(_userGroupService, _entityService, _contentTypeService);

        var name = await sut.GetRoleDisplayNameAsync("ghosts");

        Assert.Equal("ghosts", name);
    }

    /// <summary>A verb maps to the substring after the last dot.</summary>
    [Theory]
    [InlineData("Umb.Document.Delete", "Delete")]
    [InlineData("Umb.Document.Read", "Read")]
    [InlineData("Umb.Document.Publish", "Publish")]
    [InlineData("Read", "Read")]
    public void Verb_MapsToFriendlyName(string verb, string expected)
    {
        var sut = CreateSut();
        Assert.Equal(expected, sut.GetVerbDisplayName(verb));
    }

    /// <summary>Each scope value maps to friendly text.</summary>
    [Theory]
    [InlineData(PermissionScope.ThisNodeOnly, "This node only")]
    [InlineData(PermissionScope.ThisNodeAndDescendants, "This node and descendants")]
    [InlineData(PermissionScope.DescendantsOnly, "Descendants only")]
    public void Scope_MapsToFriendlyText(PermissionScope scope, string expected)
    {
        var sut = CreateSut();
        Assert.Equal(expected, sut.GetScopeText(scope));
    }

    /// <summary>Each state value maps to friendly text.</summary>
    [Theory]
    [InlineData(PermissionState.Allow, "Allowed")]
    [InlineData(PermissionState.Deny, "Denied")]
    public void State_MapsToFriendlyText(PermissionState state, string expected)
    {
        var sut = CreateSut();
        Assert.Equal(expected, sut.GetStateText(state));
    }

    /// <summary>A resolvable node key maps to its content name.</summary>
    [Fact]
    public void NodeName_Resolvable_MapsToName()
    {
        var key = Guid.NewGuid();
        SetupNode(key, "Homepage");
        var sut = CreateSut();

        Assert.Equal("Homepage", sut.GetNodeName(key));
    }

    /// <summary>The virtual-root sentinel key maps to the special root-default label.</summary>
    [Fact]
    public void NodeName_VirtualRoot_MapsToSpecialLabel()
    {
        var sut = CreateSut();
        Assert.Equal(
            "All content (root-level default)",
            sut.GetNodeName(AdvancedPermissionsConstants.VirtualRootNodeKey));
    }

    /// <summary>An unresolvable node key falls back to the generic "this node" label.</summary>
    [Fact]
    public void NodeName_Unresolvable_FallsBackToThisNode()
    {
        var sut = CreateSut();
        Assert.Equal("this node", sut.GetNodeName(Guid.NewGuid()));
    }

    /// <summary>
    /// An effective permission with reasoning maps to a friendly verdict: friendly action,
    /// friendly result, and friendly reasons with no raw identifiers.
    /// </summary>
    [Fact]
    public async Task ToVerdict_MapsReasoningToFriendlyLabels()
    {
        SetupGroups(Group("editors", "Editors"));
        var nodeKey = Guid.NewGuid();
        SetupNode(nodeKey, "News");
        var sut = new PermissionPresenter(_userGroupService, _entityService, _contentTypeService);

        var permission = new EffectivePermission(
            "Umb.Document.Delete",
            IsAllowed: false,
            IsExplicit: true,
            Reasoning:
            [
                new PermissionReasoning(
                    "editors",
                    PermissionState.Deny,
                    IsExplicit: true,
                    SourceNodeKey: nodeKey,
                    SourceScope: PermissionScope.ThisNodeOnly,
                    IsFromGroupDefault: false,
                    IsPriorityOverride: true),
                new PermissionReasoning(
                    AdvancedPermissionsConstants.EveryoneRoleAlias,
                    PermissionState.Allow,
                    IsExplicit: false,
                    SourceNodeKey: AdvancedPermissionsConstants.VirtualRootNodeKey,
                    SourceScope: PermissionScope.ThisNodeAndDescendants,
                    IsFromGroupDefault: true),
            ]);

        var verdict = await sut.ToVerdictAsync(permission);

        Assert.Equal("Delete", verdict.Action);
        Assert.Equal("Denied", verdict.Result);
        Assert.Equal(2, verdict.Reasons.Count);

        var first = verdict.Reasons[0];
        Assert.Equal("Editors", first.Role);
        Assert.Equal("Denied", first.Decision);
        Assert.Equal("This node only", first.Scope);
        Assert.Equal("News", first.SetOn);
        Assert.False(first.Inherited);
        Assert.True(first.PriorityOverride);

        var second = verdict.Reasons[1];
        Assert.Equal("All Users", second.Role);
        Assert.Equal("Allowed", second.Decision);
        Assert.Equal("This node and descendants", second.Scope);
        Assert.Equal("All content (root-level default)", second.SetOn);
        Assert.True(second.Inherited);
        Assert.False(second.PriorityOverride);
    }

    /// <summary>
    /// A verb dictionary maps to a friendly explanation with the node name and one verdict per verb.
    /// </summary>
    [Fact]
    public async Task ToExplanation_MapsDictionaryToFriendlyExplanation()
    {
        SetupGroups(Group("editors", "Editors"));
        var nodeKey = Guid.NewGuid();
        SetupNode(nodeKey, "News");
        var sut = new PermissionPresenter(_userGroupService, _entityService, _contentTypeService);

        var permissions = new Dictionary<string, EffectivePermission>
        {
            ["Umb.Document.Read"] = new(
                "Umb.Document.Read", IsAllowed: true, IsExplicit: false, Reasoning: []),
            ["Umb.Document.Delete"] = new(
                "Umb.Document.Delete", IsAllowed: false, IsExplicit: true, Reasoning: []),
        };

        var explanation = await sut.ToExplanationAsync(permissions, nodeKey);

        Assert.Equal("News", explanation.Node);
        Assert.Equal(2, explanation.Permissions.Count);
        Assert.Contains(explanation.Permissions, p => p.Action == "Read" && p.Result == "Allowed");
        Assert.Contains(explanation.Permissions, p => p.Action == "Delete" && p.Result == "Denied");
    }

    /// <summary>
    /// A raw audit report is projected to friendly findings: role alias becomes display name, verb
    /// becomes friendly action, node key becomes node name, severity becomes text, and the rewritten
    /// message contains none of the raw identifiers.
    /// </summary>
    [Fact]
    public async Task ToFriendlyAudit_ProjectsFindingsToFriendlyLabels()
    {
        SetupGroups(Group("editors", "Editors"));
        var nodeKey = Guid.NewGuid();
        SetupNode(nodeKey, "News");
        var sut = new PermissionPresenter(_userGroupService, _entityService, _contentTypeService);

        var report = new AuditReport(
            new[]
            {
                new AuditFinding(
                    "everyone-broad-write",
                    AuditSeverity.Risk,
                    "Everyone is allowed 'Umb.Document.Update' across the whole tree from the root.",
                    AdvancedPermissionsConstants.VirtualRootNodeKey,
                    AdvancedPermissionsConstants.EveryoneRoleAlias,
                    "Umb.Document.Update"),
                new AuditFinding(
                    "allow-deny-conflict",
                    AuditSeverity.Warning,
                    "Role 'editors' has both Allow and Deny for 'Umb.Document.Delete' on the same node.",
                    nodeKey,
                    "editors",
                    "Umb.Document.Delete"),
            },
            EntriesAnalyzed: 5);

        var friendly = await sut.ToFriendlyAuditAsync(report);

        Assert.Equal(5, friendly.EntriesAnalyzed);
        Assert.Equal(2, friendly.Findings.Count);

        var first = friendly.Findings[0];
        Assert.Equal("everyone-broad-write", first.RuleId);
        Assert.Equal("Risk", first.Severity);
        Assert.Equal("All Users", first.Role);
        Assert.Equal("Update", first.Action);
        Assert.Equal("All content (root-level default)", first.Node);

        var second = friendly.Findings[1];
        Assert.Equal("Warning", second.Severity);
        Assert.Equal("Editors", second.Role);
        Assert.Equal("Delete", second.Action);
        Assert.Equal("News", second.Node);

        // No raw identifiers anywhere in the projected report.
        var json = System.Text.Json.JsonSerializer.Serialize(friendly);
        Assert.DoesNotContain("$everyone", json);
        Assert.DoesNotContain("Umb.Document.", json);
        Assert.DoesNotContain(nodeKey.ToString(), json);
    }
}
