# Start Nodes in the Viewers (v18) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** All four v18 viewers (Access Viewer, Insert Options Viewer, Library Access Viewer, Library Insert Viewer) show the subject's content or Library start nodes, mark every node the subject cannot reach as "blocked", and explain why in a greyed reasoning dialog.

**Architecture:** One server service (`IStartNodeAccessService`) with a `StartNodeTree` switch (`Content` / `Library`). Every per-tree difference lives in one `StartNodeTreeDefinition` record: which Umbraco calculation, which group and user fields, which node object type, which recycle bin. The service takes Umbraco's own combined start nodes (`CalculateContentStartNodeIds` / `CalculateElementStartNodeIds`, the values enforcement uses) and classifies paths with Umbraco's `HasPathAccess`. Pure static rules (`StartNodeRules`) classify and attribute. The effective and audit responses gain one additive `startNodeAccess` field; four new endpoints feed the start-node card. The browser gets tree-aware shared pieces (display helpers, loader, card, badge, blocked cell, blocked dialog mode) wired into all four viewers.

**Tech Stack:** C# / .NET 10, Umbraco 18 (`[18.0.0,19.0.0)`), xUnit + NSubstitute; TypeScript + Lit, Vite, Vitest, hey-api generated client.

**Spec:** `docs/superpowers/specs/2026-10-15-start-nodes-in-viewers-v18-design.md`

**Reference implementation (v17, read-only):** `git -C C:\GitHub\UmbracoAdvancedSecurity show cf91d85:<path>`. Where a step says "port from cf91d85", take that file's content and apply exactly the listed changes.

## Global Constraints

- Repo `C:\GitHub\UmbracoAdvancedSecurity_v18`, branch `feature/66_start_nodes_access_viewer` (already created from `origin/main`). All paths below are relative to that repo.
- Umbraco evidence: Umbraco 18.0.0 and 18.2.1 source, sparse clones in the session scratchpad (`u18.0.0`, `u18.2.1`). `UserExtensions.cs` is identical in both. Do not read `node_modules`.
- **Never commit, push or open a PR.** Every task ends with the changes left uncommitted.
- Test-first for all backend and pure client code: write the test, watch it fail, then implement.
- C#: primary constructors, `var` for locals, `record` for data types, an XML doc comment on every type, method and property (any accessibility) with `<param>`/`<returns>`.
- C# namespace gotcha: inside `Umbraco.Community.AdvancedPermissions.*`, reach `Umbraco.Cms.*` only through `using` directives (or `global::`), never inline.
- Do not change enforcement (`AdvancedContentPermissionService`, `AdvancedElementPermissionService`, `AdvancedElementContainerPermissionService`), the resolvers, the element conditions, stored data, or the `Abstractions` contract.
- API changes are additive. Enum values travel as strings (`.ToString()`).
- `IEntityService.GetAll(objectType, ids)` with an empty id array returns **every** entity; never call it with an empty array.
- TS: `exactOptionalPropertyTypes: true` (never assign `undefined` to `field?: T`); recursive Lit methods need explicit return types; `UmbExtensionManifest` is global (never import it).
- UI copy: "user group", never "role"; "All Users group", never `$everyone`; they/them, never he/she.
- Every new localization term goes into all 28 files in `src/Umbraco.Community.AdvancedPermissions.Client/src/localization/`. In v18 those files are translated (unlike v17): English in `en.ts` and `en-us.ts`, Dutch in `nl.ts`, and a real translation in each other file, using that file's existing words for "start node", "user group", "Library", "blocked" etc. Help text in `help-docs/en` and `help-docs/nl`.

## Review Focus

1. **A Library verb that does not apply to a node kind on a blocked row** (e.g. Publish on a folder outside the start nodes) must stay N/A, not turn into a blocked lock. Pinned by `cellMode` tests in Task 9.
2. **An upgraded site's user group with no Library start node** (`StartElementId = null`) must show Library `NoAccess` (every Library row blocked, every Library Insert row blocked) while its content start node keeps working. Pinned in Tasks 2 and 6.
3. **A path through the other tree's recycle bin id**: classification and the "ignored" attribution must use the tree's own bin (`-20` content, `-22` Library); a Library node in `-1,-22,…` is not reachable without root, and a content path is never judged against `-22`. Pinned in Task 1.
4. **The All Users group and unknown subjects** on every effective and audit endpoint (content and Library) report `Accessible`, never blocked. Pinned in Tasks 4, 5 and 6.
5. **Saving a user group's content or Library start node** refreshes an open viewer with the new start nodes. Pinned in Task 7 and the manual check in Task 14.

---

### Task 1: Tree definition, models and pure rules

**Files:**
- Create: `src/Umbraco.Community.AdvancedPermissions/Services/StartNodes/StartNodeModels.cs`
- Create: `src/Umbraco.Community.AdvancedPermissions/Services/StartNodes/StartNodeTreeDefinition.cs`
- Create: `src/Umbraco.Community.AdvancedPermissions/Services/StartNodes/StartNodeRules.cs`
- Test: `tests/Umbraco.Community.AdvancedPermissions.Tests/StartNodeRulesTests.cs`

**Interfaces:**
- Produces: `enum StartNodeTree { Content, Library }`; `enum StartNodeAccess { Accessible, PathOnly, NotVisible }`; `enum StartNodeSubjectState { NotApplicable, RootAccess, Restricted, NoAccess }`; `enum StartNodeSourceKind { UserGroup, User }`; `enum StartNodeSourceStatus { InUse, NoStartNode, NotSet, CoveredBy, ReplacedByUserStartNode, NarrowedBy, Ignored, NotApplied }`; `record StartNodeSource(StartNodeSourceKind Kind, string? UserGroupAlias, string? UserGroupName, int? StartNodeId)`; `record StartNodeSourceResult(StartNodeSource Source, StartNodeSourceStatus Status, int? RelatedStartNodeId)`; `record StartNodeInfo(int Id, Guid Key, string Name, string Path)`; `record StartNodeSnapshot(StartNodeTree Tree, StartNodeSubjectState State, IReadOnlyList<int> EffectiveStartNodeIds, IReadOnlyDictionary<int, StartNodeInfo> Nodes, IReadOnlyList<StartNodeSourceResult> Sources)` with `static StartNodeSnapshot NotApplicableFor(StartNodeTree tree)`; `record StartNodeTreeDefinition(...)` with `Content`, `Library`, `For(StartNodeTree)`; `StartNodeRules.Classify(string nodePath, IReadOnlyCollection<int> effectiveIds, IEnumerable<string> effectivePaths, int recycleBinId)`, `StartNodeRules.Attribute(IReadOnlyList<StartNodeSource>, IReadOnlyCollection<int>, IReadOnlyDictionary<int, string>, int recycleBinId)`, `StartNodeRules.IsStrictAncestor(string, string)`, `StartNodeRules.IsInRecycleBin(string path, int recycleBinId)`.

- [ ] **Step 1: Write the failing rules tests**

Port `tests/Umbraco.Community.AdvancedPermissions.Tests/StartNodeRulesTests.cs` from cf91d85 with these changes:
- Class summary: "The cases mirror Umbraco 18's `UserExtensions.CombineStartNodes` and `ContentPermissions.HasPathAccess` (identical in 18.0.0 and 18.2.1)."
- Add a constant `private const int ContentBin = -20;` and pass `ContentBin` as the new last argument to every `StartNodeRules.Classify(...)` and `StartNodeRules.Attribute(...)` call (including the private `Attribute` helper).

Then add these tests before the helpers:

```csharp
    /// <summary>The Library recycle bin id.</summary>
    private const int LibraryBin = -22;

    /// <summary>Review focus 3: a Library node in the Library recycle bin is not reachable without root.</summary>
    [Fact]
    public void Classify_LibraryRecycleBinWithoutRootAccess_IsNotVisible() =>
        Assert.Equal(StartNodeAccess.NotVisible, StartNodeRules.Classify("-1,-22,50", [10], ["-1,10"], LibraryBin));

    /// <summary>With Library root access the Library recycle bin is reachable.</summary>
    [Fact]
    public void Classify_LibraryRecycleBinWithRootAccess_IsAccessible() =>
        Assert.Equal(StartNodeAccess.Accessible, StartNodeRules.Classify("-1,-22,50", [-1], ["-1"], LibraryBin));

    /// <summary>A Library start node in the Library recycle bin is ignored.</summary>
    [Fact]
    public void Attribute_LibraryTrashedStartNode_IsIgnored()
    {
        var paths = new Dictionary<int, string> { [50] = "-1,-22,50" };
        Assert.Equal(StartNodeSourceStatus.Ignored, StartNodeRules.Attribute([Group(50)], [], paths, LibraryBin)[0].Status);
    }

    /// <summary>Review focus 3: a content path is not judged against the Library bin, so -22 in a content tree is not a bin.</summary>
    [Fact]
    public void IsInRecycleBin_UsesTheTreesOwnBin()
    {
        Assert.True(StartNodeRules.IsInRecycleBin("-1,-20,40", -20));
        Assert.False(StartNodeRules.IsInRecycleBin("-1,-20,40", -22));
        Assert.True(StartNodeRules.IsInRecycleBin("-1,-22,50", -22));
        Assert.False(StartNodeRules.IsInRecycleBin("-1,-22,50", -20));
    }

    /// <summary>
    /// v17 lesson: a user-level start node on a branch no group covers adds access. Group Home (10)
    /// plus user Shop (30) is Home and Shop; both lines are in use.
    /// </summary>
    [Fact]
    public void Attribute_UserNodeOnUncoveredBranch_BothInUse()
    {
        var result = Attribute([Group(10), User(30)], [10, 30]);
        Assert.All(result, r => Assert.Equal(StartNodeSourceStatus.InUse, r.Status));
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Umbraco.Community.AdvancedPermissions.Tests --filter "FullyQualifiedName~StartNodeRulesTests"`
Expected: build FAILS: namespace `Umbraco.Community.AdvancedPermissions.Services.StartNodes` does not exist.

- [ ] **Step 3: Create the models**

`StartNodeModels.cs`: port from cf91d85 (`src/Umbraco.Community.AdvancedPermissions/Services/StartNodes/StartNodeModels.cs`) with these changes:
- Add before `StartNodeAccess`:

```csharp
/// <summary>Which start nodes apply: the content tree's or the Library's (element folders).</summary>
public enum StartNodeTree
{
    /// <summary>Content start nodes (<c>StartContentId</c> / <c>StartContentIds</c>).</summary>
    Content,

    /// <summary>Library start nodes on element folders (<c>StartElementId</c> / <c>StartElementIds</c>).</summary>
    Library,
}
```

- In the `StartNodeAccess`, `StartNodeSubjectState` and `StartNodeSourceStatus` docs, say "node" / "access" instead of "content node" / "content access" (they now cover both trees).
- Replace the `StartNodeSnapshot` record with:

```csharp
/// <summary>Everything needed to classify nodes and explain start nodes for one subject in one tree.</summary>
/// <param name="Tree">The tree these start nodes belong to.</param>
/// <param name="State">The subject's overall start-node situation.</param>
/// <param name="EffectiveStartNodeIds">Umbraco's combined start node ids; <c>-1</c> means root.</param>
/// <param name="Nodes">Every node the snapshot references, keyed by id, including the root (<c>-1</c>).</param>
/// <param name="Sources">One explained line per configured start node.</param>
public sealed record StartNodeSnapshot(
    StartNodeTree Tree,
    StartNodeSubjectState State,
    IReadOnlyList<int> EffectiveStartNodeIds,
    IReadOnlyDictionary<int, StartNodeInfo> Nodes,
    IReadOnlyList<StartNodeSourceResult> Sources)
{
    /// <summary>The snapshot for the All Users group, which has no start nodes of its own.</summary>
    /// <param name="tree">The tree the snapshot is for.</param>
    /// <returns>A not-applicable snapshot that treats every node as accessible.</returns>
    public static StartNodeSnapshot NotApplicableFor(StartNodeTree tree) =>
        new(tree, StartNodeSubjectState.NotApplicable, [], new Dictionary<int, StartNodeInfo>(), []);
}
```

- [ ] **Step 4: Create the tree definition**

`StartNodeTreeDefinition.cs`:

```csharp
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;

namespace Umbraco.Community.AdvancedPermissions.Services.StartNodes;

/// <summary>
/// Everything that differs between content and Library start nodes, in one place, so the service
/// and the rules stay the same for both trees.
/// </summary>
/// <param name="Tree">The tree this definition describes.</param>
/// <param name="NodeObjectType">The object type of the start nodes (documents, or element folders).</param>
/// <param name="RecycleBinId">The tree's recycle bin id (<c>-20</c> content, <c>-22</c> Library).</param>
/// <param name="UserGroupStartNodeId">Reads a group's start node as held on a user.</param>
/// <param name="StoredUserGroupStartNodeId">Reads a group's start node as loaded by alias.</param>
/// <param name="UserStartNodeIds">Reads the user-level start nodes.</param>
/// <param name="CalculateEffective">Umbraco's own combination, the value enforcement uses.</param>
public sealed record StartNodeTreeDefinition(
    StartNodeTree Tree,
    UmbracoObjectTypes NodeObjectType,
    int RecycleBinId,
    Func<IReadOnlyUserGroup, int?> UserGroupStartNodeId,
    Func<IUserGroup, int?> StoredUserGroupStartNodeId,
    Func<IUser, int[]?> UserStartNodeIds,
    Func<IUser, IEntityService, AppCaches, int[]?> CalculateEffective)
{
    /// <summary>Content start nodes.</summary>
    public static StartNodeTreeDefinition Content { get; } = new(
        StartNodeTree.Content,
        UmbracoObjectTypes.Document,
        Constants.System.RecycleBinContent,
        group => group.StartContentId,
        group => group.StartContentId,
        user => user.StartContentIds,
        (user, entityService, appCaches) => user.CalculateContentStartNodeIds(entityService, appCaches));

    /// <summary>Library start nodes, which are always element folders.</summary>
    public static StartNodeTreeDefinition Library { get; } = new(
        StartNodeTree.Library,
        UmbracoObjectTypes.ElementContainer,
        Constants.System.RecycleBinElement,
        group => group.StartElementId,
        group => group.StartElementId,
        user => user.StartElementIds,
        (user, entityService, appCaches) => user.CalculateElementStartNodeIds(entityService, appCaches));

    /// <summary>Gets the definition for a tree.</summary>
    /// <param name="tree">The tree.</param>
    /// <returns>The tree's definition.</returns>
    public static StartNodeTreeDefinition For(StartNodeTree tree) =>
        tree == StartNodeTree.Library ? Library : Content;
}
```

If `using Umbraco.Extensions;` is reported as unnecessary because the project has it as a global using, remove it.

- [ ] **Step 5: Create the rules**

`StartNodeRules.cs`: port from cf91d85 with these changes:
- Add `using System.Globalization;`. Remove the `RecycleBinSegment` constant.
- `Classify` gets a last parameter `int recycleBinId` (doc: "The tree's recycle bin id.") and passes it to `ContentPermissions.HasPathAccess` instead of `Constants.System.RecycleBinContent`.
- `Attribute` gets a last parameter `int recycleBinId`, passed down to `AttributeOne`, which passes it to `IsInRecycleBin`.
- Replace `IsInRecycleBin` with:

```csharp
    /// <summary>Whether a path lies in a tree's recycle bin.</summary>
    /// <param name="path">The materialised path.</param>
    /// <param name="recycleBinId">The tree's recycle bin id.</param>
    /// <returns><see langword="true"/> when the path passes through that recycle bin.</returns>
    public static bool IsInRecycleBin(string path, int recycleBinId) =>
        string.Concat(",", path, ",").Contains(
            string.Concat(",", recycleBinId.ToString(CultureInfo.InvariantCulture), ","),
            StringComparison.Ordinal);
```

- Class remarks: replace "content root" with "tree root" where it describes `-1`.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/Umbraco.Community.AdvancedPermissions.Tests --filter "FullyQualifiedName~StartNodeRulesTests"`
Expected: PASS (all ported tests plus the five new ones).

- [ ] **Step 7: Leave uncommitted**

---

### Task 2: Start-node access service (both trees)

**Files:**
- Create: `src/Umbraco.Community.AdvancedPermissions/Services/StartNodes/IStartNodeAccessService.cs`
- Create: `src/Umbraco.Community.AdvancedPermissions/Services/StartNodes/StartNodeAccessService.cs`
- Modify: `src/Umbraco.Community.AdvancedPermissions/Composing/AdvancedPermissionsComposer.cs` (after `AddSingleton<IDocTypePermissionService, DocTypePermissionService>()`, line ~114)
- Test: `tests/Umbraco.Community.AdvancedPermissions.Tests/StartNodeAccessServiceTests.cs`

**Interfaces:**
- Consumes: Task 1 types.
- Produces: `IStartNodeAccessService.GetForUserAsync(Guid userKey, StartNodeTree tree): Task<StartNodeSnapshot?>`; `GetForUserGroupAsync(string userGroupAlias, StartNodeTree tree): Task<StartNodeSnapshot?>`; `Classify(StartNodeSnapshot snapshot, string nodePath): StartNodeAccess`. Registered as a singleton.

- [ ] **Step 1: Write the failing service tests**

Port `StartNodeAccessServiceTests.cs` from cf91d85 with these changes:
- Class summary: "…Umbraco's real `CalculateContentStartNodeIds` / `CalculateElementStartNodeIds` (run against substitutes with caching disabled), so these tests also pin how Umbraco 18 combines group and user start nodes."
- Every `_sut.GetForUserAsync(x)` becomes `_sut.GetForUserAsync(x, StartNodeTree.Content)`; every `_sut.GetForUserGroupAsync(x)` becomes `_sut.GetForUserGroupAsync(x, StartNodeTree.Content)`.
- `StartNodeSnapshot.NotApplicable` becomes `StartNodeSnapshot.NotApplicableFor(StartNodeTree.Content)`.
- `SeedTree` gets a first parameter `UmbracoObjectTypes objectType` and uses it in both `GetAllPaths(objectType, …)` and `GetAll(objectType, …)` stubs. The constructor calls `SeedTree(UmbracoObjectTypes.Document, …)` with the existing four nodes, then:

```csharp
        // Library: 60 Shared > 70 Campaigns; 80 Brand; 90 in the Library recycle bin.
        SeedTree(
            UmbracoObjectTypes.ElementContainer,
            (60, "Shared", "-1,60"),
            (70, "Campaigns", "-1,60,70"),
            (80, "Brand", "-1,80"),
            (90, "Old folder", "-1,-22,90"));
```

- `StubUser` gets a third parameter `int[]? startElementIds = null` and sets `user.StartElementIds.Returns(startElementIds ?? [])`.
- `Group(alias, startContentId)` gets a third parameter `int? startElementId = null` and sets `group.StartElementId.Returns(startElementId)`. Same for `StubGroup`.

Then add these tests:

```csharp
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
        _entityService.DidNotReceive().GetAll(UmbracoObjectTypes.ElementContainer, Arg.Is<int[]>(ids => ids.Length == 0));
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
        var snapshot = await _sut.GetForUserGroupAsync("campaigns", StartNodeTree.Library);

        Assert.Equal(StartNodeAccess.PathOnly, _sut.Classify(snapshot!, "-1,60"));
        Assert.Equal(StartNodeAccess.Accessible, _sut.Classify(snapshot!, "-1,60,70,123"));
        Assert.Equal(StartNodeAccess.NotVisible, _sut.Classify(snapshot!, "-1,-22,90"));
        Assert.Equal(StartNodeAccess.PathOnly, _sut.Classify(snapshot!, "-1"));
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Umbraco.Community.AdvancedPermissions.Tests --filter "FullyQualifiedName~StartNodeAccessServiceTests"`
Expected: build FAILS: `StartNodeAccessService` does not exist.

- [ ] **Step 3: Create the interface**

`IStartNodeAccessService.cs`: port from cf91d85 with these changes:
- Summary: "Works out which nodes of the content tree or the Library a user or a single user group can reach through start nodes, and explains where those start nodes come from. Feeds the viewers only; enforcement does not use it."
- `GetForUserAsync(Guid userKey, StartNodeTree tree)` and `GetForUserGroupAsync(string userGroupAlias, StartNodeTree tree)`, each with `<param name="tree">Which start nodes to read: content or Library.</param>`. The group method's summary says the All Users group yields `StartNodeSnapshot.NotApplicableFor(tree)`.
- `Classify` keeps its signature; its `nodePath` doc says "`-1` is the tree root".

- [ ] **Step 4: Create the service**

`StartNodeAccessService.cs`:

```csharp
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.Core.Constants;

namespace Umbraco.Community.AdvancedPermissions.Services.StartNodes;

/// <summary>
/// Default <see cref="IStartNodeAccessService"/>. Takes the effective start nodes from Umbraco
/// (<c>CalculateContentStartNodeIds</c> or <c>CalculateElementStartNodeIds</c>, cached per user exactly
/// as enforcement reads them) and reads node names and paths fresh, never from Umbraco's start-node
/// path cache, which a move leaves stale.
/// </summary>
/// <param name="userService">Loads the user.</param>
/// <param name="userGroupService">Loads a user group by alias.</param>
/// <param name="entityService">Reads node keys, names and paths.</param>
/// <param name="appCaches">Umbraco's caches, passed to Umbraco's start-node calculation.</param>
public sealed class StartNodeAccessService(
    IUserService userService,
    IUserGroupService userGroupService,
    IEntityService entityService,
    AppCaches appCaches) : IStartNodeAccessService
{
    /// <summary>The tree root as a snapshot node.</summary>
    private static readonly StartNodeInfo RootNode =
        new(Constants.System.Root, Guid.Empty, string.Empty, Constants.System.RootString);

    /// <inheritdoc />
    public async Task<StartNodeSnapshot?> GetForUserAsync(Guid userKey, StartNodeTree tree)
    {
        var user = await userService.GetAsync(userKey);
        if (user is null)
        {
            return null;
        }

        var definition = StartNodeTreeDefinition.For(tree);
        var effective = definition.CalculateEffective(user, entityService, appCaches) ?? [];

        var sources = user.Groups
            .Select(g => new StartNodeSource(StartNodeSourceKind.UserGroup, g.Alias, g.Name, definition.UserGroupStartNodeId(g)))
            .ToList();
        var userIds = definition.UserStartNodeIds(user) ?? [];
        if (userIds.Length == 0)
        {
            sources.Add(new StartNodeSource(StartNodeSourceKind.User, null, null, null));
        }
        else
        {
            sources.AddRange(userIds.Select(id => new StartNodeSource(StartNodeSourceKind.User, null, null, id)));
        }

        return Build(definition, effective, sources);
    }

    /// <inheritdoc />
    public async Task<StartNodeSnapshot?> GetForUserGroupAsync(string userGroupAlias, StartNodeTree tree)
    {
        if (userGroupAlias == AdvancedPermissionsConstants.EveryoneRoleAlias)
        {
            return StartNodeSnapshot.NotApplicableFor(tree);
        }

        var group = await userGroupService.GetAsync(userGroupAlias);
        if (group is null)
        {
            return null;
        }

        var definition = StartNodeTreeDefinition.For(tree);
        var startNodeId = definition.StoredUserGroupStartNodeId(group);
        var source = new StartNodeSource(StartNodeSourceKind.UserGroup, group.Alias, group.Name, startNodeId);
        var nodes = LoadNodes(definition, startNodeId is { } id && id != Constants.System.Root ? [id] : []);

        // A group on its own: root, its one start node when it still exists outside the bin, or nothing.
        int[] effective = startNodeId switch
        {
            null => [],
            Constants.System.Root => [Constants.System.Root],
            int nodeId when nodes.TryGetValue(nodeId, out var node)
                && !StartNodeRules.IsInRecycleBin(node.Path, definition.RecycleBinId) => [nodeId],
            _ => [],
        };

        return Snapshot(definition, effective, [source], nodes);
    }

    /// <inheritdoc />
    public StartNodeAccess Classify(StartNodeSnapshot snapshot, string nodePath)
    {
        if (snapshot.State == StartNodeSubjectState.NotApplicable)
        {
            return StartNodeAccess.Accessible;
        }

        var effectivePaths = snapshot.EffectiveStartNodeIds
            .Where(snapshot.Nodes.ContainsKey)
            .Select(id => snapshot.Nodes[id].Path);

        return StartNodeRules.Classify(
            nodePath,
            snapshot.EffectiveStartNodeIds,
            effectivePaths,
            StartNodeTreeDefinition.For(snapshot.Tree).RecycleBinId);
    }

    /// <summary>Loads every node a user snapshot references and builds the snapshot.</summary>
    /// <param name="definition">The tree's definition.</param>
    /// <param name="effective">Umbraco's combined start node ids.</param>
    /// <param name="sources">The configured start nodes.</param>
    /// <returns>The snapshot.</returns>
    private StartNodeSnapshot Build(
        StartNodeTreeDefinition definition,
        IReadOnlyList<int> effective,
        IReadOnlyList<StartNodeSource> sources)
    {
        var ids = effective
            .Concat(sources.Where(s => s.StartNodeId.HasValue).Select(s => s.StartNodeId!.Value))
            .Where(id => id != Constants.System.Root)
            .Distinct()
            .ToArray();

        return Snapshot(definition, effective, sources, LoadNodes(definition, ids));
    }

    /// <summary>Assembles a snapshot from its parts.</summary>
    /// <param name="definition">The tree's definition.</param>
    /// <param name="effective">The effective start node ids.</param>
    /// <param name="sources">The configured start nodes.</param>
    /// <param name="nodes">Every referenced node, including the root.</param>
    /// <returns>The snapshot.</returns>
    private static StartNodeSnapshot Snapshot(
        StartNodeTreeDefinition definition,
        IReadOnlyList<int> effective,
        IReadOnlyList<StartNodeSource> sources,
        IReadOnlyDictionary<int, StartNodeInfo> nodes)
    {
        var state = effective.Contains(Constants.System.Root)
            ? StartNodeSubjectState.RootAccess
            : effective.Count == 0
                ? StartNodeSubjectState.NoAccess
                : StartNodeSubjectState.Restricted;

        var paths = nodes.ToDictionary(n => n.Key, n => n.Value.Path);
        return new StartNodeSnapshot(
            definition.Tree,
            state,
            effective,
            nodes,
            StartNodeRules.Attribute(sources, effective, paths, definition.RecycleBinId));
    }

    /// <summary>Loads keys, names and paths for a set of start node ids, plus the tree root.</summary>
    /// <param name="definition">The tree's definition (which object type to read).</param>
    /// <param name="ids">The node ids; must not contain the root.</param>
    /// <returns>The found nodes by id; deleted nodes are absent.</returns>
    private Dictionary<int, StartNodeInfo> LoadNodes(StartNodeTreeDefinition definition, int[] ids)
    {
        var nodes = new Dictionary<int, StartNodeInfo> { [Constants.System.Root] = RootNode };

        // GetAll with no ids returns every entity, so an empty set must never reach it.
        if (ids.Length == 0)
        {
            return nodes;
        }

        foreach (var entity in entityService.GetAll(definition.NodeObjectType, ids))
        {
            nodes[entity.Id] = new StartNodeInfo(entity.Id, entity.Key, entity.Name ?? string.Empty, entity.Path);
        }

        return nodes;
    }
}
```

- [ ] **Step 5: Register it**

In `AdvancedPermissionsComposer.cs` add `using Umbraco.Community.AdvancedPermissions.Services.StartNodes;` and, after the `IDocTypePermissionService` registration:

```csharp
        // Start nodes for the viewers (issue #66): reads Umbraco's combined content and Library start
        // nodes, never enforces.
        builder.Services.AddSingleton<IStartNodeAccessService, StartNodeAccessService>();
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/Umbraco.Community.AdvancedPermissions.Tests --filter "FullyQualifiedName~StartNodeAccessServiceTests|FullyQualifiedName~StartNodeRulesTests"`
Expected: PASS.

- [ ] **Step 7: Leave uncommitted**

---

### Task 3: Start-node endpoints (content and Library)

**Files:**
- Create: `src/Umbraco.Community.AdvancedPermissions/Controllers/Models/StartNodesResponseModels.cs`
- Create: `src/Umbraco.Community.AdvancedPermissions/Controllers/AdvancedPermissionsStartNodesController.cs`
- Modify: `tests/Umbraco.Community.AdvancedPermissions.Tests/AdvancedPermissionsControllerBaseAuthorizationTests.cs` (both `[InlineData]` lists, lines ~69-76 and ~113-121)
- Test: `tests/Umbraco.Community.AdvancedPermissions.Tests/AdvancedPermissionsStartNodesControllerTests.cs`

**Interfaces:**
- Consumes: `IStartNodeAccessService` (Task 2).
- Produces: `GET start-nodes?userKey=` (`GetStartNodesForUser`), `GET start-nodes/by-role?roleAlias=` (`GetStartNodesForRole`), `GET element/start-nodes?userKey=` (`GetElementStartNodesForUser`), `GET element/start-nodes/by-role?roleAlias=` (`GetElementStartNodesForRole`), all returning `StartNodesResponseModel(string State, IReadOnlyList<StartNodeItemResponseModel> StartNodes, IReadOnlyList<StartNodeSourceResponseModel> Sources)` or 404.

- [ ] **Step 1: Write the failing tests**

Port `AdvancedPermissionsStartNodesControllerTests.cs` from cf91d85 with these changes:
- Every `_startNodes.GetForUserAsync(x)` stub becomes `GetForUserAsync(x, StartNodeTree.Content)`; every `GetForUserGroupAsync(x)` becomes `GetForUserGroupAsync(x, StartNodeTree.Content)`.
- Every `new StartNodeSnapshot(state, …)` gets `StartNodeTree.Content` as its new first argument.

Add:

```csharp
    /// <summary>The Library endpoints read Library start nodes.</summary>
    [Fact]
    public async Task GetElementForUser_ReadsLibraryTree()
    {
        var userKey = Guid.NewGuid();
        var folderKey = Guid.NewGuid();
        var nodes = new Dictionary<int, StartNodeInfo>
        {
            [-1] = new(-1, Guid.Empty, string.Empty, "-1"),
            [60] = new(60, folderKey, "Shared", "-1,60"),
        };
        var source = new StartNodeSource(StartNodeSourceKind.UserGroup, "editors", "Editors", 60);
        _startNodes.GetForUserAsync(userKey, StartNodeTree.Library).Returns(new StartNodeSnapshot(
            StartNodeTree.Library, StartNodeSubjectState.Restricted, [60], nodes,
            [new StartNodeSourceResult(source, StartNodeSourceStatus.InUse, null)]));

        var ok = Assert.IsType<OkObjectResult>(await _sut.GetElementStartNodesForUser(userKey));
        var body = Assert.IsType<StartNodesResponseModel>(ok.Value);

        Assert.Equal("Restricted", body.State);
        Assert.Equal("Shared", Assert.Single(body.StartNodes).Name);
    }

    /// <summary>An unknown user group on the Library endpoint is a 404.</summary>
    [Fact]
    public async Task GetElementForRole_UnknownGroup_Returns404()
    {
        _startNodes.GetForUserGroupAsync("nope", StartNodeTree.Library).Returns((StartNodeSnapshot?)null);

        Assert.IsType<NotFoundObjectResult>(await _sut.GetElementStartNodesForRole("nope"));
    }
```

In `AdvancedPermissionsControllerBaseAuthorizationTests.cs`, add `[InlineData(typeof(AdvancedPermissionsStartNodesController))]` to the "management controllers are section gated" list and to the "concrete controllers derive from base" list.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Umbraco.Community.AdvancedPermissions.Tests --filter "FullyQualifiedName~StartNodesControllerTests|FullyQualifiedName~ControllerBaseAuthorizationTests"`
Expected: build FAILS: `AdvancedPermissionsStartNodesController` does not exist.

- [ ] **Step 3: Create the response models**

`StartNodesResponseModels.cs`: port from cf91d85 unchanged, except the `IsRoot` and `RelatedIsRoot` docs say "the tree root (content root or Library root)" and the `StartNodesResponseModel` summary says "for the viewers' start-node card".

- [ ] **Step 4: Create the controller**

`AdvancedPermissionsStartNodesController.cs`:

```csharp
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Community.AdvancedPermissions.Controllers.Models;
using Umbraco.Community.AdvancedPermissions.Services.StartNodes;

namespace Umbraco.Community.AdvancedPermissions.Controllers;

/// <summary>
/// Provides the content and Library start nodes of a user or user group, with where each one comes
/// from. Feeds the start-node card in the four viewers.
/// </summary>
/// <param name="startNodeAccessService">Builds start-node snapshots.</param>
[ApiVersion("1.0")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessUsers)]
public sealed class AdvancedPermissionsStartNodesController(IStartNodeAccessService startNodeAccessService)
    : AdvancedPermissionsControllerBase
{
    /// <summary>Gets a user's effective content start nodes and their sources.</summary>
    /// <param name="userKey">The user's key.</param>
    /// <returns>The start nodes, or 404 when the user does not exist.</returns>
    [HttpGet("start-nodes", Name = "GetStartNodesForUser")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<StartNodesResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [EndpointSummary("Gets a user's effective content start nodes and where each comes from.")]
    public Task<IActionResult> GetStartNodesForUser(Guid userKey) => ForUserAsync(userKey, StartNodeTree.Content);

    /// <summary>Gets one user group's own content start node.</summary>
    /// <param name="roleAlias">The user group alias, or <c>$everyone</c>.</param>
    /// <returns>The start nodes, or 404 when the group does not exist.</returns>
    [HttpGet("start-nodes/by-role", Name = "GetStartNodesForRole")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<StartNodesResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [EndpointSummary("Gets one user group's own content start node.")]
    public Task<IActionResult> GetStartNodesForRole(string roleAlias) => ForUserGroupAsync(roleAlias, StartNodeTree.Content);

    /// <summary>Gets a user's effective Library start nodes and their sources.</summary>
    /// <param name="userKey">The user's key.</param>
    /// <returns>The start nodes, or 404 when the user does not exist.</returns>
    [HttpGet("element/start-nodes", Name = "GetElementStartNodesForUser")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<StartNodesResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [EndpointSummary("Gets a user's effective Library start nodes and where each comes from.")]
    public Task<IActionResult> GetElementStartNodesForUser(Guid userKey) => ForUserAsync(userKey, StartNodeTree.Library);

    /// <summary>Gets one user group's own Library start node.</summary>
    /// <param name="roleAlias">The user group alias, or <c>$everyone</c>.</param>
    /// <returns>The start nodes, or 404 when the group does not exist.</returns>
    [HttpGet("element/start-nodes/by-role", Name = "GetElementStartNodesForRole")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<StartNodesResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [EndpointSummary("Gets one user group's own Library start node.")]
    public Task<IActionResult> GetElementStartNodesForRole(string roleAlias) => ForUserGroupAsync(roleAlias, StartNodeTree.Library);

    /// <summary>Loads and maps a user's snapshot.</summary>
    /// <param name="userKey">The user's key.</param>
    /// <param name="tree">The tree.</param>
    /// <returns>200 with the start nodes, or 404.</returns>
    private async Task<IActionResult> ForUserAsync(Guid userKey, StartNodeTree tree)
    {
        var snapshot = await startNodeAccessService.GetForUserAsync(userKey, tree);
        return snapshot is null
            ? NotFound(new ProblemDetails { Title = "User not found", Detail = $"No user with key {userKey}.", Status = StatusCodes.Status404NotFound })
            : Ok(Map(snapshot));
    }

    /// <summary>Loads and maps a user group's snapshot.</summary>
    /// <param name="roleAlias">The user group alias.</param>
    /// <param name="tree">The tree.</param>
    /// <returns>200 with the start nodes, or 404.</returns>
    private async Task<IActionResult> ForUserGroupAsync(string roleAlias, StartNodeTree tree)
    {
        var snapshot = await startNodeAccessService.GetForUserGroupAsync(roleAlias, tree);
        return snapshot is null
            ? NotFound(new ProblemDetails { Title = "User group not found", Detail = $"No user group with alias {roleAlias}.", Status = StatusCodes.Status404NotFound })
            : Ok(Map(snapshot));
    }
```

Then append `Map` and `NodeOf` exactly as in cf91d85's `AdvancedPermissionsStartNodesController.cs` and close the class.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/Umbraco.Community.AdvancedPermissions.Tests --filter "FullyQualifiedName~StartNodesControllerTests|FullyQualifiedName~ControllerBaseAuthorizationTests"`
Expected: PASS.

- [ ] **Step 6: Leave uncommitted**

---

### Task 4: `startNodeAccess` on the content effective endpoints

**Files:**
- Modify: `src/Umbraco.Community.AdvancedPermissions/Controllers/Models/EffectivePermissionsResponseModel.cs`
- Modify: `src/Umbraco.Community.AdvancedPermissions/Controllers/AdvancedPermissionsControllerBase.cs` (after `BuildPathFromRoot`)
- Modify: `src/Umbraco.Community.AdvancedPermissions/Controllers/AdvancedPermissionsEffectiveController.cs`
- Modify (compile only): `src/Umbraco.Community.AdvancedPermissions/Controllers/ElementPermissionsEffectiveController.cs`
- Test: `tests/Umbraco.Community.AdvancedPermissions.Tests/AdvancedPermissionsEffectiveControllerTests.cs`

**Interfaces:**
- Consumes: `IStartNodeAccessService` (Task 2).
- Produces: `EffectivePermissionsResponseModel(Guid NodeKey, IReadOnlyList<EffectivePermissionItem> Permissions, string StartNodeAccess)`; `AdvancedPermissionsControllerBase.GetDocumentPath(Guid nodeKey, IEntityService entityService): string?`.

- [ ] **Step 1: Write the failing tests**

Apply cf91d85's diff of `AdvancedPermissionsEffectiveControllerTests.cs` (shown by `git -C C:\GitHub\UmbracoAdvancedSecurity show cf91d85 -- tests/Umbraco.Community.AdvancedPermissions.Tests/AdvancedPermissionsEffectiveControllerTests.cs`) to the v18 file, with these changes:
- `_startNodes.GetForUserAsync(userKey)` → `GetForUserAsync(userKey, StartNodeTree.Content)`; `GetForUserAsync(Arg.Any<Guid>())` → `GetForUserAsync(Arg.Any<Guid>(), StartNodeTree.Content)`; `GetForUserGroupAsync("editors")` → `GetForUserGroupAsync("editors", StartNodeTree.Content)`.
- `StartNodeSnapshot.NotApplicable with { State = StartNodeSubjectState.Restricted }` → `StartNodeSnapshot.NotApplicableFor(StartNodeTree.Content) with { State = StartNodeSubjectState.Restricted }`.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Umbraco.Community.AdvancedPermissions.Tests --filter "FullyQualifiedName~AdvancedPermissionsEffectiveControllerTests"`
Expected: build FAILS: the controller has no 3-argument constructor; the model has no `StartNodeAccess`.

- [ ] **Step 3: Extend the model**

Replace the `EffectivePermissionsResponseModel` record and its doc with:

```csharp
/// <summary>
/// Represents the full set of resolved effective permissions for a user or role at a content node or
/// Library node, plus whether start nodes let the subject reach it.
/// </summary>
/// <param name="NodeKey">The node key these permissions were resolved for.</param>
/// <param name="Permissions">
/// The effective permission for each verb. Always the advanced-permission result, even when start
/// nodes block the node, so the viewer can show what would apply if the node were reachable.
/// </param>
/// <param name="StartNodeAccess">
/// Whether the subject can reach the node through its start nodes (content start nodes for content
/// nodes, Library start nodes for Library nodes): <c>Accessible</c>, <c>PathOnly</c> (an ancestor of
/// a start node) or <c>NotVisible</c>.
/// </param>
public sealed record EffectivePermissionsResponseModel(
    Guid NodeKey,
    IReadOnlyList<EffectivePermissionItem> Permissions,
    string StartNodeAccess);
```

- [ ] **Step 4: Add the path helper and wire the content controller**

In `AdvancedPermissionsControllerBase.cs`, after `BuildPathFromRoot`, add `GetDocumentPath` exactly as in cf91d85's diff of that file.

In `AdvancedPermissionsEffectiveController.cs` apply cf91d85's diff of that file, with `GetForUserAsync(userKey)` → `GetForUserAsync(userKey, StartNodeTree.Content)` and `GetForUserGroupAsync(roleAlias)` → `GetForUserGroupAsync(roleAlias, StartNodeTree.Content)`. Keep the v18 `Name = "GetEffectiveForUser"` / `"GetEffectiveForRole"` route names.

In `ElementPermissionsEffectiveController.cs`, make it compile for now by passing `nameof(StartNodeAccess.Accessible)` as the third argument at every `new EffectivePermissionsResponseModel(...)` (add `using Umbraco.Community.AdvancedPermissions.Services.StartNodes;`). Task 5 replaces the two viewer endpoints' values.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/Umbraco.Community.AdvancedPermissions.Tests --filter "FullyQualifiedName~AdvancedPermissionsEffectiveControllerTests"`
Expected: PASS.

- [ ] **Step 6: Leave uncommitted**

---

### Task 5: `startNodeAccess` on the Library effective endpoints

**Files:**
- Modify: `src/Umbraco.Community.AdvancedPermissions/Services/ElementTreePathResolver.cs`
- Modify: `src/Umbraco.Community.AdvancedPermissions/Controllers/ElementPermissionsEffectiveController.cs`
- Test: `tests/Umbraco.Community.AdvancedPermissions.Tests/ElementPermissionsEffectiveControllerStartNodeTests.cs` (new)

**Interfaces:**
- Consumes: `IStartNodeAccessService` (Task 2), `EffectivePermissionsResponseModel` (Task 4).
- Produces: `ElementTreePathResolver.GetPath(IEntityService entityService, Guid nodeKey): string?`; `ElementPermissionsEffectiveController(IElementNodePermissionService, IEntityService, IBackOfficeSecurityAccessor, IStartNodeAccessService)`.

- [ ] **Step 1: Write the failing tests**

```csharp
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.Controllers;
using Umbraco.Community.AdvancedPermissions.Controllers.Models;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;
using Umbraco.Community.AdvancedPermissions.Services.StartNodes;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests that the Library effective endpoints report whether the subject can reach the node through
/// its Library start nodes (issue #66).
/// </summary>
public sealed class ElementPermissionsEffectiveControllerStartNodeTests
{
    /// <summary>The element permission service substitute.</summary>
    private readonly IElementNodePermissionService _permissionService = Substitute.For<IElementNodePermissionService>();

    /// <summary>The entity service substitute.</summary>
    private readonly IEntityService _entityService = Substitute.For<IEntityService>();

    /// <summary>The start-node service substitute.</summary>
    private readonly IStartNodeAccessService _startNodes = Substitute.For<IStartNodeAccessService>();

    /// <summary>The system under test.</summary>
    private readonly ElementPermissionsEffectiveController _sut;

    /// <summary>A restricted Library snapshot used as the classification input.</summary>
    private readonly StartNodeSnapshot _restricted =
        StartNodeSnapshot.NotApplicableFor(StartNodeTree.Library) with { State = StartNodeSubjectState.Restricted };

    /// <summary>Initializes a new instance of the <see cref="ElementPermissionsEffectiveControllerStartNodeTests"/> class.</summary>
    public ElementPermissionsEffectiveControllerStartNodeTests() =>
        _sut = new ElementPermissionsEffectiveController(
            _permissionService, _entityService, Substitute.For<IBackOfficeSecurityAccessor>(), _startNodes);

    /// <summary>A known element carries the Library classification for the user.</summary>
    [Fact]
    public async Task GetEffectiveForUser_KnownElement_CarriesLibraryClassification()
    {
        var userKey = Guid.NewGuid();
        var nodeKey = SeedElement(123, "-1,60,123");
        _startNodes.GetForUserAsync(userKey, StartNodeTree.Library).Returns(_restricted);
        _startNodes.Classify(_restricted, "-1,60,123").Returns(StartNodeAccess.NotVisible);

        var ok = Assert.IsType<OkObjectResult>(await _sut.GetEffectiveForUser(CancellationToken.None, userKey, nodeKey));

        Assert.Equal("NotVisible", Assert.IsType<EffectivePermissionsResponseModel>(ok.Value).StartNodeAccess);
    }

    /// <summary>The virtual root classifies the Library root path <c>-1</c>.</summary>
    [Fact]
    public async Task GetEffectiveForRole_VirtualRoot_ClassifiesLibraryRoot()
    {
        _startNodes.GetForUserGroupAsync("editors", StartNodeTree.Library).Returns(_restricted);
        _startNodes.Classify(_restricted, "-1").Returns(StartNodeAccess.PathOnly);
        _permissionService
            .ResolveForRoleAsync("editors", AdvancedPermissionsConstants.VirtualRootNodeKey, Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<IEnumerable<string>?>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, EffectivePermission>());

        var ok = Assert.IsType<OkObjectResult>(
            await _sut.GetEffectiveForRole(CancellationToken.None, "editors", AdvancedPermissionsConstants.VirtualRootNodeKey));

        Assert.Equal("PathOnly", Assert.IsType<EffectivePermissionsResponseModel>(ok.Value).StartNodeAccess);
    }

    /// <summary>Review focus 4: an unknown user is never shown as blocked.</summary>
    [Fact]
    public async Task GetEffectiveForUser_UnknownUser_IsAccessible()
    {
        var nodeKey = SeedElement(123, "-1,60,123");
        _startNodes.GetForUserAsync(Arg.Any<Guid>(), StartNodeTree.Library).Returns((StartNodeSnapshot?)null);

        var ok = Assert.IsType<OkObjectResult>(await _sut.GetEffectiveForUser(CancellationToken.None, Guid.NewGuid(), nodeKey));

        Assert.Equal("Accessible", Assert.IsType<EffectivePermissionsResponseModel>(ok.Value).StartNodeAccess);
    }

    /// <summary>Review focus 4: the All Users group is never shown as blocked.</summary>
    [Fact]
    public async Task GetEffectiveForRole_Everyone_IsAccessible()
    {
        var nodeKey = SeedElement(123, "-1,60,123");
        var notApplicable = StartNodeSnapshot.NotApplicableFor(StartNodeTree.Library);
        _startNodes.GetForUserGroupAsync(AdvancedPermissionsConstants.EveryoneRoleAlias, StartNodeTree.Library).Returns(notApplicable);
        _startNodes.Classify(notApplicable, Arg.Any<string>()).Returns(StartNodeAccess.Accessible);

        var ok = Assert.IsType<OkObjectResult>(
            await _sut.GetEffectiveForRole(CancellationToken.None, AdvancedPermissionsConstants.EveryoneRoleAlias, nodeKey));

        Assert.Equal("Accessible", Assert.IsType<EffectivePermissionsResponseModel>(ok.Value).StartNodeAccess);
    }

    /// <summary>An unknown Library node is reported as not visible.</summary>
    [Fact]
    public async Task GetEffectiveForUser_UnknownNode_IsNotVisible()
    {
        _entityService.GetAllPaths(Arg.Any<UmbracoObjectTypes>(), Arg.Any<Guid[]>()).Returns([]);

        var ok = Assert.IsType<OkObjectResult>(await _sut.GetEffectiveForUser(CancellationToken.None, Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal("NotVisible", Assert.IsType<EffectivePermissionsResponseModel>(ok.Value).StartNodeAccess);
    }

    /// <summary>Seeds one element (not a folder) and an empty resolver answer.</summary>
    /// <param name="id">The element id.</param>
    /// <param name="path">The element path.</param>
    /// <returns>The element key.</returns>
    private Guid SeedElement(int id, string path)
    {
        var nodeKey = Guid.NewGuid();
        // Build every stub entity first: configuring a substitute inside a .Returns(...) argument
        // corrupts NSubstitute's last-call tracking.
        var folder = Substitute.For<IEntitySlim>();
        folder.Id.Returns(60);
        folder.Key.Returns(Guid.NewGuid());
        var element = Substitute.For<IEntitySlim>();
        element.Id.Returns(id);
        element.Key.Returns(nodeKey);
        _entityService.GetAllPaths(UmbracoObjectTypes.Element, Arg.Any<Guid[]>())
            .Returns([new TreeEntityPath { Id = id, Key = nodeKey, Path = path }]);
        _entityService.GetAll(Arg.Any<UmbracoObjectTypes>(), Arg.Any<int[]>()).Returns([folder, element]);
        _permissionService
            .ResolveAllAsync(Arg.Any<Guid>(), nodeKey, Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<IEnumerable<string>?>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, EffectivePermission>());
        _permissionService
            .ResolveForRoleAsync(Arg.Any<string>(), nodeKey, Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<IEnumerable<string>?>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, EffectivePermission>());
        return nodeKey;
    }
}
```

Check `ElementTreePathResolver.BuildPathFromRoot(IEntityService, string)` (line ~48) for how it maps ids to keys; if it calls `GetAll` per object type, the `Arg.Any<UmbracoObjectTypes>()` stub above covers both. If an existing seam needs a different stub, adjust `SeedElement` only; the assertions stay.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Umbraco.Community.AdvancedPermissions.Tests --filter "FullyQualifiedName~ElementPermissionsEffectiveControllerStartNodeTests"`
Expected: build FAILS: no 4-argument constructor.

- [ ] **Step 3: Add the element path helper**

In `ElementTreePathResolver.cs`, add before the first `BuildPathFromRoot`:

```csharp
    /// <summary>
    /// Gets an element's or element folder's materialised path (e.g. <c>-1,1001,1002</c>), read fresh
    /// from the database.
    /// </summary>
    /// <param name="entityService">The Umbraco entity service.</param>
    /// <param name="nodeKey">The element or folder key.</param>
    /// <returns>The path, or <see langword="null"/> when the node is not in the element tree.</returns>
    public static string? GetPath(IEntityService entityService, Guid nodeKey) =>
        (entityService.GetAllPaths(UmbracoObjectTypes.Element, nodeKey).FirstOrDefault()
            ?? entityService.GetAllPaths(UmbracoObjectTypes.ElementContainer, nodeKey).FirstOrDefault())?.Path;
```

and make `BuildPathFromRoot(IEntityService, Guid)` use it:

```csharp
    public static IReadOnlyList<Guid> BuildPathFromRoot(IEntityService entityService, Guid nodeKey)
    {
        // The key may be an element or a folder; GetPath tries both object types.
        var path = GetPath(entityService, nodeKey);
        return path is null ? [] : BuildPathFromRoot(entityService, path);
    }
```

- [ ] **Step 4: Wire the Library controller**

In `ElementPermissionsEffectiveController.cs`:
- Add `using Umbraco.Cms.Core;` and the constructor parameter `IStartNodeAccessService startNodeAccessService` (last), with `/// <param name="startNodeAccessService">Classifies the node against the subject's Library start nodes.</param>`.
- In `GetEffectiveForUser`: the empty-path return passes `nameof(StartNodeAccess.NotVisible)`; the final return passes `(await ClassifyAsync(startNodeAccessService.GetForUserAsync(userKey, StartNodeTree.Library), nodeKey)).ToString()`.
- In `GetEffectiveForRole`: the same, with `GetForUserGroupAsync(roleAlias, StartNodeTree.Library)`.
- `GetEffectiveForCurrentUser` keeps `nameof(StartNodeAccess.Accessible)` everywhere, with a `<remarks>` line: "Start nodes are not classified here: this endpoint gates action visibility for the current user, and enforcement already refuses unreachable nodes. `StartNodeAccess` is always `Accessible`."
- Add the `<returns>` wording "…with full reasoning, plus the node's Library start-node access." to the two viewer endpoints.
- Add:

```csharp
    /// <summary>
    /// Classifies a Library node for a subject. A subject that cannot be found reports
    /// <c>Accessible</c>, so the viewer never shows a restriction it could not verify. The virtual root
    /// is the Library root (<c>-1</c>).
    /// </summary>
    /// <param name="snapshotTask">Loads the subject's Library start-node snapshot (null when unknown).</param>
    /// <param name="nodeKey">The element, folder or virtual-root key.</param>
    /// <returns>The node's start-node access.</returns>
    private async Task<StartNodeAccess> ClassifyAsync(Task<StartNodeSnapshot?> snapshotTask, Guid nodeKey)
    {
        var snapshot = await snapshotTask;
        if (snapshot is null)
        {
            return StartNodeAccess.Accessible;
        }

        var path = nodeKey == AdvancedPermissionsConstants.VirtualRootNodeKey
            ? Constants.System.RootString
            : ElementTreePathResolver.GetPath(entityService, nodeKey);
        return path is null ? StartNodeAccess.NotVisible : startNodeAccessService.Classify(snapshot, path);
    }
```

If the compiler resolves `Constants` to the package's `Core.Constants` namespace, write `global::Umbraco.Cms.Core.Constants.System.RootString` instead.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/Umbraco.Community.AdvancedPermissions.Tests --filter "FullyQualifiedName~ElementPermissionsEffective|FullyQualifiedName~ElementPermissionsTreeControllerTests"`
Expected: PASS.

- [ ] **Step 6: Leave uncommitted**

---

### Task 6: `startNodeAccess` on both audits

**Files:**
- Modify: `src/Umbraco.Community.AdvancedPermissions/Controllers/Models/DocTypePermissionResponseModels.cs` (`DocTypeAuditForNodeResponseModel`)
- Modify: `src/Umbraco.Community.AdvancedPermissions/Controllers/DocTypePermissionsController.cs` (constructor; `AuditForNode` return at line ~293; `ElementTypeAudit` return at line ~387)
- Modify: `tests/Umbraco.Community.AdvancedPermissions.Tests/DocTypePermissionsControllerElementTypeAuditTests.cs` (constructor)
- Modify: `tests/Umbraco.Community.AdvancedPermissions.Tests/PermissionStampEndpointTests.cs:321-326`
- Test: `tests/Umbraco.Community.AdvancedPermissions.Tests/DocTypeAuditStartNodeTests.cs` (new)

**Interfaces:**
- Consumes: `IStartNodeAccessService` (Task 2).
- Produces: `DocTypeAuditForNodeResponseModel(Guid NodeKey, IReadOnlyList<DocTypeAuditForNodeRowResponseModel> Results, string StartNodeAccess)`; `DocTypePermissionsController` gains a last constructor parameter `IStartNodeAccessService startNodeAccessService`.

- [ ] **Step 1: Write the failing tests**

Port `DocTypeAuditStartNodeTests.cs` from cf91d85 with these changes:
- Stubs: `GetForUserAsync(userKey)` → `GetForUserAsync(userKey, StartNodeTree.Content)`; `GetForUserGroupAsync(x)` → `GetForUserGroupAsync(x, StartNodeTree.Content)`.
- `StartNodeSnapshot.NotApplicable` → `StartNodeSnapshot.NotApplicableFor(StartNodeTree.Content)` (in the `with` expression as well).
- Class summary also mentions the element-type audit.

Add these Library Insert Viewer tests:

```csharp
    /// <summary>
    /// Review focus 2: a subject without any Library access is blocked on the whole element-type
    /// audit; creation is section-global, so the verdict is per subject.
    /// </summary>
    [Fact]
    public async Task ElementTypeAudit_NoLibraryAccess_IsNotVisible()
    {
        var noAccess = StartNodeSnapshot.NotApplicableFor(StartNodeTree.Library) with { State = StartNodeSubjectState.NoAccess };
        _startNodes.GetForUserGroupAsync("editors", StartNodeTree.Library).Returns(noAccess);

        var ok = Assert.IsType<OkObjectResult>(await _sut.ElementTypeAudit(CancellationToken.None, null, "editors"));

        Assert.Equal("NotVisible", Assert.IsType<DocTypeAuditForNodeResponseModel>(ok.Value).StartNodeAccess);
    }

    /// <summary>A restricted subject can still create inside its folders, so the audit is not blocked.</summary>
    [Fact]
    public async Task ElementTypeAudit_RestrictedLibrary_IsAccessible()
    {
        var restricted = StartNodeSnapshot.NotApplicableFor(StartNodeTree.Library) with { State = StartNodeSubjectState.Restricted };
        _startNodes.GetForUserGroupAsync("editors", StartNodeTree.Library).Returns(restricted);

        var ok = Assert.IsType<OkObjectResult>(await _sut.ElementTypeAudit(CancellationToken.None, null, "editors"));

        Assert.Equal("Accessible", Assert.IsType<DocTypeAuditForNodeResponseModel>(ok.Value).StartNodeAccess);
    }

    /// <summary>Review focus 4: an unknown user group on the element-type audit is never blocked.</summary>
    [Fact]
    public async Task ElementTypeAudit_UnknownGroup_IsAccessible()
    {
        _startNodes.GetForUserGroupAsync("ghosts", StartNodeTree.Library).Returns((StartNodeSnapshot?)null);

        var ok = Assert.IsType<OkObjectResult>(await _sut.ElementTypeAudit(CancellationToken.None, null, "ghosts"));

        Assert.Equal("Accessible", Assert.IsType<DocTypeAuditForNodeResponseModel>(ok.Value).StartNodeAccess);
    }
```

In `DocTypePermissionsControllerElementTypeAuditTests.cs` and `PermissionStampEndpointTests.cs`, pass `Substitute.For<IStartNodeAccessService>()` as the new last constructor argument (add `using Umbraco.Community.AdvancedPermissions.Services.StartNodes;`).

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Umbraco.Community.AdvancedPermissions.Tests --filter "FullyQualifiedName~DocType"`
Expected: build FAILS: no 6-argument constructor.

- [ ] **Step 3: Extend the model**

Apply cf91d85's change to `DocTypeAuditForNodeResponseModel` (add `string StartNodeAccess` and its doc), and extend the doc: "For the element-type audit (Library Insert Viewer) the verdict is per subject: `NotVisible` when the subject has no Library access at all, otherwise `Accessible`."

- [ ] **Step 4: Wire the controller**

In `DocTypePermissionsController.cs`:
- Add `using Umbraco.Cms.Core;` and `using Umbraco.Community.AdvancedPermissions.Services.StartNodes;`, the constructor parameter and its `<param>` (as in cf91d85).
- Replace the `AuditForNode` return with cf91d85's block, using `GetForUserAsync(userKey!.Value, StartNodeTree.Content)` / `GetForUserGroupAsync(roleAlias!, StartNodeTree.Content)`.
- Replace the `ElementTypeAudit` return with:

```csharp
        // Element-type creation is section-global, so the verdict is per subject: blocked only when the
        // subject cannot reach the Library at all. A restricted subject still creates inside its folders.
        var snapshot = hasUser
            ? await startNodeAccessService.GetForUserAsync(userKey!.Value, StartNodeTree.Library)
            : await startNodeAccessService.GetForUserGroupAsync(roleAlias!, StartNodeTree.Library);
        var access = snapshot?.State == StartNodeSubjectState.NoAccess
            ? StartNodeAccess.NotVisible
            : StartNodeAccess.Accessible;

        return Ok(new DocTypeAuditForNodeResponseModel(AdvancedPermissionsConstants.VirtualRootNodeKey, rows, access.ToString()));
```

- [ ] **Step 5: Run all backend tests**

Run: `dotnet test`
Expected: PASS, every project.

- [ ] **Step 6: Leave uncommitted**

---

### Task 7: Live refresh after a user group's start node changes

**Files:**
- Modify: `src/Umbraco.Community.AdvancedPermissions/ServerEvents/AccessServerEventHandler.cs`
- Modify: `src/Umbraco.Community.AdvancedPermissions/Composing/AdvancedPermissionsComposer.cs:209`
- Test: `tests/Umbraco.Community.AdvancedPermissions.Tests/AccessServerEventHandlerTests.cs`

**Interfaces:**
- Produces: `AccessServerEventHandler` also implements `INotificationAsyncHandler<UserGroupWithUsersSavedNotification>`, routing one `Access` event per saved group key.

- [ ] **Step 1: Write the failing test**

After `UserGroupSaved_RoutesAccessEvent` add:

```csharp
    /// <summary>
    /// A user group saved together with its users is also routed. Only this notification clears the
    /// cached content and Library start nodes of the group's users, so routing it guarantees a viewer
    /// refetches after the cache is cleared (issue #66).
    /// </summary>
    [Fact]
    public async Task UserGroupWithUsersSaved_RoutesAccessEvent()
    {
        var groupKey = Guid.NewGuid();

        await CreateHandler().HandleAsync(
            new UserGroupWithUsersSavedNotification(new UserGroupWithUsers(BuildGroup(groupKey), [], []), new EventMessages()),
            CancellationToken.None);

        await AssertAccessRoutedAsync(groupKey);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Umbraco.Community.AdvancedPermissions.Tests --filter "FullyQualifiedName~AccessServerEventHandlerTests"`
Expected: build FAILS: no `HandleAsync(UserGroupWithUsersSavedNotification, …)`.

- [ ] **Step 3: Handle the notification**

Apply cf91d85's change to `AccessServerEventHandler.cs` (the interface line and the `HandleAsync` with its "Defensive" remarks; in v18 the remarks say "cached content and Library start nodes"). Update any count of handled notifications in the class summary.

- [ ] **Step 4: Run, then watch the registration test fail**

Run: `dotnet test tests/Umbraco.Community.AdvancedPermissions.Tests --filter "FullyQualifiedName~AccessServerEventHandlerTests|FullyQualifiedName~ServerEventRegistrationTests"`
Expected: the handler test PASSES; `ServerEventRegistrationTests` FAILS (handler implements a notification the composer does not register). If it does not fail, note it in the report; the registration in Step 5 is still required.

- [ ] **Step 5: Register it**

After `builder.AddNotificationAsyncHandler<UserGroupSavedNotification, AccessServerEventHandler>();` add:

```csharp
        builder.AddNotificationAsyncHandler<UserGroupWithUsersSavedNotification, AccessServerEventHandler>();
```

- [ ] **Step 6: Run all backend tests**

Run: `dotnet build && dotnet test`
Expected: PASS.

- [ ] **Step 7: Leave uncommitted**

---

### Task 8: Browser models, API wrappers and the generated client

**Files:**
- Regenerate: `src/Umbraco.Community.AdvancedPermissions.Client/src/api/generated/*`
- Modify: `src/Umbraco.Community.AdvancedPermissions.Client/src/models/permission.models.ts:81`
- Modify: `src/Umbraco.Community.AdvancedPermissions.Client/src/models/doc-type-permission.models.ts:1,49-52`
- Modify: `src/Umbraco.Community.AdvancedPermissions.Client/src/api/advanced-permissions.api.ts`
- Modify: `src/Umbraco.Community.AdvancedPermissions.Client/src/api/element-permissions.api.ts`

**Interfaces:**
- Produces (TS): everything cf91d85 added to `permission.models.ts` (`StartNodeAccess`, `StartNodeSubjectState`, `StartNodeSourceStatus`, `StartNodeItem`, `StartNodeSourceItem`, `StartNodesResponse`, `EffectivePermissions.startNodeAccess`), plus `export type StartNodeTree = 'content' | 'library';`; `DocTypeAuditForNodeResponse.startNodeAccess: StartNodeAccess`; `getStartNodes(subject, signal?)` in `advanced-permissions.api.ts`; `getElementStartNodes(subject: { userKey: string } | { roleAlias: string }, signal?: AbortSignal): Promise<StartNodesResponse>` in `element-permissions.api.ts`.

- [ ] **Step 1: Regenerate the OpenAPI client**

Start the v18 test site on the HTTP profile as a background task: `dotnet run --project tests/Umbraco.Community.AdvancedPermissions.TestSite --launch-profile http` (serves `http://localhost:5266`). When it answers, in `src/Umbraco.Community.AdvancedPermissions.Client` run `npm run generate-client`, then stop the site.
Expected: `sdk.gen.ts` exports `getStartNodesForUser`, `getStartNodesForRole`, `getElementStartNodesForUser`, `getElementStartNodesForRole`; `types.gen.ts` has `StartNodesResponseModel` and `startNodeAccess` on `EffectivePermissionsResponseModel` and `DocTypeAuditForNodeResponseModel`. Check `git diff --stat src/api/generated` shows only these additions (no unrelated churn); if it shows unrelated churn, report it.

- [ ] **Step 2: Add the models**

In `permission.models.ts`, replace the `EffectivePermissions` interface with cf91d85's version and add all of cf91d85's start-node types after it (copy from cf91d85's diff of that file), then add:

```ts
/** Which start nodes apply: the content tree's or the Library's. */
export type StartNodeTree = 'content' | 'library';
```

In `doc-type-permission.models.ts`, add `StartNodeAccess` to the import from `./permission.models.js` and add to `DocTypeAuditForNodeResponse`:

```ts
  /**
   * Whether the subject can reach the parent node through its start nodes. For the Library element-type
   * audit it is per subject: `NotVisible` when the subject has no Library access at all.
   */
  startNodeAccess: StartNodeAccess;
```

- [ ] **Step 3: Add the API wrappers**

In `advanced-permissions.api.ts`, add `StartNodesResponse` to the model imports and add after `getEffectiveForRole`:

```ts
/**
 * Gets the content start nodes of a user (combined over all their groups) or of one user group on its own.
 * @param subject The user key or the user group alias.
 * @param signal Aborts the request when the selection changes.
 * @returns The effective start nodes and one explained line per configured start node.
 */
export async function getStartNodes(
  subject: { userKey: string } | { roleAlias: string },
  signal?: AbortSignal,
): Promise<StartNodesResponse> {
  const { data } = 'userKey' in subject
    ? await Sdk.getStartNodesForUser({ throwOnError: true, query: { userKey: subject.userKey }, ...(signal ? { signal } : {}) })
    : await Sdk.getStartNodesForRole({ throwOnError: true, query: { roleAlias: subject.roleAlias }, ...(signal ? { signal } : {}) });
  return data as StartNodesResponse;
}
```

In `element-permissions.api.ts`, add `StartNodesResponse` to its model imports and add after `getElementEffectiveForRole`:

```ts
/**
 * Gets the Library start nodes of a user (combined over all their groups) or of one user group on its own.
 * @param subject The user key or the user group alias.
 * @param signal Aborts the request when the selection changes.
 * @returns The effective Library start nodes and one explained line per configured start node.
 */
export async function getElementStartNodes(
  subject: { userKey: string } | { roleAlias: string },
  signal?: AbortSignal,
): Promise<StartNodesResponse> {
  const { data } = 'userKey' in subject
    ? await Sdk.getElementStartNodesForUser({ throwOnError: true, query: { userKey: subject.userKey }, ...(signal ? { signal } : {}) })
    : await Sdk.getElementStartNodesForRole({ throwOnError: true, query: { roleAlias: subject.roleAlias }, ...(signal ? { signal } : {}) });
  return data as StartNodesResponse;
}
```

- [ ] **Step 4: Type-check**

Run (Client folder): `npx tsc --noEmit`
Expected: no errors.

- [ ] **Step 5: Leave uncommitted**

---

### Task 9: Pure display rules and loader (Vitest)

**Files:**
- Create: `src/Umbraco.Community.AdvancedPermissions.Client/src/start-nodes/start-node-display.ts`
- Create: `src/Umbraco.Community.AdvancedPermissions.Client/src/start-nodes/start-nodes-loader.ts`
- Test: `src/Umbraco.Community.AdvancedPermissions.Client/src/start-nodes/start-node-display.test.ts`
- Test: `src/Umbraco.Community.AdvancedPermissions.Client/src/start-nodes/start-nodes-loader.test.ts`

**Interfaces:**
- Consumes: Task 8 models.
- Produces: everything cf91d85's `start-node-display.ts` exports, with these signatures changed or added: `stripIntroTerm(state, subjectKind, tree: StartNodeTree = 'content'): string`; `blockedBannerTerm(access: StartNodeAccess, subjectWide = false): string`; `blockedHeadingTerm(subjectWide = false): string`; `blockedNoteTerm(startNodes: StartNodesResponse | null, tree: StartNodeTree = 'content'): TermRef`; `createNoteTerm(state: StartNodeSubjectState | null | undefined): string | null`; `type CellMode = 'na' | 'loading' | 'blocked' | 'value'`; `cellMode(applicable: boolean, loaded: boolean, access: StartNodeAccess | null | undefined): CellMode`. `StartNodesLoader` exactly as cf91d85.

- [ ] **Step 1: Write the failing tests**

Port `start-node-display.test.ts` and `start-nodes-loader.test.ts` from cf91d85 unchanged, then append to `start-node-display.test.ts` (and add `blockedHeadingTerm`, `cellMode`, `createNoteTerm` to its import):

```ts
describe('Library terms', () => {
  it('uses the Library intro for the Library tree', () => {
    expect(stripIntroTerm('Restricted', 'user', 'library')).toBe('uap_startNodes_library_introRestrictedUser');
    expect(stripIntroTerm('RootAccess', 'role', 'library')).toBe('uap_startNodes_library_introRootRole');
    expect(stripIntroTerm('NoAccess', 'user', 'library')).toBe('uap_startNodes_library_introNoAccessUser');
  });
  it('shares the All Users intro between trees', () => {
    expect(stripIntroTerm('NotApplicable', 'role', 'library')).toBe('uap_startNodes_introNotApplicable');
  });
  it('uses the Library note in the blocked dialog', () => {
    expect(blockedNoteTerm(restricted, 'library')).toEqual({ key: 'uap_startNodes_library_blockedNote', args: ['Home'] });
    expect(blockedNoteTerm({ state: 'NoAccess', startNodes: [], sources: [] }, 'library'))
      .toEqual({ key: 'uap_startNodes_library_blockedNoAccessNote', args: [] });
    expect(blockedNoteTerm(null, 'library')).toEqual({ key: 'uap_startNodes_blockedNoListNote', args: [] });
  });
  it('explains a subject-wide block as having no Library access', () => {
    expect(blockedBannerTerm('NotVisible', true)).toBe('uap_startNodes_library_blockedNoAccess');
    expect(blockedHeadingTerm(true)).toBe('uap_startNodes_library_ifAccessHeading');
    expect(blockedHeadingTerm()).toBe('uap_startNodes_ifInsideHeading');
  });
  it('adds the create-inside note only for a restricted subject', () => {
    expect(createNoteTerm('Restricted')).toBe('uap_startNodes_library_createInsideNote');
    expect(createNoteTerm('RootAccess')).toBeNull();
    expect(createNoteTerm('NoAccess')).toBeNull();
    expect(createNoteTerm(null)).toBeNull();
  });
});

describe('cellMode', () => {
  it('review focus 1: a verb that does not apply stays N/A even on a blocked row', () => {
    expect(cellMode(false, true, 'NotVisible')).toBe('na');
    expect(cellMode(false, false, null)).toBe('na');
  });
  it('shows loading until the row has loaded', () => {
    expect(cellMode(true, false, null)).toBe('loading');
  });
  it('blocks loaded rows outside the start nodes', () => {
    expect(cellMode(true, true, 'PathOnly')).toBe('blocked');
    expect(cellMode(true, true, 'NotVisible')).toBe('blocked');
  });
  it('shows the value for reachable rows', () => {
    expect(cellMode(true, true, 'Accessible')).toBe('value');
  });
});
```

- [ ] **Step 2: Run the tests to verify they fail**

Run (Client folder): `npm test -- src/start-nodes`
Expected: FAIL: cannot resolve `./start-node-display.js` / `./start-nodes-loader.js`.

- [ ] **Step 3: Implement**

`start-nodes-loader.ts`: port from cf91d85 unchanged.

`start-node-display.ts`: port from cf91d85, add `StartNodeTree` to the model import, and replace `stripIntroTerm`, `blockedBannerTerm` and `blockedNoteTerm` with the versions below; add the new helpers after `blockedNoteTerm`.

```ts
/**
 * The sentence that opens the start-node card: whether start nodes limit the subject at all.
 * @param state The subject's start-node state.
 * @param subjectKind Whether the subject is a user or a single user group.
 * @param tree Which start nodes the card is about.
 * @returns The localization key.
 */
export function stripIntroTerm(
  state: StartNodeSubjectState,
  subjectKind: 'user' | 'role',
  tree: StartNodeTree = 'content',
): string {
  if (state === 'NotApplicable') return 'uap_startNodes_introNotApplicable';
  const prefix = tree === 'library' ? 'uap_startNodes_library_' : 'uap_startNodes_';
  const suffix = subjectKind === 'role' ? 'Role' : 'User';
  switch (state) {
    case 'Restricted':
      return `${prefix}introRestricted${suffix}`;
    case 'RootAccess':
      return `${prefix}introRoot${suffix}`;
    default:
      return `${prefix}introNoAccess${suffix}`;
  }
}

/**
 * The banner of the reasoning dialog for a blocked cell.
 * @param access A blocked start-node access.
 * @param subjectWide True when the block is for the whole subject (Library Insert Viewer: no Library access).
 * @returns The localization key.
 */
export function blockedBannerTerm(access: StartNodeAccess, subjectWide = false): string {
  if (subjectWide) return 'uap_startNodes_library_blockedNoAccess';
  return access === 'PathOnly' ? 'uap_startNodes_blockedPathOnly' : 'uap_startNodes_blockedNotVisible';
}

/**
 * The heading above the greyed preview of the permissions in the blocked dialog.
 * @param subjectWide True when the block is for the whole subject.
 * @returns The localization key.
 */
export function blockedHeadingTerm(subjectWide = false): string {
  return subjectWide ? 'uap_startNodes_library_ifAccessHeading' : 'uap_startNodes_ifInsideHeading';
}

/**
 * The info note of the reasoning dialog for a blocked cell: the subject's start nodes. The "no access
 * at all" note is only for the NoAccess state; an empty list for RootAccess or NotApplicable (for
 * example a node deleted after the tree loaded) gets the plain note.
 * @param startNodes The subject's start nodes, or null when they could not be loaded.
 * @param tree Which start nodes they are.
 * @returns The localization key and its arguments.
 */
export function blockedNoteTerm(startNodes: StartNodesResponse | null, tree: StartNodeTree = 'content'): TermRef {
  const prefix = tree === 'library' ? 'uap_startNodes_library_' : 'uap_startNodes_';
  if (!startNodes) return { key: 'uap_startNodes_blockedNoListNote', args: [] };
  if (startNodes.state === 'NoAccess') return { key: `${prefix}blockedNoAccessNote`, args: [] };
  if (startNodes.startNodes.length === 0) return { key: 'uap_startNodes_blockedNoListNote', args: [] };
  return { key: `${prefix}blockedNote`, args: [startNodes.startNodes.map((n) => n.name).join(', ')] };
}

/**
 * The extra card line for the Library Insert Viewer: a restricted subject can only create inside its
 * Library start nodes, not at the Library root.
 * @param state The subject's Library start-node state; null while loading.
 * @returns The localization key, or null when no note applies.
 */
export function createNoteTerm(state: StartNodeSubjectState | null | undefined): string | null {
  return state === 'Restricted' ? 'uap_startNodes_library_createInsideNote' : null;
}

/** What a viewer cell shows. */
export type CellMode = 'na' | 'loading' | 'blocked' | 'value';

/**
 * Decides what a viewer cell shows. A verb that cannot apply to the node kind stays N/A even on a
 * blocked row, because the verb would never be available there, start nodes or not.
 * @param applicable Whether the verb applies to the node kind.
 * @param loaded Whether the row's effective permissions have arrived.
 * @param access The row's start-node access.
 * @returns The cell mode.
 */
export function cellMode(applicable: boolean, loaded: boolean, access: StartNodeAccess | null | undefined): CellMode {
  if (!applicable) return 'na';
  if (!loaded) return 'loading';
  if (isBlocked(access)) return 'blocked';
  return 'value';
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run (Client folder): `npm test` then `npx tsc --noEmit`
Expected: all Vitest suites PASS; no type errors.

- [ ] **Step 5: Leave uncommitted**

---

### Task 10: Localization terms (28 files)

**Files:**
- Modify: all 28 files in `src/Umbraco.Community.AdvancedPermissions.Client/src/localization/` (after the `notAnInsertOptionDeniedNote` line in each file)

**Interfaces:**
- Produces: the 36 `uap_startNodes_*` keys used by Tasks 9, 11, 12 and 13.

- [ ] **Step 1: English (`en.ts`, and identical in `en-us.ts`)**

After the `notAnInsertOptionDeniedNote` line, insert cf91d85's English block from `en.ts` (the 25 `startNodes_*` keys under `// ── Start nodes (issue #66)`), followed by:

```ts
    startNodes_library_introRestrictedUser: 'Library start nodes are in effect: this user can only reach Library folders and items at or under the start nodes below. Everything else is blocked in the grid.',
    startNodes_library_introRestrictedRole: 'Library start nodes are in effect: this user group only gives access to the Library at or under the start node below. Everything else is blocked in the grid.',
    startNodes_library_introRootUser: "Library start nodes don't limit this user: they can reach the whole Library.",
    startNodes_library_introRootRole: "Library start nodes don't limit this user group: it gives access to the whole Library.",
    startNodes_library_introNoAccessUser: "This user has no Library start nodes, so they can't reach the Library at all. Everything is blocked.",
    startNodes_library_introNoAccessRole: 'This user group has no Library start node, so on its own it gives no Library access. Everything is blocked.',
    startNodes_library_createInsideNote: 'They can only create inside their Library start nodes, not at the Library root.',
    startNodes_library_blockedNote: (nodes: string) => `Library start nodes: ${nodes}.`,
    startNodes_library_blockedNoAccessNote: 'There are no Library start nodes, so there is no Library access at all.',
    startNodes_library_blockedNoAccess: (subject: string, verb: string, typeName: string) =>
      `${subject} can't use ${verb} for \u201c${typeName}\u201d: they have no access to the Library.`,
    startNodes_library_ifAccessHeading: 'If they had access to the Library:',
```

- [ ] **Step 2: Dutch (`nl.ts`)**

Same position: cf91d85's Dutch block from `nl.ts`, followed by:

```ts
    startNodes_library_introRestrictedUser: 'Library-startnodes zijn van kracht: deze gebruiker kan alleen Library-mappen en -items op of onder de startnodes hieronder bereiken. Al het andere is geblokkeerd in het overzicht.',
    startNodes_library_introRestrictedRole: 'Library-startnodes zijn van kracht: deze gebruikersgroep geeft alleen toegang tot de Library op of onder de startnode hieronder. Al het andere is geblokkeerd in het overzicht.',
    startNodes_library_introRootUser: 'Library-startnodes beperken deze gebruiker niet: de gebruiker kan de hele Library bereiken.',
    startNodes_library_introRootRole: 'Library-startnodes beperken deze gebruikersgroep niet: de groep geeft toegang tot de hele Library.',
    startNodes_library_introNoAccessUser: 'Deze gebruiker heeft geen Library-startnodes en kan de Library dus helemaal niet bereiken. Alles is geblokkeerd.',
    startNodes_library_introNoAccessRole: 'Deze gebruikersgroep heeft geen Library-startnode en geeft op zichzelf dus geen toegang tot de Library. Alles is geblokkeerd.',
    startNodes_library_createInsideNote: 'Ze kunnen alleen aanmaken binnen hun Library-startnodes, niet in de root van de Library.',
    startNodes_library_blockedNote: (nodes: string) => `Library-startnodes: ${nodes}.`,
    startNodes_library_blockedNoAccessNote: 'Er zijn geen Library-startnodes, dus er is helemaal geen toegang tot de Library.',
    startNodes_library_blockedNoAccess: (subject: string, verb: string, typeName: string) =>
      `${subject} kan ${verb} niet gebruiken voor \u201c${typeName}\u201d: er is geen toegang tot de Library.`,
    startNodes_library_ifAccessHeading: 'Als er wel toegang tot de Library zou zijn:',
```

Before using these Dutch words, check `nl.ts` for the existing word for "Library" and "item" (e.g. `library_accessViewerHeadline`) and align.

- [ ] **Step 3: The other 25 languages**

For each of `ar, bs, cs, cy, da, de, es, fr, he, hr, it, ja, ko, nb, pl, pt-br, pt, ro, ru, sv, tr, uk, vi, zh-tw, zh`: insert the same 36 keys at the same position, translated into that language. Use the file's existing words for "user group", "Library", "blocked", "allow/deny" (read its `library_*`, `chooseRole`, `allow`, `deny` terms first). Keep the argument lists and placeholder order of every function term unchanged. Keep `\u201c`/`\u201d` quotes, or the language's own quotation marks if the file already uses them.

Then check consistency (Client folder, Git Bash):

Run: `grep -c "startNodes_" src/localization/*.ts`
Expected: `36` for every one of the 28 files.

- [ ] **Step 4: Type-check and leave uncommitted**

Run: `npx tsc --noEmit`
Expected: no errors.

---

### Task 11: Shared UI: blocked cell, badge, card and blocked dialog

**Files:**
- Modify: `src/Umbraco.Community.AdvancedPermissions.Client/src/shared/components/uap-perm-block.element.ts`
- Modify: `src/Umbraco.Community.AdvancedPermissions.Client/src/shared/components/uap-reasoning-dialog.element.ts`
- Create: `src/Umbraco.Community.AdvancedPermissions.Client/src/start-nodes/uap-start-node-badge.element.ts`
- Create: `src/Umbraco.Community.AdvancedPermissions.Client/src/start-nodes/uap-start-node-strip.element.ts`

**Interfaces:**
- Consumes: Task 9 helpers, Task 10 terms.
- Produces: `<uap-perm-block blocked blocked-title=…>`; `<uap-start-node-badge .badge=…>`; `<uap-start-node-strip .data ?loading ?error subject-kind tree .note>` (`tree: StartNodeTree = 'content'`, `note: string | null = null` — a localization key for one extra line); `<uap-reasoning-dialog>` gains `startNodeAccess: StartNodeAccess = 'Accessible'`, `startNodes: StartNodesResponse | null = null`, `startNodeTree: StartNodeTree = 'content'`, `blockedSubjectWide = false`.

- [ ] **Step 1: Blocked cell**

Apply cf91d85's diff of `uap-perm-block.element.ts` to the v18 file (the anchors — the "Display priority" doc line, `outsideAllowed = false`, `if (this.loading)`, and the "Split state" style comment — are unchanged in v18). Extend the blocked style comment: "…distinct from N/A (dotted, no icon), which the Library viewers also use."

- [ ] **Step 2: Badge**

Create `uap-start-node-badge.element.ts` as cf91d85's file, unchanged.

- [ ] **Step 3: Card**

Create `uap-start-node-strip.element.ts` as cf91d85's file, with these changes:
- Import `StartNodeTree` from the models.
- Class summary: "The start-node card shown under the subject picker in all four viewers…"; mention that `tree` picks content or Library wording.
- Add properties:

```ts
  /** Which start nodes the card is about: the content tree's or the Library's. */
  @property() tree: StartNodeTree = 'content';

  /** One extra line under the start nodes, as a localization key (e.g. the Library create note); null for none. */
  @property({ attribute: false }) note: string | null = null;
```

- In `render()`, call `stripIntroTerm(data.state, this.subjectKind, this.tree)`, and after the lines list add `${this.note ? html`<p class="note">${this.#localize.term(this.note)}</p>` : nothing}` (before the role-mode note).

- [ ] **Step 4: Blocked dialog**

Apply cf91d85's diff of `uap-reasoning-dialog.element.ts` to the v18 file (anchors unchanged in v18), with these changes:
- Import also `blockedHeadingTerm` from the display module and `StartNodeTree` from the models.
- Add after the `startNodes` property:

```ts
  /** Which start nodes the blocked note lists: content or Library. */
  @property() startNodeTree: StartNodeTree = 'content';

  /**
   * True when the block is for the whole subject rather than one node (Library Insert Viewer: the
   * subject has no Library access at all). Changes the banner and the preview heading.
   */
  @property({ type: Boolean }) blockedSubjectWide = false;
```

- The heading line uses `this.#localize.term(blockedHeadingTerm(this.blockedSubjectWide))`.
- The banner uses `blockedBannerTerm(this.startNodeAccess, this.blockedSubjectWide)`. For a subject-wide block the third argument is `this.nodeName` (the element type), not the path: `this.blockedSubjectWide ? this.nodeName : (contentPath || this.nodeName)`.
- The note uses `blockedNoteTerm(this.startNodes, this.startNodeTree)`.

- [ ] **Step 5: Type-check, test and build**

Run (Client folder): `npx tsc --noEmit && npm test`
Expected: PASS.

- [ ] **Step 6: Leave uncommitted**

---

### Task 12: Wire the Access Viewer and the Insert Options Viewer

**Files:**
- Modify: `src/Umbraco.Community.AdvancedPermissions.Client/src/access-viewer/uap-access-viewer-root.element.ts`
- Modify: `src/Umbraco.Community.AdvancedPermissions.Client/src/doc-type-permissions/uap-doc-type-create-audit-root.element.ts`

**Interfaces:**
- Consumes: Tasks 8, 9, 11.

- [ ] **Step 1: Access Viewer**

Apply cf91d85's diff of `uap-access-viewer-root.element.ts` to the v18 file. The v18 anchors (all present): `interface ViewerTreeNode` (line 34), `clearSelection(SURFACE_ID)` in `#onClearSelection` (line 177), `this.#loadAbortController = controller;` in `#loadTree` (line 222) and `#reloadEffective` (line 272), `effectivePerms: null,` in `#loadTree` (line 240) and `#toggleExpand` (line 407), `#clearEffectiveRecursive` (line 313), `node.effectivePerms = permsMap;` (line 374), `<span class="node-name">` (line 666), `#renderEffectiveCell` (line 681), `<uap-live-refresh` (line 720), `.defaultState=${'deny'}` (line 756), `#currentEffectivePerm()` (line 764), `.node-name {` style (line 876). Differences from v17:
- v18's `#subject` getter returns a string; keep it. `#loadStartNodes` builds the subject object itself exactly as cf91d85 does (from `_activeSubject`, `_selectedRole`, `_selectedUser`).
- In v18, `.node-name` has its own style block; add `.node-name.muted { color: var(--uui-color-text-alt, #888); }` after it.

- [ ] **Step 2: Insert Options Viewer**

Apply cf91d85's diff of `uap-doc-type-create-audit-root.element.ts` to the v18 file. The anchors are present (`interface AuditTreeNode` line 58, `get #subject()` returning the subject object line 247, `auditResults: null,` lines 284 and 448, `#clearAuditRecursive` line 351, `<span class="node-name">` line 680, `#renderCell` line 696, `#currentEffective()` line 753, `<uap-live-refresh` line 830, `.defaultState=${'allow'}` line 870, `.node-name` style line 930). v18 calls `clearSelection(SURFACE_ID)` in two places (lines 215 and 228): add `this.#startNodes.clear();` before the one in the clear-selection handler, and check what the other one does; if it is also a reset of the subject, clear the loader there too.

- [ ] **Step 3: Type-check, test and build**

Run (Client folder): `npx tsc --noEmit && npm test && npm run build`
Expected: PASS.

- [ ] **Step 4: Leave uncommitted**

---

### Task 13: Wire the Library Access Viewer and the Library Insert Viewer

**Files:**
- Modify: `src/Umbraco.Community.AdvancedPermissions.Client/src/library-permissions/uap-library-access-viewer-root.element.ts`
- Modify: `src/Umbraco.Community.AdvancedPermissions.Client/src/library-permissions/uap-library-insert-viewer-root.element.ts`

**Interfaces:**
- Consumes: Tasks 8, 9, 11.

- [ ] **Step 1: Library Access Viewer: state and loading**

- Imports: add `StartNodeAccess` to the model imports; `getElementStartNodes` from `../api/element-permissions.api.js`; `blockedPlaceholderPermission, cellMode, rowBadge` from `../start-nodes/start-node-display.js`; `StartNodesLoader` from `../start-nodes/start-nodes-loader.js`; side-effect imports `../start-nodes/uap-start-node-badge.element.js` and `../start-nodes/uap-start-node-strip.element.js`.
- `ViewerTreeNode`: add `/** Whether the subject can reach the node through its Library start nodes (null = not yet loaded). */ startNodeAccess: StartNodeAccess | null;` and add `startNodeAccess: null,` to both node literals (`#loadTree`, `#toggleExpand`).
- Field after `#loadAbortController`:

```ts
  /** The subject's Library start nodes and their load state, for the card and the blocked dialog. */
  #startNodes = new StartNodesLoader(this, (subject, signal) => getElementStartNodes(subject, signal));
```

- `#onClearSelection`: before `clearSelection(SURFACE_ID);` add `this.#startNodes.clear();` with the comment "A stale card must not outlive the subject it described; this also discards a load in flight."
- `#loadTree` and `#reloadEffective`: right after `this.#loadAbortController = controller;` add `this.#loadStartNodes(controller.signal);` (in `#reloadEffective`: `this.#loadStartNodes(controller.signal, background);` with cf91d85's comment).
- Add `#loadStartNodes` exactly as in cf91d85's Access Viewer diff.
- `#clearEffectiveRecursive`: also `node.startNodeAccess = null;`.
- `#loadEffective`: after `node.effectivePerms = permsMap;` add `node.startNodeAccess = result.startNodeAccess;`.

- [ ] **Step 2: Library Access Viewer: rendering**

- `#renderRow`: replace `<span class="node-name">${node.name}</span>` with cf91d85's muted name plus `<uap-start-node-badge .badge=${rowBadge(node.key, node.startNodeAccess, this.#startNodes.data)}></uap-start-node-badge>`; add the `.node-name.muted` style.
- Replace the start of `#renderEffectiveCell` with a `cellMode` switch so N/A keeps priority (review focus 1):

```ts
  #renderEffectiveCell(node: ViewerTreeNode, verb: string): TemplateResult {
    const mode = cellMode(libraryApplicability(verb, node.isFolder).nodeApplicable, node.effectivePerms !== null, node.startNodeAccess);
    // Verbs that can't apply to this node kind render as a hatched N/A cell, matching the editor,
    // even on a row outside the start nodes: the verb would never be available there.
    if (mode === 'na') {
      return html`<td class="perm-td na-td" title=${this.#localize.term('uap_library_notApplicableTitle', verb.split('.').pop() ?? '')}>
        <uap-perm-block na></uap-perm-block>
      </td>`;
    }
    if (mode === 'loading') {
      return html`<td class="perm-td" title=${verb}><uap-perm-block loading></uap-perm-block></td>`;
    }
    if (mode === 'blocked') {
      return html`
        <td class="perm-td" @click=${() => this.#openReasoning(node, verb)}>
          <uap-perm-block blocked blocked-title=${this.#localize.term('uap_startNodes_blockedCellTitle')}></uap-perm-block>
        </td>
      `;
    }

    const perm = node.effectivePerms!.get(verb);
```

and keep the rest of the method as it is.
- `render()`: after the error line add the card:

```ts
          ${this._activeSubject
            ? html`<uap-start-node-strip
                .data=${this.#startNodes.data}
                ?loading=${this.#startNodes.loading}
                ?error=${this.#startNodes.error}
                subject-kind=${this._activeSubject}
                tree="library"></uap-start-node-strip>`
            : nothing}
```

- Dialog: add `.startNodeAccess=${this._reasoningNode?.startNodeAccess ?? 'Accessible'}`, `.startNodes=${this.#startNodes.data}` and `startNodeTree="library"`.
- `#currentEffectivePerm()`: same fallback as cf91d85's Access Viewer (return `blockedPlaceholderPermission(this._reasoningVerb)` for a blocked node without a permission); add `isBlocked` to the display import.

- [ ] **Step 3: Library Insert Viewer**

- Imports: `StartNodeAccess` (models); `getElementStartNodes` (`../api/element-permissions.api.js`); `blockedPlaceholderPermission, createNoteTerm, isBlocked` (display); `StartNodesLoader`; side-effect import of the strip element.
- State and loader:

```ts
  /** Whether the subject can reach the Library at all (per subject; null until the audit loads). */
  @state() private _startNodeAccess: StartNodeAccess | null = null;

  /** The subject's Library start nodes and their load state, for the card and the blocked dialog. */
  #startNodes = new StartNodesLoader(this, (subject, signal) => getElementStartNodes(subject, signal));
```

- `#onClearSelection`: `this._startNodeAccess = null;` and `this.#startNodes.clear();` before `clearSelection(SURFACE_ID);`.
- `#load`: right after `this.#abort = controller;` add `void this.#startNodes.load(subject, controller.signal, background);` with the comment "Reloaded in the same pass, so a live event or a subject change refreshes the card too. The card reports its own failure; it does not count towards whether the list refreshed." After `this._rows = result.results;` add `this._startNodeAccess = result.startNodeAccess;`.
- `#renderRow`: when `isBlocked(this._startNodeAccess)`, render the cell as:

```ts
        <div class="type-cell" title=${this.#localize.term('uap_startNodes_blockedCellTitle')} @click=${() => void this.#openReasoning(row)}>
          <uap-perm-block blocked blocked-title=${this.#localize.term('uap_startNodes_blockedCellTitle')}></uap-perm-block>
        </div>
```

- `#currentEffective()`: unchanged (a row always exists here).
- `render()`: after the error line add the card with `tree="library"` and `.note=${createNoteTerm(this.#startNodes.data?.state)}`.
- Dialog: add `.startNodeAccess=${this._startNodeAccess ?? 'Accessible'}`, `.startNodes=${this.#startNodes.data}`, `startNodeTree="library"`, `?blockedSubjectWide=${true}`. (`blockedSubjectWide` only matters when the access is blocked.) `blockedPlaceholderPermission` is not needed here; drop it from the import if unused.

- [ ] **Step 4: Type-check, test and build**

Run (Client folder): `npx tsc --noEmit && npm test && npm run build`
Expected: PASS.

- [ ] **Step 5: Leave uncommitted**

---

### Task 14: Help, project docs and end-to-end verification

**Files:**
- Modify: `src/Umbraco.Community.AdvancedPermissions.Client/help-docs/en/{access-viewer,insert-options-viewer,library-access-viewer,library-insert-viewer,concepts}.md`
- Modify: `src/Umbraco.Community.AdvancedPermissions.Client/help-docs/nl/` (same five files)
- Modify: `CLAUDE.md` (Management API table, line ~52)

- [ ] **Step 1: Help text (English)**

- `en/access-viewer.md`: insert cf91d85's "## Start nodes" section before the final "See the **Concepts** tab" line.
- `en/insert-options-viewer.md`: add cf91d85's "Creating under a node needs access…" bullet under "Good to know".
- `en/concepts.md`: add cf91d85's `<a id="start-nodes"></a>` "## Start nodes" section at the end, then append these bullets to it:

```markdown
- **The Library has its own start nodes.** A user group has one Library start node (a Library folder), access to the whole Library, or none. They combine exactly like content start nodes, and Umbraco checks them before any Library permission.
- **After an upgrade to Umbraco 18**, only the Administrators group has access to the whole Library. Every other existing user group has no Library start node, so its users can't reach the Library until you give the group one.
```

- `en/library-access-viewer.md`, before the final "See the **Concepts** tab" line:

```markdown
## Library start nodes

The viewer also takes the subject's **Library start nodes** into account, because Umbraco blocks every Library folder and item outside them before any permission is checked.

- The **start-node card** under the picker says whether Library start nodes limit the subject, and lists the Library start nodes in effect with who set each one.
- A folder or item the subject can't reach shows **blocked** cells (grey, hatched, with a lock) instead of allow or deny: **Browse only (start nodes)** for a folder above a start node, **Not visible (start nodes)** for everything outside them. Verbs that never apply to a folder or an item stay N/A.
- Click a blocked cell to see why, and what the permissions would be if it were inside a start node.
```

- `en/library-insert-viewer.md`, under "Good to know":

```markdown
- Creating an item needs access to the Library. A subject without any Library start node gets **blocked** rows; a subject with Library start nodes can only create inside them. The start-node card under the picker shows which Library start nodes apply.
```

- [ ] **Step 2: Help text (Dutch)**

The same five changes in `nl/`: cf91d85's Dutch sections for `access-viewer.md`, `insert-options-viewer.md` and `concepts.md`, plus:

`nl/concepts.md` (appended to the section):

```markdown
- **De Library heeft eigen startnodes.** Een gebruikersgroep heeft één Library-startnode (een Library-map), toegang tot de hele Library, of geen. Ze worden precies zo gecombineerd als contentstartnodes, en Umbraco controleert ze vóór elke Library-machtiging.
- **Na een upgrade naar Umbraco 18** heeft alleen de groep Beheerders toegang tot de hele Library. Elke andere bestaande gebruikersgroep heeft geen Library-startnode, dus de gebruikers ervan kunnen de Library pas bereiken als je de groep er een geeft.
```

`nl/library-access-viewer.md`:

```markdown
## Library-startnodes

De viewer houdt ook rekening met de **Library-startnodes** van het onderwerp, omdat Umbraco elke Library-map en elk item daarbuiten blokkeert voordat er naar machtigingen wordt gekeken.

- De **startnodekaart** onder de kiezer zegt of Library-startnodes het onderwerp beperken, en toont de geldende Library-startnodes met wie elke startnode heeft ingesteld.
- Een map of item dat het onderwerp niet kan bereiken, toont **geblokkeerde** cellen (grijs, gearceerd, met een slotje) in plaats van toestaan of weigeren: **Alleen navigeren (startnodes)** voor een map boven een startnode, **Niet zichtbaar (startnodes)** voor alles daarbuiten. Acties die nooit voor een map of item gelden, blijven N.v.t.
- Klik op een geblokkeerde cel om te zien waarom, en wat de machtigingen zouden zijn als het binnen een startnode viel.
```

`nl/library-insert-viewer.md`, under the "Goed om te weten" heading:

```markdown
- Een item aanmaken vereist toegang tot de Library. Een onderwerp zonder Library-startnode krijgt **geblokkeerde** rijen; een onderwerp met Library-startnodes kan alleen daarbinnen aanmaken. De startnodekaart onder de kiezer toont welke Library-startnodes gelden.
```

Match the words each `nl` file already uses for "item", "map", "N.v.t." and "Goed om te weten".

- [ ] **Step 3: Project docs**

In `CLAUDE.md`'s Management API table add:

```markdown
| GET | `/start-nodes?userKey=` | A user's effective content start nodes and where each comes from |
| GET | `/start-nodes/by-role?roleAlias=` | One user group's own content start node |
| GET | `/element/start-nodes?userKey=` | A user's effective Library start nodes and where each comes from |
| GET | `/element/start-nodes/by-role?roleAlias=` | One user group's own Library start node |
```

and note on the `/effective` rows that the response carries `startNodeAccess`.

- [ ] **Step 4: Full automated run**

Run (repo root): `dotnet build && dotnet test`
Run (Client folder): `npx tsc --noEmit && npm test && npm run build`
Expected: everything passes. Record the test counts for the report.

- [ ] **Step 5: Manual verification on the v18 test site**

Use the `running-umbraco-instances` skill. Run the v18 site as a background task (`dotnet run --launch-profile https` in `tests/Umbraco.Community.AdvancedPermissions.TestSite`, `https://localhost:7067/umbraco`). Close every v17 backoffice tab first (same origin → login loop). The user logs in themselves; never read or type passwords. Set up:
- user group **A**: content start node *Home*, Library start node folder *F1*;
- user group **B**: content start node a sibling of *Home*, no Library start node;
- user group **C**: no content start node, no Library start node;
- user **U1** in A and B; user **U2** in A with a user-level content start node below *Home*.

Check, with screenshots as proof:
1. Access Viewer, U1: the card lists *Home* (from A) and the sibling (from B); the website root shows "Browse only (start nodes)" with blocked cells; a third sibling "Not visible (start nodes)"; nodes under *Home* show normal allow/deny.
2. Click a blocked cell: grey banner with a lock, the start-node note, the greyed table under "If this node were inside a start node:".
3. U2: *Home* itself is browse only; the user-level node is the start node.
4. User group mode, C: "no start node … Every node is blocked"; All Users group: "Start nodes don't apply…", no blocked cells.
5. Insert Options Viewer, U1: the same rows are blocked; a blocked cell opens the blocked dialog.
6. Library Access Viewer, U1: the card says Library start nodes are in effect and lists *F1* (from A); folders outside *F1* are blocked; N/A cells on folders stay N/A.
7. Library Access Viewer, user group B: "no Library start node … Everything is blocked".
8. Library Insert Viewer, user group B: every row blocked; the dialog says they have no access to the Library. U1: rows normal, card shows "They can only create inside their Library start nodes".
9. **Live refresh (review focus 5):** keep the Access Viewer open on U1, change group B's content start node in a second tab and save; within a few seconds, without reloading, the card and rows update. Repeat with the Library Access Viewer and group A's Library start node. If a viewer still shows the old start nodes, stop and investigate before claiming done.
10. Light and dark theme: blocked cells, badges and the card stay readable.

- [ ] **Step 6: Report and leave uncommitted**

Report what changed per file, the test counts and the screenshots. Do not commit; ask the user whether to commit.
