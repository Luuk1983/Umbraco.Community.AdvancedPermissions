using NSubstitute;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.AuthorizationStatus;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;
using Umbraco.Community.AdvancedPermissions.Services;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests <see cref="AdvancedElementPermissionService.GetPermissionsAsync"/> — the per-element effective
/// permission seam that resolves library element permissions through the Advanced Security model — plus
/// the Library root check (<see cref="AdvancedElementPermissionService.AuthorizeRootAccessAsync"/>). The
/// other path-walking <c>Authorize*</c> methods are start-node-coupled and follow the same
/// not-unit-tested precedent as <see cref="AdvancedContentPermissionService"/>.
/// </summary>
public sealed class AdvancedElementPermissionServiceTests
{
    private readonly IElementNodePermissionService _elementPermissionService = Substitute.For<IElementNodePermissionService>();
    private readonly IEntityService _entityService = Substitute.For<IEntityService>();
    private readonly ILanguageService _languageService = Substitute.For<ILanguageService>();
    private readonly AdvancedElementPermissionService _sut;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdvancedElementPermissionServiceTests"/> class.
    /// </summary>
    public AdvancedElementPermissionServiceTests()
    {
        _sut = new AdvancedElementPermissionService(
            _elementPermissionService,
            _entityService,
            AppCaches.Disabled,
            _languageService);
    }

    /// <summary>
    /// An empty input set yields an empty result without touching any dependency.
    /// </summary>
    [Fact]
    public async Task GetPermissionsAsync_EmptyInput_ReturnsEmpty()
    {
        var user = StubUser(Guid.NewGuid());

        var result = await _sut.GetPermissionsAsync(user, []);

        Assert.Empty(result);
    }

    /// <summary>
    /// For a known element, only the verbs the resolver marks allowed are surfaced; denied verbs are omitted.
    /// </summary>
    [Fact]
    public async Task GetPermissionsAsync_KnownElement_ReturnsOnlyAllowedVerbs()
    {
        var userKey = Guid.NewGuid();
        var user = StubUser(userKey);
        var nodeKey = Guid.NewGuid();
        var entity = StubEntity(42, nodeKey);

        _entityService
            .GetAllPaths(Arg.Any<IEnumerable<UmbracoObjectTypes>>(), Arg.Any<Guid[]>())
            .Returns([new TreeEntityPath { Id = 42, Key = nodeKey, Path = "-1,42" }]);
        _entityService
            .GetAll(Arg.Any<IEnumerable<UmbracoObjectTypes>>(), Arg.Any<int[]>())
            .Returns([entity]);
        _entityService
            .GetAll(Arg.Any<IEnumerable<UmbracoObjectTypes>>(), Arg.Any<Guid[]>())
            .Returns([entity]);
        _elementPermissionService
            .ResolveAllAsync(userKey, nodeKey, Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<IEnumerable<string>?>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, EffectivePermission>
            {
                [AdvancedPermissionsConstants.VerbElementRead] = new(AdvancedPermissionsConstants.VerbElementRead, IsAllowed: true, IsExplicit: true, Reasoning: []),
                [AdvancedPermissionsConstants.VerbElementDelete] = new(AdvancedPermissionsConstants.VerbElementDelete, IsAllowed: false, IsExplicit: false, Reasoning: []),
            });

        var result = (await _sut.GetPermissionsAsync(user, [nodeKey])).ToArray();

        var node = Assert.Single(result);
        Assert.Equal(nodeKey, node.NodeKey);
        Assert.Contains(AdvancedPermissionsConstants.VerbElementRead, node.Permissions);
        Assert.DoesNotContain(AdvancedPermissionsConstants.VerbElementDelete, node.Permissions);
    }

    /// <summary>
    /// Exactly one entry is returned per requested key — including keys that do not resolve to an
    /// existing element (unknown elements return an empty verb set).
    /// </summary>
    [Fact]
    public async Task GetPermissionsAsync_ReturnsOneEntryPerRequestedKey_IncludingUnknownElements()
    {
        var userKey = Guid.NewGuid();
        var user = StubUser(userKey);
        var knownKey = Guid.NewGuid();
        var unknownKey = Guid.NewGuid();
        var entity = StubEntity(7, knownKey);

        _entityService
            .GetAllPaths(Arg.Any<IEnumerable<UmbracoObjectTypes>>(), Arg.Any<Guid[]>())
            .Returns([new TreeEntityPath { Id = 7, Key = knownKey, Path = "-1,7" }]);
        _entityService
            .GetAll(Arg.Any<IEnumerable<UmbracoObjectTypes>>(), Arg.Any<int[]>())
            .Returns([entity]);
        _entityService
            .GetAll(Arg.Any<IEnumerable<UmbracoObjectTypes>>(), Arg.Any<Guid[]>())
            .Returns([entity]);
        _elementPermissionService
            .ResolveAllAsync(userKey, knownKey, Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<IEnumerable<string>?>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, EffectivePermission>
            {
                [AdvancedPermissionsConstants.VerbElementRead] = new(AdvancedPermissionsConstants.VerbElementRead, IsAllowed: true, IsExplicit: true, Reasoning: []),
            });

        var result = (await _sut.GetPermissionsAsync(user, [knownKey, unknownKey])).ToArray();

        Assert.Equal(2, result.Length);
        Assert.Contains(AdvancedPermissionsConstants.VerbElementRead, result.Single(r => r.NodeKey == knownKey).Permissions);
        Assert.Empty(result.Single(r => r.NodeKey == unknownKey).Permissions);
    }

    /// <summary>
    /// Regression test for the "folders created but hidden from the Library tree" bug: the tree's permission
    /// filter checks a folder's browse verb <c>Umb.ElementContainer.Read</c>, but permissions are stored
    /// against the canonical <c>Umb.Element.Read</c>. <see cref="AdvancedElementPermissionService.GetPermissionsAsync"/>
    /// must therefore return the CONTAINER verbs for a folder key, not the raw canonical element verbs.
    /// </summary>
    [Fact]
    public async Task GetPermissionsAsync_FolderKey_ReturnsContainerVerbs()
    {
        var userKey = Guid.NewGuid();
        var user = StubUser(userKey);
        var folderKey = Guid.NewGuid();
        var folder = StubFolderEntity(55, folderKey);

        _entityService
            .GetAllPaths(Arg.Any<IEnumerable<UmbracoObjectTypes>>(), Arg.Any<Guid[]>())
            .Returns([new TreeEntityPath { Id = 55, Key = folderKey, Path = "-1,55" }]);
        _entityService
            .GetAll(Arg.Any<IEnumerable<UmbracoObjectTypes>>(), Arg.Any<Guid[]>())
            .Returns([folder]);
        _entityService
            .GetAll(Arg.Any<IEnumerable<UmbracoObjectTypes>>(), Arg.Any<int[]>())
            .Returns([folder]);
        _elementPermissionService
            .ResolveAllAsync(userKey, folderKey, Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<IEnumerable<string>?>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, EffectivePermission>
            {
                [AdvancedPermissionsConstants.VerbElementRead] = new(AdvancedPermissionsConstants.VerbElementRead, IsAllowed: true, IsExplicit: false, Reasoning: []),
                [AdvancedPermissionsConstants.VerbElementPublish] = new(AdvancedPermissionsConstants.VerbElementPublish, IsAllowed: true, IsExplicit: false, Reasoning: []),
            });

        var node = Assert.Single(await _sut.GetPermissionsAsync(user, [folderKey]));

        // Read maps to the container browse verb the tree filter checks.
        Assert.Contains(AdvancedPermissionsConstants.VerbElementContainerRead, node.Permissions);
        // The canonical element verb itself is NOT surfaced for a folder…
        Assert.DoesNotContain(AdvancedPermissionsConstants.VerbElementRead, node.Permissions);
        // …and element-only verbs (Publish) have no container counterpart, so they're dropped.
        Assert.DoesNotContain(AdvancedPermissionsConstants.VerbElementPublish, node.Permissions);
    }

    // ─── AuthorizeRootAccessAsync ─────────────────────────────────────────────

    /// <summary>
    /// Umbraco asks the root check whenever an element is created, moved, copied or published at the
    /// Library root. Beyond start-node access, every requested verb must be allowed at the package's virtual
    /// root, the same layer the Library Access Viewer shows for the root.
    /// </summary>
    /// <param name="isAllowed">Whether the resolver allows the verb at the virtual root.</param>
    /// <param name="expected">The status the root check must return.</param>
    [Theory]
    [InlineData(true, ElementAuthorizationStatus.Success)]
    [InlineData(false, ElementAuthorizationStatus.UnauthorizedMissingPermissionAccess)]
    public async Task AuthorizeRootAccessAsync_ResolvesTheVerbsAtTheVirtualRoot(bool isAllowed, ElementAuthorizationStatus expected)
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

        var result = await _sut.AuthorizeRootAccessAsync(user, new HashSet<string> { AdvancedPermissionsConstants.VerbElementCreate });

        Assert.Equal(expected, result);
    }

    /// <summary>
    /// Start nodes are checked first: without access to the Library root the verbs are not resolved at all.
    /// </summary>
    [Fact]
    public async Task AuthorizeRootAccessAsync_WithoutRootStartNodeAccess_IsDeniedBeforeResolving()
    {
        var user = StubUserWithElementStartNode(Guid.NewGuid(), 1234);

        var result = await _sut.AuthorizeRootAccessAsync(user, new HashSet<string> { AdvancedPermissionsConstants.VerbElementCreate });

        Assert.Equal(ElementAuthorizationStatus.UnauthorizedMissingRootAccess, result);
        await _elementPermissionService.DidNotReceiveWithAnyArgs().ResolveAllAsync(default, default, default!, default, default);
    }

    // ─── AuthorizeBinAccessAsync ──────────────────────────────────────────────

    /// <summary>
    /// Umbraco asks the recycle bin check to delete an element permanently or empty the bin (Delete), to
    /// restore (Move) and to look up an element's original parent (Read). The bin is not in the tree, so, like
    /// Umbraco's own service, the verbs are decided by the group defaults: the package's virtual root.
    /// </summary>
    /// <param name="isAllowed">Whether the resolver allows the verb at the virtual root.</param>
    /// <param name="expected">The status the recycle bin check must return.</param>
    [Theory]
    [InlineData(true, ElementAuthorizationStatus.Success)]
    [InlineData(false, ElementAuthorizationStatus.UnauthorizedMissingPermissionAccess)]
    public async Task AuthorizeBinAccessAsync_ResolvesTheVerbsAtTheVirtualRoot(bool isAllowed, ElementAuthorizationStatus expected)
    {
        var userKey = Guid.NewGuid();
        var user = StubUserWithElementStartNode(userKey, Constants.System.Root);
        _elementPermissionService
            .ResolveAllAsync(
                userKey,
                AdvancedPermissionsConstants.VirtualRootNodeKey,
                Arg.Is<IReadOnlyList<Guid>>(path => path.SequenceEqual(new[] { AdvancedPermissionsConstants.VirtualRootNodeKey })),
                Arg.Is<IEnumerable<string>?>(verbs => verbs != null && verbs.SequenceEqual(new[] { AdvancedPermissionsConstants.VerbElementDelete })),
                Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, EffectivePermission>
            {
                [AdvancedPermissionsConstants.VerbElementDelete] = new(AdvancedPermissionsConstants.VerbElementDelete, isAllowed, IsExplicit: false, Reasoning: []),
            });

        var result = await _sut.AuthorizeBinAccessAsync(user, new HashSet<string> { AdvancedPermissionsConstants.VerbElementDelete });

        Assert.Equal(expected, result);
    }

    /// <summary>
    /// Start nodes are checked first: without access to the Library recycle bin the verbs are not resolved.
    /// </summary>
    [Fact]
    public async Task AuthorizeBinAccessAsync_WithoutBinStartNodeAccess_IsDeniedBeforeResolving()
    {
        var user = StubUserWithElementStartNode(Guid.NewGuid(), 1234);

        var result = await _sut.AuthorizeBinAccessAsync(user, new HashSet<string> { AdvancedPermissionsConstants.VerbElementDelete });

        Assert.Equal(ElementAuthorizationStatus.UnauthorizedMissingBinAccess, result);
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

    private static IUser StubUser(Guid key)
    {
        var user = Substitute.For<IUser>();
        user.Key.Returns(key);
        return user;
    }

    private static IEntitySlim StubFolderEntity(int id, Guid key)
    {
        var entity = Substitute.For<IEntitySlim>();
        entity.Id.Returns(id);
        entity.Key.Returns(key);
        entity.NodeObjectType.Returns(Constants.ObjectTypes.ElementContainer);
        return entity;
    }

    private static IEntitySlim StubEntity(int id, Guid key)
    {
        var entity = Substitute.For<IEntitySlim>();
        entity.Id.Returns(id);
        entity.Key.Returns(key);
        return entity;
    }
}
