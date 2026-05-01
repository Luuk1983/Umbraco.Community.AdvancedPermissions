using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Models.Membership.Permissions;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Runtime;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.Navigation;
using Umbraco.Cms.Core.Sync;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;
using Umbraco.Community.AdvancedPermissions.Core.Services;
using Umbraco.Community.AdvancedPermissions.Data.Context;
using Umbraco.Community.AdvancedPermissions.Data.Entities;
using Umbraco.Community.AdvancedPermissions.Migrations;

namespace Umbraco.Community.AdvancedPermissions.Data.Tests.Migrations;

/// <summary>
/// Integration tests for <see cref="AdvancedPermissionsDataImport"/>.
/// Exercises the first-boot migration end-to-end against a real SQLite in-memory
/// <see cref="AdvancedPermissionsDbContext"/> and asserts on the resulting rows plus
/// resolver-level effective permissions.
/// </summary>
public sealed class AdvancedPermissionsDataImportTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private SaveCountingDbContext _dbContext = null!;
    private IUserGroupService _userGroupService = null!;
    private IDocumentNavigationQueryService _navigationQueryService = null!;
    private IPermissionResolver _permissionResolver = null!;
    private IMainDom _mainDom = null!;
    private IServerRoleAccessor _serverRoleAccessor = null!;
    private CapturingLogger _logger = null!;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AdvancedPermissionsDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new SaveCountingDbContext(options);
        await _dbContext.Database.EnsureCreatedAsync();

        _userGroupService = Substitute.For<IUserGroupService>();
        _userGroupService.GetAllAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns(new PagedModel<IUserGroup>(0, []));

        _navigationQueryService = Substitute.For<IDocumentNavigationQueryService>();

        _permissionResolver = new PermissionResolver();

        _mainDom = Substitute.For<IMainDom>();
        _mainDom.IsMainDom.Returns(true);

        _serverRoleAccessor = Substitute.For<IServerRoleAccessor>();
        _serverRoleAccessor.CurrentServerRole.Returns(ServerRole.Single);

        _logger = new CapturingLogger();
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private AdvancedPermissionsDataImport CreateHandler() =>
        new(
            _dbContext,
            _userGroupService,
            _navigationQueryService,
            _permissionResolver,
            _mainDom,
            _serverRoleAccessor,
            _logger);

    private static UmbracoApplicationStartingNotification RunNotification(
        RuntimeLevel level = RuntimeLevel.Run) =>
        new(level, isRestarting: false);

    /// <summary>
    /// Builds a fake <see cref="IUserGroup"/> with the given alias, default verbs
    /// and optional granular permissions (node → verbs).
    /// </summary>
    private static IUserGroup BuildGroup(
        string alias,
        IEnumerable<string> defaultVerbs,
        IDictionary<Guid, string[]>? granular = null)
    {
        var group = Substitute.For<IUserGroup>();
        group.Alias.Returns(alias);
        group.Permissions.Returns(new HashSet<string>(defaultVerbs, StringComparer.Ordinal));

        var granularSet = new HashSet<IGranularPermission>();
        if (granular is not null)
        {
            foreach ((Guid nodeKey, string[] verbs) in granular)
            {
                foreach (var verb in verbs)
                {
                    granularSet.Add(new DocumentGranularPermission
                    {
                        Key = nodeKey,
                        Permission = verb,
                    });
                }
            }
        }

        group.GranularPermissions.Returns(granularSet);
        return group;
    }

    private void GivenGroups(params IUserGroup[] groups)
    {
        _userGroupService.GetAllAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns(new PagedModel<IUserGroup>(groups.Length, groups));
    }

    /// <summary>
    /// Loads every persisted row for the given role as <see cref="AdvancedPermissionEntry"/>
    /// records so they can be fed directly into the real <see cref="PermissionResolver"/>.
    /// </summary>
    private async Task<IReadOnlyList<AdvancedPermissionEntry>> LoadEntriesAsync(string roleAlias) =>
        await _dbContext.Permissions.AsNoTracking()
            .Where(r => r.RoleAlias == roleAlias || r.NodeKey == AdvancedPermissionsConstants.VirtualRootNodeKey)
            .Select(r => new AdvancedPermissionEntry(r.Id, r.NodeKey, r.RoleAlias, r.Verb, r.State, r.Scope))
            .ToListAsync();

    /// <summary>
    /// Runs the real <see cref="PermissionResolver"/> for a single role over the supplied stored
    /// entries using the given root-to-target content path.
    /// </summary>
    private EffectivePermission Resolve(
        IReadOnlyList<AdvancedPermissionEntry> storedEntries,
        string verb,
        IReadOnlyList<Guid> pathFromRoot,
        string roleAlias = "editors")
    {
        var context = new PermissionResolutionContext(
            TargetNodeKey: pathFromRoot[^1],
            PathFromRoot: pathFromRoot,
            RoleAliases: [roleAlias],
            StoredEntries: storedEntries);
        return _permissionResolver.Resolve(context, verb);
    }

    /// <summary>
    /// Stubs the navigation query service so <paramref name="nodeKey"/>'s
    /// ancestors-or-self chain is <c>[node, parent, grandparent, …, root]</c>.
    /// </summary>
    private void GivenAncestorsOrSelf(Guid nodeKey, params Guid[] ancestorsOrSelfFromNode)
    {
        var local = ancestorsOrSelfFromNode.ToArray();
        _navigationQueryService
            .TryGetAncestorsOrSelfKeys(nodeKey, out Arg.Any<IEnumerable<Guid>>()!)
            .Returns(ci =>
            {
                ci[1] = local.AsEnumerable();
                return true;
            });
    }

    // -------------------------------------------------------------------------
    // Tests
    // -------------------------------------------------------------------------

    /// <summary>
    /// A group with defaults and no granular permissions produces one Allow virtual-root row per default verb
    /// and nothing else.
    /// </summary>
    [Fact]
    public async Task Handle_GroupWithDefaultsOnly_EmitsVirtualRootAllowsPerVerb()
    {
        var group = BuildGroup(
            alias: "editors",
            defaultVerbs: [AdvancedPermissionsConstants.VerbRead, AdvancedPermissionsConstants.VerbUpdate]);
        GivenGroups(group);

        var handler = CreateHandler();
        await handler.HandleAsync(RunNotification(), CancellationToken.None);

        var rows = await _dbContext.Permissions.AsNoTracking().ToListAsync();
        var editorRows = rows.Where(r => r.RoleAlias == "editors").ToList();

        Assert.Equal(2, editorRows.Count);
        Assert.All(editorRows, r =>
        {
            Assert.Equal(AdvancedPermissionsConstants.VirtualRootNodeKey, r.NodeKey);
            Assert.Equal(PermissionState.Allow, r.State);
            Assert.Equal(PermissionScope.ThisNodeAndDescendants, r.Scope);
        });
        Assert.Contains(editorRows, r => r.Verb == AdvancedPermissionsConstants.VerbRead);
        Assert.Contains(editorRows, r => r.Verb == AdvancedPermissionsConstants.VerbUpdate);
    }

    /// <summary>
    /// Rows are flushed to the database per user group — not accumulated across all groups and
    /// persisted in one shot — so memory stays bounded to one group's row count.
    /// </summary>
    [Fact]
    public async Task Handle_MultipleGroups_FlushesOncePerGroup()
    {
        var editors = BuildGroup("editors", [AdvancedPermissionsConstants.VerbRead]);
        var writers = BuildGroup("writers", [AdvancedPermissionsConstants.VerbRead, AdvancedPermissionsConstants.VerbUpdate]);
        GivenGroups(editors, writers);

        var handler = CreateHandler();
        await handler.HandleAsync(RunNotification(), CancellationToken.None);

        // Expected saves:
        //   1 for $everyone seed row,
        //   + 1 per group with emitted rows (2).
        // = 3 total.
        Assert.Equal(3, _dbContext.SaveChangesAsyncCount);
    }

    /// <summary>
    /// A granular permission set that is identical to the group's defaults produces
    /// no rows at the granular node — its effect is already covered by the virtual-root
    /// Allow entries.
    /// </summary>
    [Fact]
    public async Task Handle_GranularMatchesDefaults_EmitsNothingAtNode()
    {
        var nodeX = Guid.NewGuid();
        var group = BuildGroup(
            "editors",
            [AdvancedPermissionsConstants.VerbRead, AdvancedPermissionsConstants.VerbUpdate],
            granular: new Dictionary<Guid, string[]>
            {
                [nodeX] = [AdvancedPermissionsConstants.VerbRead, AdvancedPermissionsConstants.VerbUpdate],
            });
        GivenGroups(group);
        GivenAncestorsOrSelf(nodeX, nodeX);

        var handler = CreateHandler();
        await handler.HandleAsync(RunNotification(), CancellationToken.None);

        var nodeRows = await _dbContext.Permissions.AsNoTracking()
            .Where(r => r.NodeKey == nodeX)
            .ToListAsync();
        Assert.Empty(nodeRows);
    }

    /// <summary>
    /// A granular permission set that strips one or more of the group's defaults produces a
    /// single Deny (T&amp;D) row per stripped verb at the granular node, and no redundant Allow
    /// rows for verbs that remain granted through inheritance.
    /// </summary>
    [Fact]
    public async Task Handle_GranularStripsDefault_EmitsOnlyDenyAtNode()
    {
        var nodeX = Guid.NewGuid();
        var group = BuildGroup(
            "editors",
            [
                AdvancedPermissionsConstants.VerbRead,
                AdvancedPermissionsConstants.VerbUpdate,
                AdvancedPermissionsConstants.VerbDelete,
            ],
            granular: new Dictionary<Guid, string[]>
            {
                [nodeX] = [AdvancedPermissionsConstants.VerbRead],
            });
        GivenGroups(group);
        GivenAncestorsOrSelf(nodeX, nodeX);

        var handler = CreateHandler();
        await handler.HandleAsync(RunNotification(), CancellationToken.None);

        var nodeRows = await _dbContext.Permissions.AsNoTracking()
            .Where(r => r.NodeKey == nodeX && r.RoleAlias == "editors")
            .ToListAsync();

        Assert.Equal(2, nodeRows.Count);
        Assert.All(nodeRows, r =>
        {
            Assert.Equal(PermissionState.Deny, r.State);
            Assert.Equal(PermissionScope.ThisNodeAndDescendants, r.Scope);
        });
        Assert.Contains(nodeRows, r => r.Verb == AdvancedPermissionsConstants.VerbUpdate);
        Assert.Contains(nodeRows, r => r.Verb == AdvancedPermissionsConstants.VerbDelete);
    }

    /// <summary>
    /// Nested granulars where the inner node re-enables a verb that the outer node stripped:
    /// the migration must emit an Allow at the inner node so that the resolver produces the
    /// native Umbraco result (inner granular fully replaces outer granular for its subtree).
    /// </summary>
    [Fact]
    public async Task Handle_NestedGranularInnerReEnablesStrippedVerb_ResolverMatchesNative()
    {
        var nodeA = Guid.NewGuid();
        var nodeB = Guid.NewGuid();
        var group = BuildGroup(
            "editors",
            [AdvancedPermissionsConstants.VerbRead, AdvancedPermissionsConstants.VerbUpdate],
            granular: new Dictionary<Guid, string[]>
            {
                [nodeA] = [AdvancedPermissionsConstants.VerbRead],
                [nodeB] = [AdvancedPermissionsConstants.VerbRead, AdvancedPermissionsConstants.VerbUpdate],
            });
        GivenGroups(group);
        GivenAncestorsOrSelf(nodeA, nodeA);
        GivenAncestorsOrSelf(nodeB, nodeB, nodeA);

        var handler = CreateHandler();
        await handler.HandleAsync(RunNotification(), CancellationToken.None);

        var storedEntries = await LoadEntriesAsync("editors");

        // At A, Update = Deny (A strips Update from defaults).
        Assert.False(Resolve(storedEntries, AdvancedPermissionsConstants.VerbUpdate, [nodeA]).IsAllowed);

        // At B (child of A), Update = Allow (B re-grants Update).
        Assert.True(Resolve(storedEntries, AdvancedPermissionsConstants.VerbUpdate, [nodeA, nodeB]).IsAllowed);

        // At a descendant of B, Update = Allow (inherited from B).
        var descendantOfB = Guid.NewGuid();
        Assert.True(Resolve(storedEntries, AdvancedPermissionsConstants.VerbUpdate, [nodeA, nodeB, descendantOfB]).IsAllowed);

        // At a descendant of A that is NOT under B, Update = Deny (inherited from A).
        var descendantOfANotUnderB = Guid.NewGuid();
        Assert.False(Resolve(storedEntries, AdvancedPermissionsConstants.VerbUpdate, [nodeA, descendantOfANotUnderB]).IsAllowed);

        // Read remains Allow everywhere along the chain.
        Assert.True(Resolve(storedEntries, AdvancedPermissionsConstants.VerbRead, [nodeA]).IsAllowed);
        Assert.True(Resolve(storedEntries, AdvancedPermissionsConstants.VerbRead, [nodeA, nodeB]).IsAllowed);
    }

    /// <summary>
    /// Nested granulars where the inner node strips an additional verb: the migration must not
    /// re-emit a Deny for a verb that is already denied at the outer node (inheritance does
    /// the work), yet must add a Deny for the verb the inner node newly strips.
    /// </summary>
    [Fact]
    public async Task Handle_NestedGranularInnerStripsFurther_EmitsMinimalEntries()
    {
        var nodeA = Guid.NewGuid();
        var nodeB = Guid.NewGuid();
        var group = BuildGroup(
            "editors",
            [
                AdvancedPermissionsConstants.VerbRead,
                AdvancedPermissionsConstants.VerbUpdate,
                AdvancedPermissionsConstants.VerbDelete,
            ],
            granular: new Dictionary<Guid, string[]>
            {
                [nodeA] = [AdvancedPermissionsConstants.VerbRead, AdvancedPermissionsConstants.VerbUpdate],
                [nodeB] = [AdvancedPermissionsConstants.VerbRead],
            });
        GivenGroups(group);
        GivenAncestorsOrSelf(nodeA, nodeA);
        GivenAncestorsOrSelf(nodeB, nodeB, nodeA);

        var handler = CreateHandler();
        await handler.HandleAsync(RunNotification(), CancellationToken.None);

        // Row-level assertion: no redundant Deny Delete at B (A already denies it, B inherits).
        var bRows = await _dbContext.Permissions.AsNoTracking()
            .Where(r => r.NodeKey == nodeB && r.RoleAlias == "editors")
            .ToListAsync();
        Assert.DoesNotContain(bRows, r => r.Verb == AdvancedPermissionsConstants.VerbDelete);
        Assert.Contains(bRows, r => r.Verb == AdvancedPermissionsConstants.VerbUpdate && r.State == PermissionState.Deny);

        // Resolver-level assertion: effective permissions match native Umbraco.
        var storedEntries = await LoadEntriesAsync("editors");

        // At B and below → Read only.
        Assert.True(Resolve(storedEntries, AdvancedPermissionsConstants.VerbRead, [nodeA, nodeB]).IsAllowed);
        Assert.False(Resolve(storedEntries, AdvancedPermissionsConstants.VerbUpdate, [nodeA, nodeB]).IsAllowed);
        Assert.False(Resolve(storedEntries, AdvancedPermissionsConstants.VerbDelete, [nodeA, nodeB]).IsAllowed);

        // At A and its non-B descendants → Read + Update.
        var descA = Guid.NewGuid();
        Assert.True(Resolve(storedEntries, AdvancedPermissionsConstants.VerbRead, [nodeA, descA]).IsAllowed);
        Assert.True(Resolve(storedEntries, AdvancedPermissionsConstants.VerbUpdate, [nodeA, descA]).IsAllowed);
        Assert.False(Resolve(storedEntries, AdvancedPermissionsConstants.VerbDelete, [nodeA, descA]).IsAllowed);

        // Outside A → Read + Update + Delete (defaults).
        var outside = Guid.NewGuid();
        Assert.True(Resolve(storedEntries, AdvancedPermissionsConstants.VerbRead, [outside]).IsAllowed);
        Assert.True(Resolve(storedEntries, AdvancedPermissionsConstants.VerbUpdate, [outside]).IsAllowed);
        Assert.True(Resolve(storedEntries, AdvancedPermissionsConstants.VerbDelete, [outside]).IsAllowed);
    }

    /// <summary>
    /// A granular permission set that adds verbs beyond the group's defaults produces
    /// a single Allow (T&amp;D) row per added verb at the granular node — and nothing for
    /// verbs that are already inherited from defaults.
    /// </summary>
    [Fact]
    public async Task Handle_GranularAddsBeyondDefaults_EmitsOnlyAllowAtNode()
    {
        var nodeX = Guid.NewGuid();
        var group = BuildGroup(
            "editors",
            [AdvancedPermissionsConstants.VerbRead],
            granular: new Dictionary<Guid, string[]>
            {
                [nodeX] = [AdvancedPermissionsConstants.VerbRead, AdvancedPermissionsConstants.VerbPublish],
            });
        GivenGroups(group);
        GivenAncestorsOrSelf(nodeX, nodeX);

        var handler = CreateHandler();
        await handler.HandleAsync(RunNotification(), CancellationToken.None);

        var nodeRows = await _dbContext.Permissions.AsNoTracking()
            .Where(r => r.NodeKey == nodeX && r.RoleAlias == "editors")
            .ToListAsync();

        Assert.Single(nodeRows);
        Assert.Equal(AdvancedPermissionsConstants.VerbPublish, nodeRows[0].Verb);
        Assert.Equal(PermissionState.Allow, nodeRows[0].State);
        Assert.Equal(PermissionScope.ThisNodeAndDescendants, nodeRows[0].Scope);
    }

    /// <summary>
    /// Granular permissions whose node key no longer resolves in the content tree (orphans)
    /// are skipped with a warning; other granular nodes are still processed normally.
    /// </summary>
    [Fact]
    public async Task Handle_OrphanedGranularNode_IsSkippedAndLogged()
    {
        var liveNode = Guid.NewGuid();
        var orphanNode = Guid.NewGuid();
        var group = BuildGroup(
            "editors",
            [AdvancedPermissionsConstants.VerbRead, AdvancedPermissionsConstants.VerbUpdate],
            granular: new Dictionary<Guid, string[]>
            {
                [liveNode] = [AdvancedPermissionsConstants.VerbRead],
                [orphanNode] = [AdvancedPermissionsConstants.VerbRead],
            });
        GivenGroups(group);
        GivenAncestorsOrSelf(liveNode, liveNode);
        // orphanNode: no stub set ⇒ navigation returns false (orphan).

        var handler = CreateHandler();
        await handler.HandleAsync(RunNotification(), CancellationToken.None);

        // Orphan node produced no rows.
        var orphanRows = await _dbContext.Permissions.AsNoTracking()
            .Where(r => r.NodeKey == orphanNode)
            .ToListAsync();
        Assert.Empty(orphanRows);

        // Live node still produced its Deny Update (defaults include Update, granular set does not).
        var liveRows = await _dbContext.Permissions.AsNoTracking()
            .Where(r => r.NodeKey == liveNode && r.RoleAlias == "editors")
            .ToListAsync();
        Assert.Single(liveRows);
        Assert.Equal(AdvancedPermissionsConstants.VerbUpdate, liveRows[0].Verb);
        Assert.Equal(PermissionState.Deny, liveRows[0].State);

        // Warning logged for the orphan, naming the group alias and the node key.
        Assert.Contains(
            _logger.Records,
            r => r.Level == LogLevel.Warning
                 && r.Message.Contains("editors", StringComparison.Ordinal)
                 && r.Message.Contains(orphanNode.ToString(), StringComparison.Ordinal));
    }

    /// <summary>
    /// The <c>$everyone</c> role receives exactly one row: Allow Read (T&amp;D) at the virtual root.
    /// </summary>
    [Fact]
    public async Task Handle_EveryoneRole_GetsSingleAllowReadAtVirtualRoot()
    {
        // No groups — only the $everyone seed row should be written.
        var handler = CreateHandler();
        await handler.HandleAsync(RunNotification(), CancellationToken.None);

        var rows = await _dbContext.Permissions.AsNoTracking().ToListAsync();
        Assert.Single(rows);

        var row = rows[0];
        Assert.Equal(AdvancedPermissionsConstants.EveryoneRoleAlias, row.RoleAlias);
        Assert.Equal(AdvancedPermissionsConstants.VerbRead, row.Verb);
        Assert.Equal(PermissionState.Allow, row.State);
        Assert.Equal(PermissionScope.ThisNodeAndDescendants, row.Scope);
        Assert.Equal(AdvancedPermissionsConstants.VirtualRootNodeKey, row.NodeKey);
    }

    /// <summary>
    /// If the permission table already contains any row, the import short-circuits and no
    /// additional rows are written — the first-boot-only guard.
    /// </summary>
    [Fact]
    public async Task Handle_WhenTableHasExistingRows_DoesNotReimport()
    {
        // Pre-seed a row.
        _dbContext.Permissions.Add(new AdvancedPermissionEntity
        {
            Id = Guid.NewGuid(),
            NodeKey = Guid.NewGuid(),
            RoleAlias = "pre-existing",
            Verb = AdvancedPermissionsConstants.VerbRead,
            State = PermissionState.Allow,
            Scope = PermissionScope.ThisNodeAndDescendants,
        });
        await _dbContext.SaveChangesAsync();
        var preSaveCount = _dbContext.SaveChangesAsyncCount;

        var group = BuildGroup("editors", [AdvancedPermissionsConstants.VerbRead]);
        GivenGroups(group);

        var handler = CreateHandler();
        await handler.HandleAsync(RunNotification(), CancellationToken.None);

        // Still one row overall — the pre-existing one.
        var rows = await _dbContext.Permissions.AsNoTracking().ToListAsync();
        Assert.Single(rows);
        Assert.Equal("pre-existing", rows[0].RoleAlias);

        // No additional SaveChangesAsync calls from the handler.
        Assert.Equal(preSaveCount, _dbContext.SaveChangesAsyncCount);
    }

    /// <summary>
    /// The handler must be a no-op when a startup guard is tripped — runtime level below Run,
    /// not the main domain instance, or a subscriber server role.
    /// </summary>
    [Theory]
    [InlineData("not-run-level")]
    [InlineData("not-maindom")]
    [InlineData("subscriber")]
    public async Task Handle_StartupGuardTripped_WritesNothing(string scenario)
    {
        switch (scenario)
        {
            case "not-run-level":
                break; // will pass a non-Run RuntimeLevel below.
            case "not-maindom":
                _mainDom.IsMainDom.Returns(false);
                break;
            case "subscriber":
                _serverRoleAccessor.CurrentServerRole.Returns(ServerRole.Subscriber);
                break;
        }

        var group = BuildGroup("editors", [AdvancedPermissionsConstants.VerbRead]);
        GivenGroups(group);

        var notification = scenario == "not-run-level"
            ? RunNotification(RuntimeLevel.Install)
            : RunNotification();

        var handler = CreateHandler();
        await handler.HandleAsync(notification, CancellationToken.None);

        var rows = await _dbContext.Permissions.AsNoTracking().ToListAsync();
        Assert.Empty(rows);
    }

    /// <summary>
    /// Invariant: every row written with <c>NodeKey == VirtualRootNodeKey</c> has
    /// <see cref="PermissionState.Allow"/>. No Deny at the virtual root, ever.
    /// </summary>
    [Fact]
    public async Task Invariant_VirtualRootRows_AreAlwaysAllow()
    {
        ArrangeVariedScenario();

        var handler = CreateHandler();
        await handler.HandleAsync(RunNotification(), CancellationToken.None);

        var virtualRootRows = await _dbContext.Permissions.AsNoTracking()
            .Where(r => r.NodeKey == AdvancedPermissionsConstants.VirtualRootNodeKey)
            .ToListAsync();

        Assert.NotEmpty(virtualRootRows);
        Assert.All(virtualRootRows, r => Assert.Equal(PermissionState.Allow, r.State));
    }

    /// <summary>
    /// Invariant: every row written at a specific node (non-virtual-root) uses
    /// <see cref="PermissionScope.ThisNodeAndDescendants"/>. The migration never emits
    /// <c>ThisNodeOnly</c> or <c>DescendantsOnly</c>.
    /// </summary>
    [Fact]
    public async Task Invariant_NodeSpecificRows_AlwaysUseThisNodeAndDescendants()
    {
        ArrangeVariedScenario();

        var handler = CreateHandler();
        await handler.HandleAsync(RunNotification(), CancellationToken.None);

        var nodeSpecificRows = await _dbContext.Permissions.AsNoTracking()
            .Where(r => r.NodeKey != AdvancedPermissionsConstants.VirtualRootNodeKey)
            .ToListAsync();

        Assert.NotEmpty(nodeSpecificRows);
        Assert.All(nodeSpecificRows, r => Assert.Equal(PermissionScope.ThisNodeAndDescendants, r.Scope));
    }

    /// <summary>
    /// Sets up a mixed scenario (defaults, strip-via-granular, nested granular, granular matching defaults)
    /// so invariant assertions cover a realistic row-set.
    /// </summary>
    private void ArrangeVariedScenario()
    {
        var nodeA = Guid.NewGuid();
        var nodeB = Guid.NewGuid();
        var nodeC = Guid.NewGuid();
        var group = BuildGroup(
            "editors",
            [
                AdvancedPermissionsConstants.VerbRead,
                AdvancedPermissionsConstants.VerbUpdate,
                AdvancedPermissionsConstants.VerbDelete,
            ],
            granular: new Dictionary<Guid, string[]>
            {
                [nodeA] = [AdvancedPermissionsConstants.VerbRead, AdvancedPermissionsConstants.VerbUpdate],
                [nodeB] = [AdvancedPermissionsConstants.VerbRead],
                [nodeC] =
                [
                    AdvancedPermissionsConstants.VerbRead,
                    AdvancedPermissionsConstants.VerbUpdate,
                    AdvancedPermissionsConstants.VerbDelete,
                ],
            });
        GivenGroups(group);
        GivenAncestorsOrSelf(nodeA, nodeA);
        GivenAncestorsOrSelf(nodeB, nodeB, nodeA);
        GivenAncestorsOrSelf(nodeC, nodeC);
    }
}

/// <summary>
/// Captures logger invocations so tests can assert on level + rendered message.
/// </summary>
internal sealed class CapturingLogger : ILogger<AdvancedPermissionsDataImport>
{
    public List<(LogLevel Level, string Message)> Records { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        Records.Add((logLevel, formatter(state, exception)));
}

/// <summary>
/// Test-only derived <see cref="AdvancedPermissionsDbContext"/> that counts how many times
/// <see cref="SaveChangesAsync(CancellationToken)"/> is called during a test. Used to assert
/// the per-group-flush behaviour of the data importer.
/// </summary>
internal sealed class SaveCountingDbContext(DbContextOptions<AdvancedPermissionsDbContext> options)
    : AdvancedPermissionsDbContext(options)
{
    /// <summary>
    /// Gets the number of <see cref="SaveChangesAsync(CancellationToken)"/> invocations observed.
    /// </summary>
    public int SaveChangesAsyncCount { get; private set; }

    /// <inheritdoc />
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesAsyncCount++;
        return base.SaveChangesAsync(cancellationToken);
    }
}
