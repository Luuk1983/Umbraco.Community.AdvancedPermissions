using NSubstitute;
using Umbraco.Cms.Api.Management.Services.PermissionFilter;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;
using Umbraco.Community.AdvancedPermissions.Services;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests the Umbraco 17.4 (<see href="https://github.com/umbraco/Umbraco-CMS/pull/22400">PR #22400</see>)
/// extension points on <see cref="AdvancedContentPermissionService"/>: <c>GetPermissionsAsync</c> (per-node
/// effective permissions, consumed by the server-side document tree filter and the backoffice UI) and
/// <c>FilterFallbackPermissionsAsync</c> (group default fallback).
/// </summary>
/// <remarks>
/// Both members have default implementations on <see cref="IContentPermissionService"/> that read Umbraco's
/// native group permissions. If the package does not override them, the document tree drops every node a
/// user group was granted Read on only through Advanced Permissions (issue #64). The tests therefore call
/// through the interface — exactly as Umbraco does — so a missing override fails here instead of silently
/// falling back to the default.
/// </remarks>
public sealed class AdvancedContentPermissionServiceTests
{
    /// <summary>The advanced permission resolver substitute that decides which verbs are allowed.</summary>
    private readonly IAdvancedPermissionService _permissionService = Substitute.For<IAdvancedPermissionService>();

    /// <summary>The entity service substitute that supplies content paths and ids.</summary>
    private readonly IEntityService _entityService = Substitute.For<IEntityService>();

    /// <summary>The language service substitute; unused by the members under test but required by the constructor.</summary>
    private readonly ILanguageService _languageService = Substitute.For<ILanguageService>();

    /// <summary>The system under test, typed as the interface Umbraco resolves it as.</summary>
    private readonly IContentPermissionService _sut;

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
                [AdvancedPermissionsConstants.VerbRead] = new(AdvancedPermissionsConstants.VerbRead, IsAllowed: true, IsExplicit: true, Reasoning: []),
                [AdvancedPermissionsConstants.VerbDelete] = new(AdvancedPermissionsConstants.VerbDelete, IsAllowed: false, IsExplicit: false, Reasoning: []),
            });

        var result = (await _sut.GetPermissionsAsync(user, [nodeKey])).ToArray();

        var node = Assert.Single(result);
        Assert.Equal(nodeKey, node.NodeKey);
        Assert.Contains(AdvancedPermissionsConstants.VerbRead, node.Permissions);
        Assert.DoesNotContain(AdvancedPermissionsConstants.VerbDelete, node.Permissions);
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
                [AdvancedPermissionsConstants.VerbRead] = new(AdvancedPermissionsConstants.VerbRead, IsAllowed: true, IsExplicit: true, Reasoning: []),
            });

        var result = (await _sut.GetPermissionsAsync(user, [knownKey, unknownKey])).ToArray();

        Assert.Equal(2, result.Length);
        var known = result.Single(r => r.NodeKey == knownKey);
        Assert.Contains(AdvancedPermissionsConstants.VerbRead, known.Permissions);
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

    // ─── Umbraco's document tree filter (issue #64) ───────────────────────────

    /// <summary>
    /// Umbraco's own server-side document tree filter keeps a node a user group was granted Read on only
    /// through Advanced Permissions. Before the fix the filter fell back to native group permissions — which
    /// a group created after installing the package does not have — and returned an empty tree.
    /// </summary>
    [Fact]
    public async Task DocumentTreeFilter_KeepsNodeWithReadGrantedOnlyByAdvancedPermissions()
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
                [AdvancedPermissionsConstants.VerbRead] = new(AdvancedPermissionsConstants.VerbRead, IsAllowed: true, IsExplicit: true, Reasoning: []),
            });

        var filter = CreateUmbracoDocumentTreeFilter(user);

        var (entities, totalItems) = await filter.FilterAsync([entity], 1);

        Assert.Equal(nodeKey, Assert.Single(entities).Key);
        Assert.Equal(1, totalItems);
    }

    /// <summary>
    /// Umbraco's own server-side document tree filter removes a node Advanced Permissions denies Read on,
    /// so a denied node's name no longer leaks into the tree through a native group default.
    /// </summary>
    [Fact]
    public async Task DocumentTreeFilter_RemovesNodeWithReadDeniedByAdvancedPermissions()
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
                [AdvancedPermissionsConstants.VerbRead] = new(AdvancedPermissionsConstants.VerbRead, IsAllowed: false, IsExplicit: true, Reasoning: []),
            });

        var filter = CreateUmbracoDocumentTreeFilter(user);

        var (entities, totalItems) = await filter.FilterAsync([entity], 1);

        Assert.Empty(entities);
        Assert.Equal(0, totalItems);
    }

    // ─── FilterFallbackPermissionsAsync ───────────────────────────────────────

    /// <summary>
    /// Verbs the package manages (the node-level document verbs in <see cref="AdvancedPermissionsConstants.AllVerbs"/>)
    /// are stripped from the fallback set so per-node resolution stays the sole authority for them. Every other
    /// verb — notably the property-value (<c>Umb.Document.PropertyValue.Read/Write</c>) permissions, plus any
    /// future verb — is passed through unchanged so Umbraco's native, client-side gating for permissions we do
    /// not manage keeps working.
    /// </summary>
    [Fact]
    public async Task FilterFallbackPermissionsAsync_StripsManagedVerbs_PassesThroughTheRest()
    {
        var user = StubUser(Guid.NewGuid());
        ISet<string> fallback = new HashSet<string>(StringComparer.Ordinal)
        {
            AdvancedPermissionsConstants.VerbRead,    // managed → stripped
            AdvancedPermissionsConstants.VerbUpdate,  // managed → stripped
            "Umb.Document.PropertyValue.Read",        // not managed → kept
            "Umb.Document.PropertyValue.Write",       // not managed → kept
            "Some.Future.Umbraco.Verb",               // not managed → kept
        };

        var result = await _sut.FilterFallbackPermissionsAsync(user, fallback);

        Assert.DoesNotContain(AdvancedPermissionsConstants.VerbRead, result);
        Assert.DoesNotContain(AdvancedPermissionsConstants.VerbUpdate, result);
        Assert.Contains("Umb.Document.PropertyValue.Read", result);
        Assert.Contains("Umb.Document.PropertyValue.Write", result);
        Assert.Contains("Some.Future.Umbraco.Verb", result);
        Assert.Equal(3, result.Count);
    }

    /// <summary>
    /// A fallback set consisting only of verbs the package manages collapses to empty — there is nothing
    /// to pass through to the native UI.
    /// </summary>
    [Fact]
    public async Task FilterFallbackPermissionsAsync_AllManaged_ReturnsEmpty()
    {
        var user = StubUser(Guid.NewGuid());
        ISet<string> fallback = new HashSet<string>(AdvancedPermissionsConstants.AllVerbs, StringComparer.Ordinal);

        var result = await _sut.FilterFallbackPermissionsAsync(user, fallback);

        Assert.Empty(result);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates Umbraco's own (internal) document tree permission filter, wired to the system under test and
    /// to a backoffice security accessor whose current user is <paramref name="user"/>.
    /// </summary>
    /// <remarks>
    /// The filter is internal to <c>Umbraco.Cms.Api.Management</c>, so it is created by reflection. Using the
    /// real filter — rather than re-stating its logic — is what makes the issue #64 tests meaningful.
    /// </remarks>
    /// <param name="user">The user the filter treats as the current backoffice user.</param>
    /// <returns>The Umbraco document tree permission filter.</returns>
    private IDocumentPermissionFilterService CreateUmbracoDocumentTreeFilter(IUser user)
    {
        var security = Substitute.For<IBackOfficeSecurity>();
        security.CurrentUser.Returns(user);
        var securityAccessor = Substitute.For<IBackOfficeSecurityAccessor>();
        securityAccessor.BackOfficeSecurity.Returns(security);

        var filterType = typeof(IDocumentPermissionFilterService).Assembly.GetType(
            "Umbraco.Cms.Api.Management.Services.PermissionFilter.DocumentPermissionFilterService",
            throwOnError: true)!;

        return (IDocumentPermissionFilterService)Activator.CreateInstance(filterType, securityAccessor, _sut)!;
    }

    /// <summary>
    /// Creates a user substitute with the given key.
    /// </summary>
    /// <param name="key">The user's key.</param>
    /// <returns>The user substitute.</returns>
    private static IUser StubUser(Guid key)
    {
        var user = Substitute.For<IUser>();
        user.Key.Returns(key);
        return user;
    }

    /// <summary>
    /// Creates a document entity substitute with the given id and key.
    /// </summary>
    /// <param name="id">The entity's integer id.</param>
    /// <param name="key">The entity's key.</param>
    /// <returns>The entity substitute.</returns>
    private static IEntitySlim StubEntity(int id, Guid key)
    {
        var entity = Substitute.For<IEntitySlim>();
        entity.Id.Returns(id);
        entity.Key.Returns(key);
        return entity;
    }
}
