using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Models;
using Umbraco.Community.AdvancedPermissions.Data.Context;
using Umbraco.Community.AdvancedPermissions.Data.Entities;
using Umbraco.Community.AdvancedPermissions.Notifications;

namespace Umbraco.Community.AdvancedPermissions.Data.Tests;

/// <summary>
/// Integration tests for <see cref="UserGroupPermissionSeeder"/>. Exercises the
/// <see cref="Umbraco.Cms.Core.Notifications.UserGroupSavedNotification"/> handler end-to-end against a real
/// SQLite in-memory <see cref="AdvancedPermissionsDbContext"/>.
/// </summary>
public sealed class UserGroupPermissionSeederTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private DbContextOptions<AdvancedPermissionsDbContext> _options = null!;

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
    }

    /// <inheritdoc />
    public async Task DisposeAsync() => await _connection.DisposeAsync();

    /// <summary>
    /// A brand-new group whose native default permissions include verbs the package does not manage
    /// (the Umbraco 18 property-value and element verbs) must seed ONLY the verbs in
    /// <see cref="AdvancedPermissionsConstants.AllVerbs"/>. Storing an unmanaged verb would later be re-sent by
    /// the editor and rejected by the save endpoint's verb validation.
    /// </summary>
    [Fact]
    public async Task Handle_NewGroupWithUnmanagedVerbs_SeedsOnlyManagedVerbs()
    {
        var group = BuildGroup(
            "editors",
            [
                AdvancedPermissionsConstants.VerbRead,
                AdvancedPermissionsConstants.VerbUpdate,
                "Umb.Document.PropertyValue.Read",
                "Umb.Element.Create",
            ]);

        await CreateHandler().HandleAsync(Saved(group), CancellationToken.None);

        var verbs = await StoredVerbsAsync("editors");

        Assert.Equal(2, verbs.Count);
        Assert.Contains(AdvancedPermissionsConstants.VerbRead, verbs);
        Assert.Contains(AdvancedPermissionsConstants.VerbUpdate, verbs);
        Assert.DoesNotContain("Umb.Document.PropertyValue.Read", verbs);
        Assert.DoesNotContain("Umb.Element.Create", verbs);
    }

    /// <summary>
    /// A new group whose defaults contain only unmanaged verbs seeds nothing — the group starts empty
    /// (Deny-all) and is configured via the Permissions Editor.
    /// </summary>
    [Fact]
    public async Task Handle_NewGroupWithOnlyUnmanagedVerbs_SeedsNothing()
    {
        var group = BuildGroup("editors", ["Umb.Document.PropertyValue.Read", "Umb.Element.Create"]);

        await CreateHandler().HandleAsync(Saved(group), CancellationToken.None);

        Assert.Empty(await StoredVerbsAsync("editors"));
    }

    /// <summary>
    /// If the role already has any stored entries it has been configured, so the seeder leaves it untouched.
    /// </summary>
    [Fact]
    public async Task Handle_GroupAlreadyHasEntries_DoesNotReseed()
    {
        await using (var db = new AdvancedPermissionsDbContext(_options))
        {
            db.Permissions.Add(new AdvancedPermissionEntity
            {
                Id = Guid.NewGuid(),
                NodeKey = AdvancedPermissionsConstants.VirtualRootNodeKey,
                RoleAlias = "editors",
                Verb = AdvancedPermissionsConstants.VerbRead,
                State = PermissionState.Allow,
                Scope = PermissionScope.ThisNodeAndDescendants,
            });
            await db.SaveChangesAsync();
        }

        var group = BuildGroup("editors", [AdvancedPermissionsConstants.VerbUpdate]);
        await CreateHandler().HandleAsync(Saved(group), CancellationToken.None);

        var verbs = await StoredVerbsAsync("editors");
        Assert.Single(verbs);
        Assert.Equal(AdvancedPermissionsConstants.VerbRead, verbs[0]);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private UserGroupPermissionSeeder CreateHandler() =>
        new(new TestDbContextFactory(_options), Substitute.For<ILogger<UserGroupPermissionSeeder>>());

    private async Task<IReadOnlyList<string>> StoredVerbsAsync(string roleAlias)
    {
        await using var db = new AdvancedPermissionsDbContext(_options);
        return await db.Permissions.AsNoTracking()
            .Where(r => r.RoleAlias == roleAlias)
            .Select(r => r.Verb)
            .ToListAsync();
    }

    private static IUserGroup BuildGroup(string alias, IEnumerable<string> permissions)
    {
        var group = Substitute.For<IUserGroup>();
        group.Alias.Returns(alias);
        group.Permissions.Returns(new HashSet<string>(permissions, StringComparer.Ordinal));
        return group;
    }

    private static UserGroupSavedNotification Saved(params IUserGroup[] groups) =>
        new(groups.ToList(), new EventMessages());

    /// <summary>
    /// Minimal <see cref="IDbContextFactory{TContext}"/> that hands out short-lived contexts over the shared
    /// in-memory connection. <c>CreateDbContextAsync</c> uses the interface default, which calls
    /// <see cref="CreateDbContext"/> — mirroring the production adapter.
    /// </summary>
    private sealed class TestDbContextFactory(DbContextOptions<AdvancedPermissionsDbContext> options)
        : IDbContextFactory<AdvancedPermissionsDbContext>
    {
        public AdvancedPermissionsDbContext CreateDbContext() => new(options);
    }
}
