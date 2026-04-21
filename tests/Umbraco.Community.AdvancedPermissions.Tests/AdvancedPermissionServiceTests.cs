using Umbraco.Community.AdvancedPermissions.Caching;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;
using Umbraco.Community.AdvancedPermissions.Core.Services;
using Umbraco.Community.AdvancedPermissions.Services;
using NSubstitute;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using static Umbraco.Community.AdvancedPermissions.Core.Constants.AdvancedPermissionsConstants;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Unit tests for <see cref="AdvancedPermissionService"/>.
/// Tests are written test-first to define the expected behaviour of the service orchestration layer.
/// </summary>
public sealed class AdvancedPermissionServiceTests
{
    private readonly IAdvancedPermissionRepository _repository = Substitute.For<IAdvancedPermissionRepository>();
    private readonly IPermissionResolver _resolver = Substitute.For<IPermissionResolver>();
    private readonly IUserService _userService = Substitute.For<IUserService>();
    private readonly AdvancedPermissionService _sut;

    /// <summary>Initialises the system under test with no-op caches and substituted dependencies.</summary>
    public AdvancedPermissionServiceTests()
    {
        _sut = BuildService(AppCaches.NoCache);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private AdvancedPermissionService BuildService(AppCaches appCaches) =>
        new(_repository, _resolver, _userService, new AdvancedPermissionCache(appCaches));

    private static AppCaches RealAppCaches() => new(
        new ObjectCacheAppCache(),
        NoAppCache.Instance,
        new IsolatedCaches(_ => NoAppCache.Instance));

    /// <summary>
    /// Creates an <see cref="IUser"/> mock with the specified key and group aliases,
    /// and configures <c>_userService.GetAsync(userKey)</c> to return it.
    /// </summary>
    private IUser SetupUser(Guid userKey, params string[] groupAliases)
    {
        var user = Substitute.For<IUser>();
        user.Key.Returns(userKey);
        var groups = groupAliases.Select(alias =>
        {
            var g = Substitute.For<IReadOnlyUserGroup>();
            g.Alias.Returns(alias);
            return g;
        }).ToArray<IReadOnlyUserGroup>();
        user.Groups.Returns(groups);

        _userService.GetAsync(userKey).Returns(Task.FromResult<IUser?>(user));
        return user;
    }

    /// <summary>
    /// Configures the <c>_userService</c> to return <see langword="null"/> for any
    /// <see cref="IUserService.GetAsync(Guid)"/> call, simulating a user not found.
    /// </summary>
    private void SetupNullUser()
    {
        _userService.GetAsync(Arg.Any<Guid>()).Returns(Task.FromResult<IUser?>(null));
    }

    private static AdvancedPermissionEntry MakeEntry(
        Guid nodeKey,
        string role,
        string verb,
        PermissionState state = PermissionState.Allow,
        PermissionScope scope = PermissionScope.ThisNodeAndDescendants) =>
        new(Guid.NewGuid(), nodeKey, role, verb, state, scope);

    /// <summary>Returns a dictionary where every standard verb is implicitly denied.</summary>
    private static Dictionary<string, EffectivePermission> AllDeny() =>
        AllVerbs.ToDictionary(v => v, v => new EffectivePermission(v, IsAllowed: false, IsExplicit: false, Reasoning: []));

    // ─── ResolveAsync ─────────────────────────────────────────────────────────

    /// <summary>
    /// ResolveAsync should return the effective permission for the requested verb
    /// from the resolved dictionary.
    /// </summary>
    [Fact]
    public async Task ResolveAsync_ReturnsPermissionForRequestedVerb()
    {
        var userKey = Guid.NewGuid();
        var nodeKey = Guid.NewGuid();
        var path = new List<Guid> { Guid.NewGuid(), nodeKey };
        SetupUser(userKey, "editors");

        var resolved = AllDeny();
        resolved[VerbRead] = new EffectivePermission(VerbRead, IsAllowed: true, IsExplicit: true, Reasoning: []);
        _resolver.ResolveAll(Arg.Any<PermissionResolutionContext>(), Arg.Any<IEnumerable<string>>())
            .Returns(resolved);
        _repository.GetByRoleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await _sut.ResolveAsync(userKey, nodeKey, path, VerbRead);

        Assert.Equal(VerbRead, result.Verb);
        Assert.True(result.IsAllowed);
        Assert.True(result.IsExplicit);
    }

    /// <summary>
    /// ResolveAsync should return an implicit deny when the resolver produces no entry
    /// for the requested verb (defensive fallback).
    /// </summary>
    [Fact]
    public async Task ResolveAsync_ReturnsImplicitDeny_WhenVerbAbsentFromResolvedDictionary()
    {
        var userKey = Guid.NewGuid();
        var nodeKey = Guid.NewGuid();
        var path = new List<Guid> { Guid.NewGuid(), nodeKey };
        SetupUser(userKey);

        // Resolver returns an empty dictionary — no opinion on any verb
        _resolver.ResolveAll(Arg.Any<PermissionResolutionContext>(), Arg.Any<IEnumerable<string>>())
            .Returns(new Dictionary<string, EffectivePermission>());
        _repository.GetByRoleAsync(EveryoneRoleAlias, Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await _sut.ResolveAsync(userKey, nodeKey, path, VerbRead);

        Assert.Equal(VerbRead, result.Verb);
        Assert.False(result.IsAllowed);
        Assert.False(result.IsExplicit);
        Assert.Empty(result.Reasoning);
    }

    // ─── ResolveAllAsync — context building ──────────────────────────────────

    /// <summary>
    /// ResolveAllAsync should build a resolution context that includes the user's group aliases
    /// plus the implicit $everyone role.
    /// </summary>
    [Fact]
    public async Task ResolveAllAsync_BuildsContextWithUserGroupsAndEveryone()
    {
        var userKey = Guid.NewGuid();
        var nodeKey = Guid.NewGuid();
        var path = new List<Guid> { Guid.NewGuid(), nodeKey };
        SetupUser(userKey, "editors", "writers");

        _repository.GetByRoleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([]);

        PermissionResolutionContext? capturedContext = null;
        _resolver.ResolveAll(
                Arg.Do<PermissionResolutionContext>(ctx => capturedContext = ctx),
                Arg.Any<IEnumerable<string>>())
            .Returns(AllDeny());

        await _sut.ResolveAllAsync(userKey, nodeKey, path);

        Assert.NotNull(capturedContext);
        Assert.Contains("editors", capturedContext.RoleAliases);
        Assert.Contains("writers", capturedContext.RoleAliases);
        Assert.Contains(EveryoneRoleAlias, capturedContext.RoleAliases);
        Assert.Equal(3, capturedContext.RoleAliases.Count);
    }

    /// <summary>
    /// ResolveAllAsync with a user who has no group memberships should produce a context
    /// containing only the $everyone role.
    /// </summary>
    [Fact]
    public async Task ResolveAllAsync_UserWithNoGroups_ContextContainsOnlyEveryone()
    {
        var userKey = Guid.NewGuid();
        var nodeKey = Guid.NewGuid();
        var path = new List<Guid> { Guid.NewGuid(), nodeKey };
        SetupUser(userKey); // no group aliases

        _repository.GetByRoleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([]);

        PermissionResolutionContext? capturedContext = null;
        _resolver.ResolveAll(
                Arg.Do<PermissionResolutionContext>(ctx => capturedContext = ctx),
                Arg.Any<IEnumerable<string>>())
            .Returns(AllDeny());

        await _sut.ResolveAllAsync(userKey, nodeKey, path);

        Assert.NotNull(capturedContext);
        Assert.Single(capturedContext.RoleAliases);
        Assert.Equal(EveryoneRoleAlias, capturedContext.RoleAliases[0]);
    }

    /// <summary>
    /// ResolveAllAsync when the user cannot be found should treat the user as having no groups,
    /// so the context contains only $everyone.
    /// </summary>
    [Fact]
    public async Task ResolveAllAsync_NullUser_ContextContainsOnlyEveryone()
    {
        var userKey = Guid.NewGuid();
        var nodeKey = Guid.NewGuid();
        var path = new List<Guid> { Guid.NewGuid(), nodeKey };
        SetupNullUser();

        _repository.GetByRoleAsync(EveryoneRoleAlias, Arg.Any<CancellationToken>())
            .Returns([]);

        PermissionResolutionContext? capturedContext = null;
        _resolver.ResolveAll(
                Arg.Do<PermissionResolutionContext>(ctx => capturedContext = ctx),
                Arg.Any<IEnumerable<string>>())
            .Returns(AllDeny());

        await _sut.ResolveAllAsync(userKey, nodeKey, path);

        Assert.NotNull(capturedContext);
        Assert.Single(capturedContext.RoleAliases);
        Assert.Equal(EveryoneRoleAlias, capturedContext.RoleAliases[0]);
    }

    /// <summary>
    /// ResolveAllAsync when called with a specific verb filter should return only the requested
    /// verbs, even though the resolver always resolves all verbs internally before filtering.
    /// </summary>
    [Fact]
    public async Task ResolveAllAsync_VerbFilter_ReturnsOnlyRequestedVerbs()
    {
        var userKey = Guid.NewGuid();
        var nodeKey = Guid.NewGuid();
        var path = new List<Guid> { Guid.NewGuid(), nodeKey };
        SetupUser(userKey);
        _repository.GetByRoleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([]);
        // Resolver returns ALL verbs; the service should filter down to the requested two
        _resolver.ResolveAll(Arg.Any<PermissionResolutionContext>(), Arg.Any<IEnumerable<string>>())
            .Returns(AllDeny());

        var result = await _sut.ResolveAllAsync(userKey, nodeKey, path, verbs: [VerbRead, VerbDelete]);

        Assert.Equal(2, result.Count);
        Assert.True(result.ContainsKey(VerbRead));
        Assert.True(result.ContainsKey(VerbDelete));
    }

    /// <summary>
    /// ResolveAllAsync with no verb filter should return results for every standard verb.
    /// </summary>
    [Fact]
    public async Task ResolveAllAsync_NoVerbFilter_ReturnsAllStandardVerbs()
    {
        var userKey = Guid.NewGuid();
        var nodeKey = Guid.NewGuid();
        var path = new List<Guid> { Guid.NewGuid(), nodeKey };
        SetupUser(userKey);
        _repository.GetByRoleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _resolver.ResolveAll(Arg.Any<PermissionResolutionContext>(), Arg.Any<IEnumerable<string>>())
            .Returns(AllDeny());

        var result = await _sut.ResolveAllAsync(userKey, nodeKey, path, verbs: null);

        Assert.Equal(AllVerbs.Count, result.Count);
        Assert.True(AllVerbs.All(v => result.ContainsKey(v)));
    }

    /// <summary>
    /// ResolveAllAsync should pass the target node key and full path to the resolution context.
    /// </summary>
    [Fact]
    public async Task ResolveAllAsync_ContextHasCorrectNodeKeyAndPath()
    {
        var userKey = Guid.NewGuid();
        var root = Guid.NewGuid();
        var parent = Guid.NewGuid();
        var nodeKey = Guid.NewGuid();
        var path = new List<Guid> { root, parent, nodeKey };
        SetupUser(userKey);
        _repository.GetByRoleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([]);

        PermissionResolutionContext? capturedContext = null;
        _resolver.ResolveAll(
                Arg.Do<PermissionResolutionContext>(ctx => capturedContext = ctx),
                Arg.Any<IEnumerable<string>>())
            .Returns(AllDeny());

        await _sut.ResolveAllAsync(userKey, nodeKey, path);

        Assert.NotNull(capturedContext);
        Assert.Equal(nodeKey, capturedContext.TargetNodeKey);
        Assert.Equal(path, capturedContext.PathFromRoot);
    }

    /// <summary>
    /// ResolveAllAsync should always call the resolver with ALL standard verbs,
    /// regardless of any verb filter requested by the caller.
    /// The filtering is applied after resolution, not before.
    /// </summary>
    [Fact]
    public async Task ResolveAllAsync_AlwaysResolvesAllVerbs_FilterAppliedAfterwards()
    {
        var userKey = Guid.NewGuid();
        var nodeKey = Guid.NewGuid();
        var path = new List<Guid> { Guid.NewGuid(), nodeKey };
        SetupUser(userKey);
        _repository.GetByRoleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([]);

        IEnumerable<string>? resolverVerbs = null;
        _resolver.ResolveAll(
                Arg.Any<PermissionResolutionContext>(),
                Arg.Do<IEnumerable<string>>(v => resolverVerbs = v))
            .Returns(AllDeny());

        // Request only VerbRead — but resolver should still be called with AllVerbs
        await _sut.ResolveAllAsync(userKey, nodeKey, path, verbs: [VerbRead]);

        Assert.NotNull(resolverVerbs);
        Assert.Equal(AllVerbs.Count, resolverVerbs!.Count());
    }

    // ─── ResolveForRoleAsync — role context building ─────────────────────────

    /// <summary>
    /// ResolveForRoleAsync with a non-$everyone role should produce a context containing ONLY
    /// that role — a role is self-contained and must not be influenced by $everyone.
    /// </summary>
    [Fact]
    public async Task ResolveForRoleAsync_NonEveryoneRole_ContextContainsOnlyThatRole()
    {
        const string role = "editors";
        var nodeKey = Guid.NewGuid();
        var path = new List<Guid> { Guid.NewGuid(), nodeKey };

        _repository.GetByRoleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([]);

        PermissionResolutionContext? capturedContext = null;
        _resolver.ResolveAll(
                Arg.Do<PermissionResolutionContext>(ctx => capturedContext = ctx),
                Arg.Any<IEnumerable<string>>())
            .Returns(AllDeny());

        await _sut.ResolveForRoleAsync(role, nodeKey, path);

        Assert.NotNull(capturedContext);
        Assert.Single(capturedContext.RoleAliases);
        Assert.Equal(role, capturedContext.RoleAliases[0]);
    }

    /// <summary>
    /// ResolveForRoleAsync when called with the $everyone role must NOT add $everyone twice.
    /// The context should contain exactly one $everyone entry.
    /// This is the regression test for the duplicate-reasoning bug.
    /// </summary>
    [Fact]
    public async Task ResolveForRoleAsync_EveryoneRole_ContextContainsExactlyOneEveryoneEntry()
    {
        var nodeKey = Guid.NewGuid();
        var path = new List<Guid> { Guid.NewGuid(), nodeKey };

        _repository.GetByRoleAsync(EveryoneRoleAlias, Arg.Any<CancellationToken>())
            .Returns([]);

        PermissionResolutionContext? capturedContext = null;
        _resolver.ResolveAll(
                Arg.Do<PermissionResolutionContext>(ctx => capturedContext = ctx),
                Arg.Any<IEnumerable<string>>())
            .Returns(AllDeny());

        await _sut.ResolveForRoleAsync(EveryoneRoleAlias, nodeKey, path);

        Assert.NotNull(capturedContext);
        Assert.Single(capturedContext.RoleAliases);
        Assert.Equal(EveryoneRoleAlias, capturedContext.RoleAliases[0]);
    }

    /// <summary>
    /// ResolveForRoleAsync when called with the $everyone role and a virtual-root entry exists
    /// should produce a reasoning list with exactly one entry, not two identical entries.
    /// This is the end-to-end regression test for the duplicate-reasoning bug visible in the Access Viewer.
    /// </summary>
    [Fact]
    public async Task ResolveForRoleAsync_EveryoneRole_WithVirtualRootEntry_ReasoningHasNoDuplicates()
    {
        var nodeKey = Guid.NewGuid();
        var path = new List<Guid> { Guid.NewGuid(), nodeKey };

        // Virtual-root entry for $everyone — the scenario that produced duplicate reasoning rows
        var virtualEntry = MakeEntry(VirtualRootNodeKey, EveryoneRoleAlias, VerbRead);
        _repository.GetByRoleAsync(EveryoneRoleAlias, Arg.Any<CancellationToken>())
            .Returns([virtualEntry]);

        // Use the real resolver so reasoning is built from the actual resolution context
        var sut = new AdvancedPermissionService(
            _repository, new PermissionResolver(), _userService, new AdvancedPermissionCache(AppCaches.NoCache));

        var results = await sut.ResolveForRoleAsync(EveryoneRoleAlias, nodeKey, path);

        var readResult = results[VerbRead];
        Assert.True(readResult.IsAllowed);
        Assert.Single(readResult.Reasoning);
        Assert.Equal(EveryoneRoleAlias, readResult.Reasoning[0].ContributingRole);
        Assert.True(readResult.Reasoning[0].IsFromGroupDefault);
    }

    /// <summary>
    /// Regression: when a role sets Deny (ThisNodeOnly) + Allow (DescendantsOnly) on an ancestor node,
    /// a descendant must resolve to Allow — not Deny.
    /// This verifies the split-entry scope logic in the resolver and that $everyone cannot
    /// override a role's own DescendantsOnly Allow (because $everyone is excluded from role resolution).
    /// </summary>
    [Fact]
    public async Task ResolveForRoleAsync_SplitEntryAtAncestor_DescendantResolvesToAllow()
    {
        // Arrange: path is [ancestorKey, descendantKey]
        var ancestorKey = Guid.NewGuid();
        var descendantKey = Guid.NewGuid();
        var path = new List<Guid> { ancestorKey, descendantKey };
        const string role = "administrators";

        // Role has a split entry at the ancestor:
        //   - Deny (ThisNodeOnly)  → applies only to the ancestor itself
        //   - Allow (DescendantsOnly) → applies to all descendants (should reach descendantKey)
        var denyAtAncestor = MakeEntry(ancestorKey, role, VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeOnly);
        var allowDescendants = MakeEntry(ancestorKey, role, VerbDelete, PermissionState.Allow, PermissionScope.DescendantsOnly);

        _repository.GetByRoleAsync(role, Arg.Any<CancellationToken>())
            .Returns([denyAtAncestor, allowDescendants]);

        // $everyone has a Deny on the ancestor (ThisNodeAndDescendants).
        // This must NOT influence the role-mode resolution — roles are self-contained.
        _repository.GetByRoleAsync(EveryoneRoleAlias, Arg.Any<CancellationToken>())
            .Returns([MakeEntry(ancestorKey, EveryoneRoleAlias, VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeAndDescendants)]);

        // Use the real resolver to exercise the actual scope + priority logic
        var sut = new AdvancedPermissionService(
            _repository, new PermissionResolver(), _userService, new AdvancedPermissionCache(AppCaches.NoCache));

        // Act: resolve for the descendant
        var results = await sut.ResolveForRoleAsync(role, descendantKey, path, verbs: [VerbDelete]);

        // Assert: descendant must be allowed (DescendantsOnly Allow wins for depth > 0)
        var result = results[VerbDelete];
        Assert.True(result.IsAllowed, "DescendantsOnly Allow at ancestor should propagate to the descendant.");
        Assert.False(result.IsExplicit, "Inherited from ancestor — not explicit at the descendant itself.");
    }

    /// <summary>
    /// Regression: ResolveForRoleAsync should show Deny for the ancestor node itself
    /// when it has ThisNodeOnly:Deny (even if DescendantsOnly:Allow also exists there).
    /// Companion to the split-entry descendant test above.
    /// </summary>
    [Fact]
    public async Task ResolveForRoleAsync_SplitEntryAtNode_NodeItselfResolvesToDeny()
    {
        var nodeKey = Guid.NewGuid();
        var path = new List<Guid> { nodeKey };
        const string role = "administrators";

        var denyAtNode = MakeEntry(nodeKey, role, VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeOnly);
        var allowDescendants = MakeEntry(nodeKey, role, VerbDelete, PermissionState.Allow, PermissionScope.DescendantsOnly);

        _repository.GetByRoleAsync(role, Arg.Any<CancellationToken>())
            .Returns([denyAtNode, allowDescendants]);

        var sut = new AdvancedPermissionService(
            _repository, new PermissionResolver(), _userService, new AdvancedPermissionCache(AppCaches.NoCache));

        var results = await sut.ResolveForRoleAsync(role, nodeKey, path, verbs: [VerbDelete]);

        var result = results[VerbDelete];
        Assert.False(result.IsAllowed, "ThisNodeOnly Deny must apply to the node itself.");
        Assert.True(result.IsExplicit, "Entry is on the target node — must be explicit.");
    }

    /// <summary>
    /// ResolveForRoleAsync should forward the verb filter to the resolver, not apply it itself.
    /// The caller-supplied verbs should be exactly what the resolver receives.
    /// </summary>
    [Fact]
    public async Task ResolveForRoleAsync_VerbFilter_ForwardsVerbsToResolver()
    {
        const string role = "editors";
        var nodeKey = Guid.NewGuid();
        var path = new List<Guid> { Guid.NewGuid(), nodeKey };

        _repository.GetByRoleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([]);

        IEnumerable<string>? resolverVerbs = null;
        _resolver.ResolveAll(
                Arg.Any<PermissionResolutionContext>(),
                Arg.Do<IEnumerable<string>>(v => resolverVerbs = v))
            .Returns(new Dictionary<string, EffectivePermission>
            {
                [VerbRead] = new EffectivePermission(VerbRead, IsAllowed: false, IsExplicit: false, Reasoning: []),
                [VerbCreate] = new EffectivePermission(VerbCreate, IsAllowed: false, IsExplicit: false, Reasoning: []),
            });

        await _sut.ResolveForRoleAsync(role, nodeKey, path, verbs: [VerbRead, VerbCreate]);

        Assert.NotNull(resolverVerbs);
        var verbList = resolverVerbs!.ToList();
        Assert.Equal(2, verbList.Count);
        Assert.Contains(VerbRead, verbList);
        Assert.Contains(VerbCreate, verbList);
    }

    /// <summary>
    /// ResolveForRoleAsync should load entries only for the specified role — not for $everyone.
    /// A role is self-contained and must not be influenced by other roles.
    /// </summary>
    [Fact]
    public async Task ResolveForRoleAsync_LoadsEntriesOnlyForRole()
    {
        const string role = "editors";
        var nodeKey = Guid.NewGuid();
        var path = new List<Guid> { Guid.NewGuid(), nodeKey };

        _repository.GetByRoleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _resolver.ResolveAll(Arg.Any<PermissionResolutionContext>(), Arg.Any<IEnumerable<string>>())
            .Returns(AllDeny());

        await _sut.ResolveForRoleAsync(role, nodeKey, path);

        await _repository.Received(1).GetByRoleAsync(role, Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().GetByRoleAsync(EveryoneRoleAlias, Arg.Any<CancellationToken>());
    }

    // ─── Multi-group user resolution (end-to-end with real resolver) ────────────

    /// <summary>
    /// A user belonging to two groups where one group explicitly allows and the other has no opinion.
    /// The explicit allow must propagate — the silent group must not prevent it.
    /// </summary>
    [Fact]
    public async Task ResolveAllAsync_UserInTwoGroups_SilentGroupDoesNotPreventAllow()
    {
        var userKey = Guid.NewGuid();
        var nodeKey = Guid.NewGuid();
        var parentKey = Guid.NewGuid();
        var path = new List<Guid> { parentKey, nodeKey };
        SetupUser(userKey, "editors", "writers");

        var editorsAllow = MakeEntry(nodeKey, "editors", VerbDelete, PermissionState.Allow, PermissionScope.ThisNodeOnly);
        // "writers" has no entries for VerbDelete

        _repository.GetByRoleAsync("editors", Arg.Any<CancellationToken>()).Returns([editorsAllow]);
        _repository.GetByRoleAsync("writers", Arg.Any<CancellationToken>()).Returns([]);
        _repository.GetByRoleAsync(EveryoneRoleAlias, Arg.Any<CancellationToken>()).Returns([]);

        var sut = new AdvancedPermissionService(
            _repository, new PermissionResolver(), _userService, new AdvancedPermissionCache(AppCaches.NoCache));

        var results = await sut.ResolveAllAsync(userKey, nodeKey, path, verbs: [VerbDelete]);

        Assert.True(results[VerbDelete].IsAllowed, "editors' explicit Allow must win when writers has no opinion.");
        Assert.True(results[VerbDelete].IsExplicit);
    }

    /// <summary>
    /// A user belonging to two groups where one group denies and the other allows, both implicitly
    /// (from ancestor entries). Implicit Deny beats Implicit Allow.
    /// </summary>
    [Fact]
    public async Task ResolveAllAsync_UserInTwoGroups_ImplicitDenyBeatsImplicitAllow()
    {
        var userKey = Guid.NewGuid();
        var ancestorKey = Guid.NewGuid();
        var nodeKey = Guid.NewGuid();
        var path = new List<Guid> { ancestorKey, nodeKey };
        SetupUser(userKey, "editors", "writers");

        // editors: Allow at ancestor (inherited at nodeKey)
        var editorsAllow = MakeEntry(ancestorKey, "editors", VerbUpdate, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants);
        // writers: Deny at ancestor (also inherited at nodeKey)
        var writersDeny = MakeEntry(ancestorKey, "writers", VerbUpdate, PermissionState.Deny, PermissionScope.ThisNodeAndDescendants);

        _repository.GetByRoleAsync("editors", Arg.Any<CancellationToken>()).Returns([editorsAllow]);
        _repository.GetByRoleAsync("writers", Arg.Any<CancellationToken>()).Returns([writersDeny]);
        _repository.GetByRoleAsync(EveryoneRoleAlias, Arg.Any<CancellationToken>()).Returns([]);

        var sut = new AdvancedPermissionService(
            _repository, new PermissionResolver(), _userService, new AdvancedPermissionCache(AppCaches.NoCache));

        var results = await sut.ResolveAllAsync(userKey, nodeKey, path, verbs: [VerbUpdate]);

        Assert.False(results[VerbUpdate].IsAllowed, "Implicit Deny from writers must beat Implicit Allow from editors.");
        Assert.False(results[VerbUpdate].IsExplicit);
    }

    /// <summary>
    /// A user belonging to two groups where one group explicitly allows and the other explicitly denies
    /// on the same node. Explicit Deny beats Explicit Allow regardless of group order.
    /// </summary>
    [Fact]
    public async Task ResolveAllAsync_UserInTwoGroups_ExplicitDenyBeatsExplicitAllow()
    {
        var userKey = Guid.NewGuid();
        var nodeKey = Guid.NewGuid();
        var path = new List<Guid> { Guid.NewGuid(), nodeKey };
        SetupUser(userKey, "editors", "writers");

        var editorsAllow = MakeEntry(nodeKey, "editors", VerbDelete, PermissionState.Allow, PermissionScope.ThisNodeOnly);
        var writersDeny  = MakeEntry(nodeKey, "writers", VerbDelete, PermissionState.Deny,  PermissionScope.ThisNodeOnly);

        _repository.GetByRoleAsync("editors", Arg.Any<CancellationToken>()).Returns([editorsAllow]);
        _repository.GetByRoleAsync("writers", Arg.Any<CancellationToken>()).Returns([writersDeny]);
        _repository.GetByRoleAsync(EveryoneRoleAlias, Arg.Any<CancellationToken>()).Returns([]);

        var sut = new AdvancedPermissionService(
            _repository, new PermissionResolver(), _userService, new AdvancedPermissionCache(AppCaches.NoCache));

        var results = await sut.ResolveAllAsync(userKey, nodeKey, path, verbs: [VerbDelete]);

        Assert.False(results[VerbDelete].IsAllowed, "Explicit Deny from writers must beat Explicit Allow from editors.");
        Assert.True(results[VerbDelete].IsExplicit);
    }

    /// <summary>
    /// A user in a group with a split entry (Deny ThisNodeOnly + Allow DescendantsOnly) at a parent:
    /// the user must get Allow at the child, because the DescendantsOnly Allow propagates down.
    /// $everyone's Deny at that parent must NOT influence the user resolution (it is included but must
    /// not override the group's own DescendantsOnly Allow when priority is considered).
    /// In this test $everyone also has ImplicitDeny, so editors ImplicitAllow (depth 1) vs
    /// $everyone ImplicitDeny (depth 1) — ImplicitDeny wins. This confirms the priority logic
    /// and acts as a regression anchor for future changes.
    /// </summary>
    [Fact]
    public async Task ResolveAllAsync_UserInGroup_SplitEntryAtParent_PriorityAppliedCorrectly()
    {
        var userKey = Guid.NewGuid();
        var parentKey = Guid.NewGuid();
        var childKey = Guid.NewGuid();
        var path = new List<Guid> { parentKey, childKey };
        SetupUser(userKey, "editors");

        // editors: split at parent — Deny (ThisNodeOnly) + Allow (DescendantsOnly)
        var editorsDeny    = MakeEntry(parentKey, "editors", VerbDelete, PermissionState.Deny,  PermissionScope.ThisNodeOnly);
        var editorsAllow   = MakeEntry(parentKey, "editors", VerbDelete, PermissionState.Allow, PermissionScope.DescendantsOnly);
        // $everyone: Deny at parent (ThisNodeAndDescendants) → ImplicitDeny for child
        var everyoneDeny   = MakeEntry(parentKey, EveryoneRoleAlias, VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeAndDescendants);

        _repository.GetByRoleAsync("editors", Arg.Any<CancellationToken>()).Returns([editorsDeny, editorsAllow]);
        _repository.GetByRoleAsync(EveryoneRoleAlias, Arg.Any<CancellationToken>()).Returns([everyoneDeny]);

        var sut = new AdvancedPermissionService(
            _repository, new PermissionResolver(), _userService, new AdvancedPermissionCache(AppCaches.NoCache));

        var results = await sut.ResolveAllAsync(userKey, childKey, path, verbs: [VerbDelete]);
        var result = results[VerbDelete];

        // editors→ImplicitAllow (DescendantsOnly at parent, depth 1)
        // $everyone→ImplicitDeny (ThisNodeAndDescendants at parent, depth 1)
        // Priority: ImplicitDeny beats ImplicitAllow → Deny
        Assert.False(result.IsAllowed,
            "User resolution includes $everyone, whose ImplicitDeny beats the group's ImplicitAllow.");
        Assert.False(result.IsExplicit);
    }

    /// <summary>
    /// $everyone provides a baseline allow (virtual-root) but a user's own group has an explicit deny
    /// on the target node — the explicit deny must win.
    /// </summary>
    [Fact]
    public async Task ResolveAllAsync_GroupExplicitDenyBeatsEveryoneDefaultAllow()
    {
        var userKey = Guid.NewGuid();
        var nodeKey = Guid.NewGuid();
        var path = new List<Guid> { Guid.NewGuid(), nodeKey };
        SetupUser(userKey, "editors");

        // $everyone: virtual-root Allow (global default)
        var everyoneDefault = MakeEntry(VirtualRootNodeKey, EveryoneRoleAlias, VerbRead, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants);
        // editors: explicit Deny at target
        var editorsDeny = MakeEntry(nodeKey, "editors", VerbRead, PermissionState.Deny, PermissionScope.ThisNodeOnly);

        _repository.GetByRoleAsync("editors", Arg.Any<CancellationToken>()).Returns([editorsDeny]);
        _repository.GetByRoleAsync(EveryoneRoleAlias, Arg.Any<CancellationToken>()).Returns([everyoneDefault]);

        var sut = new AdvancedPermissionService(
            _repository, new PermissionResolver(), _userService, new AdvancedPermissionCache(AppCaches.NoCache));

        var results = await sut.ResolveAllAsync(userKey, nodeKey, path, verbs: [VerbRead]);

        Assert.False(results[VerbRead].IsAllowed, "Explicit Deny from editors must beat $everyone's implicit allow.");
        Assert.True(results[VerbRead].IsExplicit);
    }

    // ─── Repository delegation ────────────────────────────────────────────────

    /// <summary>
    /// GetEntriesAsync should delegate directly to repository.GetByNodeAndRoleAsync.
    /// </summary>
    [Fact]
    public async Task GetEntriesAsync_DelegatesTo_RepositoryGetByNodeAndRoleAsync()
    {
        var nodeKey = Guid.NewGuid();
        const string role = "editors";
        var expected = new List<AdvancedPermissionEntry> { MakeEntry(nodeKey, role, VerbRead) };
        _repository.GetByNodeAndRoleAsync(nodeKey, role, Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await _sut.GetEntriesAsync(nodeKey, role);

        Assert.Equal(expected, result);
        await _repository.Received(1).GetByNodeAndRoleAsync(nodeKey, role, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// GetEntriesByNodesAndRoleAsync should delegate directly to repository.GetByNodesAndRoleAsync.
    /// </summary>
    [Fact]
    public async Task GetEntriesByNodesAndRoleAsync_DelegatesTo_RepositoryGetByNodesAndRoleAsync()
    {
        var nodeKeys = new[] { Guid.NewGuid(), Guid.NewGuid() };
        const string role = "editors";
        var expected = new List<AdvancedPermissionEntry> { MakeEntry(nodeKeys[0], role, VerbRead) };
        _repository.GetByNodesAndRoleAsync(nodeKeys, role, Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await _sut.GetEntriesByNodesAndRoleAsync(nodeKeys, role);

        Assert.Equal(expected, result);
        await _repository.Received(1).GetByNodesAndRoleAsync(nodeKeys, role, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// GetEntriesByNodeAsync should delegate directly to repository.GetByNodeAsync.
    /// </summary>
    [Fact]
    public async Task GetEntriesByNodeAsync_DelegatesTo_RepositoryGetByNodeAsync()
    {
        var nodeKey = Guid.NewGuid();
        var expected = new List<AdvancedPermissionEntry> { MakeEntry(nodeKey, "editors", VerbRead) };
        _repository.GetByNodeAsync(nodeKey, Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await _sut.GetEntriesByNodeAsync(nodeKey);

        Assert.Equal(expected, result);
        await _repository.Received(1).GetByNodeAsync(nodeKey, Arg.Any<CancellationToken>());
    }

    // ─── SaveEntriesAsync ─────────────────────────────────────────────────────

    /// <summary>
    /// SaveEntriesAsync should forward the exact node, role, and entries to the repository.
    /// </summary>
    [Fact]
    public async Task SaveEntriesAsync_DelegatesEntriesTo_RepositorySaveAsync()
    {
        var nodeKey = Guid.NewGuid();
        const string role = "editors";
        var entries = new[]
        {
            (VerbRead, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants),
            (VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeOnly),
        };

        await _sut.SaveEntriesAsync(nodeKey, role, entries);

        await _repository.Received(1).SaveAsync(nodeKey, role, entries, Arg.Any<CancellationToken>());
    }

    // ─── DeleteEntryAsync ─────────────────────────────────────────────────────

    /// <summary>
    /// DeleteEntryAsync should forward the exact node, role, and verb to the repository.
    /// </summary>
    [Fact]
    public async Task DeleteEntryAsync_DelegatesTo_RepositoryDeleteAsync()
    {
        var nodeKey = Guid.NewGuid();
        const string role = "editors";

        await _sut.DeleteEntryAsync(nodeKey, role, VerbRead);

        await _repository.Received(1).DeleteAsync(nodeKey, role, VerbRead, Arg.Any<CancellationToken>());
    }

    // ─── Cache behaviour ──────────────────────────────────────────────────────

    /// <summary>
    /// Tests that verify the L1 (role entry) and L2 (resolved permission) cache layers behave
    /// correctly. These tests use a real in-memory cache so cache hits and invalidations are
    /// exercised end-to-end.
    /// </summary>
    public sealed class CacheTests
    {
        private readonly IAdvancedPermissionRepository _repository = Substitute.For<IAdvancedPermissionRepository>();
        private readonly IPermissionResolver _resolver = Substitute.For<IPermissionResolver>();
        private readonly IUserService _userService = Substitute.For<IUserService>();
        private readonly AdvancedPermissionService _sut;

        /// <summary>Initialises substituted dependencies and a real in-memory cache.</summary>
        public CacheTests()
        {
            var cache = new AdvancedPermissionCache(new AppCaches(
                new ObjectCacheAppCache(),
                NoAppCache.Instance,
                new IsolatedCaches(_ => NoAppCache.Instance)));
            _sut = new AdvancedPermissionService(_repository, _resolver, _userService, cache);
        }

        private void SetupUserWithKey(Guid userKey)
        {
            var user = Substitute.For<IUser>();
            user.Key.Returns(userKey);
            user.Groups.Returns(Array.Empty<IReadOnlyUserGroup>());
            long total;
            _userService.GetAll(Arg.Any<long>(), Arg.Any<int>(), out total)
                .Returns(new[] { user });
        }

        private static Dictionary<string, EffectivePermission> AllDeny() =>
            AllVerbs.ToDictionary(v => v, v => new EffectivePermission(v, IsAllowed: false, IsExplicit: false, Reasoning: []));

        /// <summary>
        /// The L1 cache should store role entries after the first resolution so that the
        /// repository is not queried again for the same role on a subsequent call.
        /// </summary>
        [Fact]
        public async Task L1Cache_RoleEntries_CachedAfterFirstResolve_RepositoryNotCalledAgain()
        {
            const string role = "editors";
            var nodeKey = Guid.NewGuid();
            var path = new List<Guid> { Guid.NewGuid(), nodeKey };

            _repository.GetByRoleAsync(role, Arg.Any<CancellationToken>()).Returns([]);
            _repository.GetByRoleAsync(EveryoneRoleAlias, Arg.Any<CancellationToken>()).Returns([]);
            _resolver.ResolveAll(Arg.Any<PermissionResolutionContext>(), Arg.Any<IEnumerable<string>>())
                .Returns(AllDeny());

            // First call — L1 cache cold
            await _sut.ResolveForRoleAsync(role, nodeKey, path);
            // Second call — L1 should be warm, repository should NOT be called again
            await _sut.ResolveForRoleAsync(role, nodeKey, path);

            await _repository.Received(1).GetByRoleAsync(role, Arg.Any<CancellationToken>());
        }

        /// <summary>
        /// The L2 cache should store resolved permissions after the first resolution so that
        /// the resolver is not invoked again for the same user and node on a subsequent call.
        /// </summary>
        [Fact]
        public async Task L2Cache_ResolvedPermissions_CachedAfterFirstResolve_ResolverNotCalledAgain()
        {
            var userKey = Guid.NewGuid();
            var nodeKey = Guid.NewGuid();
            var path = new List<Guid> { Guid.NewGuid(), nodeKey };
            SetupUserWithKey(userKey);
            _repository.GetByRoleAsync(EveryoneRoleAlias, Arg.Any<CancellationToken>()).Returns([]);
            _resolver.ResolveAll(Arg.Any<PermissionResolutionContext>(), Arg.Any<IEnumerable<string>>())
                .Returns(AllDeny());

            // First call — L2 cache cold
            await _sut.ResolveAllAsync(userKey, nodeKey, path);
            // Second call — L2 should be warm, resolver should NOT be called again
            await _sut.ResolveAllAsync(userKey, nodeKey, path);

            _resolver.Received(1).ResolveAll(Arg.Any<PermissionResolutionContext>(), Arg.Any<IEnumerable<string>>());
        }

        /// <summary>
        /// SaveEntriesAsync should invalidate the L1 cache for the saved role so that the
        /// repository is consulted again on the next resolution for that role.
        /// </summary>
        [Fact]
        public async Task L1Cache_InvalidatedBySaveEntriesAsync_RepositoryCalledAgainAfterSave()
        {
            const string role = "editors";
            var nodeKey = Guid.NewGuid();
            var path = new List<Guid> { Guid.NewGuid(), nodeKey };

            _repository.GetByRoleAsync(role, Arg.Any<CancellationToken>()).Returns([]);
            _repository.GetByRoleAsync(EveryoneRoleAlias, Arg.Any<CancellationToken>()).Returns([]);
            _resolver.ResolveAll(Arg.Any<PermissionResolutionContext>(), Arg.Any<IEnumerable<string>>())
                .Returns(AllDeny());

            // Warm the L1 cache
            await _sut.ResolveForRoleAsync(role, nodeKey, path);
            await _repository.Received(1).GetByRoleAsync(role, Arg.Any<CancellationToken>());

            // Invalidate via save
            await _sut.SaveEntriesAsync(nodeKey, role, []);

            // Resolve again — L1 cache invalidated, repository must be called again
            await _sut.ResolveForRoleAsync(role, nodeKey, path);
            await _repository.Received(2).GetByRoleAsync(role, Arg.Any<CancellationToken>());
        }

        /// <summary>
        /// DeleteEntryAsync should invalidate the L1 cache for the affected role so that
        /// the repository is consulted again on the next resolution for that role.
        /// </summary>
        [Fact]
        public async Task L1Cache_InvalidatedByDeleteEntryAsync_RepositoryCalledAgainAfterDelete()
        {
            const string role = "editors";
            var nodeKey = Guid.NewGuid();
            var path = new List<Guid> { Guid.NewGuid(), nodeKey };

            _repository.GetByRoleAsync(role, Arg.Any<CancellationToken>()).Returns([]);
            _repository.GetByRoleAsync(EveryoneRoleAlias, Arg.Any<CancellationToken>()).Returns([]);
            _resolver.ResolveAll(Arg.Any<PermissionResolutionContext>(), Arg.Any<IEnumerable<string>>())
                .Returns(AllDeny());

            // Warm the L1 cache
            await _sut.ResolveForRoleAsync(role, nodeKey, path);
            await _repository.Received(1).GetByRoleAsync(role, Arg.Any<CancellationToken>());

            // Invalidate via delete
            await _sut.DeleteEntryAsync(nodeKey, role, VerbRead);

            // Resolve again — L1 cache invalidated, repository must be called again
            await _sut.ResolveForRoleAsync(role, nodeKey, path);
            await _repository.Received(2).GetByRoleAsync(role, Arg.Any<CancellationToken>());
        }

        /// <summary>
        /// SaveEntriesAsync should invalidate the entire L2 cache so that resolved permissions
        /// for any user are recalculated after a save.
        /// </summary>
        [Fact]
        public async Task L2Cache_InvalidatedBySaveEntriesAsync_ResolverCalledAgainAfterSave()
        {
            var userKey = Guid.NewGuid();
            var nodeKey = Guid.NewGuid();
            var path = new List<Guid> { Guid.NewGuid(), nodeKey };
            SetupUserWithKey(userKey);
            _repository.GetByRoleAsync(EveryoneRoleAlias, Arg.Any<CancellationToken>()).Returns([]);
            _resolver.ResolveAll(Arg.Any<PermissionResolutionContext>(), Arg.Any<IEnumerable<string>>())
                .Returns(AllDeny());

            // Warm the L2 cache
            await _sut.ResolveAllAsync(userKey, nodeKey, path);
            _resolver.Received(1).ResolveAll(Arg.Any<PermissionResolutionContext>(), Arg.Any<IEnumerable<string>>());

            // Invalidate via save
            await _sut.SaveEntriesAsync(nodeKey, EveryoneRoleAlias, []);

            // Resolve again — L2 cache invalidated, resolver must be called again
            await _sut.ResolveAllAsync(userKey, nodeKey, path);
            _resolver.Received(2).ResolveAll(Arg.Any<PermissionResolutionContext>(), Arg.Any<IEnumerable<string>>());
        }

        /// <summary>
        /// DeleteEntryAsync should invalidate the entire L2 cache so that resolved permissions
        /// for any user are recalculated after a delete.
        /// </summary>
        [Fact]
        public async Task L2Cache_InvalidatedByDeleteEntryAsync_ResolverCalledAgainAfterDelete()
        {
            var userKey = Guid.NewGuid();
            var nodeKey = Guid.NewGuid();
            var path = new List<Guid> { Guid.NewGuid(), nodeKey };
            SetupUserWithKey(userKey);
            _repository.GetByRoleAsync(EveryoneRoleAlias, Arg.Any<CancellationToken>()).Returns([]);
            _resolver.ResolveAll(Arg.Any<PermissionResolutionContext>(), Arg.Any<IEnumerable<string>>())
                .Returns(AllDeny());

            // Warm the L2 cache
            await _sut.ResolveAllAsync(userKey, nodeKey, path);
            _resolver.Received(1).ResolveAll(Arg.Any<PermissionResolutionContext>(), Arg.Any<IEnumerable<string>>());

            // Invalidate via delete
            await _sut.DeleteEntryAsync(nodeKey, EveryoneRoleAlias, VerbRead);

            // Resolve again — L2 cache invalidated, resolver must be called again
            await _sut.ResolveAllAsync(userKey, nodeKey, path);
            _resolver.Received(2).ResolveAll(Arg.Any<PermissionResolutionContext>(), Arg.Any<IEnumerable<string>>());
        }
    }
}
