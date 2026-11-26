using NSubstitute;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.AuthorizationStatus;
using Umbraco.Community.AdvancedPermissions.Abstractions.Constants;
using Umbraco.Community.AdvancedPermissions.Abstractions.Interfaces;
using Umbraco.Community.AdvancedPermissions.Abstractions.Models;
using Umbraco.Community.AdvancedPermissions.Services;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests the root check of <see cref="AdvancedElementContainerPermissionService"/>, which Umbraco asks when a
/// Library folder is created or moved at the Library root.
/// </summary>
public sealed class AdvancedElementContainerPermissionServiceTests
{
    private readonly IElementNodePermissionService _elementPermissionService = Substitute.For<IElementNodePermissionService>();
    private readonly IEntityService _entityService = Substitute.For<IEntityService>();
    private readonly AdvancedElementContainerPermissionService _sut;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdvancedElementContainerPermissionServiceTests"/> class.
    /// </summary>
    public AdvancedElementContainerPermissionServiceTests()
    {
        _sut = new AdvancedElementContainerPermissionService(
            _elementPermissionService,
            _entityService,
            AppCaches.Disabled);
    }

    /// <summary>
    /// Folder verbs are stored as their canonical element verbs, so the root check maps
    /// <c>Umb.ElementContainer.Create</c> to <c>Umb.Element.Create</c> and resolves it at the package's
    /// virtual root.
    /// </summary>
    /// <param name="isAllowed">Whether the resolver allows the canonical verb at the virtual root.</param>
    /// <param name="expected">The status the root check must return.</param>
    [Theory]
    [InlineData(true, ElementAuthorizationStatus.Success)]
    [InlineData(false, ElementAuthorizationStatus.UnauthorizedMissingPermissionAccess)]
    public async Task AuthorizeRootAccessAsync_ResolvesTheCanonicalVerbsAtTheVirtualRoot(bool isAllowed, ElementAuthorizationStatus expected)
    {
        var userKey = Guid.NewGuid();
        var user = StubUserWithElementStartNode(userKey, Constants.System.Root);
        _elementPermissionService
            .ResolveAllAsync(
                userKey,
                AdvancedPermissionsConstants.VirtualRootNodeKey,
                Arg.Is<IReadOnlyList<Guid>>(path => path.SequenceEqual(new[] { AdvancedPermissionsConstants.VirtualRootNodeKey })),
                Arg.Is<IEnumerable<string>?>(verbs => verbs != null && verbs.SequenceEqual(new[] { AdvancedPermissionsConstants.VerbElementCreate })),
                Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, EffectivePermission>
            {
                [AdvancedPermissionsConstants.VerbElementCreate] = new(AdvancedPermissionsConstants.VerbElementCreate, isAllowed, IsExplicit: false, Reasoning: []),
            });

        var result = await _sut.AuthorizeRootAccessAsync(user, new HashSet<string> { AdvancedPermissionsConstants.VerbElementContainerCreate });

        Assert.Equal(expected, result);
    }

    /// <summary>
    /// Start nodes are checked first: without access to the Library root the verbs are not resolved at all.
    /// </summary>
    [Fact]
    public async Task AuthorizeRootAccessAsync_WithoutRootStartNodeAccess_IsDeniedBeforeResolving()
    {
        var user = StubUserWithElementStartNode(Guid.NewGuid(), 1234);

        var result = await _sut.AuthorizeRootAccessAsync(user, new HashSet<string> { AdvancedPermissionsConstants.VerbElementContainerCreate });

        Assert.Equal(ElementAuthorizationStatus.UnauthorizedMissingRootAccess, result);
        await _elementPermissionService.DidNotReceiveWithAnyArgs().ResolveAllAsync(default, default, default!, default, default);
    }

    /// <summary>
    /// Creates a user whose single group has the given Library start node.
    /// </summary>
    /// <param name="key">The user key.</param>
    /// <param name="startElementId">The group's Library start node id; <c>-1</c> is the Library root.</param>
    /// <returns>The substitute user.</returns>
    private static IUser StubUserWithElementStartNode(Guid key, int startElementId)
    {
        var group = Substitute.For<IReadOnlyUserGroup>();
        group.StartElementId.Returns(startElementId);
        var user = Substitute.For<IUser>();
        user.Key.Returns(key);
        user.Id.Returns(Math.Abs(key.GetHashCode()));
        user.Groups.Returns([group]);
        user.StartElementIds.Returns([]);
        return user;
    }
}
