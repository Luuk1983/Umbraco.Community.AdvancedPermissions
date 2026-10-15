using NSubstitute;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Services.StartNodes;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests <see cref="StartNodeAccessService"/>. The effective start nodes come from Umbraco's real
/// <c>CalculateContentStartNodeIds</c> (run against substitutes with caching disabled), so these
/// tests also pin how Umbraco 17 combines group and user start nodes.
/// </summary>
public sealed class StartNodeAccessServiceTests
{
    /// <summary>The user service substitute.</summary>
    private readonly IUserService _userService = Substitute.For<IUserService>();

    /// <summary>The user group service substitute.</summary>
    private readonly IUserGroupService _userGroupService = Substitute.For<IUserGroupService>();

    /// <summary>The entity service substitute; holds the content tree.</summary>
    private readonly IEntityService _entityService = Substitute.For<IEntityService>();

    /// <summary>The system under test.</summary>
    private readonly StartNodeAccessService _sut;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartNodeAccessServiceTests"/> class with a tree:
    /// 10 Home &gt; 20 News; 30 Shop; 40 in the recycle bin.
    /// </summary>
    public StartNodeAccessServiceTests()
    {
        SeedTree(
            (10, "Home", "-1,10"),
            (20, "News", "-1,10,20"),
            (30, "Shop", "-1,30"),
            (40, "Old", "-1,-20,40"));
        _sut = new StartNodeAccessService(_userService, _userGroupService, _entityService, AppCaches.Disabled);
    }

    /// <summary>An unknown user yields no snapshot.</summary>
    [Fact]
    public async Task GetForUserAsync_UnknownUser_ReturnsNull()
    {
        _userService.GetAsync(Arg.Any<Guid>()).Returns((IUser?)null);

        Assert.Null(await _sut.GetForUserAsync(Guid.NewGuid()));
    }

    /// <summary>Two groups on sibling branches: both start nodes are in use.</summary>
    [Fact]
    public async Task GetForUserAsync_SiblingGroupStartNodes_BothInUse()
    {
        var user = StubUser([Group("editors", 10), Group("shop", 30)], []);

        var snapshot = await _sut.GetForUserAsync(user.Key);

        Assert.NotNull(snapshot);
        Assert.Equal(StartNodeSubjectState.Restricted, snapshot.State);
        Assert.Equal(new[] { 10, 30 }, snapshot.EffectiveStartNodeIds.Order());
        Assert.Equal(
            new[] { StartNodeSourceStatus.InUse, StartNodeSourceStatus.InUse, StartNodeSourceStatus.NotSet },
            snapshot.Sources.Select(s => s.Status));
        Assert.Equal("Home", snapshot.Nodes[10].Name);
    }

    /// <summary>A group with root access gives root access.</summary>
    [Fact]
    public async Task GetForUserAsync_RootGroup_RootAccess()
    {
        var user = StubUser([Group("admin", -1)], []);

        var snapshot = await _sut.GetForUserAsync(user.Key);

        Assert.Equal(StartNodeSubjectState.RootAccess, snapshot!.State);
    }

    /// <summary>A user-level start node narrows a root-access group.</summary>
    [Fact]
    public async Task GetForUserAsync_UserNodeUnderRootGroup_NarrowsToUserNode()
    {
        var user = StubUser([Group("admin", -1)], [20]);

        var snapshot = await _sut.GetForUserAsync(user.Key);

        Assert.Equal(StartNodeSubjectState.Restricted, snapshot!.State);
        Assert.Equal(new[] { 20 }, snapshot.EffectiveStartNodeIds);
        Assert.Equal(StartNodeSourceStatus.ReplacedByUserStartNode, snapshot.Sources[0].Status);
    }

    /// <summary>
    /// Review focus 3: a deleted user-level start node is ignored by Umbraco, so the user falls back
    /// to the group start nodes. The snapshot must show that, not hide it.
    /// </summary>
    [Fact]
    public async Task GetForUserAsync_DeletedUserNode_FallsBackToGroupNodes()
    {
        var user = StubUser([Group("editors", 10)], [99]);

        var snapshot = await _sut.GetForUserAsync(user.Key);

        Assert.Equal(new[] { 10 }, snapshot!.EffectiveStartNodeIds);
        Assert.Equal(StartNodeSourceStatus.InUse, snapshot.Sources[0].Status);
        Assert.Equal(StartNodeSourceStatus.Ignored, snapshot.Sources[1].Status);
    }

    /// <summary>
    /// A user-level start node in the recycle bin is ignored by Umbraco, so the user falls back to the
    /// group start node: the group line is in use and the trashed user line is ignored.
    /// </summary>
    [Fact]
    public async Task GetForUserAsync_TrashedUserNode_FallsBackToGroupNodes()
    {
        var user = StubUser([Group("editors", 10)], [40]);

        var snapshot = await _sut.GetForUserAsync(user.Key);

        Assert.Equal(new[] { 10 }, snapshot!.EffectiveStartNodeIds);
        Assert.Equal(StartNodeSourceStatus.InUse, snapshot.Sources[0].Status);
        Assert.Equal(StartNodeSourceStatus.Ignored, snapshot.Sources[1].Status);
    }

    /// <summary>
    /// Review focus 2: no start nodes means no content access, and the service must not call
    /// <c>GetAll</c> with an empty id list (which returns every document).
    /// </summary>
    [Fact]
    public async Task GetForUserAsync_NoStartNodes_NoAccess_WithoutQueryingAllDocuments()
    {
        var user = StubUser([Group("translators", null)], []);

        var snapshot = await _sut.GetForUserAsync(user.Key);

        Assert.Equal(StartNodeSubjectState.NoAccess, snapshot!.State);
        Assert.Equal(StartNodeSourceStatus.NoStartNode, snapshot.Sources[0].Status);
        _entityService.DidNotReceive().GetAll(UmbracoObjectTypes.Document, Arg.Is<int[]>(ids => ids.Length == 0));
    }

    /// <summary>The All Users group has no start nodes of its own.</summary>
    [Fact]
    public async Task GetForUserGroupAsync_Everyone_IsNotApplicable()
    {
        var snapshot = await _sut.GetForUserGroupAsync(AdvancedPermissionsConstants.EveryoneRoleAlias);

        Assert.Equal(StartNodeSubjectState.NotApplicable, snapshot!.State);
    }

    /// <summary>An unknown alias yields no snapshot.</summary>
    [Fact]
    public async Task GetForUserGroupAsync_UnknownAlias_ReturnsNull()
    {
        _userGroupService.GetAsync("nope").Returns((IUserGroup?)null);

        Assert.Null(await _sut.GetForUserGroupAsync("nope"));
    }

    /// <summary>A group's own start node is used on its own.</summary>
    [Fact]
    public async Task GetForUserGroupAsync_GroupWithStartNode_Restricted()
    {
        StubGroup("editors", 10);

        var snapshot = await _sut.GetForUserGroupAsync("editors");

        Assert.Equal(StartNodeSubjectState.Restricted, snapshot!.State);
        Assert.Equal(new[] { 10 }, snapshot.EffectiveStartNodeIds);
        Assert.Equal(StartNodeSourceStatus.InUse, Assert.Single(snapshot.Sources).Status);
    }

    /// <summary>A group without a start node gives no content access.</summary>
    [Fact]
    public async Task GetForUserGroupAsync_NoStartNode_NoAccess()
    {
        StubGroup("translators", null);

        var snapshot = await _sut.GetForUserGroupAsync("translators");

        Assert.Equal(StartNodeSubjectState.NoAccess, snapshot!.State);
    }

    /// <summary>A group with root access reaches everything.</summary>
    [Fact]
    public async Task GetForUserGroupAsync_Root_RootAccess()
    {
        StubGroup("admin", -1);

        var snapshot = await _sut.GetForUserGroupAsync("admin");

        Assert.Equal(StartNodeSubjectState.RootAccess, snapshot!.State);
    }

    /// <summary>A group whose start node is in the recycle bin gives no access.</summary>
    [Fact]
    public async Task GetForUserGroupAsync_TrashedStartNode_NoAccess()
    {
        StubGroup("old", 40);

        var snapshot = await _sut.GetForUserGroupAsync("old");

        Assert.Equal(StartNodeSubjectState.NoAccess, snapshot!.State);
        Assert.Equal(StartNodeSourceStatus.Ignored, snapshot.Sources[0].Status);
    }

    /// <summary>Classification uses the snapshot's effective start node paths.</summary>
    [Fact]
    public async Task Classify_AncestorOfEffectiveStartNode_IsPathOnly()
    {
        StubGroup("news", 20);
        var snapshot = await _sut.GetForUserGroupAsync("news");

        Assert.Equal(StartNodeAccess.PathOnly, _sut.Classify(snapshot!, "-1,10"));
        Assert.Equal(StartNodeAccess.Accessible, _sut.Classify(snapshot!, "-1,10,20"));
        Assert.Equal(StartNodeAccess.NotVisible, _sut.Classify(snapshot!, "-1,30"));
    }

    /// <summary>Not-applicable subjects reach every node.</summary>
    [Fact]
    public void Classify_NotApplicable_IsAccessible() =>
        Assert.Equal(StartNodeAccess.Accessible, _sut.Classify(StartNodeSnapshot.NotApplicable, "-1,30"));

    /// <summary>
    /// Seeds the content tree into both entity-service lookups Umbraco and the service use.
    /// Unknown ids are left out, exactly as Umbraco leaves out deleted nodes.
    /// </summary>
    /// <param name="nodes">The nodes: id, name and path.</param>
    private void SeedTree(params (int Id, string Name, string Path)[] nodes)
    {
        _entityService
            .GetAllPaths(UmbracoObjectTypes.Document, Arg.Any<int[]?>())
            .Returns(ci =>
            {
                var ids = ci.ArgAt<int[]?>(1) ?? [];
                return nodes
                    .Where(n => ids.Contains(n.Id))
                    .Select(n => new TreeEntityPath { Id = n.Id, Key = KeyFor(n.Id), Path = n.Path })
                    .ToList();
            });

        // Build every stub entity up front: configuring a substitute inside a Returns callback
        // corrupts NSubstitute's last-call tracking.
        var entities = nodes.Select(n => StubEntity(n.Id, n.Name, n.Path)).ToList();
        _entityService
            .GetAll(UmbracoObjectTypes.Document, Arg.Any<int[]>())
            .Returns(ci =>
            {
                var ids = ci.ArgAt<int[]>(1);
                return entities.Where(e => ids.Contains(e.Id)).ToList();
            });
    }

    /// <summary>Builds a substitute user and registers it with the user service.</summary>
    /// <param name="groups">The user's groups.</param>
    /// <param name="startContentIds">The user-level start node ids.</param>
    /// <returns>The substitute user.</returns>
    private IUser StubUser(IReadOnlyUserGroup[] groups, int[] startContentIds)
    {
        var user = Substitute.For<IUser>();
        var key = Guid.NewGuid();
        user.Key.Returns(key);
        user.Groups.Returns(groups);
        user.StartContentIds.Returns(startContentIds);
        _userService.GetAsync(key).Returns(user);
        return user;
    }

    /// <summary>Builds a substitute read-only user group, as held on a user.</summary>
    /// <param name="alias">The group alias.</param>
    /// <param name="startContentId">The group start node id; <c>-1</c> is root; <see langword="null"/> is none.</param>
    /// <returns>The substitute group.</returns>
    private static IReadOnlyUserGroup Group(string alias, int? startContentId)
    {
        var group = Substitute.For<IReadOnlyUserGroup>();
        group.Alias.Returns(alias);
        group.Name.Returns(alias);
        group.StartContentId.Returns(startContentId);
        return group;
    }

    /// <summary>Registers a substitute user group with the user group service.</summary>
    /// <param name="alias">The group alias.</param>
    /// <param name="startContentId">The group start node id; <c>-1</c> is root; <see langword="null"/> is none.</param>
    private void StubGroup(string alias, int? startContentId)
    {
        var group = Substitute.For<IUserGroup>();
        group.Alias.Returns(alias);
        group.Name.Returns(alias);
        group.StartContentId.Returns(startContentId);
        _userGroupService.GetAsync(alias).Returns(group);
    }

    /// <summary>Builds a substitute entity.</summary>
    /// <param name="id">The node id.</param>
    /// <param name="name">The node name.</param>
    /// <param name="path">The node path.</param>
    /// <returns>The substitute entity.</returns>
    private static IEntitySlim StubEntity(int id, string name, string path)
    {
        var entity = Substitute.For<IEntitySlim>();
        entity.Id.Returns(id);
        entity.Key.Returns(KeyFor(id));
        entity.Name.Returns(name);
        entity.Path.Returns(path);
        return entity;
    }

    /// <summary>A stable key per node id.</summary>
    /// <param name="id">The node id.</param>
    /// <returns>The node key.</returns>
    private static Guid KeyFor(int id) => new($"00000000-0000-0000-0000-{id:D12}");
}
