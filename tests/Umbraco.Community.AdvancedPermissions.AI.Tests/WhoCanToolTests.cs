using NSubstitute;
using Umbraco.AI.Core.Tools;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.AI.Services;
using Umbraco.Community.AdvancedPermissions.AI.Tools;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Tests;

/// <summary>
/// Unit tests for <see cref="WhoCanTool"/>. The tool is invoked through the public
/// <see cref="IAITool.ExecuteAsync(object?, System.Threading.CancellationToken)"/> entry point so the
/// real <see cref="AIToolBase{TArgs}"/> base-class plumbing (argument cast + dispatch) is exercised.
/// </summary>
public sealed class WhoCanToolTests
{
    /// <summary>The mocked user group service the tool enumerates assignable roles from.</summary>
    private readonly IUserGroupService _userGroupService = Substitute.For<IUserGroupService>();

    /// <summary>The mocked path resolver the tool uses to build the root-to-node key path.</summary>
    private readonly IContentPathResolver _pathResolver = Substitute.For<IContentPathResolver>();

    /// <summary>The mocked permission service the tool delegates per-role resolution to.</summary>
    private readonly IAdvancedPermissionService _permissions = Substitute.For<IAdvancedPermissionService>();

    /// <summary>Builds a minimal <see cref="EffectivePermission"/> for a single verb.</summary>
    /// <param name="verb">The verb the permission applies to.</param>
    /// <param name="isAllowed">Whether the permission is allowed.</param>
    /// <returns>An effective permission with an empty reasoning chain.</returns>
    private static EffectivePermission Perm(string verb, bool isAllowed) =>
        new(verb, isAllowed, IsExplicit: true, Reasoning: []);

    /// <summary>Builds a mocked user group exposing the given alias.</summary>
    /// <param name="alias">The alias the group should report.</param>
    /// <returns>A substitute user group.</returns>
    private static IUserGroup Group(string alias)
    {
        var group = Substitute.For<IUserGroup>();
        group.Alias.Returns(alias);
        group.Name.Returns(alias);
        return group;
    }

    /// <summary>
    /// The tool resolves the node path once, enumerates the user groups plus <c>$everyone</c>,
    /// resolves the verb per role, and partitions the roles into allowed and denied buckets.
    /// </summary>
    [Fact]
    public async Task WhoCan_PartitionsRolesByAllowState()
    {
        var nodeKey = Guid.NewGuid();
        var path = new[] { Guid.NewGuid(), nodeKey };
        const string verb = "Umb.Document.Publish";

        _pathResolver.GetPathFromRoot(nodeKey).Returns(path);

        // Build the groups (and configure their substituted properties) before configuring
        // GetAllAsync — configuring a nested substitute inside the Returns argument corrupts
        // NSubstitute's call context.
        var editors = Group("editors");
        var writers = Group("writers");
        var page = new PagedModel<IUserGroup>(2, new[] { editors, writers });

        _userGroupService
            .GetAllAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns(page);

        // editors allow; writers deny; $everyone deny.
        _permissions
            .ResolveForRoleAsync(
                "editors",
                nodeKey,
                path,
                Arg.Is<IEnumerable<string>>(v => v != null && v.SequenceEqual(new[] { verb })),
                Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, EffectivePermission> { [verb] = Perm(verb, isAllowed: true) });

        _permissions
            .ResolveForRoleAsync(
                "writers",
                nodeKey,
                path,
                Arg.Is<IEnumerable<string>>(v => v != null && v.SequenceEqual(new[] { verb })),
                Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, EffectivePermission> { [verb] = Perm(verb, isAllowed: false) });

        _permissions
            .ResolveForRoleAsync(
                AdvancedPermissionsConstants.EveryoneRoleAlias,
                nodeKey,
                path,
                Arg.Is<IEnumerable<string>>(v => v != null && v.SequenceEqual(new[] { verb })),
                Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, EffectivePermission> { [verb] = Perm(verb, isAllowed: false) });

        var tool = new WhoCanTool(_userGroupService, _pathResolver, _permissions);
        var result = await ((IAITool)tool).ExecuteAsync(
            new WhoCanArgs(nodeKey, verb), CancellationToken.None);

        var typed = Assert.IsType<WhoCanResult>(result);
        Assert.Equal(verb, typed.Verb);
        Assert.Equal(nodeKey, typed.NodeKey);
        Assert.Contains("editors", typed.AllowedRoles);
        Assert.Contains("writers", typed.DeniedRoles);
        Assert.Contains(AdvancedPermissionsConstants.EveryoneRoleAlias, typed.DeniedRoles);
        Assert.DoesNotContain("writers", typed.AllowedRoles);
        Assert.DoesNotContain("editors", typed.DeniedRoles);

        _pathResolver.Received(1).GetPathFromRoot(nodeKey);
    }
}
