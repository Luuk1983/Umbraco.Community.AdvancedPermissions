using NSubstitute;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;
using Umbraco.Community.AdvancedPermissions.Services;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests the Umbraco 18 (<see href="https://github.com/umbraco/Umbraco-CMS/pull/22400">PR #22400</see>)
/// extension points on <see cref="AdvancedContentPermissionService"/>: <c>GetPermissionsAsync</c> (per-node
/// effective permissions consumed by the backoffice UI) and <c>FilterFallbackPermissionsAsync</c> (group
/// default fallback). These route the now-fixed native UI permission resolution through the Advanced
/// Permissions model.
/// </summary>
public sealed class AdvancedContentPermissionServiceTests
{
    private readonly IAdvancedPermissionService _permissionService = Substitute.For<IAdvancedPermissionService>();
    private readonly IEntityService _entityService = Substitute.For<IEntityService>();
    private readonly ILanguageService _languageService = Substitute.For<ILanguageService>();
    private readonly AdvancedContentPermissionService _sut;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdvancedContentPermissionServiceTests"/> class.
    /// </summary>
    public AdvancedContentPermissionServiceTests()
    {
        _sut = new AdvancedContentPermissionService(
            _permissionService,
            _entityService,
            AppCaches.Disabled,
            _languageService);
    }

    // ─── GetPermissionsAsync ──────────────────────────────────────────────────

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
    /// For a known node, only the verbs the resolver marks allowed are surfaced; denied verbs are omitted.
    /// </summary>
    [Fact]
    public async Task GetPermissionsAsync_KnownNode_ReturnsOnlyAllowedVerbs()
    {
        var userKey = Guid.NewGuid();
        var user = StubUser(userKey);
        var nodeKey = Guid.NewGuid();
        // Build the stub entity first: configuring a substitute inside a .Returns(...) argument
        // corrupts NSubstitute's last-call tracking.
        var entity = StubEntity(42, nodeKey);

        _entityService
            .GetAllPaths(UmbracoObjectTypes.Document, Arg.Any<Guid[]>())
            .Returns([new TreeEntityPath { Id = 42, Key = nodeKey, Path = "-1,42" }]);
        _entityService
            .GetAll(UmbracoObjectTypes.Document, Arg.Any<int[]>())
            .Returns([entity]);
        _permissionService
            .ResolveAllAsync(userKey, nodeKey, Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<IEnumerable<string>?>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, EffectivePermission>
            {
                ["Umb.Document.Read"] = new("Umb.Document.Read", IsAllowed: true, IsExplicit: true, Reasoning: []),
                ["Umb.Document.Delete"] = new("Umb.Document.Delete", IsAllowed: false, IsExplicit: false, Reasoning: []),
            });

        var result = (await _sut.GetPermissionsAsync(user, [nodeKey])).ToArray();

        var node = Assert.Single(result);
        Assert.Equal(nodeKey, node.NodeKey);
        Assert.Contains("Umb.Document.Read", node.Permissions);
        Assert.DoesNotContain("Umb.Document.Delete", node.Permissions);
    }

    /// <summary>
    /// The result must contain exactly one entry per requested key — including keys that do not resolve to
    /// an existing node (e.g. an unsaved draft's pre-assigned Guid). Unknown nodes return an empty verb set.
    /// This invariant prevents the native per-document current-user endpoint from returning a spurious 404
    /// (it 404s when fewer permission entries come back than ids requested).
    /// </summary>
    [Fact]
    public async Task GetPermissionsAsync_ReturnsOneEntryPerRequestedKey_IncludingUnknownNodes()
    {
        var userKey = Guid.NewGuid();
        var user = StubUser(userKey);
        var knownKey = Guid.NewGuid();
        var unknownKey = Guid.NewGuid();
        // Build the stub entity first: configuring a substitute inside a .Returns(...) argument
        // corrupts NSubstitute's last-call tracking.
        var entity = StubEntity(7, knownKey);

        // Only the known key resolves to a path.
        _entityService
            .GetAllPaths(UmbracoObjectTypes.Document, Arg.Any<Guid[]>())
            .Returns([new TreeEntityPath { Id = 7, Key = knownKey, Path = "-1,7" }]);
        _entityService
            .GetAll(UmbracoObjectTypes.Document, Arg.Any<int[]>())
            .Returns([entity]);
        _permissionService
            .ResolveAllAsync(userKey, knownKey, Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<IEnumerable<string>?>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, EffectivePermission>
            {
                ["Umb.Document.Read"] = new("Umb.Document.Read", IsAllowed: true, IsExplicit: true, Reasoning: []),
            });

        var result = (await _sut.GetPermissionsAsync(user, [knownKey, unknownKey])).ToArray();

        Assert.Equal(2, result.Length);
        var known = result.Single(r => r.NodeKey == knownKey);
        Assert.Contains("Umb.Document.Read", known.Permissions);
        var unknown = result.Single(r => r.NodeKey == unknownKey);
        Assert.Empty(unknown.Permissions);
    }

    /// <summary>
    /// When a key has no resolvable path the resolver is not invoked for it (no pointless work).
    /// </summary>
    [Fact]
    public async Task GetPermissionsAsync_UnknownNode_DoesNotInvokeResolver()
    {
        var user = StubUser(Guid.NewGuid());
        _entityService
            .GetAllPaths(UmbracoObjectTypes.Document, Arg.Any<Guid[]>())
            .Returns([]);

        await _sut.GetPermissionsAsync(user, [Guid.NewGuid()]);

        await _permissionService
            .DidNotReceive()
            .ResolveAllAsync(
                Arg.Any<Guid>(),
                Arg.Any<Guid>(),
                Arg.Any<IReadOnlyList<Guid>>(),
                Arg.Any<IEnumerable<string>?>(),
                Arg.Any<CancellationToken>());
    }

    // ─── FilterFallbackPermissionsAsync ───────────────────────────────────────

    /// <summary>
    /// The Advanced Permissions model has no independent "fallback" layer — group defaults are modelled as
    /// virtual-root entries and resolved per-node through <c>GetPermissionsAsync</c>. So the fallback set our
    /// service contributes is empty: the native UI must never grant a document action purely on un-scoped
    /// group defaults.
    /// </summary>
    [Fact]
    public async Task FilterFallbackPermissionsAsync_ReturnsEmptySet()
    {
        var user = StubUser(Guid.NewGuid());
        ISet<string> fallback = new HashSet<string> { "Umb.Document.Read", "Umb.Document.Create" };

        var result = await _sut.FilterFallbackPermissionsAsync(user, fallback);

        Assert.Empty(result);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

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
