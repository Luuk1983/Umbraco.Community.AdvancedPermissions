using NSubstitute;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.Abstractions.Constants;
using Umbraco.Community.AdvancedPermissions.Abstractions.Interfaces;
using Umbraco.Community.AdvancedPermissions.Abstractions.Models;
using Umbraco.Community.AdvancedPermissions.Services;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests <see cref="AdvancedElementPermissionService.GetPermissionsAsync"/> — the per-element effective
/// permission seam that resolves library element permissions through the Advanced Security model. The
/// path-walking <c>Authorize*</c> methods are start-node-coupled and follow the same
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
    /// A folder's answer carries both vocabularies. Regression for the "folders created but hidden from the
    /// Library tree" bug: the tree's permission filter checks a folder's browse verb
    /// <c>Umb.ElementContainer.Read</c>, so the container verbs must be present. The canonical
    /// <c>Umb.Element.*</c> verbs are present too, because the backoffice asks Umbraco's current-user element
    /// endpoint for the parent folder of a new, unsaved element and needs the element verbs (Publish included)
    /// it will inherit there.
    /// </summary>
    [Fact]
    public async Task GetPermissionsAsync_FolderKey_ReturnsContainerAndCanonicalVerbs()
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

        // Read maps to the container browse verb the tree filter checks…
        Assert.Contains(AdvancedPermissionsConstants.VerbElementContainerRead, node.Permissions);
        // …and the canonical element verbs, element-only Publish included, are surfaced as well.
        Assert.Contains(AdvancedPermissionsConstants.VerbElementRead, node.Permissions);
        Assert.Contains(AdvancedPermissionsConstants.VerbElementPublish, node.Permissions);
        // Container verbs only appear for verbs that are allowed (Delete was not resolved).
        Assert.DoesNotContain(AdvancedPermissionsConstants.VerbElementContainerDelete, node.Permissions);
    }

    // ─── FilterAuthorizedAccessAsync (Umbraco 19 batch filter) ────────────────

    /// <summary>
    /// An empty input set yields an empty result without touching any dependency.
    /// </summary>
    [Fact]
    public async Task FilterAuthorizedAccessAsync_EmptyInput_ReturnsEmpty()
    {
        var user = StubUserWithElementStartNode(Guid.NewGuid(), Constants.System.Root);

        var result = await _sut.FilterAuthorizedAccessAsync(user, [], new HashSet<string> { AdvancedPermissionsConstants.VerbElementRead });

        Assert.Empty(result);
        _entityService.DidNotReceiveWithAnyArgs().GetAllPaths(UmbracoObjectTypes.Element, Array.Empty<Guid>());
    }

    /// <summary>
    /// Only the elements on which every requested verb resolves as allowed are kept. Denied and unknown
    /// elements are dropped, exactly as a per-key <c>AuthorizeAccessAsync</c> call would decide.
    /// </summary>
    [Fact]
    public async Task FilterAuthorizedAccessAsync_KeepsOnlyElementsWithEveryVerbAllowed()
    {
        var userKey = Guid.NewGuid();
        var user = StubUserWithElementStartNode(userKey, Constants.System.Root);
        var allowedKey = Guid.NewGuid();
        var deniedKey = Guid.NewGuid();
        var unknownKey = Guid.NewGuid();
        StubElementPaths(
            new TreeEntityPath { Id = 10, Key = allowedKey, Path = "-1,10" },
            new TreeEntityPath { Id = 11, Key = deniedKey, Path = "-1,11" });
        StubResolve(userKey, allowedKey, isAllowed: true);
        StubResolve(userKey, deniedKey, isAllowed: false);

        var result = await _sut.FilterAuthorizedAccessAsync(
            user,
            [allowedKey, deniedKey, unknownKey],
            new HashSet<string> { AdvancedPermissionsConstants.VerbElementRead });

        Assert.Equal([allowedKey], result);
    }

    /// <summary>
    /// An element outside the user's Library start nodes is dropped even when the advanced permissions
    /// would allow it: start-node access is enforced before permissions are resolved.
    /// </summary>
    [Fact]
    public async Task FilterAuthorizedAccessAsync_DropsElementsOutsideStartNodes()
    {
        var userKey = Guid.NewGuid();
        var user = StubUserWithElementStartNode(userKey, 20);
        var insideKey = Guid.NewGuid();
        var outsideKey = Guid.NewGuid();
        _entityService
            .GetAllPaths(UmbracoObjectTypes.ElementContainer, Arg.Any<int[]>())
            .Returns([new TreeEntityPath { Id = 20, Key = Guid.NewGuid(), Path = "-1,20" }]);
        StubElementPaths(
            new TreeEntityPath { Id = 21, Key = insideKey, Path = "-1,20,21" },
            new TreeEntityPath { Id = 31, Key = outsideKey, Path = "-1,30,31" });
        StubResolve(userKey, insideKey, isAllowed: true);
        StubResolve(userKey, outsideKey, isAllowed: true);

        var result = await _sut.FilterAuthorizedAccessAsync(
            user,
            [insideKey, outsideKey],
            new HashSet<string> { AdvancedPermissionsConstants.VerbElementRead });

        Assert.Equal([insideKey], result);
    }

    /// <summary>
    /// The batch reads every element path in one call instead of once per element (Umbraco's default
    /// implementation calls <c>AuthorizeAccessAsync</c> per key), which is the point of overriding it.
    /// </summary>
    [Fact]
    public async Task FilterAuthorizedAccessAsync_ReadsAllPathsInOneCall()
    {
        var userKey = Guid.NewGuid();
        var user = StubUserWithElementStartNode(userKey, Constants.System.Root);
        var keys = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
        StubElementPaths(keys.Select((key, i) => new TreeEntityPath { Id = 100 + i, Key = key, Path = $"-1,{100 + i}" }).ToArray());
        foreach (var key in keys)
        {
            StubResolve(userKey, key, isAllowed: true);
        }

        var result = await _sut.FilterAuthorizedAccessAsync(user, keys, new HashSet<string> { AdvancedPermissionsConstants.VerbElementRead });

        Assert.Equal(keys.ToHashSet(), result);
        _entityService.Received(1).GetAllPaths(UmbracoObjectTypes.Element, Arg.Any<Guid[]>());
    }

    /// <summary>
    /// Makes the entity service return the given element paths, and the ids on them resolve to keys.
    /// </summary>
    /// <param name="paths">The element paths to return.</param>
    private void StubElementPaths(params TreeEntityPath[] paths)
    {
        _entityService
            .GetAllPaths(UmbracoObjectTypes.Element, Arg.Any<Guid[]>())
            .Returns(paths);
        var entities = paths.Select(p => StubEntity(p.Id, p.Key)).ToArray();
        _entityService
            .GetAll(Arg.Any<IEnumerable<UmbracoObjectTypes>>(), Arg.Any<int[]>())
            .Returns(entities);
    }

    /// <summary>
    /// Makes the element resolver answer Read for one element.
    /// </summary>
    /// <param name="userKey">The user the answer is for.</param>
    /// <param name="nodeKey">The element the answer is for.</param>
    /// <param name="isAllowed">Whether Read is allowed.</param>
    private void StubResolve(Guid userKey, Guid nodeKey, bool isAllowed) =>
        _elementPermissionService
            .ResolveAllAsync(userKey, nodeKey, Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<IEnumerable<string>?>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, EffectivePermission>
            {
                [AdvancedPermissionsConstants.VerbElementRead] = new(AdvancedPermissionsConstants.VerbElementRead, isAllowed, IsExplicit: true, Reasoning: []),
            });

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
