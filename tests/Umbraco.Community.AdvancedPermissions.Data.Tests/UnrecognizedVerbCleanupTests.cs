using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Runtime;
using Umbraco.Cms.Core.Sync;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Models;
using Umbraco.Community.AdvancedPermissions.Data.Context;
using Umbraco.Community.AdvancedPermissions.Data.Entities;
using Umbraco.Community.AdvancedPermissions.Notifications;

namespace Umbraco.Community.AdvancedPermissions.Data.Tests;

/// <summary>
/// Integration tests for <see cref="UnrecognizedVerbCleanup"/>. Exercises the startup self-heal that purges
/// stored permission entries whose verb the package no longer manages, against a real SQLite in-memory
/// <see cref="AdvancedPermissionsDbContext"/>.
/// </summary>
public sealed class UnrecognizedVerbCleanupTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private DbContextOptions<AdvancedPermissionsDbContext> _options = null!;
    private IMainDom _mainDom = null!;
    private IServerRoleAccessor _serverRoleAccessor = null!;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();

        _options = new DbContextOptionsBuilder<AdvancedPermissionsDbContext>()
            .UseSqlite(_connection)
            .Options;

        await using var db = new AdvancedPermissionsDbContext(_options);
        await db.Database.EnsureCreatedAsync();

        _mainDom = Substitute.For<IMainDom>();
        _mainDom.IsMainDom.Returns(true);

        _serverRoleAccessor = Substitute.For<IServerRoleAccessor>();
        _serverRoleAccessor.CurrentServerRole.Returns(ServerRole.Single);
    }

    /// <inheritdoc />
    public async Task DisposeAsync() => await _connection.DisposeAsync();

    /// <summary>
    /// Entries whose verb is outside <see cref="AdvancedPermissionsConstants.AllVerbs"/> are deleted; managed
    /// verbs survive untouched, across all roles.
    /// </summary>
    [Fact]
    public async Task Handle_RemovesUnmanagedVerbs_KeepsManaged()
    {
        await SeedAsync(
            (AdvancedPermissionsConstants.VerbRead, "editors"),
            (AdvancedPermissionsConstants.VerbUpdate, "editors"),
            ("Umb.Document.PropertyValue.Read", "editors"),
            ("Umb.Element.Create", "writers"));

        await CreateHandler().HandleAsync(Notification(), CancellationToken.None);

        var verbs = await AllVerbsAsync();
        Assert.Equal(2, verbs.Count);
        Assert.Contains(AdvancedPermissionsConstants.VerbRead, verbs);
        Assert.Contains(AdvancedPermissionsConstants.VerbUpdate, verbs);
        Assert.DoesNotContain("Umb.Document.PropertyValue.Read", verbs);
        Assert.DoesNotContain("Umb.Element.Create", verbs);
    }

    /// <summary>
    /// A store containing only managed verbs is left completely untouched.
    /// </summary>
    [Fact]
    public async Task Handle_AllManaged_KeepsEverything()
    {
        await SeedAsync(
            (AdvancedPermissionsConstants.VerbRead, "editors"),
            (AdvancedPermissionsConstants.VerbPublish, "editors"));

        await CreateHandler().HandleAsync(Notification(), CancellationToken.None);

        Assert.Equal(2, (await AllVerbsAsync()).Count);
    }

    /// <summary>
    /// The handler must write nothing when a startup guard is tripped — runtime level below Run,
    /// not the main domain instance, or a subscriber server role.
    /// </summary>
    [Theory]
    [InlineData("not-run")]
    [InlineData("not-maindom")]
    [InlineData("subscriber")]
    public async Task Handle_StartupGuardTripped_DeletesNothing(string scenario)
    {
        await SeedAsync(("Umb.Document.PropertyValue.Read", "editors"));

        var notification = Notification();
        switch (scenario)
        {
            case "not-run":
                notification = new UmbracoApplicationStartingNotification(RuntimeLevel.Install, false);
                break;
            case "not-maindom":
                _mainDom.IsMainDom.Returns(false);
                break;
            case "subscriber":
                _serverRoleAccessor.CurrentServerRole.Returns(ServerRole.Subscriber);
                break;
        }

        await CreateHandler().HandleAsync(notification, CancellationToken.None);

        Assert.Single(await AllVerbsAsync());
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private UnrecognizedVerbCleanup CreateHandler() =>
        new(
            new TestDbContextFactory(_options),
            _mainDom,
            _serverRoleAccessor,
            Substitute.For<ILogger<UnrecognizedVerbCleanup>>());

    private static UmbracoApplicationStartingNotification Notification() =>
        new(RuntimeLevel.Run, false);

    private async Task SeedAsync(params (string Verb, string Role)[] rows)
    {
        await using var db = new AdvancedPermissionsDbContext(_options);
        foreach (var (verb, role) in rows)
        {
            db.Permissions.Add(new AdvancedPermissionEntity
            {
                Id = Guid.NewGuid(),
                NodeKey = AdvancedPermissionsConstants.VirtualRootNodeKey,
                RoleAlias = role,
                Verb = verb,
                State = PermissionState.Allow,
                Scope = PermissionScope.ThisNodeAndDescendants,
            });
        }

        await db.SaveChangesAsync();
    }

    private async Task<IReadOnlyList<string>> AllVerbsAsync()
    {
        await using var db = new AdvancedPermissionsDbContext(_options);
        return await db.Permissions.AsNoTracking().Select(r => r.Verb).ToListAsync();
    }

    private sealed class TestDbContextFactory(DbContextOptions<AdvancedPermissionsDbContext> options)
        : IDbContextFactory<AdvancedPermissionsDbContext>
    {
        public AdvancedPermissionsDbContext CreateDbContext() => new(options);
    }
}
