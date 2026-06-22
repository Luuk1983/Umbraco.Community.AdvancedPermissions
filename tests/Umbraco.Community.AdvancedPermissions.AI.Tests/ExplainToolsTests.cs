using NSubstitute;
using Umbraco.AI.Core.Tools;
using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.AI.Services;
using Umbraco.Community.AdvancedPermissions.AI.Tools;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Tests;

/// <summary>
/// Unit tests for <see cref="ExplainUserAccessTool"/> and <see cref="ExplainRoleAccessTool"/>.
/// Each tool is invoked through the public <see cref="IAITool.ExecuteAsync(object?, System.Threading.CancellationToken)"/>
/// entry point so the real <see cref="AIToolBase{TArgs}"/> base-class plumbing (argument cast + dispatch)
/// is exercised, not just the protected typed override.
/// </summary>
public sealed class ExplainToolsTests
{
    /// <summary>The mocked permission service the tools delegate resolution to.</summary>
    private readonly IAdvancedPermissionService _permissions = Substitute.For<IAdvancedPermissionService>();

    /// <summary>The mocked path resolver the tools use to build the root-to-node key path.</summary>
    private readonly IContentPathResolver _pathResolver = Substitute.For<IContentPathResolver>();

    /// <summary>Builds a minimal <see cref="EffectivePermission"/> for assertions.</summary>
    /// <param name="verb">The verb the permission applies to.</param>
    /// <param name="isAllowed">Whether the permission is allowed.</param>
    /// <returns>An effective permission with an empty reasoning chain.</returns>
    private static EffectivePermission Perm(string verb, bool isAllowed) =>
        new(verb, isAllowed, IsExplicit: true, Reasoning: []);

    /// <summary>
    /// With no verb, <see cref="ExplainUserAccessTool"/> resolves the path once, calls
    /// <see cref="IAdvancedPermissionService.ResolveAllAsync"/> with that exact path, and returns the dictionary.
    /// </summary>
    [Fact]
    public async Task ExplainUserAccess_NoVerb_CallsResolveAll_ReturnsDictionary()
    {
        var userKey = Guid.NewGuid();
        var nodeKey = Guid.NewGuid();
        var path = new[] { Guid.NewGuid(), nodeKey };
        var expected = new Dictionary<string, EffectivePermission>
        {
            ["Umb.Document.Read"] = Perm("Umb.Document.Read", isAllowed: true),
        };

        _pathResolver.GetPathFromRoot(nodeKey).Returns(path);
        _permissions
            .ResolveAllAsync(userKey, nodeKey, path, null, Arg.Any<CancellationToken>())
            .Returns(expected);

        var tool = new ExplainUserAccessTool(_permissions, _pathResolver);
        var result = await ((IAITool)tool).ExecuteAsync(
            new ExplainUserAccessArgs(userKey, nodeKey), CancellationToken.None);

        var dict = Assert.IsAssignableFrom<IReadOnlyDictionary<string, EffectivePermission>>(result);
        Assert.True(dict["Umb.Document.Read"].IsAllowed);
        _pathResolver.Received(1).GetPathFromRoot(nodeKey);
        await _permissions.Received(1).ResolveAllAsync(userKey, nodeKey, path, null, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// With a verb, <see cref="ExplainUserAccessTool"/> calls
    /// <see cref="IAdvancedPermissionService.ResolveAsync"/> with the resolved path and returns the single permission.
    /// </summary>
    [Fact]
    public async Task ExplainUserAccess_WithVerb_CallsResolve_ReturnsSinglePermission()
    {
        var userKey = Guid.NewGuid();
        var nodeKey = Guid.NewGuid();
        var path = new[] { Guid.NewGuid(), nodeKey };
        const string verb = "Umb.Document.Publish";
        var expected = Perm(verb, isAllowed: false);

        _pathResolver.GetPathFromRoot(nodeKey).Returns(path);
        _permissions
            .ResolveAsync(userKey, nodeKey, path, verb, Arg.Any<CancellationToken>())
            .Returns(expected);

        var tool = new ExplainUserAccessTool(_permissions, _pathResolver);
        var result = await ((IAITool)tool).ExecuteAsync(
            new ExplainUserAccessArgs(userKey, nodeKey, verb), CancellationToken.None);

        var perm = Assert.IsType<EffectivePermission>(result);
        Assert.Equal(verb, perm.Verb);
        Assert.False(perm.IsAllowed);
        _pathResolver.Received(1).GetPathFromRoot(nodeKey);
        await _permissions.Received(1).ResolveAsync(userKey, nodeKey, path, verb, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// With no verb, <see cref="ExplainRoleAccessTool"/> calls
    /// <see cref="IAdvancedPermissionService.ResolveForRoleAsync"/> with a <see langword="null"/> verb set
    /// and returns the dictionary.
    /// </summary>
    [Fact]
    public async Task ExplainRoleAccess_NoVerb_CallsResolveForRole_ReturnsDictionary()
    {
        const string roleAlias = "editors";
        var nodeKey = Guid.NewGuid();
        var path = new[] { Guid.NewGuid(), nodeKey };
        var expected = new Dictionary<string, EffectivePermission>
        {
            ["Umb.Document.Read"] = Perm("Umb.Document.Read", isAllowed: true),
        };

        _pathResolver.GetPathFromRoot(nodeKey).Returns(path);
        _permissions
            .ResolveForRoleAsync(roleAlias, nodeKey, path, null, Arg.Any<CancellationToken>())
            .Returns(expected);

        var tool = new ExplainRoleAccessTool(_permissions, _pathResolver);
        var result = await ((IAITool)tool).ExecuteAsync(
            new ExplainRoleAccessArgs(roleAlias, nodeKey), CancellationToken.None);

        var dict = Assert.IsAssignableFrom<IReadOnlyDictionary<string, EffectivePermission>>(result);
        Assert.True(dict["Umb.Document.Read"].IsAllowed);
        _pathResolver.Received(1).GetPathFromRoot(nodeKey);
        await _permissions.Received(1).ResolveForRoleAsync(roleAlias, nodeKey, path, null, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// With a verb, <see cref="ExplainRoleAccessTool"/> calls
    /// <see cref="IAdvancedPermissionService.ResolveForRoleAsync"/> with a single-element verb set.
    /// </summary>
    [Fact]
    public async Task ExplainRoleAccess_WithVerb_CallsResolveForRole_WithSingleVerb()
    {
        const string roleAlias = "editors";
        var nodeKey = Guid.NewGuid();
        var path = new[] { Guid.NewGuid(), nodeKey };
        const string verb = "Umb.Document.Publish";
        var expected = new Dictionary<string, EffectivePermission>
        {
            [verb] = Perm(verb, isAllowed: false),
        };

        _pathResolver.GetPathFromRoot(nodeKey).Returns(path);
        _permissions
            .ResolveForRoleAsync(
                roleAlias,
                nodeKey,
                path,
                Arg.Is<IEnumerable<string>>(v => v != null && v.SequenceEqual(new[] { verb })),
                Arg.Any<CancellationToken>())
            .Returns(expected);

        var tool = new ExplainRoleAccessTool(_permissions, _pathResolver);
        var result = await ((IAITool)tool).ExecuteAsync(
            new ExplainRoleAccessArgs(roleAlias, nodeKey, verb), CancellationToken.None);

        var dict = Assert.IsAssignableFrom<IReadOnlyDictionary<string, EffectivePermission>>(result);
        Assert.False(dict[verb].IsAllowed);
        _pathResolver.Received(1).GetPathFromRoot(nodeKey);
        await _permissions.Received(1).ResolveForRoleAsync(
            roleAlias,
            nodeKey,
            path,
            Arg.Is<IEnumerable<string>>(v => v != null && v.SequenceEqual(new[] { verb })),
            Arg.Any<CancellationToken>());
    }
}
