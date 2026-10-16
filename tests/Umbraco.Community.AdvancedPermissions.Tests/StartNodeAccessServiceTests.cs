using NSubstitute;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.Caching;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Services.StartNodes;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests <see cref="StartNodeAccessService"/>. The effective start nodes come from Umbraco's real
/// <c>CalculateContentStartNodeIds</c> / <c>CalculateElementStartNodeIds</c> (run against substitutes
/// with caching disabled), so these tests also pin how Umbraco 18 combines group and user start nodes.
/// </summary>
public sealed class StartNodeAccessServiceTests
{
    /// <summary>The user service substitute.</summary>
    private readonly IUserService _userService = Substitute.For<IUserService>();

    /// <summary>The user group service substitute.</summary>
    private readonly IUserGroupService _userGroupService = Substitute.For<IUserGroupService>();

    /// <summary>The entity service substitute; holds the content tree.</summary>
    private readonly IEntityService _entityService = Substitute.For<IEntityService>();

    /// <summary>The per-subject scope cache the service fills.</summary>
    private readonly StartNodeScopeCache _scopeCache;

    /// <summary>The system under test.</summary>
    private readonly StartNodeAccessService _sut;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartNodeAccessServiceTests"/> class with a tree:
    /// 10 Home &gt; 20 News; 30 Shop; 40 in the recycle bin.
    /// </summary>
    public StartNodeAccessServiceTests()
    {
        SeedTree(
            UmbracoObjectTypes.Document,
            (10, "Home", "-1,10"),
            (20, "News", "-1,10,20"),
            (30, "Shop", "-1,30"),
            (40, "Old", "-1,-20,40"));

        // Library: 60 Shared > 70 Campaigns; 80 Brand; 90 in the Library recycle bin.
        SeedTree(
            UmbracoObjectTypes.ElementContainer,
            (60, "Shared", "-1,60"),
            (70, "Campaigns", "-1,60,70"),
            (80, "Brand", "-1,80"),
            (90, "Old folder", "-1,-22,90"));
        // A real runtime cache for the package's scope cache; Umbraco's own isolated user cache stays
        // disabled, so every miss recalculates the start nodes from the seeded tree.
        var appCaches = new AppCaches(new ObjectCacheAppCache(), NoAppCache.Instance, new IsolatedCaches(_ => NoAppCache.Instance));
        _scopeCache = new StartNodeScopeCache(appCaches);
        _sut = new StartNodeAccessService(_userService, _userGroupService, _entityService, appCaches, _scopeCache);
    }

    /// <summary>An unknown user yields no snapshot.</summary>
    [Fact]
    public async Task GetForUserAsync_UnknownUser_ReturnsNull()
    {
        _userService.GetAsync(Arg.Any<Guid>()).Returns((IUser?)null);

        Assert.Null(await _sut.GetForUserAsync(Guid.NewGuid(), StartNodeTree.Content));
    }

    /// <summary>Two groups on sibling branches: both start nodes are in use.</summary>
    [Fact]
    public async Task GetForUserAsync_SiblingGroupStartNodes_BothInUse()
    {
        var user = StubUser([Group("editors", 10), Group("shop", 30)], []);

        var snapshot = await _sut.GetForUserAsync(user.Key, StartNodeTree.Content);

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

        var snapshot = await _sut.GetForUserAsync(user.Key, StartNodeTree.Content);

        Assert.Equal(StartNodeSubjectState.RootAccess, snapshot!.State);
    }

    /// <summary>A user-level start node narrows a root-access group.</summary>
    [Fact]
    public async Task GetForUserAsync_UserNodeUnderRootGroup_NarrowsToUserNode()
    {
        var user = StubUser([Group("admin", -1)], [20]);

        var snapshot = await _sut.GetForUserAsync(user.Key, StartNodeTree.Content);

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

        var snapshot = await _sut.GetForUserAsync(user.Key, StartNodeTree.Content);

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

        var snapshot = await _sut.GetForUserAsync(user.Key, StartNodeTree.Content);

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

        var snapshot = await _sut.GetForUserAsync(user.Key, StartNodeTree.Content);

        Assert.Equal(StartNodeSubjectState.NoAccess, snapshot!.State);
        Assert.Equal(StartNodeSourceStatus.NoStartNode, snapshot.Sources[0].Status);
        _entityService.DidNotReceive().GetAll(Arg.Any<IEnumerable<UmbracoObjectTypes>>(), Arg.Is<int[]>(ids => ids.Length == 0));
        _entityService.DidNotReceive().GetAll(UmbracoObjectTypes.Document, Arg.Is<int[]>(ids => ids.Length == 0));
    }

    /// <summary>The All Users group has no start nodes of its own.</summary>
    [Fact]
    public async Task GetForUserGroupAsync_Everyone_IsNotApplicable()
    {
        var snapshot = await _sut.GetForUserGroupAsync(AdvancedPermissionsConstants.EveryoneRoleAlias, StartNodeTree.Content);

        Assert.Equal(StartNodeSubjectState.NotApplicable, snapshot!.State);
    }

    /// <summary>An unknown alias yields no snapshot.</summary>
    [Fact]
    public async Task GetForUserGroupAsync_UnknownAlias_ReturnsNull()
    {
        _userGroupService.GetAsync("nope").Returns((IUserGroup?)null);

        Assert.Null(await _sut.GetForUserGroupAsync("nope", StartNodeTree.Content));
    }

    /// <summary>A group's own start node is used on its own.</summary>
    [Fact]
    public async Task GetForUserGroupAsync_GroupWithStartNode_Restricted()
    {
        StubGroup("editors", 10);

        var snapshot = await _sut.GetForUserGroupAsync("editors", StartNodeTree.Content);

        Assert.Equal(StartNodeSubjectState.Restricted, snapshot!.State);
        Assert.Equal(new[] { 10 }, snapshot.EffectiveStartNodeIds);
        Assert.Equal(StartNodeSourceStatus.InUse, Assert.Single(snapshot.Sources).Status);
    }

    /// <summary>A group without a start node gives no content access.</summary>
    [Fact]
    public async Task GetForUserGroupAsync_NoStartNode_NoAccess()
    {
        StubGroup("translators", null);

        var snapshot = await _sut.GetForUserGroupAsync("translators", StartNodeTree.Content);

        Assert.Equal(StartNodeSubjectState.NoAccess, snapshot!.State);
    }

    /// <summary>A group with root access reaches everything.</summary>
    [Fact]
    public async Task GetForUserGroupAsync_Root_RootAccess()
    {
        StubGroup("admin", -1);

        var snapshot = await _sut.GetForUserGroupAsync("admin", StartNodeTree.Content);

        Assert.Equal(StartNodeSubjectState.RootAccess, snapshot!.State);
    }

    /// <summary>A group whose start node is in the recycle bin gives no access.</summary>
    [Fact]
    public async Task GetForUserGroupAsync_TrashedStartNode_NoAccess()
    {
        StubGroup("old", 40);

        var snapshot = await _sut.GetForUserGroupAsync("old", StartNodeTree.Content);

        Assert.Equal(StartNodeSubjectState.NoAccess, snapshot!.State);
        Assert.Equal(StartNodeSourceStatus.Ignored, snapshot.Sources[0].Status);
    }

    /// <summary>Classification uses the snapshot's effective start node paths.</summary>
    [Fact]
    public async Task Classify_AncestorOfEffectiveStartNode_IsPathOnly()
    {
        StubGroup("news", 20);
        var scope = await _sut.GetScopeForUserGroupAsync("news", StartNodeTree.Content);

        Assert.Equal(StartNodeAccess.PathOnly, _sut.Classify(scope!, "-1,10"));
        Assert.Equal(StartNodeAccess.Accessible, _sut.Classify(scope!, "-1,10,20"));
        Assert.Equal(StartNodeAccess.NotVisible, _sut.Classify(scope!, "-1,30"));
    }

    /// <summary>Not-applicable subjects reach every node.</summary>
    [Fact]
    public void Classify_NotApplicable_IsAccessible() =>
        Assert.Equal(StartNodeAccess.Accessible, _sut.Classify(StartNodeScope.NotApplicableFor(StartNodeTree.Content), "-1,30"));

    /// <summary>
    /// v17 lesson, verified against v18: a user-level start node on a branch no group covers adds
    /// access. Group Home (10) plus user Shop (30) reaches both.
    /// </summary>
    [Fact]
    public async Task GetForUserAsync_UserNodeOnUncoveredBranch_AddsAccess()
    {
        var user = StubUser([Group("editors", 10)], [30]);

        var snapshot = await _sut.GetForUserAsync(user.Key, StartNodeTree.Content);

        Assert.Equal(new[] { 10, 30 }, snapshot!.EffectiveStartNodeIds.Order());
        Assert.All(snapshot.Sources, s => Assert.Equal(StartNodeSourceStatus.InUse, s.Status));
    }

    /// <summary>Library start nodes come from the groups' element start node, not the content one.</summary>
    [Fact]
    public async Task GetForUserAsync_Library_UsesElementStartNodes()
    {
        var user = StubUser([Group("editors", 10, startElementId: 70)], []);

        var snapshot = await _sut.GetForUserAsync(user.Key, StartNodeTree.Library);

        Assert.Equal(StartNodeTree.Library, snapshot!.Tree);
        Assert.Equal(StartNodeSubjectState.Restricted, snapshot.State);
        Assert.Equal(new[] { 70 }, snapshot.EffectiveStartNodeIds);
        Assert.Equal("Campaigns", snapshot.Nodes[70].Name);
    }

    /// <summary>
    /// Review focus 2: a group with a content start node but no Library start node (an upgraded
    /// site) has no Library access, and its content access is unaffected.
    /// </summary>
    [Fact]
    public async Task GetForUserAsync_GroupWithoutLibraryStartNode_LibraryNoAccess_ContentRestricted()
    {
        var user = StubUser([Group("editors", 10, startElementId: null)], []);

        var library = await _sut.GetForUserAsync(user.Key, StartNodeTree.Library);
        var content = await _sut.GetForUserAsync(user.Key, StartNodeTree.Content);

        Assert.Equal(StartNodeSubjectState.NoAccess, library!.State);
        Assert.Equal(StartNodeSourceStatus.NoStartNode, library.Sources[0].Status);
        Assert.Equal(StartNodeSubjectState.Restricted, content!.State);
        _entityService.DidNotReceive().GetAll(Arg.Any<IEnumerable<UmbracoObjectTypes>>(), Arg.Is<int[]>(ids => ids.Length == 0));
    }

    /// <summary>A user-level Library start node in the Library recycle bin is ignored.</summary>
    [Fact]
    public async Task GetForUserAsync_Library_TrashedUserNode_FallsBackToGroupNodes()
    {
        var user = StubUser([Group("editors", 10, startElementId: 80)], [], startElementIds: [90]);

        var snapshot = await _sut.GetForUserAsync(user.Key, StartNodeTree.Library);

        Assert.Equal(new[] { 80 }, snapshot!.EffectiveStartNodeIds);
        Assert.Equal(StartNodeSourceStatus.Ignored, snapshot.Sources[1].Status);
    }

    /// <summary>A group's own Library start node is used in user group mode.</summary>
    [Fact]
    public async Task GetForUserGroupAsync_Library_UsesElementStartNode()
    {
        StubGroup("editors", -1, startElementId: 60);

        var snapshot = await _sut.GetForUserGroupAsync("editors", StartNodeTree.Library);

        Assert.Equal(StartNodeSubjectState.Restricted, snapshot!.State);
        Assert.Equal(new[] { 60 }, snapshot.EffectiveStartNodeIds);
    }

    /// <summary>The All Users group is not applicable in the Library either.</summary>
    [Fact]
    public async Task GetForUserGroupAsync_Library_Everyone_IsNotApplicable()
    {
        var snapshot = await _sut.GetForUserGroupAsync(AdvancedPermissionsConstants.EveryoneRoleAlias, StartNodeTree.Library);

        Assert.Equal(StartNodeTree.Library, snapshot!.Tree);
        Assert.Equal(StartNodeSubjectState.NotApplicable, snapshot.State);
    }

    /// <summary>Library classification uses the Library recycle bin and the element folder paths.</summary>
    [Fact]
    public async Task Classify_Library_AncestorAndBin()
    {
        StubGroup("campaigns", null, startElementId: 70);
        var scope = await _sut.GetScopeForUserGroupAsync("campaigns", StartNodeTree.Library);

        Assert.Equal(StartNodeAccess.PathOnly, _sut.Classify(scope!, "-1,60"));
        Assert.Equal(StartNodeAccess.Accessible, _sut.Classify(scope!, "-1,60,70,123"));
        Assert.Equal(StartNodeAccess.NotVisible, _sut.Classify(scope!, "-1,-22,90"));
        Assert.Equal(StartNodeAccess.PathOnly, _sut.Classify(scope!, "-1"));
    }


    /// <summary>A user's scope holds Umbraco's effective start nodes and their paths, nothing more.</summary>
    [Fact]
    public async Task GetScopeForUserAsync_HoldsEffectiveIdsAndPaths()
    {
        var user = StubUser([Group("editors", 10), Group("shop", 30)], []);

        var scope = await _sut.GetScopeForUserAsync(user.Key, StartNodeTree.Content);

        Assert.Equal(StartNodeTree.Content, scope!.Tree);
        Assert.Equal(StartNodeSubjectState.Restricted, scope.State);
        Assert.Equal(new[] { 10, 30 }, scope.EffectiveStartNodeIds.Order());
        Assert.Equal(new[] { "-1,10", "-1,30" }, scope.EffectiveStartNodePaths.Order());
    }

    /// <summary>
    /// Start nodes belong to the subject, not the row: the second row asked for the same user is
    /// answered from the cache, without loading the user or its start nodes again.
    /// </summary>
    [Fact]
    public async Task GetScopeForUserAsync_SecondCall_IsServedFromCache()
    {
        var user = StubUser([Group("editors", 10)], []);

        await _sut.GetScopeForUserAsync(user.Key, StartNodeTree.Content);
        await _sut.GetScopeForUserAsync(user.Key, StartNodeTree.Content);

        await _userService.Received(1).GetAsync(user.Key);
    }

    /// <summary>The content and the Library scope of one user are cached apart.</summary>
    [Fact]
    public async Task GetScopeForUserAsync_TreesAreCachedApart()
    {
        var user = StubUser([Group("editors", 10, startElementId: 70)], []);

        var content = await _sut.GetScopeForUserAsync(user.Key, StartNodeTree.Content);
        var library = await _sut.GetScopeForUserAsync(user.Key, StartNodeTree.Library);

        Assert.Equal(new[] { 10 }, content!.EffectiveStartNodeIds);
        Assert.Equal(new[] { 70 }, library!.EffectiveStartNodeIds);
    }

    /// <summary>Clearing the cache makes the next row load the subject's start nodes again.</summary>
    [Fact]
    public async Task GetScopeForUserAsync_AfterClear_LoadsAgain()
    {
        var user = StubUser([Group("editors", 10)], []);

        await _sut.GetScopeForUserAsync(user.Key, StartNodeTree.Content);
        _scopeCache.Clear();
        await _sut.GetScopeForUserAsync(user.Key, StartNodeTree.Content);

        await _userService.Received(2).GetAsync(user.Key);
    }

    /// <summary>An unknown user has no scope, and that answer is not cached.</summary>
    [Fact]
    public async Task GetScopeForUserAsync_UnknownUser_IsNullAndNotCached()
    {
        var key = Guid.NewGuid();
        _userService.GetAsync(key).Returns((IUser?)null);

        Assert.Null(await _sut.GetScopeForUserAsync(key, StartNodeTree.Content));
        Assert.Null(await _sut.GetScopeForUserAsync(key, StartNodeTree.Content));

        await _userService.Received(2).GetAsync(key);
    }

    /// <summary>A user group's scope is cached as well.</summary>
    [Fact]
    public async Task GetScopeForUserGroupAsync_SecondCall_IsServedFromCache()
    {
        StubGroup("editors", 10);

        await _sut.GetScopeForUserGroupAsync("editors", StartNodeTree.Content);
        await _sut.GetScopeForUserGroupAsync("editors", StartNodeTree.Content);

        await _userGroupService.Received(1).GetAsync("editors");
    }

    /// <summary>The All Users group has a not-applicable scope that classifies every node as accessible.</summary>
    [Fact]
    public async Task GetScopeForUserGroupAsync_Everyone_IsNotApplicable()
    {
        var scope = await _sut.GetScopeForUserGroupAsync(AdvancedPermissionsConstants.EveryoneRoleAlias, StartNodeTree.Library);

        Assert.Equal(StartNodeSubjectState.NotApplicable, scope!.State);
        Assert.Equal(StartNodeAccess.Accessible, _sut.Classify(scope, "-1,-22,90"));
    }

    /// <summary>
    /// Seeds a tree into the entity-service lookups Umbraco and the service use. Unknown ids are left
    /// out, exactly as Umbraco leaves out deleted nodes. Like Umbraco 18's <c>EntityService</c>, the
    /// single-type <c>GetAll(objectType, ids)</c> throws for element folders (they have no CLR entity
    /// type); only the multi-type overload can read them.
    /// </summary>
    /// <param name="objectType">The object type the nodes belong to (documents or element folders).</param>
    /// <param name="nodes">The nodes: id, name and path.</param>
    private void SeedTree(UmbracoObjectTypes objectType, params (int Id, string Name, string Path)[] nodes)
    {
        _entityService
            .GetAllPaths(objectType, Arg.Any<int[]?>())
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
            .GetAll(objectType, Arg.Any<int[]>())
            .Returns(ci => objectType == UmbracoObjectTypes.ElementContainer
                ? throw new NotSupportedException($"Type \"{objectType}\" is not supported here.")
                : entities.Where(e => ci.ArgAt<int[]>(1).Contains(e.Id)).ToList());
        _entityService
            .GetAll(Arg.Is<IEnumerable<UmbracoObjectTypes>>(types => types.Contains(objectType)), Arg.Any<int[]>())
            .Returns(ci => entities.Where(e => ci.ArgAt<int[]>(1).Contains(e.Id)).ToList());
    }

    /// <summary>
    /// Umbraco 18 cannot read element folders through the single-type <c>GetAll</c>, so the service must
    /// use the multi-type overload; otherwise every Library card with a start node fails.
    /// </summary>
    [Fact]
    public async Task GetForUserGroupAsync_Library_ReadsFolderNamesThroughMultiTypeLookup()
    {
        StubGroup("shared", null, startElementId: 60);

        var snapshot = await _sut.GetForUserGroupAsync("shared", StartNodeTree.Library);

        Assert.Equal("Shared", snapshot!.Nodes[60].Name);
    }

    /// <summary>Builds a substitute user and registers it with the user service.</summary>
    /// <param name="groups">The user's groups.</param>
    /// <param name="startContentIds">The user-level content start node ids.</param>
    /// <param name="startElementIds">The user-level Library start node ids; none when omitted.</param>
    /// <returns>The substitute user.</returns>
    private IUser StubUser(IReadOnlyUserGroup[] groups, int[] startContentIds, int[]? startElementIds = null)
    {
        var user = Substitute.For<IUser>();
        var key = Guid.NewGuid();
        user.Key.Returns(key);
        user.Groups.Returns(groups);
        user.StartContentIds.Returns(startContentIds);
        user.StartElementIds.Returns(startElementIds ?? []);
        _userService.GetAsync(key).Returns(user);
        return user;
    }

    /// <summary>Builds a substitute read-only user group, as held on a user.</summary>
    /// <param name="alias">The group alias.</param>
    /// <param name="startContentId">The group start node id; <c>-1</c> is root; <see langword="null"/> is none.</param>
    /// <param name="startElementId">The group Library start node id; <see langword="null"/> is none.</param>
    /// <returns>The substitute group.</returns>
    private static IReadOnlyUserGroup Group(string alias, int? startContentId, int? startElementId = null)
    {
        var group = Substitute.For<IReadOnlyUserGroup>();
        group.Alias.Returns(alias);
        group.Name.Returns(alias);
        group.StartContentId.Returns(startContentId);
        group.StartElementId.Returns(startElementId);
        return group;
    }

    /// <summary>Registers a substitute user group with the user group service.</summary>
    /// <param name="alias">The group alias.</param>
    /// <param name="startContentId">The group start node id; <c>-1</c> is root; <see langword="null"/> is none.</param>
    /// <param name="startElementId">The group Library start node id; <see langword="null"/> is none.</param>
    private void StubGroup(string alias, int? startContentId, int? startElementId = null)
    {
        var group = Substitute.For<IUserGroup>();
        group.Alias.Returns(alias);
        group.Name.Returns(alias);
        group.StartContentId.Returns(startContentId);
        group.StartElementId.Returns(startElementId);
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
