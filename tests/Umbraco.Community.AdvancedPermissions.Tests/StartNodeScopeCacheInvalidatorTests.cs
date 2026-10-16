using NSubstitute;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Community.AdvancedPermissions.Caching;
using Umbraco.Community.AdvancedPermissions.Notifications;
using Umbraco.Community.AdvancedPermissions.Services.StartNodes;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests that <see cref="StartNodeScopeCacheInvalidator"/> forgets every cached start-node scope on
/// each event that can change a subject's start nodes or the paths of start nodes, so a viewer that
/// refetches on the matching live event reads the new start nodes (issue #66).
/// </summary>
public sealed class StartNodeScopeCacheInvalidatorTests
{
    /// <summary>A user whose scope is cached before each test.</summary>
    private static readonly Guid UserKey = Guid.NewGuid();

    /// <summary>The cache under test, on a real runtime cache.</summary>
    private readonly StartNodeScopeCache _cache = new(new AppCaches(
        new ObjectCacheAppCache(), NoAppCache.Instance, new IsolatedCaches(_ => NoAppCache.Instance)));

    /// <summary>The invalidator under test.</summary>
    private readonly StartNodeScopeCacheInvalidator _sut;

    /// <summary>Initializes a new instance of the <see cref="StartNodeScopeCacheInvalidatorTests"/> class.</summary>
    public StartNodeScopeCacheInvalidatorTests()
    {
        _sut = new StartNodeScopeCacheInvalidator(_cache);
        var scope = new StartNodeScope(StartNodeTree.Content, StartNodeSubjectState.Restricted, [10], ["-1,10"]);
        _cache.SetForUser(UserKey, scope);
        _cache.SetForUserGroup("editors", scope with { Tree = StartNodeTree.Library });
    }

    /// <summary>A saved user (their own start nodes or groups may have changed).</summary>
    [Fact]
    public void UserSaved_ClearsScopes() =>
        AssertCleared(() => _sut.Handle(new UserSavedNotification(Substitute.For<IUser>(), new EventMessages())));

    /// <summary>A deleted user.</summary>
    [Fact]
    public void UserDeleted_ClearsScopes() =>
        AssertCleared(() => _sut.Handle(new UserDeletedNotification(Substitute.For<IUser>(), new EventMessages())));

    /// <summary>A saved user group (its content or Library start node may have changed).</summary>
    [Fact]
    public void UserGroupSaved_ClearsScopes() =>
        AssertCleared(() => _sut.Handle(new UserGroupSavedNotification(Substitute.For<IUserGroup>(), new EventMessages())));

    /// <summary>
    /// A user group saved with its users. Umbraco clears its own cached start nodes only on this one,
    /// so clearing here too keeps a scope from being rebuilt from Umbraco's stale value.
    /// </summary>
    [Fact]
    public void UserGroupWithUsersSaved_ClearsScopes() =>
        AssertCleared(() => _sut.Handle(new UserGroupWithUsersSavedNotification(
            new UserGroupWithUsers(Substitute.For<IUserGroup>(), [], []), new EventMessages())));

    /// <summary>A deleted user group.</summary>
    [Fact]
    public void UserGroupDeleted_ClearsScopes() =>
        AssertCleared(() => _sut.Handle(new UserGroupDeletedNotification(Substitute.For<IUserGroup>(), new EventMessages())));

    /// <summary>Moved content: a content start node's path may have changed (also a restore from the bin).</summary>
    [Fact]
    public void ContentMoved_ClearsScopes() =>
#pragma warning disable CS0618 // The obsolete MoveEventInfo overload is enough to build the notification.
        AssertCleared(() => _sut.Handle(new ContentMovedNotification(
            new MoveEventInfo<IContent>(Substitute.For<IContent>(), "-1,10", -1), new EventMessages())));
#pragma warning restore CS0618

    /// <summary>Trashed content: a trashed start node no longer counts.</summary>
    [Fact]
    public void ContentMovedToRecycleBin_ClearsScopes() =>
        AssertCleared(() => _sut.Handle(new ContentMovedToRecycleBinNotification(
            new MoveToRecycleBinEventInfo<IContent>(Substitute.For<IContent>(), "-1,10"), new EventMessages())));

    /// <summary>Deleted content: a deleted start node no longer counts.</summary>
    [Fact]
    public void ContentDeleted_ClearsScopes() =>
        AssertCleared(() => _sut.Handle(new ContentDeletedNotification(Substitute.For<IContent>(), new EventMessages())));

    /// <summary>A moved Library folder: a Library start node's path may have changed.</summary>
    [Fact]
    public void EntityContainerMoved_ClearsScopes() =>
#pragma warning disable CS0618 // The obsolete MoveEventInfo overload is enough to build the notification.
        AssertCleared(() => _sut.Handle(new EntityContainerMovedNotification(
            new MoveEventInfo<EntityContainer>(Folder(), "-1,60", -1), new EventMessages())));
#pragma warning restore CS0618

    /// <summary>A trashed Library folder.</summary>
    [Fact]
    public void EntityContainerMovedToRecycleBin_ClearsScopes() =>
        AssertCleared(() => _sut.Handle(new EntityContainerMovedToRecycleBinNotification(
            new MoveToRecycleBinEventInfo<EntityContainer>(Folder(), "-1,60"), new EventMessages())));

    /// <summary>A deleted Library folder.</summary>
    [Fact]
    public void EntityContainerDeleted_ClearsScopes() =>
        AssertCleared(() => _sut.Handle(new EntityContainerDeletedNotification(Folder(), new EventMessages())));

    /// <summary>Runs a handler and asserts both seeded scopes are gone.</summary>
    /// <param name="handle">Delivers one notification to the invalidator.</param>
    private void AssertCleared(Action handle)
    {
        Assert.NotNull(_cache.GetForUser(UserKey, StartNodeTree.Content));

        handle();

        Assert.Null(_cache.GetForUser(UserKey, StartNodeTree.Content));
        Assert.Null(_cache.GetForUserGroup("editors", StartNodeTree.Library));
    }

    /// <summary>Builds a Library folder.</summary>
    /// <returns>An element container.</returns>
    private static EntityContainer Folder() =>
        new(global::Umbraco.Cms.Core.Constants.ObjectTypes.Element) { Key = Guid.NewGuid() };
}
