using NSubstitute;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;
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
            .GetAllPaths(UmbracoObjectTypes.Element, Arg.Any<Guid[]>())
            .Returns([new TreeEntityPath { Id = 42, Key = nodeKey, Path = "-1,42" }]);
        _entityService
            .GetAll(Arg.Any<IEnumerable<UmbracoObjectTypes>>(), Arg.Any<int[]>())
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
            .GetAllPaths(UmbracoObjectTypes.Element, Arg.Any<Guid[]>())
            .Returns([new TreeEntityPath { Id = 7, Key = knownKey, Path = "-1,7" }]);
        _entityService
            .GetAll(Arg.Any<IEnumerable<UmbracoObjectTypes>>(), Arg.Any<int[]>())
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

    private static IUser StubUser(Guid key)
    {
        var user = Substitute.For<IUser>();
        user.Key.Returns(key);
        return user;
    }

    private static IEntitySlim StubEntity(int id, Guid key)
    {
        var entity = Substitute.For<IEntitySlim>();
        entity.Id.Returns(id);
        entity.Key.Returns(key);
        return entity;
    }
}
