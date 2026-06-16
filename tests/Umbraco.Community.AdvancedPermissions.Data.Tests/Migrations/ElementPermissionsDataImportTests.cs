using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Runtime;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Sync;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Models;
using Umbraco.Community.AdvancedPermissions.Data.Context;
using Umbraco.Community.AdvancedPermissions.Data.Entities;
using Umbraco.Community.AdvancedPermissions.Migrations;

namespace Umbraco.Community.AdvancedPermissions.Data.Tests.Migrations;

/// <summary>
/// Integration tests for <see cref="ElementPermissionsDataImport"/>. Exercises the first-boot element
/// seed end-to-end against a real SQLite in-memory <see cref="AdvancedPermissionsDbContext"/>.
/// </summary>
public sealed class ElementPermissionsDataImportTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AdvancedPermissionsDbContext _dbContext = null!;
    private IUserGroupService _userGroupService = null!;
    private IMainDom _mainDom = null!;
    private IServerRoleAccessor _serverRoleAccessor = null!;
    private ILogger<ElementPermissionsDataImport> _logger = null!;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AdvancedPermissionsDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new AdvancedPermissionsDbContext(options);
        await _dbContext.Database.EnsureCreatedAsync();

        _userGroupService = Substitute.For<IUserGroupService>();
        _userGroupService.GetAllAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns(new PagedModel<IUserGroup>(0, []));

        _mainDom = Substitute.For<IMainDom>();
        _mainDom.IsMainDom.Returns(true);

        _serverRoleAccessor = Substitute.For<IServerRoleAccessor>();
        _serverRoleAccessor.CurrentServerRole.Returns(ServerRole.Single);

        _logger = Substitute.For<ILogger<ElementPermissionsDataImport>>();
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private ElementPermissionsDataImport CreateHandler() =>
        new(_dbContext, _userGroupService, _mainDom, _serverRoleAccessor, _logger);

    private static UmbracoApplicationStartingNotification RunNotification(RuntimeLevel level = RuntimeLevel.Run) =>
        new(level, isRestarting: false);

    private static IUserGroup BuildGroup(string alias, IEnumerable<string> defaultVerbs)
    {
        var group = Substitute.For<IUserGroup>();
        group.Alias.Returns(alias);
        group.Permissions.Returns(new HashSet<string>(defaultVerbs, StringComparer.Ordinal));
        return group;
    }

    private void GivenGroups(params IUserGroup[] groups) =>
        _userGroupService.GetAllAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns(new PagedModel<IUserGroup>(groups.Length, groups));

    /// <summary>
    /// With no groups, the import writes exactly the <c>$everyone</c> element read default at the
    /// virtual root — guaranteeing the library is browsable out of the box.
    /// </summary>
    [Fact]
    public async Task Handle_SeedsEveryoneElementReadDefault()
    {
        var handler = CreateHandler();
        await handler.HandleAsync(RunNotification(), CancellationToken.None);

        var row = Assert.Single(await _dbContext.ElementPermissions.AsNoTracking().ToListAsync());
        Assert.Equal(AdvancedPermissionsConstants.EveryoneRoleAlias, row.RoleAlias);
        Assert.Equal(AdvancedPermissionsConstants.VerbElementRead, row.Verb);
        Assert.Equal(PermissionState.Allow, row.State);
        Assert.Equal(PermissionScope.ThisNodeAndDescendants, row.Scope);
        Assert.Equal(AdvancedPermissionsConstants.VirtualRootNodeKey, row.NodeKey);
    }

    /// <summary>
    /// A group's native element default verbs become virtual-root Allow entries.
    /// </summary>
    [Fact]
    public async Task Handle_GroupWithElementDefaults_EmitsVirtualRootAllows()
    {
        var group = BuildGroup("editors",
            [AdvancedPermissionsConstants.VerbElementRead, AdvancedPermissionsConstants.VerbElementUpdate]);
        GivenGroups(group);

        var handler = CreateHandler();
        await handler.HandleAsync(RunNotification(), CancellationToken.None);

        var editorRows = await _dbContext.ElementPermissions.AsNoTracking()
            .Where(r => r.RoleAlias == "editors")
            .ToListAsync();

        Assert.Equal(2, editorRows.Count);
        Assert.All(editorRows, r =>
        {
            Assert.Equal(AdvancedPermissionsConstants.VirtualRootNodeKey, r.NodeKey);
            Assert.Equal(PermissionState.Allow, r.State);
            Assert.Equal(PermissionScope.ThisNodeAndDescendants, r.Scope);
        });
        Assert.Contains(editorRows, r => r.Verb == AdvancedPermissionsConstants.VerbElementRead);
        Assert.Contains(editorRows, r => r.Verb == AdvancedPermissionsConstants.VerbElementUpdate);
    }

    /// <summary>
    /// Native folder (container) default verbs are mapped onto the canonical element verbs, and a group
    /// that has both a folder verb and its element equivalent yields a single de-duplicated row.
    /// </summary>
    [Fact]
    public async Task Handle_MapsContainerVerbsToCanonical_AndDeduplicates()
    {
        var group = BuildGroup("editors",
        [
            AdvancedPermissionsConstants.VerbElementContainerRead,  // → Umb.Element.Read
            AdvancedPermissionsConstants.VerbElementRead,           // duplicate of the above after mapping
            AdvancedPermissionsConstants.VerbElementContainerCreate, // → Umb.Element.Create
        ]);
        GivenGroups(group);

        var handler = CreateHandler();
        await handler.HandleAsync(RunNotification(), CancellationToken.None);

        var verbs = await _dbContext.ElementPermissions.AsNoTracking()
            .Where(r => r.RoleAlias == "editors")
            .Select(r => r.Verb)
            .ToListAsync();

        Assert.Equal(2, verbs.Count);
        Assert.Contains(AdvancedPermissionsConstants.VerbElementRead, verbs);
        Assert.Contains(AdvancedPermissionsConstants.VerbElementCreate, verbs);
        // Folder verbs are never stored verbatim — only their canonical element equivalents.
        Assert.DoesNotContain(AdvancedPermissionsConstants.VerbElementContainerRead, verbs);
    }

    /// <summary>
    /// Verbs the package does not manage (e.g. content document verbs) are ignored during the element seed.
    /// </summary>
    [Fact]
    public async Task Handle_IgnoresUnmanagedVerbs()
    {
        var group = BuildGroup("editors",
        [
            AdvancedPermissionsConstants.VerbElementRead,
            AdvancedPermissionsConstants.VerbRead,             // content verb — not an element verb
            "Umb.Document.PropertyValue.Read",                // unmanaged
        ]);
        GivenGroups(group);

        var handler = CreateHandler();
        await handler.HandleAsync(RunNotification(), CancellationToken.None);

        var row = Assert.Single(await _dbContext.ElementPermissions.AsNoTracking()
            .Where(r => r.RoleAlias == "editors")
            .ToListAsync());
        Assert.Equal(AdvancedPermissionsConstants.VerbElementRead, row.Verb);
    }

    /// <summary>
    /// If the element table already has any row, the import short-circuits (first-boot-only guard).
    /// </summary>
    [Fact]
    public async Task Handle_WhenTableHasExistingRows_DoesNotReimport()
    {
        _dbContext.ElementPermissions.Add(new ElementPermissionEntity
        {
            Id = Guid.NewGuid(),
            NodeKey = Guid.NewGuid(),
            RoleAlias = "pre-existing",
            Verb = AdvancedPermissionsConstants.VerbElementRead,
            State = PermissionState.Allow,
            Scope = PermissionScope.ThisNodeAndDescendants,
        });
        await _dbContext.SaveChangesAsync();

        GivenGroups(BuildGroup("editors", [AdvancedPermissionsConstants.VerbElementRead]));

        var handler = CreateHandler();
        await handler.HandleAsync(RunNotification(), CancellationToken.None);

        var rows = await _dbContext.ElementPermissions.AsNoTracking().ToListAsync();
        Assert.Single(rows);
        Assert.Equal("pre-existing", rows[0].RoleAlias);
    }

    /// <summary>
    /// The handler is a no-op when a startup guard is tripped — runtime below Run, not main domain,
    /// or a subscriber server role.
    /// </summary>
    [Theory]
    [InlineData("not-run-level")]
    [InlineData("not-maindom")]
    [InlineData("subscriber")]
    public async Task Handle_StartupGuardTripped_WritesNothing(string scenario)
    {
        switch (scenario)
        {
            case "not-maindom":
                _mainDom.IsMainDom.Returns(false);
                break;
            case "subscriber":
                _serverRoleAccessor.CurrentServerRole.Returns(ServerRole.Subscriber);
                break;
        }

        GivenGroups(BuildGroup("editors", [AdvancedPermissionsConstants.VerbElementRead]));

        var notification = scenario == "not-run-level"
            ? RunNotification(RuntimeLevel.Install)
            : RunNotification();

        var handler = CreateHandler();
        await handler.HandleAsync(notification, CancellationToken.None);

        Assert.Empty(await _dbContext.ElementPermissions.AsNoTracking().ToListAsync());
    }
}
