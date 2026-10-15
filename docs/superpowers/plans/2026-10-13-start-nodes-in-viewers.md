# Start Nodes in the Viewers Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The Access Viewer and the Insert Options Viewer show a subject's start nodes, mark every node the subject cannot reach as "blocked", and explain why in a greyed reasoning dialog.

**Architecture:** A new server service (`IStartNodeAccessService`) takes Umbraco's own combined start nodes (`CalculateContentStartNodeIds`, the exact value enforcement uses) and classifies each node with Umbraco's `HasPathAccess`. Pure static rules (`StartNodeRules`) do classification and source attribution. The `/effective` and `audit-for-node` responses gain one additive field; two new endpoints feed a start-node strip. The browser adds a `blocked` cell state, a blocked dialog mode, a strip element and a row badge, and wires them into both viewers.

**Tech Stack:** C# / .NET 10, Umbraco 17 (`[17.3.0,18.0.0)`), xUnit + NSubstitute; TypeScript + Lit, Vite, Vitest, hey-api generated client.

**Spec:** `docs/superpowers/specs/2026-10-13-start-nodes-in-viewers-design.md`

## Global Constraints

- Branch `v17/feature/66_start_nodes_access_viewer` (from `origin/v17/main`). Umbraco 17 only; the evidence is the 17.7.0 source at `D:\github\UmbracoVersions\v17\Umbraco-CMS`. Never use v18 APIs (no `CalculateElementStartNodeIds`, no Library).
- **Never commit, push or open a PR.** Every "finish" step leaves changes uncommitted for the user's review.
- Test-first for all backend and pure client code: write the test, watch it fail, then implement.
- C#: primary constructors, `var` for locals, `record` for data types, XML doc comment on every type, method and property (any accessibility), with `<param>`/`<returns>`.
- C# namespace gotcha: inside `Umbraco.Community.AdvancedPermissions.*`, reach `Umbraco.Cms.*` only through `using` directives (or `global::`), never inline `Umbraco.Cms.Core...`.
- Do not change enforcement (`AdvancedContentPermissionService`), the resolver, stored data, or the public `Abstractions` contract.
- API changes are additive only. Enum values travel as strings (`.ToString()`), like every other enum in this API.
- TS: `exactOptionalPropertyTypes: true` (never assign `undefined` to `field?: T`); recursive Lit methods need explicit return types; `UmbExtensionManifest` is global (never import it).
- UI copy: "user group", never "role"; "All Users group", never `$everyone`; they/them for the subject, never he/she.
- Every new localization term goes into all 28 files in `src/Umbraco.Community.AdvancedPermissions.Client/src/localization/` (English text in all but `nl.ts`, which gets Dutch). Help text in `help-docs/en` and `help-docs/nl`.
- Do not read `node_modules`; use `D:\github\UmbracoVersions\v17\Umbraco-CMS\src\Umbraco.Web.UI.Client` for backoffice reference.

## Review Focus

1. **Sibling ids that share a prefix** (`-1,12` vs. start node `-1,123`): must be `NotVisible`, never `PathOnly`. Pinned in Task 1.
2. **`IEntityService.GetAll`/`GetAllPaths` with an empty id array** returns every document, not none: a subject with no start nodes must never trigger that query. Pinned in Task 2.
3. **A deleted or trashed user-level start node** silently widens the user to the group start nodes (possibly root): the snapshot must reflect Umbraco's result and mark the user line `Ignored`. Pinned in Task 2.
4. **The All Users group and unknown subjects** on the effective and audit endpoints must report `Accessible`, never blocked. Pinned in Tasks 4 and 5.
5. **Saving a user group's start node** must refresh an open viewer with the *new* start nodes (the event used to fire before Umbraco cleared its cache). Pinned in Task 6 and the manual check in Task 13.

---

### Task 1: Start-node models and pure rules

**Files:**
- Create: `src/Umbraco.Community.AdvancedPermissions/Services/StartNodes/StartNodeModels.cs`
- Create: `src/Umbraco.Community.AdvancedPermissions/Services/StartNodes/StartNodeRules.cs`
- Test: `tests/Umbraco.Community.AdvancedPermissions.Tests/StartNodeRulesTests.cs`

**Interfaces:**
- Produces: enums `StartNodeAccess { Accessible, PathOnly, NotVisible }`, `StartNodeSubjectState { NotApplicable, RootAccess, Restricted, NoAccess }`, `StartNodeSourceKind { UserGroup, User }`, `StartNodeSourceStatus { InUse, NoStartNode, NotSet, CoveredBy, ReplacedByUserStartNode, NarrowedBy, Ignored, NotApplied }`; records `StartNodeSource(StartNodeSourceKind Kind, string? UserGroupAlias, string? UserGroupName, int? StartNodeId)`, `StartNodeSourceResult(StartNodeSource Source, StartNodeSourceStatus Status, int? RelatedStartNodeId)`, `StartNodeInfo(int Id, Guid Key, string Name, string Path)`, `StartNodeSnapshot(StartNodeSubjectState State, IReadOnlyList<int> EffectiveStartNodeIds, IReadOnlyDictionary<int, StartNodeInfo> Nodes, IReadOnlyList<StartNodeSourceResult> Sources)` with `static StartNodeSnapshot NotApplicable`; static class `StartNodeRules` with `Classify(string nodePath, IReadOnlyCollection<int> effectiveStartNodeIds, IEnumerable<string> effectiveStartNodePaths)`, `Attribute(IReadOnlyList<StartNodeSource> sources, IReadOnlyCollection<int> effectiveStartNodeIds, IReadOnlyDictionary<int, string> pathsById)`, `IsStrictAncestor(string ancestorPath, string descendantPath)`, `IsInRecycleBin(string path)`. Namespace `Umbraco.Community.AdvancedPermissions.Services.StartNodes`.

- [ ] **Step 1: Write the failing tests**

Create `tests/Umbraco.Community.AdvancedPermissions.Tests/StartNodeRulesTests.cs`:

```csharp
using Umbraco.Community.AdvancedPermissions.Services.StartNodes;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests <see cref="StartNodeRules"/>: how a node is classified against a subject's effective start
/// nodes, and how each configured start node is explained. The cases mirror Umbraco 17.7.0's
/// <c>UserExtensions.CombineStartNodes</c> and <c>ContentPermissions.HasPathAccess</c>.
/// </summary>
public sealed class StartNodeRulesTests
{
    /// <summary>
    /// Paths of a small tree: 10 (Home) &gt; 20 (News); 30 (Shop); 40 in the recycle bin; 12 and 123
    /// are siblings whose ids share a prefix.
    /// </summary>
    private static readonly IReadOnlyDictionary<int, string> Paths = new Dictionary<int, string>
    {
        [-1] = "-1",
        [10] = "-1,10",
        [20] = "-1,10,20",
        [30] = "-1,30",
        [40] = "-1,-20,40",
        [12] = "-1,12",
        [123] = "-1,123",
    };

    /// <summary>With root access every node is reachable.</summary>
    [Fact]
    public void Classify_RootAccess_IsAccessible() =>
        Assert.Equal(StartNodeAccess.Accessible, StartNodeRules.Classify("-1,30", [-1], ["-1"]));

    /// <summary>A start node itself is reachable.</summary>
    [Fact]
    public void Classify_StartNodeItself_IsAccessible() =>
        Assert.Equal(StartNodeAccess.Accessible, StartNodeRules.Classify("-1,10", [10], ["-1,10"]));

    /// <summary>A descendant of a start node is reachable.</summary>
    [Fact]
    public void Classify_DescendantOfStartNode_IsAccessible() =>
        Assert.Equal(StartNodeAccess.Accessible, StartNodeRules.Classify("-1,10,20", [10], ["-1,10"]));

    /// <summary>An ancestor of a start node is shown for navigation only.</summary>
    [Fact]
    public void Classify_AncestorOfStartNode_IsPathOnly() =>
        Assert.Equal(StartNodeAccess.PathOnly, StartNodeRules.Classify("-1,10", [20], ["-1,10,20"]));

    /// <summary>The content root is an ancestor of every start node, so it is path-only without root access.</summary>
    [Fact]
    public void Classify_ContentRootWithoutRootAccess_IsPathOnly() =>
        Assert.Equal(StartNodeAccess.PathOnly, StartNodeRules.Classify("-1", [20], ["-1,10,20"]));

    /// <summary>A node on another branch is hidden.</summary>
    [Fact]
    public void Classify_OtherBranch_IsNotVisible() =>
        Assert.Equal(StartNodeAccess.NotVisible, StartNodeRules.Classify("-1,30", [10], ["-1,10"]));

    /// <summary>
    /// Review focus 1: node 12 is not an ancestor of start node 123 even though "-1,123" starts with
    /// "-1,12". Path comparison must be comma-bounded.
    /// </summary>
    [Fact]
    public void Classify_SiblingWithPrefixId_IsNotVisible() =>
        Assert.Equal(StartNodeAccess.NotVisible, StartNodeRules.Classify("-1,12", [123], ["-1,123"]));

    /// <summary>Without any start node nothing is reachable.</summary>
    [Fact]
    public void Classify_NoStartNodes_IsNotVisible() =>
        Assert.Equal(StartNodeAccess.NotVisible, StartNodeRules.Classify("-1,10", [], []));

    /// <summary>Only root access reaches the recycle bin.</summary>
    [Fact]
    public void Classify_RecycleBinWithoutRootAccess_IsNotVisible() =>
        Assert.Equal(StartNodeAccess.NotVisible, StartNodeRules.Classify("-1,-20,40", [10], ["-1,10"]));

    /// <summary>A group start node that survives combination is in use.</summary>
    [Fact]
    public void Attribute_GroupInEffectiveSet_IsInUse()
    {
        var result = Attribute([Group(10)], [10]);
        Assert.Equal(StartNodeSourceStatus.InUse, result[0].Status);
    }

    /// <summary>A group without a start node gives no content access.</summary>
    [Fact]
    public void Attribute_GroupWithoutStartNode_IsNoStartNode() =>
        Assert.Equal(StartNodeSourceStatus.NoStartNode, Attribute([Group(null)], [10])[0].Status);

    /// <summary>A user without user-level start nodes inherits the groups'.</summary>
    [Fact]
    public void Attribute_UserWithoutStartNodes_IsNotSet() =>
        Assert.Equal(StartNodeSourceStatus.NotSet, Attribute([User(null)], [10])[0].Status);

    /// <summary>A lower group start node on the same branch is covered by the higher one.</summary>
    [Fact]
    public void Attribute_LowerGroupNode_IsCoveredByHigherGroupNode()
    {
        var result = Attribute([Group(10), Group(20)], [10]);
        Assert.Equal(StartNodeSourceStatus.CoveredBy, result[1].Status);
        Assert.Equal(10, result[1].RelatedStartNodeId);
    }

    /// <summary>A group node under a root-access group is covered by root.</summary>
    [Fact]
    public void Attribute_GroupNodeUnderRootGroup_IsCoveredByRoot()
    {
        var result = Attribute([Group(-1), Group(30)], [-1]);
        Assert.Equal(StartNodeSourceStatus.InUse, result[0].Status);
        Assert.Equal(StartNodeSourceStatus.CoveredBy, result[1].Status);
        Assert.Equal(-1, result[1].RelatedStartNodeId);
    }

    /// <summary>A user-level start node replaces a root-access group: user start nodes only narrow.</summary>
    [Fact]
    public void Attribute_GroupRootWithUserNode_IsReplacedByUserStartNode()
    {
        var result = Attribute([Group(-1), User(20)], [20]);
        Assert.Equal(StartNodeSourceStatus.ReplacedByUserStartNode, result[0].Status);
        Assert.Equal(20, result[0].RelatedStartNodeId);
        Assert.Equal(StartNodeSourceStatus.InUse, result[1].Status);
    }

    /// <summary>A user-level start node above a group start node replaces it too.</summary>
    [Fact]
    public void Attribute_GroupNodeBelowUserNode_IsReplacedByUserStartNode()
    {
        var result = Attribute([Group(20), User(10)], [10]);
        Assert.Equal(StartNodeSourceStatus.ReplacedByUserStartNode, result[0].Status);
        Assert.Equal(10, result[0].RelatedStartNodeId);
    }

    /// <summary>A group start node on another branch stays next to a user-level start node.</summary>
    [Fact]
    public void Attribute_GroupNodeOnOtherBranch_StaysInUseNextToUserNode()
    {
        var result = Attribute([Group(30), User(20)], [30, 20]);
        Assert.All(result, r => Assert.Equal(StartNodeSourceStatus.InUse, r.Status));
    }

    /// <summary>Among user-level start nodes the deepest per branch wins.</summary>
    [Fact]
    public void Attribute_UserRootWithDeeperUserNode_IsNarrowedBy()
    {
        var result = Attribute([User(-1), User(20)], [20]);
        Assert.Equal(StartNodeSourceStatus.NarrowedBy, result[0].Status);
        Assert.Equal(20, result[0].RelatedStartNodeId);
    }

    /// <summary>A start node in the recycle bin is ignored by Umbraco.</summary>
    [Fact]
    public void Attribute_TrashedStartNode_IsIgnored() =>
        Assert.Equal(StartNodeSourceStatus.Ignored, Attribute([Group(40)], [])[0].Status);

    /// <summary>A start node that no longer exists is ignored by Umbraco.</summary>
    [Fact]
    public void Attribute_DeletedStartNode_IsIgnored() =>
        Assert.Equal(StartNodeSourceStatus.Ignored, Attribute([User(99)], [10])[0].Status);

    /// <summary>Runs the attribution against the shared tree.</summary>
    /// <param name="sources">The configured start nodes.</param>
    /// <param name="effective">Umbraco's combined result.</param>
    /// <returns>One result per source, in source order.</returns>
    private static IReadOnlyList<StartNodeSourceResult> Attribute(IReadOnlyList<StartNodeSource> sources, int[] effective) =>
        StartNodeRules.Attribute(sources, effective, Paths);

    /// <summary>Builds a user-group start-node source.</summary>
    /// <param name="id">The group's start node id, or <see langword="null"/> for none.</param>
    /// <returns>The source.</returns>
    private static StartNodeSource Group(int? id) => new(StartNodeSourceKind.UserGroup, "editors", "Editors", id);

    /// <summary>Builds a user-level start-node source.</summary>
    /// <param name="id">The user's start node id, or <see langword="null"/> for none.</param>
    /// <returns>The source.</returns>
    private static StartNodeSource User(int? id) => new(StartNodeSourceKind.User, null, null, id);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Umbraco.Community.AdvancedPermissions.Tests --filter "FullyQualifiedName~StartNodeRulesTests"`
Expected: build FAILS with `The type or namespace name 'StartNodes' does not exist`.

- [ ] **Step 3: Write the models**

Create `src/Umbraco.Community.AdvancedPermissions/Services/StartNodes/StartNodeModels.cs`:

```csharp
namespace Umbraco.Community.AdvancedPermissions.Services.StartNodes;

/// <summary>
/// Whether a subject can reach a content node, judged by start nodes alone. Independent of the
/// advanced permissions, which only matter for an <see cref="Accessible"/> node.
/// </summary>
public enum StartNodeAccess
{
    /// <summary>The node is at or under one of the subject's start nodes.</summary>
    Accessible,

    /// <summary>
    /// The node is an ancestor of a start node. Umbraco shows it in the tree, greyed out, so the user
    /// can browse down to the start node, but it cannot be opened or acted on.
    /// </summary>
    PathOnly,

    /// <summary>The node is outside every start node and hidden from the subject.</summary>
    NotVisible,
}

/// <summary>The overall start-node situation of a subject.</summary>
public enum StartNodeSubjectState
{
    /// <summary>Start nodes do not apply: the All Users group has no start node of its own.</summary>
    NotApplicable,

    /// <summary>The subject has root access and reaches every node.</summary>
    RootAccess,

    /// <summary>The subject reaches only the subtrees under its start nodes.</summary>
    Restricted,

    /// <summary>The subject has no start node at all, so no content access.</summary>
    NoAccess,
}

/// <summary>Where a configured start node comes from.</summary>
public enum StartNodeSourceKind
{
    /// <summary>A user group's document start node.</summary>
    UserGroup,

    /// <summary>A start node set on the user account itself.</summary>
    User,
}

/// <summary>What became of one configured start node after Umbraco combined them.</summary>
public enum StartNodeSourceStatus
{
    /// <summary>The start node is part of the effective set.</summary>
    InUse,

    /// <summary>A user group without a start node: it gives no content access.</summary>
    NoStartNode,

    /// <summary>A user without user-level start nodes: the user inherits the groups' start nodes.</summary>
    NotSet,

    /// <summary>A higher start node on the same branch already covers it.</summary>
    CoveredBy,

    /// <summary>A user-level start node on the same branch replaced this group start node.</summary>
    ReplacedByUserStartNode,

    /// <summary>A deeper user-level start node on the same branch narrowed this user-level start node.</summary>
    NarrowedBy,

    /// <summary>The start node is in the recycle bin or no longer exists, so Umbraco ignores it.</summary>
    Ignored,

    /// <summary>Not part of the effective set for a reason the rules do not recognise.</summary>
    NotApplied,
}

/// <summary>One start node as Umbraco stores it, before combination.</summary>
/// <param name="Kind">Whether a user group or the user account configures it.</param>
/// <param name="UserGroupAlias">The group alias, for <see cref="StartNodeSourceKind.UserGroup"/> only.</param>
/// <param name="UserGroupName">The group display name, for <see cref="StartNodeSourceKind.UserGroup"/> only.</param>
/// <param name="StartNodeId">The node id; <c>-1</c> is root; <see langword="null"/> is none.</param>
public sealed record StartNodeSource(
    StartNodeSourceKind Kind,
    string? UserGroupAlias,
    string? UserGroupName,
    int? StartNodeId);

/// <summary>One configured start node together with what became of it.</summary>
/// <param name="Source">The configured start node.</param>
/// <param name="Status">What became of it.</param>
/// <param name="RelatedStartNodeId">
/// The effective start node that covers, replaces or narrows it; <see langword="null"/> otherwise.
/// </param>
public sealed record StartNodeSourceResult(
    StartNodeSource Source,
    StartNodeSourceStatus Status,
    int? RelatedStartNodeId);

/// <summary>The identity and location of a node referenced by a snapshot.</summary>
/// <param name="Id">The node id; <c>-1</c> is the content root.</param>
/// <param name="Key">The node key; <see cref="Guid.Empty"/> for the content root.</param>
/// <param name="Name">The node name; empty for the content root.</param>
/// <param name="Path">The node's materialised path, e.g. <c>-1,1050,1063</c>.</param>
public sealed record StartNodeInfo(int Id, Guid Key, string Name, string Path);

/// <summary>Everything needed to classify nodes and explain start nodes for one subject.</summary>
/// <param name="State">The subject's overall start-node situation.</param>
/// <param name="EffectiveStartNodeIds">Umbraco's combined start node ids; <c>-1</c> means root.</param>
/// <param name="Nodes">Every node the snapshot references, keyed by id, including the root (<c>-1</c>).</param>
/// <param name="Sources">One explained line per configured start node.</param>
public sealed record StartNodeSnapshot(
    StartNodeSubjectState State,
    IReadOnlyList<int> EffectiveStartNodeIds,
    IReadOnlyDictionary<int, StartNodeInfo> Nodes,
    IReadOnlyList<StartNodeSourceResult> Sources)
{
    /// <summary>The snapshot for the All Users group, which has no start nodes of its own.</summary>
    public static StartNodeSnapshot NotApplicable { get; } =
        new(StartNodeSubjectState.NotApplicable, [], new Dictionary<int, StartNodeInfo>(), []);
}
```

- [ ] **Step 4: Write the rules**

Create `src/Umbraco.Community.AdvancedPermissions/Services/StartNodes/StartNodeRules.cs`:

```csharp
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Security;

namespace Umbraco.Community.AdvancedPermissions.Services.StartNodes;

/// <summary>
/// Pure start-node rules: classifying a node against a subject's effective start nodes, and
/// explaining what became of each configured start node.
/// </summary>
/// <remarks>
/// Umbraco's own combination (<c>UserExtensions.CombineStartNodes</c>) is internal, so it is not
/// reimplemented. The effective set always comes from Umbraco; these rules only classify against it
/// and attribute it. Reachability calls Umbraco's <see cref="ContentPermissions.HasPathAccess"/>, the
/// check enforcement uses, so the viewers can never disagree with what the server allows.
/// </remarks>
public static class StartNodeRules
{
    /// <summary>The recycle-bin segment as it appears in a comma-wrapped path.</summary>
    private const string RecycleBinSegment = ",-20,";

    /// <summary>Classifies one node against a subject's effective start nodes.</summary>
    /// <param name="nodePath">The node's materialised path, e.g. <c>-1,1050</c>; <c>-1</c> is the content root.</param>
    /// <param name="effectiveStartNodeIds">Umbraco's combined start node ids.</param>
    /// <param name="effectiveStartNodePaths">The paths of those start nodes.</param>
    /// <returns>Whether the subject can reach the node.</returns>
    public static StartNodeAccess Classify(
        string nodePath,
        IReadOnlyCollection<int> effectiveStartNodeIds,
        IEnumerable<string> effectiveStartNodePaths)
    {
        if (ContentPermissions.HasPathAccess(nodePath, effectiveStartNodeIds.ToArray(), Constants.System.RecycleBinContent))
        {
            return StartNodeAccess.Accessible;
        }

        return effectiveStartNodePaths.Any(startPath => IsStrictAncestor(nodePath, startPath))
            ? StartNodeAccess.PathOnly
            : StartNodeAccess.NotVisible;
    }

    /// <summary>Explains what became of each configured start node.</summary>
    /// <param name="sources">The configured start nodes, user groups first, then the user's own.</param>
    /// <param name="effectiveStartNodeIds">Umbraco's combined start node ids.</param>
    /// <param name="pathsById">Paths of every referenced node; a missing id means the node was deleted.</param>
    /// <returns>One result per source, in source order.</returns>
    public static IReadOnlyList<StartNodeSourceResult> Attribute(
        IReadOnlyList<StartNodeSource> sources,
        IReadOnlyCollection<int> effectiveStartNodeIds,
        IReadOnlyDictionary<int, string> pathsById)
    {
        var userLevelIds = sources
            .Where(s => s.Kind == StartNodeSourceKind.User && s.StartNodeId.HasValue)
            .Select(s => s.StartNodeId!.Value)
            .ToHashSet();

        return sources
            .Select(s => AttributeOne(s, effectiveStartNodeIds, pathsById, userLevelIds))
            .ToList();
    }

    /// <summary>Whether one path is a strict ancestor of another, compared on whole path segments.</summary>
    /// <param name="ancestorPath">The candidate ancestor path.</param>
    /// <param name="descendantPath">The candidate descendant path.</param>
    /// <returns><see langword="true"/> when <paramref name="descendantPath"/> lies under <paramref name="ancestorPath"/>.</returns>
    public static bool IsStrictAncestor(string ancestorPath, string descendantPath) =>
        descendantPath.StartsWith(ancestorPath + ",", StringComparison.Ordinal);

    /// <summary>Whether a path lies in the content recycle bin.</summary>
    /// <param name="path">The materialised path.</param>
    /// <returns><see langword="true"/> when the path passes through the recycle bin.</returns>
    public static bool IsInRecycleBin(string path) =>
        string.Concat(",", path, ",").Contains(RecycleBinSegment, StringComparison.Ordinal);

    /// <summary>Explains one configured start node.</summary>
    /// <param name="source">The configured start node.</param>
    /// <param name="effective">Umbraco's combined start node ids.</param>
    /// <param name="paths">Paths of every referenced node.</param>
    /// <param name="userLevelIds">The user's own start node ids.</param>
    /// <returns>The explained source.</returns>
    private static StartNodeSourceResult AttributeOne(
        StartNodeSource source,
        IReadOnlyCollection<int> effective,
        IReadOnlyDictionary<int, string> paths,
        IReadOnlySet<int> userLevelIds)
    {
        if (source.StartNodeId is not { } id)
        {
            var status = source.Kind == StartNodeSourceKind.UserGroup
                ? StartNodeSourceStatus.NoStartNode
                : StartNodeSourceStatus.NotSet;
            return new StartNodeSourceResult(source, status, null);
        }

        if (!paths.TryGetValue(id, out var path) || IsInRecycleBin(path))
        {
            return new StartNodeSourceResult(source, StartNodeSourceStatus.Ignored, null);
        }

        if (effective.Contains(id))
        {
            return new StartNodeSourceResult(source, StartNodeSourceStatus.InUse, null);
        }

        if (source.Kind == StartNodeSourceKind.UserGroup)
        {
            var replacing = FindEffective(effective, paths, (eid, epath) =>
                userLevelIds.Contains(eid) && (IsStrictAncestor(epath, path) || IsStrictAncestor(path, epath)));
            if (replacing is not null)
            {
                return new StartNodeSourceResult(source, StartNodeSourceStatus.ReplacedByUserStartNode, replacing);
            }

            var covering = FindEffective(effective, paths, (_, epath) => IsStrictAncestor(epath, path));
            if (covering is not null)
            {
                return new StartNodeSourceResult(source, StartNodeSourceStatus.CoveredBy, covering);
            }
        }
        else
        {
            var deeper = FindEffective(effective, paths, (eid, epath) =>
                userLevelIds.Contains(eid) && IsStrictAncestor(path, epath));
            if (deeper is not null)
            {
                return new StartNodeSourceResult(source, StartNodeSourceStatus.NarrowedBy, deeper);
            }
        }

        return new StartNodeSourceResult(source, StartNodeSourceStatus.NotApplied, null);
    }

    /// <summary>Finds the first effective start node matching a predicate.</summary>
    /// <param name="effective">Umbraco's combined start node ids.</param>
    /// <param name="paths">Paths of every referenced node.</param>
    /// <param name="predicate">Receives an effective id and its path.</param>
    /// <returns>The matching id, or <see langword="null"/>.</returns>
    private static int? FindEffective(
        IReadOnlyCollection<int> effective,
        IReadOnlyDictionary<int, string> paths,
        Func<int, string, bool> predicate)
    {
        foreach (var id in effective)
        {
            if (paths.TryGetValue(id, out var path) && predicate(id, path))
            {
                return id;
            }
        }

        return null;
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/Umbraco.Community.AdvancedPermissions.Tests --filter "FullyQualifiedName~StartNodeRulesTests"`
Expected: PASS (21 tests).

- [ ] **Step 6: Leave uncommitted**

No commit. `git status` shows the three new files.

---

### Task 2: Start-node access service

**Files:**
- Create: `src/Umbraco.Community.AdvancedPermissions/Services/StartNodes/IStartNodeAccessService.cs`
- Create: `src/Umbraco.Community.AdvancedPermissions/Services/StartNodes/StartNodeAccessService.cs`
- Modify: `src/Umbraco.Community.AdvancedPermissions/Composing/AdvancedPermissionsComposer.cs` (service registrations near line 129)
- Test: `tests/Umbraco.Community.AdvancedPermissions.Tests/StartNodeAccessServiceTests.cs`

**Interfaces:**
- Consumes: everything in Task 1.
- Produces: `IStartNodeAccessService` with `Task<StartNodeSnapshot?> GetForUserAsync(Guid userKey)`, `Task<StartNodeSnapshot?> GetForUserGroupAsync(string userGroupAlias)`, `StartNodeAccess Classify(StartNodeSnapshot snapshot, string nodePath)`. Registered as a singleton.

- [ ] **Step 1: Write the failing tests**

Create `tests/Umbraco.Community.AdvancedPermissions.Tests/StartNodeAccessServiceTests.cs`:

```csharp
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
```

Note: the `Core.Constants` namespace above is where `AdvancedPermissionsConstants` lives (it ships in the Abstractions project under that namespace, as the existing tests import it).

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Umbraco.Community.AdvancedPermissions.Tests --filter "FullyQualifiedName~StartNodeAccessServiceTests"`
Expected: build FAILS with `The type or namespace name 'StartNodeAccessService' could not be found`.

- [ ] **Step 3: Write the interface**

Create `src/Umbraco.Community.AdvancedPermissions/Services/StartNodes/IStartNodeAccessService.cs`:

```csharp
namespace Umbraco.Community.AdvancedPermissions.Services.StartNodes;

/// <summary>
/// Works out which content nodes a user or a single user group can reach through start nodes, and
/// explains where those start nodes come from. Feeds the viewers only; enforcement does not use it.
/// </summary>
public interface IStartNodeAccessService
{
    /// <summary>
    /// Builds the start-node snapshot for a user, from Umbraco's own combination of the user's group
    /// and user-level start nodes (the exact value enforcement uses).
    /// </summary>
    /// <param name="userKey">The user's key.</param>
    /// <returns>The snapshot, or <see langword="null"/> when the user does not exist.</returns>
    Task<StartNodeSnapshot?> GetForUserAsync(Guid userKey);

    /// <summary>
    /// Builds the start-node snapshot for one user group on its own. The All Users group yields
    /// <see cref="StartNodeSnapshot.NotApplicable"/>.
    /// </summary>
    /// <param name="userGroupAlias">The user group alias.</param>
    /// <returns>The snapshot, or <see langword="null"/> when the group does not exist.</returns>
    Task<StartNodeSnapshot?> GetForUserGroupAsync(string userGroupAlias);

    /// <summary>Classifies one node for the subject a snapshot describes.</summary>
    /// <param name="snapshot">The subject's snapshot.</param>
    /// <param name="nodePath">The node's materialised path; <c>-1</c> is the content root.</param>
    /// <returns>Whether the subject can reach the node.</returns>
    StartNodeAccess Classify(StartNodeSnapshot snapshot, string nodePath);
}
```

- [ ] **Step 4: Write the implementation**

Create `src/Umbraco.Community.AdvancedPermissions/Services/StartNodes/StartNodeAccessService.cs`:

```csharp
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.Core.Constants;

namespace Umbraco.Community.AdvancedPermissions.Services.StartNodes;

/// <summary>
/// Default <see cref="IStartNodeAccessService"/>. Takes the effective start nodes from Umbraco
/// (<c>CalculateContentStartNodeIds</c>, cached per user exactly as enforcement reads it) and reads
/// node names and paths fresh, never from Umbraco's start-node path cache, which a move leaves stale.
/// </summary>
/// <param name="userService">Loads the user.</param>
/// <param name="userGroupService">Loads a user group by alias.</param>
/// <param name="entityService">Reads node keys, names and paths.</param>
/// <param name="appCaches">Umbraco's caches, passed to <c>CalculateContentStartNodeIds</c>.</param>
public sealed class StartNodeAccessService(
    IUserService userService,
    IUserGroupService userGroupService,
    IEntityService entityService,
    AppCaches appCaches) : IStartNodeAccessService
{
    /// <summary>The content root as a snapshot node.</summary>
    private static readonly StartNodeInfo RootNode =
        new(Constants.System.Root, Guid.Empty, string.Empty, Constants.System.RootString);

    /// <inheritdoc />
    public async Task<StartNodeSnapshot?> GetForUserAsync(Guid userKey)
    {
        var user = await userService.GetAsync(userKey);
        if (user is null)
        {
            return null;
        }

        var effective = user.CalculateContentStartNodeIds(entityService, appCaches) ?? [];

        var sources = user.Groups
            .Select(g => new StartNodeSource(StartNodeSourceKind.UserGroup, g.Alias, g.Name, g.StartContentId))
            .ToList();
        var userIds = user.StartContentIds ?? [];
        if (userIds.Length == 0)
        {
            sources.Add(new StartNodeSource(StartNodeSourceKind.User, null, null, null));
        }
        else
        {
            sources.AddRange(userIds.Select(id => new StartNodeSource(StartNodeSourceKind.User, null, null, id)));
        }

        return Build(effective, sources);
    }

    /// <inheritdoc />
    public async Task<StartNodeSnapshot?> GetForUserGroupAsync(string userGroupAlias)
    {
        if (userGroupAlias == AdvancedPermissionsConstants.EveryoneRoleAlias)
        {
            return StartNodeSnapshot.NotApplicable;
        }

        var group = await userGroupService.GetAsync(userGroupAlias);
        if (group is null)
        {
            return null;
        }

        var source = new StartNodeSource(StartNodeSourceKind.UserGroup, group.Alias, group.Name, group.StartContentId);
        var nodes = LoadNodes(group.StartContentId is { } id && id != Constants.System.Root ? [id] : []);

        // A group on its own: root, its one start node when it still exists outside the bin, or nothing.
        int[] effective = group.StartContentId switch
        {
            null => [],
            Constants.System.Root => [Constants.System.Root],
            var id when nodes.TryGetValue(id, out var node) && !StartNodeRules.IsInRecycleBin(node.Path) => [id],
            _ => [],
        };

        return Snapshot(effective, [source], nodes);
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

        return StartNodeRules.Classify(nodePath, snapshot.EffectiveStartNodeIds, effectivePaths);
    }

    /// <summary>Loads every node a user snapshot references and builds the snapshot.</summary>
    /// <param name="effective">Umbraco's combined start node ids.</param>
    /// <param name="sources">The configured start nodes.</param>
    /// <returns>The snapshot.</returns>
    private StartNodeSnapshot Build(IReadOnlyList<int> effective, IReadOnlyList<StartNodeSource> sources)
    {
        var ids = effective
            .Concat(sources.Where(s => s.StartNodeId.HasValue).Select(s => s.StartNodeId!.Value))
            .Where(id => id != Constants.System.Root)
            .Distinct()
            .ToArray();

        return Snapshot(effective, sources, LoadNodes(ids));
    }

    /// <summary>Assembles a snapshot from its parts.</summary>
    /// <param name="effective">The effective start node ids.</param>
    /// <param name="sources">The configured start nodes.</param>
    /// <param name="nodes">Every referenced node, including the root.</param>
    /// <returns>The snapshot.</returns>
    private static StartNodeSnapshot Snapshot(
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
        return new StartNodeSnapshot(state, effective, nodes, StartNodeRules.Attribute(sources, effective, paths));
    }

    /// <summary>Loads keys, names and paths for a set of node ids, plus the content root.</summary>
    /// <param name="ids">The node ids; must not contain the root.</param>
    /// <returns>The found nodes by id; deleted nodes are absent.</returns>
    private Dictionary<int, StartNodeInfo> LoadNodes(int[] ids)
    {
        var nodes = new Dictionary<int, StartNodeInfo> { [Constants.System.Root] = RootNode };

        // GetAll with no ids returns every document, so an empty set must never reach it.
        if (ids.Length == 0)
        {
            return nodes;
        }

        foreach (var entity in entityService.GetAll(UmbracoObjectTypes.Document, ids))
        {
            nodes[entity.Id] = new StartNodeInfo(entity.Id, entity.Key, entity.Name ?? string.Empty, entity.Path);
        }

        return nodes;
    }
}
```

If the compiler rejects `Constants.System.Root` as a constant pattern in the `switch`, replace that arm with `var root when root == Constants.System.Root => [Constants.System.Root],`.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/Umbraco.Community.AdvancedPermissions.Tests --filter "FullyQualifiedName~StartNodeAccessServiceTests"`
Expected: PASS (14 tests). If `GetForUserAsync_SiblingGroupStartNodes_BothInUse` fails on `CalculateContentStartNodeIds` returning `null`, check that `user.StartContentIds` is stubbed to `[]` (Umbraco returns null when it is null).

- [ ] **Step 6: Register the service**

In `AdvancedPermissionsComposer.cs`, after the `IDocTypePermissionService` registration (line 129), add:

```csharp
        // Start nodes for the viewers (issue #66): reads Umbraco's combined start nodes, never enforces.
        builder.Services.AddSingleton<IStartNodeAccessService, StartNodeAccessService>();
```

Add `using Umbraco.Community.AdvancedPermissions.Services.StartNodes;` to the composer's usings.

- [ ] **Step 7: Build and leave uncommitted**

Run: `dotnet build`
Expected: Build succeeded, 0 errors. No commit.

---

### Task 3: Start-node endpoints

**Files:**
- Create: `src/Umbraco.Community.AdvancedPermissions/Controllers/Models/StartNodesResponseModels.cs`
- Create: `src/Umbraco.Community.AdvancedPermissions/Controllers/AdvancedPermissionsStartNodesController.cs`
- Test: `tests/Umbraco.Community.AdvancedPermissions.Tests/AdvancedPermissionsStartNodesControllerTests.cs`

**Interfaces:**
- Consumes: `IStartNodeAccessService`, `StartNodeSnapshot` (Tasks 1-2).
- Produces: `GET /umbraco/management/api/v1/advanced-permissions/start-nodes?userKey=` (operation `GetStartNodesForUser`) and `GET .../start-nodes/by-role?roleAlias=` (operation `GetStartNodesForRole`), both returning `StartNodesResponseModel(string State, IReadOnlyList<StartNodeItemResponseModel> StartNodes, IReadOnlyList<StartNodeSourceResponseModel> Sources)`; `StartNodeItemResponseModel(Guid Key, string Name)`; `StartNodeSourceResponseModel(string Kind, string? UserGroupAlias, string? UserGroupName, Guid? StartNodeKey, string? StartNodeName, bool IsRoot, string Status, Guid? RelatedNodeKey, string? RelatedNodeName, bool RelatedIsRoot)`.

- [ ] **Step 1: Write the failing tests**

Create `tests/Umbraco.Community.AdvancedPermissions.Tests/AdvancedPermissionsStartNodesControllerTests.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Umbraco.Community.AdvancedPermissions.Controllers;
using Umbraco.Community.AdvancedPermissions.Controllers.Models;
using Umbraco.Community.AdvancedPermissions.Services.StartNodes;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>Tests the HTTP contract of <see cref="AdvancedPermissionsStartNodesController"/>.</summary>
public sealed class AdvancedPermissionsStartNodesControllerTests
{
    /// <summary>The start-node service substitute.</summary>
    private readonly IStartNodeAccessService _startNodes = Substitute.For<IStartNodeAccessService>();

    /// <summary>The system under test.</summary>
    private readonly AdvancedPermissionsStartNodesController _sut;

    /// <summary>Initializes a new instance of the <see cref="AdvancedPermissionsStartNodesControllerTests"/> class.</summary>
    public AdvancedPermissionsStartNodesControllerTests() =>
        _sut = new AdvancedPermissionsStartNodesController(_startNodes);

    /// <summary>An unknown user is a 404.</summary>
    [Fact]
    public async Task GetForUser_UnknownUser_Returns404()
    {
        _startNodes.GetForUserAsync(Arg.Any<Guid>()).Returns((StartNodeSnapshot?)null);

        Assert.IsType<NotFoundObjectResult>(await _sut.GetStartNodesForUser(Guid.NewGuid()));
    }

    /// <summary>An unknown user group is a 404.</summary>
    [Fact]
    public async Task GetForRole_UnknownGroup_Returns404()
    {
        _startNodes.GetForUserGroupAsync("nope").Returns((StartNodeSnapshot?)null);

        Assert.IsType<NotFoundObjectResult>(await _sut.GetStartNodesForRole("nope"));
    }

    /// <summary>The snapshot maps to keys, names and string statuses; the root is flagged, not named.</summary>
    [Fact]
    public async Task GetForUser_MapsSnapshot()
    {
        var homeKey = Guid.NewGuid();
        var nodes = new Dictionary<int, StartNodeInfo>
        {
            [-1] = new(-1, Guid.Empty, string.Empty, "-1"),
            [10] = new(10, homeKey, "Home", "-1,10"),
        };
        var editors = new StartNodeSource(StartNodeSourceKind.UserGroup, "editors", "Editors", 10);
        var admin = new StartNodeSource(StartNodeSourceKind.UserGroup, "admin", "Administrators", -1);
        var snapshot = new StartNodeSnapshot(
            StartNodeSubjectState.Restricted,
            [10],
            nodes,
            [
                new StartNodeSourceResult(editors, StartNodeSourceStatus.InUse, null),
                new StartNodeSourceResult(admin, StartNodeSourceStatus.ReplacedByUserStartNode, 10),
            ]);
        _startNodes.GetForUserAsync(Arg.Any<Guid>()).Returns(snapshot);

        var ok = Assert.IsType<OkObjectResult>(await _sut.GetStartNodesForUser(Guid.NewGuid()));
        var body = Assert.IsType<StartNodesResponseModel>(ok.Value);

        Assert.Equal("Restricted", body.State);
        var startNode = Assert.Single(body.StartNodes);
        Assert.Equal(homeKey, startNode.Key);
        Assert.Equal("Home", startNode.Name);

        Assert.Equal("InUse", body.Sources[0].Status);
        Assert.Equal(homeKey, body.Sources[0].StartNodeKey);
        Assert.False(body.Sources[0].IsRoot);

        Assert.True(body.Sources[1].IsRoot);
        Assert.Null(body.Sources[1].StartNodeKey);
        Assert.Equal("ReplacedByUserStartNode", body.Sources[1].Status);
        Assert.Equal("Home", body.Sources[1].RelatedNodeName);
    }

    /// <summary>A root-access subject lists no start nodes; the state says it all.</summary>
    [Fact]
    public async Task GetForRole_RootAccess_ListsNoStartNodes()
    {
        var nodes = new Dictionary<int, StartNodeInfo> { [-1] = new(-1, Guid.Empty, string.Empty, "-1") };
        var source = new StartNodeSource(StartNodeSourceKind.UserGroup, "admin", "Administrators", -1);
        _startNodes.GetForUserGroupAsync("admin").Returns(new StartNodeSnapshot(
            StartNodeSubjectState.RootAccess, [-1], nodes, [new StartNodeSourceResult(source, StartNodeSourceStatus.InUse, null)]));

        var ok = Assert.IsType<OkObjectResult>(await _sut.GetStartNodesForRole("admin"));
        var body = Assert.IsType<StartNodesResponseModel>(ok.Value);

        Assert.Equal("RootAccess", body.State);
        Assert.Empty(body.StartNodes);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Umbraco.Community.AdvancedPermissions.Tests --filter "FullyQualifiedName~AdvancedPermissionsStartNodesControllerTests"`
Expected: build FAILS with `The type or namespace name 'AdvancedPermissionsStartNodesController' could not be found`.

- [ ] **Step 3: Write the response models**

Create `src/Umbraco.Community.AdvancedPermissions/Controllers/Models/StartNodesResponseModels.cs`:

```csharp
namespace Umbraco.Community.AdvancedPermissions.Controllers.Models;

/// <summary>
/// The start nodes of a user or user group, for the viewers' start-node strip.
/// </summary>
/// <param name="State">
/// <c>NotApplicable</c> (All Users group), <c>RootAccess</c>, <c>Restricted</c> or <c>NoAccess</c>.
/// </param>
/// <param name="StartNodes">The effective start nodes; empty for root access or no access.</param>
/// <param name="Sources">One line per configured start node, explaining what became of it.</param>
public sealed record StartNodesResponseModel(
    string State,
    IReadOnlyList<StartNodeItemResponseModel> StartNodes,
    IReadOnlyList<StartNodeSourceResponseModel> Sources);

/// <summary>One effective start node.</summary>
/// <param name="Key">The node key.</param>
/// <param name="Name">The node name.</param>
public sealed record StartNodeItemResponseModel(Guid Key, string Name);

/// <summary>One configured start node and what became of it.</summary>
/// <param name="Kind"><c>UserGroup</c> or <c>User</c>.</param>
/// <param name="UserGroupAlias">The group alias, for <c>UserGroup</c> lines.</param>
/// <param name="UserGroupName">The group display name, for <c>UserGroup</c> lines.</param>
/// <param name="StartNodeKey">The start node key; <see langword="null"/> for none, root, or a deleted node.</param>
/// <param name="StartNodeName">The start node name; <see langword="null"/> for none, root, or a deleted node.</param>
/// <param name="IsRoot">Whether the configured start node is the content root.</param>
/// <param name="Status">
/// <c>InUse</c>, <c>NoStartNode</c>, <c>NotSet</c>, <c>CoveredBy</c>, <c>ReplacedByUserStartNode</c>,
/// <c>NarrowedBy</c>, <c>Ignored</c> or <c>NotApplied</c>.
/// </param>
/// <param name="RelatedNodeKey">The node that covers, replaces or narrows it, when there is one.</param>
/// <param name="RelatedNodeName">That node's name.</param>
/// <param name="RelatedIsRoot">Whether that node is the content root.</param>
public sealed record StartNodeSourceResponseModel(
    string Kind,
    string? UserGroupAlias,
    string? UserGroupName,
    Guid? StartNodeKey,
    string? StartNodeName,
    bool IsRoot,
    string Status,
    Guid? RelatedNodeKey,
    string? RelatedNodeName,
    bool RelatedIsRoot);
```

- [ ] **Step 4: Write the controller**

Create `src/Umbraco.Community.AdvancedPermissions/Controllers/AdvancedPermissionsStartNodesController.cs`:

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
/// Provides the start nodes of a user or user group, with where each one comes from. Feeds the
/// start-node strip in the Access Viewer and the Insert Options Viewer.
/// </summary>
/// <param name="startNodeAccessService">Builds start-node snapshots.</param>
[ApiVersion("1.0")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessUsers)]
public sealed class AdvancedPermissionsStartNodesController(IStartNodeAccessService startNodeAccessService)
    : AdvancedPermissionsControllerBase
{
    /// <summary>Gets a user's effective start nodes and their sources.</summary>
    /// <param name="userKey">The user's key.</param>
    /// <returns>The start nodes, or 404 when the user does not exist.</returns>
    [HttpGet("start-nodes", Name = "GetStartNodesForUser")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<StartNodesResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [EndpointSummary("Gets a user's effective start nodes and where each comes from.")]
    public async Task<IActionResult> GetStartNodesForUser(Guid userKey)
    {
        var snapshot = await startNodeAccessService.GetForUserAsync(userKey);
        return snapshot is null
            ? NotFound(new ProblemDetails { Title = "User not found", Detail = $"No user with key {userKey}.", Status = StatusCodes.Status404NotFound })
            : Ok(Map(snapshot));
    }

    /// <summary>Gets one user group's own start node.</summary>
    /// <param name="roleAlias">The user group alias, or <c>$everyone</c>.</param>
    /// <returns>The start nodes, or 404 when the group does not exist.</returns>
    [HttpGet("start-nodes/by-role", Name = "GetStartNodesForRole")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType<StartNodesResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [EndpointSummary("Gets one user group's own start node.")]
    public async Task<IActionResult> GetStartNodesForRole(string roleAlias)
    {
        var snapshot = await startNodeAccessService.GetForUserGroupAsync(roleAlias);
        return snapshot is null
            ? NotFound(new ProblemDetails { Title = "User group not found", Detail = $"No user group with alias {roleAlias}.", Status = StatusCodes.Status404NotFound })
            : Ok(Map(snapshot));
    }

    /// <summary>Maps a snapshot to the API model.</summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <returns>The response model.</returns>
    private static StartNodesResponseModel Map(StartNodeSnapshot snapshot)
    {
        var startNodes = snapshot.EffectiveStartNodeIds
            .Where(id => id != Constants.System.Root && snapshot.Nodes.ContainsKey(id))
            .Select(id => new StartNodeItemResponseModel(snapshot.Nodes[id].Key, snapshot.Nodes[id].Name))
            .ToList();

        var sources = snapshot.Sources
            .Select(r => new StartNodeSourceResponseModel(
                r.Source.Kind.ToString(),
                r.Source.UserGroupAlias,
                r.Source.UserGroupName,
                NodeOf(snapshot, r.Source.StartNodeId)?.Key,
                NodeOf(snapshot, r.Source.StartNodeId)?.Name,
                r.Source.StartNodeId == Constants.System.Root,
                r.Status.ToString(),
                NodeOf(snapshot, r.RelatedStartNodeId)?.Key,
                NodeOf(snapshot, r.RelatedStartNodeId)?.Name,
                r.RelatedStartNodeId == Constants.System.Root))
            .ToList();

        return new StartNodesResponseModel(snapshot.State.ToString(), startNodes, sources);
    }

    /// <summary>Looks up a real (non-root) node in a snapshot.</summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <param name="id">The node id, or <see langword="null"/>.</param>
    /// <returns>The node, or <see langword="null"/> for none, root, or a deleted node.</returns>
    private static StartNodeInfo? NodeOf(StartNodeSnapshot snapshot, int? id) =>
        id is { } value && value != Constants.System.Root && snapshot.Nodes.TryGetValue(value, out var node) ? node : null;
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/Umbraco.Community.AdvancedPermissions.Tests --filter "FullyQualifiedName~AdvancedPermissionsStartNodesControllerTests|FullyQualifiedName~AdvancedPermissionsControllerBaseAuthorizationTests"`
Expected: PASS. The authorization tests confirm the new controller carries the Users-section gate.

- [ ] **Step 6: Leave uncommitted**

No commit.

---

### Task 4: `startNodeAccess` on the effective endpoints

**Files:**
- Modify: `src/Umbraco.Community.AdvancedPermissions/Controllers/Models/EffectivePermissionsResponseModel.cs:9-11`
- Modify: `src/Umbraco.Community.AdvancedPermissions/Controllers/AdvancedPermissionsControllerBase.cs` (add `GetDocumentPath`)
- Modify: `src/Umbraco.Community.AdvancedPermissions/Controllers/AdvancedPermissionsEffectiveController.cs`
- Test: `tests/Umbraco.Community.AdvancedPermissions.Tests/AdvancedPermissionsEffectiveControllerTests.cs`

**Interfaces:**
- Consumes: `IStartNodeAccessService`, `StartNodeAccess` (Tasks 1-2).
- Produces: `EffectivePermissionsResponseModel(Guid NodeKey, IReadOnlyList<EffectivePermissionItem> Permissions, string StartNodeAccess)`; protected static `string? GetDocumentPath(Guid nodeKey, IEntityService entityService)` on the controller base (used again in Task 5).

- [ ] **Step 1: Update the test fixture and write the failing tests**

In `AdvancedPermissionsEffectiveControllerTests.cs`:
- add `using Umbraco.Community.AdvancedPermissions.Services.StartNodes;`
- add the field `private readonly IStartNodeAccessService _startNodes = Substitute.For<IStartNodeAccessService>();`
- change the constructor body to `_sut = new AdvancedPermissionsEffectiveController(_permissionService, _entityService, _startNodes);`

Add these tests at the end of the class (before any private helpers):

```csharp
    // ─── Start node access (issue #66) ───────────────────────────────────────

    /// <summary>An unknown node is reported as not visible.</summary>
    [Fact]
    public async Task GetEffectiveForUser_UnknownNode_StartNodeAccessIsNotVisible()
    {
        _entityService.GetAllPaths(UmbracoObjectTypes.Document, Arg.Any<Guid[]>()).Returns([]);

        var ok = Assert.IsType<OkObjectResult>(await _sut.GetEffectiveForUser(CancellationToken.None, Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal("NotVisible", Assert.IsType<EffectivePermissionsResponseModel>(ok.Value).StartNodeAccess);
    }

    /// <summary>A known node carries the service's classification for the user.</summary>
    [Fact]
    public async Task GetEffectiveForUser_KnownNode_CarriesClassification()
    {
        var userKey = Guid.NewGuid();
        var nodeKey = SeedNode(42, "-1,42");
        var snapshot = StartNodeSnapshot.NotApplicable with { State = StartNodeSubjectState.Restricted };
        _startNodes.GetForUserAsync(userKey).Returns(snapshot);
        _startNodes.Classify(snapshot, "-1,42").Returns(StartNodeAccess.PathOnly);

        var ok = Assert.IsType<OkObjectResult>(await _sut.GetEffectiveForUser(CancellationToken.None, userKey, nodeKey));

        Assert.Equal("PathOnly", Assert.IsType<EffectivePermissionsResponseModel>(ok.Value).StartNodeAccess);
    }

    /// <summary>Review focus 4: an unknown user is never shown as blocked.</summary>
    [Fact]
    public async Task GetEffectiveForUser_UnknownUser_IsAccessible()
    {
        var nodeKey = SeedNode(42, "-1,42");
        _startNodes.GetForUserAsync(Arg.Any<Guid>()).Returns((StartNodeSnapshot?)null);

        var ok = Assert.IsType<OkObjectResult>(await _sut.GetEffectiveForUser(CancellationToken.None, Guid.NewGuid(), nodeKey));

        Assert.Equal("Accessible", Assert.IsType<EffectivePermissionsResponseModel>(ok.Value).StartNodeAccess);
    }

    /// <summary>Role mode uses the group's own snapshot.</summary>
    [Fact]
    public async Task GetEffectiveForRole_KnownNode_CarriesClassification()
    {
        var nodeKey = SeedNode(42, "-1,42");
        var snapshot = StartNodeSnapshot.NotApplicable with { State = StartNodeSubjectState.Restricted };
        _startNodes.GetForUserGroupAsync("editors").Returns(snapshot);
        _startNodes.Classify(snapshot, "-1,42").Returns(StartNodeAccess.NotVisible);

        var ok = Assert.IsType<OkObjectResult>(await _sut.GetEffectiveForRole(CancellationToken.None, "editors", nodeKey));

        Assert.Equal("NotVisible", Assert.IsType<EffectivePermissionsResponseModel>(ok.Value).StartNodeAccess);
    }

    /// <summary>Seeds one known node into the entity service and an empty resolver answer.</summary>
    /// <param name="id">The node id.</param>
    /// <param name="path">The node path.</param>
    /// <returns>The node key.</returns>
    private Guid SeedNode(int id, string path)
    {
        var nodeKey = Guid.NewGuid();
        // Build the stub entity first: configuring a substitute inside a .Returns(...) argument
        // corrupts NSubstitute's last-call tracking.
        var entity = Substitute.For<IEntitySlim>();
        entity.Id.Returns(id);
        entity.Key.Returns(nodeKey);
        _entityService.GetAllPaths(UmbracoObjectTypes.Document, Arg.Any<Guid[]>())
            .Returns([new TreeEntityPath { Id = id, Key = nodeKey, Path = path }]);
        _entityService.GetAll(UmbracoObjectTypes.Document, Arg.Any<int[]>()).Returns([entity]);
        _permissionService
            .ResolveAllAsync(Arg.Any<Guid>(), nodeKey, Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<IEnumerable<string>?>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, EffectivePermission>());
        _permissionService
            .ResolveForRoleAsync(Arg.Any<string>(), nodeKey, Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<IEnumerable<string>?>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, EffectivePermission>());
        return nodeKey;
    }
```

Add any missing usings the compiler asks for (`Umbraco.Cms.Core.Models.Entities` for `IEntitySlim`/`TreeEntityPath`, and the existing `Core.Models` namespace for `EffectivePermission`). Before writing `SeedNode`, open `IAdvancedPermissionService` in the Abstractions project and match `ResolveForRoleAsync`'s real parameter list exactly; adjust the two `.Returns` stubs if it differs.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Umbraco.Community.AdvancedPermissions.Tests --filter "FullyQualifiedName~AdvancedPermissionsEffectiveControllerTests"`
Expected: build FAILS: the controller has no 3-argument constructor and `EffectivePermissionsResponseModel` has no `StartNodeAccess`.

- [ ] **Step 3: Extend the response model**

In `EffectivePermissionsResponseModel.cs`, replace the first record with:

```csharp
/// <summary>
/// Represents the full set of resolved effective permissions for a user or role at a content node.
/// </summary>
/// <param name="NodeKey">The content node key these permissions were resolved for.</param>
/// <param name="Permissions">
/// The effective permission for each verb. Always the advanced-permission result, even when start
/// nodes block the node, so the viewer can show what would apply if the node were reachable.
/// </param>
/// <param name="StartNodeAccess">
/// Whether the subject can reach the node through its start nodes: <c>Accessible</c>,
/// <c>PathOnly</c> (an ancestor of a start node) or <c>NotVisible</c>.
/// </param>
public sealed record EffectivePermissionsResponseModel(
    Guid NodeKey,
    IReadOnlyList<EffectivePermissionItem> Permissions,
    string StartNodeAccess);
```

- [ ] **Step 4: Add the path helper to the controller base**

In `AdvancedPermissionsControllerBase.cs`, after `BuildPathFromRoot`, add:

```csharp
    /// <summary>
    /// Gets a document's materialised path (e.g. <c>-1,1050,1063</c>), read fresh from the database.
    /// </summary>
    /// <param name="nodeKey">The document key.</param>
    /// <param name="entityService">The Umbraco entity service.</param>
    /// <returns>The path, or <see langword="null"/> when the document does not exist.</returns>
    protected static string? GetDocumentPath(Guid nodeKey, IEntityService entityService) =>
        entityService.GetAllPaths(UmbracoObjectTypes.Document, nodeKey).FirstOrDefault()?.Path;
```

- [ ] **Step 5: Classify in the effective controller**

In `AdvancedPermissionsEffectiveController.cs`:
- add `using Umbraco.Community.AdvancedPermissions.Services.StartNodes;`
- add the parameter `IStartNodeAccessService startNodeAccessService` to the primary constructor (after `entityService`) and a matching `<param name="startNodeAccessService">Classifies the node against the subject's start nodes.</param>` to the class XML docs.
- in **both** actions, change the early return to
  `return Ok(new EffectivePermissionsResponseModel(nodeKey, [], nameof(StartNodeAccess.NotVisible)));`
- in `GetEffectiveForUser`, replace the final `return` with:

```csharp
        var access = Classify(await startNodeAccessService.GetForUserAsync(userKey), nodeKey);
        return Ok(new EffectivePermissionsResponseModel(nodeKey, items, access.ToString()));
```

- in `GetEffectiveForRole`, replace the final `return` with:

```csharp
        var access = Classify(await startNodeAccessService.GetForUserGroupAsync(roleAlias), nodeKey);
        return Ok(new EffectivePermissionsResponseModel(nodeKey, items, access.ToString()));
```

- add this private method to the controller:

```csharp
    /// <summary>
    /// Classifies a node for a subject. A subject that cannot be found reports <c>Accessible</c>, so
    /// the viewer never shows a restriction it could not verify.
    /// </summary>
    /// <param name="snapshot">The subject's start-node snapshot, or <see langword="null"/> when unknown.</param>
    /// <param name="nodeKey">The node key.</param>
    /// <returns>The node's start-node access.</returns>
    private StartNodeAccess Classify(StartNodeSnapshot? snapshot, Guid nodeKey)
    {
        if (snapshot is null)
        {
            return StartNodeAccess.Accessible;
        }

        var path = GetDocumentPath(nodeKey, entityService);
        return path is null ? StartNodeAccess.NotVisible : startNodeAccessService.Classify(snapshot, path);
    }
```

- update both actions' `<returns>` docs to mention the start-node access.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/Umbraco.Community.AdvancedPermissions.Tests --filter "FullyQualifiedName~AdvancedPermissionsEffectiveControllerTests"`
Expected: PASS, the existing tests included.

- [ ] **Step 7: Leave uncommitted**

Run `dotnet build` (0 errors). No commit.

---

### Task 5: `startNodeAccess` on the Insert Options audit

**Files:**
- Modify: `src/Umbraco.Community.AdvancedPermissions/Controllers/Models/DocTypePermissionResponseModels.cs:83-91`
- Modify: `src/Umbraco.Community.AdvancedPermissions/Controllers/DocTypePermissionsController.cs` (constructor and `AuditForNode`, lines 328-412)
- Test: `tests/Umbraco.Community.AdvancedPermissions.Tests/DocTypeAuditStartNodeTests.cs`

**Interfaces:**
- Consumes: `IStartNodeAccessService`, `GetDocumentPath` (Tasks 2, 4).
- Produces: `DocTypeAuditForNodeResponseModel(Guid NodeKey, IReadOnlyList<DocTypeAuditForNodeRowResponseModel> Results, string StartNodeAccess)`.

- [ ] **Step 1: Write the failing tests**

Open `DocTypePermissionsController.cs` and note the full primary-constructor parameter list (it includes at least `IDocTypePermissionService docTypeService`, `IEntityService entityService`, `IContentTypeService contentTypeService` and `IUserService userService`). Create `tests/Umbraco.Community.AdvancedPermissions.Tests/DocTypeAuditStartNodeTests.cs`, building the controller with one `Substitute.For<T>()` per existing constructor parameter, in order, plus a final `IStartNodeAccessService`:

```csharp
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.Controllers;
using Umbraco.Community.AdvancedPermissions.Controllers.Models;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Services.StartNodes;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests that <c>GET doc-type-permissions/audit-for-node</c> reports whether the subject can reach the
/// parent node through its start nodes (issue #66). Creating under a node needs access to that node.
/// </summary>
public sealed class DocTypeAuditStartNodeTests
{
    /// <summary>The entity service substitute.</summary>
    private readonly IEntityService _entityService = Substitute.For<IEntityService>();

    /// <summary>The user service substitute.</summary>
    private readonly IUserService _userService = Substitute.For<IUserService>();

    /// <summary>The start-node service substitute.</summary>
    private readonly IStartNodeAccessService _startNodes = Substitute.For<IStartNodeAccessService>();

    /// <summary>The system under test.</summary>
    private readonly DocTypePermissionsController _sut;

    /// <summary>Initializes a new instance of the <see cref="DocTypeAuditStartNodeTests"/> class.</summary>
    public DocTypeAuditStartNodeTests()
    {
        // Replace this argument list with the controller's real parameters, in order: substitutes for
        // the ones not listed as fields, the fields for IEntityService and IUserService, and
        // _startNodes last.
        _sut = new DocTypePermissionsController(
            Substitute.For<IDocTypePermissionService>(),
            _entityService,
            Substitute.For<IContentTypeService>(),
            _userService,
            _startNodes);
    }

    /// <summary>Auditing the content root classifies path <c>-1</c> for the user.</summary>
    [Fact]
    public async Task AuditForNode_ContentRoot_ClassifiesRootPath()
    {
        var user = Substitute.For<IUser>();
        var userKey = Guid.NewGuid();
        user.Key.Returns(userKey);
        user.Groups.Returns([]);
        _userService.GetAsync(userKey).Returns(user);
        var snapshot = StartNodeSnapshot.NotApplicable with { State = StartNodeSubjectState.Restricted };
        _startNodes.GetForUserAsync(userKey).Returns(snapshot);
        _startNodes.Classify(snapshot, "-1").Returns(StartNodeAccess.PathOnly);

        var result = await _sut.AuditForNode(CancellationToken.None, AdvancedPermissionsConstants.VirtualRootNodeKey, userKey);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("PathOnly", Assert.IsType<DocTypeAuditForNodeResponseModel>(ok.Value).StartNodeAccess);
    }

    /// <summary>Review focus 4: the All Users group is never blocked.</summary>
    [Fact]
    public async Task AuditForNode_Everyone_IsAccessible()
    {
        _startNodes.GetForUserGroupAsync(AdvancedPermissionsConstants.EveryoneRoleAlias).Returns(StartNodeSnapshot.NotApplicable);
        _startNodes.Classify(StartNodeSnapshot.NotApplicable, Arg.Any<string>()).Returns(StartNodeAccess.Accessible);

        var result = await _sut.AuditForNode(
            CancellationToken.None, AdvancedPermissionsConstants.VirtualRootNodeKey, null, AdvancedPermissionsConstants.EveryoneRoleAlias);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Accessible", Assert.IsType<DocTypeAuditForNodeResponseModel>(ok.Value).StartNodeAccess);
    }
}
```

If `AuditForNode` needs `IContentTypeService.GetAll()` or the allowed-children lookup to return something non-null, stub them to empty collections in the constructor; the audit then returns zero rows, which is fine for these tests.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Umbraco.Community.AdvancedPermissions.Tests --filter "FullyQualifiedName~DocTypeAuditStartNodeTests"`
Expected: build FAILS: no constructor with `IStartNodeAccessService`, no `StartNodeAccess` on the model.

- [ ] **Step 3: Extend the audit response model**

Replace `DocTypeAuditForNodeResponseModel` in `DocTypePermissionResponseModels.cs` with:

```csharp
/// <summary>
/// Top-level response of <c>GET /doc-type-permissions/audit-for-node</c>: the audited node
/// key plus one row per non-element doc type.
/// </summary>
/// <param name="NodeKey">The audited node (or virtual root).</param>
/// <param name="Results">One row per non-element doc-type.</param>
/// <param name="StartNodeAccess">
/// Whether the subject can reach the audited node through its start nodes: <c>Accessible</c>,
/// <c>PathOnly</c> or <c>NotVisible</c>. Creating under a node needs access to that node, so a
/// non-accessible node blocks every row.
/// </param>
public sealed record DocTypeAuditForNodeResponseModel(
    Guid NodeKey,
    IReadOnlyList<DocTypeAuditForNodeRowResponseModel> Results,
    string StartNodeAccess);
```

- [ ] **Step 4: Classify in `AuditForNode`**

In `DocTypePermissionsController.cs`:
- add `using Umbraco.Community.AdvancedPermissions.Services.StartNodes;`
- add `IStartNodeAccessService startNodeAccessService` as the last primary-constructor parameter, with a `<param>` doc.
- replace the final `return Ok(new DocTypeAuditForNodeResponseModel(nodeKey, rows));` with:

```csharp
        var snapshot = hasUser
            ? await startNodeAccessService.GetForUserAsync(userKey!.Value)
            : await startNodeAccessService.GetForUserGroupAsync(roleAlias!);
        var nodePath = isVirtualRoot ? Constants.System.RootString : GetDocumentPath(nodeKey, entityService);
        var access = snapshot is null
            ? StartNodeAccess.Accessible
            : nodePath is null
                ? StartNodeAccess.NotVisible
                : startNodeAccessService.Classify(snapshot, nodePath);

        return Ok(new DocTypeAuditForNodeResponseModel(nodeKey, rows, access.ToString()));
```

Add `using Umbraco.Cms.Core;` if `Constants` is not yet in scope.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/Umbraco.Community.AdvancedPermissions.Tests`
Expected: PASS, the whole project (fixes any other test that constructs `DocTypePermissionsController`; `grep -rn "new DocTypePermissionsController" tests` first, and add the extra substitute there too).

- [ ] **Step 6: Leave uncommitted**

No commit.

---

### Task 6: Live refresh after a user group's start node changes

**Files:**
- Modify: `src/Umbraco.Community.AdvancedPermissions/ServerEvents/AccessServerEventHandler.cs`
- Modify: `src/Umbraco.Community.AdvancedPermissions/Composing/AdvancedPermissionsComposer.cs:200-205`
- Test: `tests/Umbraco.Community.AdvancedPermissions.Tests/ServerEventHandlerTests.cs` (class `AccessServerEventHandlerTests`)

**Interfaces:**
- Produces: `AccessServerEventHandler` also implements `INotificationAsyncHandler<UserGroupWithUsersSavedNotification>`, routing one `Access` event per saved group key.

- [ ] **Step 1: Write the failing test**

In `AccessServerEventHandlerTests`, after `UserGroupSaved_RoutesAccessEvent`, add:

```csharp
    /// <summary>
    /// A user group saved together with its users is also routed. Umbraco clears its cached start
    /// nodes for the group's users on this notification, not on <c>UserGroupSavedNotification</c>,
    /// which it publishes first; routing here lets a viewer refetch after the cache is cleared, so a
    /// changed start node shows up live (issue #66).
    /// </summary>
    [Fact]
    public async Task UserGroupWithUsersSaved_RoutesAccessEvent()
    {
        var router = Substitute.For<IServerEventRouter>();
        var handler = CreateHandler(router);
        var groupKey = Guid.NewGuid();

        await handler.HandleAsync(
            new UserGroupWithUsersSavedNotification(new UserGroupWithUsers(BuildGroup(groupKey), [], []), new EventMessages()),
            CancellationToken.None);

        await router.Received(1).RouteEventAsync(Arg.Is<ServerEvent>(e =>
            e.EventSource == AdvancedPermissionsServerEvents.AccessSource &&
            e.EventType == AdvancedPermissionsServerEvents.EventType.Updated &&
            e.Key == groupKey));
    }
```

Add `using Umbraco.Cms.Core.Events;` if `UserGroupWithUsers` is not yet in scope (it lives in `Umbraco.Core/Events/UserGroupWithUsers.cs`).

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Umbraco.Community.AdvancedPermissions.Tests --filter "FullyQualifiedName~AccessServerEventHandlerTests"`
Expected: build FAILS: no `HandleAsync(UserGroupWithUsersSavedNotification, ...)` overload.

- [ ] **Step 3: Handle the notification**

In `AccessServerEventHandler.cs`, add `INotificationAsyncHandler<UserGroupWithUsersSavedNotification>,` to the implemented interfaces (after `UserGroupSavedNotification`), and add after the `UserGroupSavedNotification` handler:

```csharp
    /// <inheritdoc />
    /// <remarks>
    /// Umbraco publishes this after <see cref="UserGroupSavedNotification"/>, and only this one clears
    /// the cached start nodes of the group's users. Routing it too means a viewer that refetches on the
    /// event reads the new start nodes rather than the cached old ones.
    /// </remarks>
    public Task HandleAsync(UserGroupWithUsersSavedNotification notification, CancellationToken cancellationToken) =>
        RouteAllAsync(notification.SavedEntities.Select(e => e.UserGroup.Key));
```

Update the class summary's "six" notifications wording where it counts them (and the test class summary that says "six").

- [ ] **Step 4: Run the handler tests, then watch the registration test fail**

Run: `dotnet test tests/Umbraco.Community.AdvancedPermissions.Tests --filter "FullyQualifiedName~AccessServerEventHandlerTests|FullyQualifiedName~ServerEventRegistrationTests"`
Expected: the handler test PASSES; `ServerEventRegistrationTests` FAILS, because the handler now implements a notification the composer does not register.

- [ ] **Step 5: Register it**

In `AdvancedPermissionsComposer.cs`, after the `UserGroupSavedNotification` registration line, add:

```csharp
        builder.AddNotificationAsyncHandler<UserGroupWithUsersSavedNotification, AccessServerEventHandler>();
```

- [ ] **Step 6: Run all backend tests**

Run: `dotnet test`
Expected: PASS, every project (also `ServerEventSourceCoverageTests`).

- [ ] **Step 7: Leave uncommitted**

No commit.

---

### Task 7: Browser models, API wrappers and the generated client

**Files:**
- Regenerate: `src/Umbraco.Community.AdvancedPermissions.Client/src/api/generated/*`
- Modify: `src/Umbraco.Community.AdvancedPermissions.Client/src/models/permission.models.ts:81-84`
- Modify: `src/Umbraco.Community.AdvancedPermissions.Client/src/models/doc-type-permission.models.ts:49-52`
- Modify: `src/Umbraco.Community.AdvancedPermissions.Client/src/api/advanced-permissions.api.ts`

**Interfaces:**
- Produces (TS): `type StartNodeAccess = 'Accessible' | 'PathOnly' | 'NotVisible'`; `type StartNodeSubjectState = 'NotApplicable' | 'RootAccess' | 'Restricted' | 'NoAccess'`; `type StartNodeSourceStatus = 'InUse' | 'NoStartNode' | 'NotSet' | 'CoveredBy' | 'ReplacedByUserStartNode' | 'NarrowedBy' | 'Ignored' | 'NotApplied'`; `interface StartNodeItem { key: string; name: string }`; `interface StartNodeSourceItem { kind: 'UserGroup' | 'User'; userGroupAlias: string | null; userGroupName: string | null; startNodeKey: string | null; startNodeName: string | null; isRoot: boolean; status: StartNodeSourceStatus; relatedNodeKey: string | null; relatedNodeName: string | null; relatedIsRoot: boolean }`; `interface StartNodesResponse { state: StartNodeSubjectState; startNodes: StartNodeItem[]; sources: StartNodeSourceItem[] }`; `EffectivePermissions.startNodeAccess: StartNodeAccess`; `DocTypeAuditForNodeResponse.startNodeAccess: StartNodeAccess`; `getStartNodes(subject: { userKey: string } | { roleAlias: string }, signal?: AbortSignal): Promise<StartNodesResponse>`.

- [ ] **Step 1: Regenerate the OpenAPI client**

Start the test site on the HTTP profile in the background: `dotnet run --project tests/Umbraco.Community.AdvancedPermissions.TestSite --launch-profile http` (serves `http://localhost:5266`; use the profile whose `applicationUrl` is `http://localhost:5266` in `Properties/launchSettings.json`). When it is up, in `src/Umbraco.Community.AdvancedPermissions.Client` run `npm run generate-client`, then stop the site.
Expected: `src/api/generated/sdk.gen.ts` now has `getStartNodesForUser` and `getStartNodesForRole`; `types.gen.ts` has `StartNodesResponseModel` and a `startNodeAccess` field on `EffectivePermissionsResponseModel` and `DocTypeAuditForNodeResponseModel`.

- [ ] **Step 2: Add the models**

In `permission.models.ts`, replace the `EffectivePermissions` interface and add the start-node types after it:

```ts
/** The effective permissions of a subject at a node, plus whether start nodes let it reach the node. */
export interface EffectivePermissions {
  nodeKey: string;
  permissions: EffectivePermission[];
  /** Whether the subject can reach the node through its start nodes. */
  startNodeAccess: StartNodeAccess;
}

/** Whether a subject can reach a node through its start nodes. */
export type StartNodeAccess = 'Accessible' | 'PathOnly' | 'NotVisible';

/** The overall start-node situation of a subject. */
export type StartNodeSubjectState = 'NotApplicable' | 'RootAccess' | 'Restricted' | 'NoAccess';

/** What became of one configured start node after Umbraco combined them. */
export type StartNodeSourceStatus =
  | 'InUse'
  | 'NoStartNode'
  | 'NotSet'
  | 'CoveredBy'
  | 'ReplacedByUserStartNode'
  | 'NarrowedBy'
  | 'Ignored'
  | 'NotApplied';

/** One effective start node. */
export interface StartNodeItem {
  key: string;
  name: string;
}

/** One configured start node and what became of it. */
export interface StartNodeSourceItem {
  kind: 'UserGroup' | 'User';
  userGroupAlias: string | null;
  userGroupName: string | null;
  startNodeKey: string | null;
  startNodeName: string | null;
  isRoot: boolean;
  status: StartNodeSourceStatus;
  relatedNodeKey: string | null;
  relatedNodeName: string | null;
  relatedIsRoot: boolean;
}

/** The start nodes of a user or user group, for the start-node strip. */
export interface StartNodesResponse {
  state: StartNodeSubjectState;
  startNodes: StartNodeItem[];
  sources: StartNodeSourceItem[];
}
```

In `doc-type-permission.models.ts`, add `import type { StartNodeAccess } from './permission.models.js';` and replace `DocTypeAuditForNodeResponse` with:

```ts
export interface DocTypeAuditForNodeResponse {
  nodeKey: string;
  results: DocTypeAuditForNodeRow[];
  /** Whether the subject can reach the parent node through its start nodes. */
  startNodeAccess: StartNodeAccess;
}
```

- [ ] **Step 3: Add the API wrapper**

In `advanced-permissions.api.ts`, add `StartNodesResponse` to the model imports and add after `getEffectiveForRole`:

```ts
/**
 * Gets the start nodes of a user (combined over all their groups) or of one user group on its own.
 * @param subject The user key or the user group alias.
 * @param signal Aborts the request when the selection changes.
 * @returns The effective start nodes and one explained line per configured start node.
 */
export async function getStartNodes(
  subject: { userKey: string } | { roleAlias: string },
  signal?: AbortSignal,
): Promise<StartNodesResponse> {
  const { data } = 'userKey' in subject
    ? await AdvancedPermissionsService.getStartNodesForUser({
        throwOnError: true,
        query: { userKey: subject.userKey },
        ...(signal ? { signal } : {}),
      })
    : await AdvancedPermissionsService.getStartNodesForRole({
        throwOnError: true,
        query: { roleAlias: subject.roleAlias },
        ...(signal ? { signal } : {}),
      });
  return data as StartNodesResponse;
}
```

- [ ] **Step 4: Type-check**

Run (in the Client folder): `npx tsc --noEmit`
Expected: no errors.

- [ ] **Step 5: Leave uncommitted**

No commit.

---

### Task 8: Pure display rules (Vitest)

**Files:**
- Create: `src/Umbraco.Community.AdvancedPermissions.Client/src/start-nodes/start-node-display.ts`
- Test: `src/Umbraco.Community.AdvancedPermissions.Client/src/start-nodes/start-node-display.test.ts`

**Interfaces:**
- Consumes: the Task 7 types.
- Produces: `isBlocked(access: StartNodeAccess | null | undefined): boolean`; `type StartNodeBadge = 'startNode' | 'pathOnly' | 'notVisible' | null`; `rowBadge(nodeKey: string, access: StartNodeAccess | null | undefined, startNodes: StartNodesResponse | null): StartNodeBadge`; `interface TermRef { key: string; args: string[] }`; `subjectStateTerm(state: StartNodeSubjectState): string | null`; `sourceStatusTerm(source: StartNodeSourceItem, rootLabel: string): TermRef`; `sourceNodeLabel(source: StartNodeSourceItem, rootLabel: string): string | null`; `blockedBannerTerm(access: StartNodeAccess): string`; `blockedNoteTerm(startNodes: StartNodesResponse | null): TermRef`.

- [ ] **Step 1: Write the failing tests**

Create `src/start-nodes/start-node-display.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import type { StartNodeSourceItem, StartNodesResponse } from '../models/permission.models.js';
import {
  blockedBannerTerm,
  blockedNoteTerm,
  isBlocked,
  rowBadge,
  sourceNodeLabel,
  sourceStatusTerm,
  subjectStateTerm,
} from './start-node-display.js';

const source = (over: Partial<StartNodeSourceItem>): StartNodeSourceItem => ({
  kind: 'UserGroup',
  userGroupAlias: 'editors',
  userGroupName: 'Editors',
  startNodeKey: 'aaa',
  startNodeName: 'Home',
  isRoot: false,
  status: 'InUse',
  relatedNodeKey: null,
  relatedNodeName: null,
  relatedIsRoot: false,
  ...over,
});

const restricted: StartNodesResponse = {
  state: 'Restricted',
  startNodes: [{ key: 'AAA', name: 'Home' }],
  sources: [],
};

describe('isBlocked', () => {
  it('blocks path-only and not-visible nodes', () => {
    expect(isBlocked('PathOnly')).toBe(true);
    expect(isBlocked('NotVisible')).toBe(true);
  });
  it('does not block accessible or not-yet-loaded nodes', () => {
    expect(isBlocked('Accessible')).toBe(false);
    expect(isBlocked(null)).toBe(false);
    expect(isBlocked(undefined)).toBe(false);
  });
});

describe('rowBadge', () => {
  it('marks blocked rows by their kind', () => {
    expect(rowBadge('x', 'PathOnly', restricted)).toBe('pathOnly');
    expect(rowBadge('x', 'NotVisible', restricted)).toBe('notVisible');
  });
  it('marks a start node regardless of key casing', () => {
    expect(rowBadge('aaa', 'Accessible', restricted)).toBe('startNode');
  });
  it('leaves ordinary accessible rows alone', () => {
    expect(rowBadge('bbb', 'Accessible', restricted)).toBeNull();
    expect(rowBadge('aaa', 'Accessible', null)).toBeNull();
  });
});

describe('subjectStateTerm', () => {
  it('names the whole-subject states', () => {
    expect(subjectStateTerm('RootAccess')).toBe('uap_startNodes_stateRoot');
    expect(subjectStateTerm('NoAccess')).toBe('uap_startNodes_stateNoAccess');
    expect(subjectStateTerm('NotApplicable')).toBe('uap_startNodes_stateNotApplicable');
  });
  it('has no line for a restricted subject (the start nodes say it)', () => {
    expect(subjectStateTerm('Restricted')).toBeNull();
  });
});

describe('sourceStatusTerm', () => {
  it('passes the related node for covered, replaced and narrowed lines', () => {
    expect(sourceStatusTerm(source({ status: 'CoveredBy', relatedNodeName: 'Home' }), 'Root'))
      .toEqual({ key: 'uap_startNodes_statusCoveredBy', args: ['Home'] });
    expect(sourceStatusTerm(source({ status: 'ReplacedByUserStartNode', relatedNodeName: 'News' }), 'Root'))
      .toEqual({ key: 'uap_startNodes_statusReplaced', args: ['News'] });
    expect(sourceStatusTerm(source({ status: 'NarrowedBy', relatedNodeName: 'News' }), 'Root'))
      .toEqual({ key: 'uap_startNodes_statusNarrowedBy', args: ['News'] });
  });
  it('uses the root label when the related node is the root', () => {
    expect(sourceStatusTerm(source({ status: 'CoveredBy', relatedIsRoot: true }), 'Content root'))
      .toEqual({ key: 'uap_startNodes_statusCoveredBy', args: ['Content root'] });
  });
  it('maps the plain statuses', () => {
    expect(sourceStatusTerm(source({ status: 'InUse' }), 'R').key).toBe('uap_startNodes_statusInUse');
    expect(sourceStatusTerm(source({ status: 'NoStartNode' }), 'R').key).toBe('uap_startNodes_statusNoStartNode');
    expect(sourceStatusTerm(source({ status: 'NotSet' }), 'R').key).toBe('uap_startNodes_statusNotSet');
    expect(sourceStatusTerm(source({ status: 'Ignored' }), 'R').key).toBe('uap_startNodes_statusIgnored');
    expect(sourceStatusTerm(source({ status: 'NotApplied' }), 'R').key).toBe('uap_startNodes_statusNotApplied');
  });
});

describe('sourceNodeLabel', () => {
  it('names the root, a node, or nothing', () => {
    expect(sourceNodeLabel(source({ isRoot: true, startNodeName: null }), 'Content root')).toBe('Content root');
    expect(sourceNodeLabel(source({}), 'Content root')).toBe('Home');
    expect(sourceNodeLabel(source({ startNodeName: null }), 'Content root')).toBeNull();
  });
});

describe('blocked dialog terms', () => {
  it('picks the banner by kind', () => {
    expect(blockedBannerTerm('PathOnly')).toBe('uap_startNodes_blockedPathOnly');
    expect(blockedBannerTerm('NotVisible')).toBe('uap_startNodes_blockedNotVisible');
  });
  it('lists the start nodes, or says there are none', () => {
    expect(blockedNoteTerm(restricted)).toEqual({ key: 'uap_startNodes_blockedNote', args: ['Home'] });
    expect(blockedNoteTerm({ state: 'NoAccess', startNodes: [], sources: [] }))
      .toEqual({ key: 'uap_startNodes_blockedNoAccessNote', args: [] });
    expect(blockedNoteTerm(null)).toEqual({ key: 'uap_startNodes_blockedNoListNote', args: [] });
  });
});
```

- [ ] **Step 2: Run the tests to verify they fail**

Run (Client folder): `npx vitest run src/start-nodes`
Expected: FAIL: `Failed to resolve import "./start-node-display.js"`.

- [ ] **Step 3: Write the module**

Create `src/start-nodes/start-node-display.ts`:

```ts
import type {
  StartNodeAccess,
  StartNodeSourceItem,
  StartNodeSubjectState,
  StartNodesResponse,
} from '../models/permission.models.js';

/**
 * Display decisions for start nodes, kept out of the Lit elements so they can be tested without a DOM.
 * The server decides reachability; this module only turns its answers into what the viewers show.
 */

/** A localization key plus its arguments. */
export interface TermRef {
  key: string;
  args: string[];
}

/** The marker a viewer row shows for start nodes. */
export type StartNodeBadge = 'startNode' | 'pathOnly' | 'notVisible' | null;

/**
 * True when start nodes stop the subject from reaching the node, whatever the permissions say.
 * @param access The node's start-node access; null or undefined while it is still loading.
 * @returns Whether the node's cells render as blocked.
 */
export function isBlocked(access: StartNodeAccess | null | undefined): boolean {
  return access === 'PathOnly' || access === 'NotVisible';
}

/**
 * Picks the start-node marker for a row.
 * @param nodeKey The row's node key.
 * @param access The node's start-node access.
 * @param startNodes The subject's start nodes, or null when not loaded.
 * @returns The badge to show, or null for none.
 */
export function rowBadge(
  nodeKey: string,
  access: StartNodeAccess | null | undefined,
  startNodes: StartNodesResponse | null,
): StartNodeBadge {
  if (access === 'PathOnly') return 'pathOnly';
  if (access === 'NotVisible') return 'notVisible';
  const key = nodeKey.toLowerCase();
  if (startNodes?.startNodes.some((n) => n.key.toLowerCase() === key)) return 'startNode';
  return null;
}

/**
 * The one-line description of a whole-subject state.
 * @param state The subject's start-node state.
 * @returns The localization key, or null for a restricted subject (its start nodes speak for it).
 */
export function subjectStateTerm(state: StartNodeSubjectState): string | null {
  switch (state) {
    case 'RootAccess':
      return 'uap_startNodes_stateRoot';
    case 'NoAccess':
      return 'uap_startNodes_stateNoAccess';
    case 'NotApplicable':
      return 'uap_startNodes_stateNotApplicable';
    default:
      return null;
  }
}

/**
 * The status text of one start-node source line.
 * @param source The source line.
 * @param rootLabel The localized name of the content root.
 * @returns The localization key and its arguments.
 */
export function sourceStatusTerm(source: StartNodeSourceItem, rootLabel: string): TermRef {
  const related = source.relatedIsRoot ? rootLabel : (source.relatedNodeName ?? '');
  switch (source.status) {
    case 'InUse':
      return { key: 'uap_startNodes_statusInUse', args: [] };
    case 'NoStartNode':
      return { key: 'uap_startNodes_statusNoStartNode', args: [] };
    case 'NotSet':
      return { key: 'uap_startNodes_statusNotSet', args: [] };
    case 'CoveredBy':
      return { key: 'uap_startNodes_statusCoveredBy', args: [related] };
    case 'ReplacedByUserStartNode':
      return { key: 'uap_startNodes_statusReplaced', args: [related] };
    case 'NarrowedBy':
      return { key: 'uap_startNodes_statusNarrowedBy', args: [related] };
    case 'Ignored':
      return { key: 'uap_startNodes_statusIgnored', args: [] };
    default:
      return { key: 'uap_startNodes_statusNotApplied', args: [] };
  }
}

/**
 * The configured node of a source line.
 * @param source The source line.
 * @param rootLabel The localized name of the content root.
 * @returns The node name, the root label, or null when there is no (existing) node.
 */
export function sourceNodeLabel(source: StartNodeSourceItem, rootLabel: string): string | null {
  if (source.isRoot) return rootLabel;
  return source.startNodeName;
}

/**
 * The banner of the reasoning dialog for a blocked cell.
 * @param access A blocked start-node access.
 * @returns The localization key.
 */
export function blockedBannerTerm(access: StartNodeAccess): string {
  return access === 'PathOnly' ? 'uap_startNodes_blockedPathOnly' : 'uap_startNodes_blockedNotVisible';
}

/**
 * The info note of the reasoning dialog for a blocked cell: the subject's start nodes.
 * @param startNodes The subject's start nodes, or null when they could not be loaded.
 * @returns The localization key and its arguments.
 */
export function blockedNoteTerm(startNodes: StartNodesResponse | null): TermRef {
  if (!startNodes) return { key: 'uap_startNodes_blockedNoListNote', args: [] };
  if (startNodes.startNodes.length === 0) return { key: 'uap_startNodes_blockedNoAccessNote', args: [] };
  return { key: 'uap_startNodes_blockedNote', args: [startNodes.startNodes.map((n) => n.name).join(', ')] };
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `npx vitest run src/start-nodes`
Expected: PASS.

- [ ] **Step 5: Leave uncommitted**

No commit.

---

### Task 9: Localization terms

**Files:**
- Modify: all 28 files in `src/Umbraco.Community.AdvancedPermissions.Client/src/localization/` (after the `notAnInsertOptionDeniedNote` line, line 143 in every file)

**Interfaces:**
- Produces: the `uap_startNodes_*` keys used by Tasks 8, 10, 11, 12.

- [ ] **Step 1: Add the English block to `en.ts`**

After the `notAnInsertOptionDeniedNote` line, insert:

```ts

    // ── Start nodes (issue #66) ───────────────────────────────────────────
    startNodes_label: 'Start nodes',
    startNodes_loading: 'Loading start nodes\u2026',
    startNodes_error: "Start nodes couldn't be loaded.",
    startNodes_rootLabel: 'Content root',
    startNodes_userSpecific: 'User-specific',
    startNodes_stateRoot: 'Root access: every node is reachable.',
    startNodes_stateNoAccess: 'No start nodes, so no content access.',
    startNodes_stateNotApplicable: "Start nodes don't apply to the All Users group.",
    startNodes_roleModeNote: 'Users get the combined start nodes of all their groups. Pick a user to see the real result.',
    startNodes_statusInUse: 'in use',
    startNodes_statusNoStartNode: 'no start node (gives no content access)',
    startNodes_statusNotSet: 'none',
    startNodes_statusCoveredBy: (node: string) => `covered by ${node}`,
    startNodes_statusReplaced: (node: string) => `replaced by the user's own start node ${node}`,
    startNodes_statusNarrowedBy: (node: string) => `narrowed to ${node}`,
    startNodes_statusIgnored: 'ignored: in the recycle bin or deleted',
    startNodes_statusNotApplied: 'not applied',
    startNodes_badgeStartNode: 'Start node',
    startNodes_badgePathOnly: 'Path only',
    startNodes_badgePathOnlyTitle: "Shown only so they can browse to a start node. It can't be opened.",
    startNodes_badgeNotVisible: 'Not visible (start nodes)',
    startNodes_badgeNotVisibleTitle: 'Outside every start node, so hidden.',
    startNodes_blockedCellTitle: 'Blocked by start nodes \u2014 click for details',
    startNodes_blockedNotVisible: (subject: string, verb: string, nodePath: string) =>
      `${subject} can't use ${verb} on \u201c${nodePath}\u201d: this node is outside their start nodes.`,
    startNodes_blockedPathOnly: (subject: string, verb: string, nodePath: string) =>
      `${subject} can't use ${verb} on \u201c${nodePath}\u201d: this node is only shown so they can browse to their start node.`,
    startNodes_blockedNote: (nodes: string) => `Start nodes: ${nodes}.`,
    startNodes_blockedNoAccessNote: 'There are no start nodes, so there is no content access at all.',
    startNodes_blockedNoListNote: "The start nodes couldn't be loaded.",
    startNodes_ifInsideHeading: 'If this node were inside a start node:',
```

- [ ] **Step 2: Add the Dutch block to `nl.ts`**

Same position:

```ts

    // ── Startnodes (issue #66) ────────────────────────────────────────────
    startNodes_label: 'Startnodes',
    startNodes_loading: 'Startnodes laden\u2026',
    startNodes_error: 'De startnodes konden niet worden geladen.',
    startNodes_rootLabel: 'Contentroot',
    startNodes_userSpecific: 'Gebruikersspecifiek',
    startNodes_stateRoot: 'Roottoegang: elke node is bereikbaar.',
    startNodes_stateNoAccess: 'Geen startnodes, dus geen toegang tot content.',
    startNodes_stateNotApplicable: 'Startnodes gelden niet voor de groep Alle gebruikers.',
    startNodes_roleModeNote: 'Gebruikers krijgen de gecombineerde startnodes van al hun groepen. Kies een gebruiker om het echte resultaat te zien.',
    startNodes_statusInUse: 'in gebruik',
    startNodes_statusNoStartNode: 'geen startnode (geeft geen toegang tot content)',
    startNodes_statusNotSet: 'geen',
    startNodes_statusCoveredBy: (node: string) => `valt onder ${node}`,
    startNodes_statusReplaced: (node: string) => `vervangen door de eigen startnode van de gebruiker ${node}`,
    startNodes_statusNarrowedBy: (node: string) => `beperkt tot ${node}`,
    startNodes_statusIgnored: 'genegeerd: in de prullenbak of verwijderd',
    startNodes_statusNotApplied: 'niet toegepast',
    startNodes_badgeStartNode: 'Startnode',
    startNodes_badgePathOnly: 'Alleen pad',
    startNodes_badgePathOnlyTitle: 'Alleen zichtbaar om naar een startnode te navigeren. Kan niet worden geopend.',
    startNodes_badgeNotVisible: 'Niet zichtbaar (startnodes)',
    startNodes_badgeNotVisibleTitle: 'Buiten alle startnodes, dus verborgen.',
    startNodes_blockedCellTitle: 'Geblokkeerd door startnodes \u2014 klik voor details',
    startNodes_blockedNotVisible: (subject: string, verb: string, nodePath: string) =>
      `${subject} kan ${verb} niet gebruiken op \u201c${nodePath}\u201d: deze node valt buiten de startnodes.`,
    startNodes_blockedPathOnly: (subject: string, verb: string, nodePath: string) =>
      `${subject} kan ${verb} niet gebruiken op \u201c${nodePath}\u201d: deze node is alleen zichtbaar om naar een startnode te navigeren.`,
    startNodes_blockedNote: (nodes: string) => `Startnodes: ${nodes}.`,
    startNodes_blockedNoAccessNote: 'Er zijn geen startnodes, dus er is helemaal geen toegang tot content.',
    startNodes_blockedNoListNote: 'De startnodes konden niet worden geladen.',
    startNodes_ifInsideHeading: 'Als deze node binnen een startnode zou vallen:',
```

- [ ] **Step 3: Add the English block to the other 26 files**

Insert the exact English block from Step 1 at the same position in `ar, bs, cs, cy, da, de, en-us, es, fr, he, hr, it, ja, ko, nb, pl, pt-br, pt, ro, ru, sv, tr, uk, vi, zh-tw, zh`. (These files already carry English fallbacks for package terms; translation is out of scope.) Then check that the files are consistent:

Run (Client folder, Git Bash): `grep -c "startNodes_" src/localization/*.ts`
Expected: `30` for every one of the 28 files.

- [ ] **Step 4: Type-check and leave uncommitted**

Run: `npx tsc --noEmit`
Expected: no errors. No commit.

---

### Task 10: Blocked cell, row badge, strip and blocked dialog

**Files:**
- Modify: `src/Umbraco.Community.AdvancedPermissions.Client/src/shared/components/uap-perm-block.element.ts`
- Modify: `src/Umbraco.Community.AdvancedPermissions.Client/src/shared/components/uap-reasoning-dialog.element.ts`
- Create: `src/Umbraco.Community.AdvancedPermissions.Client/src/start-nodes/uap-start-node-badge.element.ts`
- Create: `src/Umbraco.Community.AdvancedPermissions.Client/src/start-nodes/uap-start-node-strip.element.ts`

**Interfaces:**
- Consumes: Tasks 7-9.
- Produces: `<uap-perm-block blocked blocked-title="…">`; dialog properties `startNodeAccess: StartNodeAccess` (default `'Accessible'`) and `startNodes: StartNodesResponse | null`; `<uap-start-node-badge .badge=${StartNodeBadge}>`; `<uap-start-node-strip .data=${StartNodesResponse | null} ?loading ?error subject-kind="role|user">`.

These are Lit elements; per `vitest.config.ts` they are not unit-tested (their decisions live in Task 8). They are verified by `tsc` here and in the browser in Task 13.

- [ ] **Step 1: Add the blocked state to `uap-perm-block`**

Add the doc bullet and display priority to the class comment (`loading` > `blocked` > `na` > `info`), then add these properties after `outsideAllowed`:

```ts
  /**
   * When true (and `loading` is false), renders the neutral "blocked by start nodes" cell: hatched
   * grey with a lock, never green or red, because the subject cannot reach the node at all.
   */
  @property({ type: Boolean }) blocked = false;

  /** Tooltip for a blocked cell. */
  @property({ attribute: 'blocked-title' }) blockedTitle = '';
```

In `render()`, right after the `loading` branch:

```ts
    if (this.blocked) {
      return html`<div class="perm-block uniform blocked" title=${this.blockedTitle}>
        <umb-icon name="icon-lock"></umb-icon>
      </div>`;
    }
```

Add to `styles`, after the `.na` rule:

```css
    /* ── Blocked by start nodes ───────────────────────────────────
       Neutral, hatched and solid-bordered: distinct from N/A (dotted) and never green or red,
       because the subject cannot reach the node whatever the permissions say. Clickable. */
    .perm-block.blocked {
      background: repeating-linear-gradient(
        45deg,
        color-mix(in srgb, var(--uui-color-text-alt, #888) 6%, transparent),
        color-mix(in srgb, var(--uui-color-text-alt, #888) 6%, transparent) 4px,
        color-mix(in srgb, var(--uui-color-text-alt, #888) 18%, transparent) 4px,
        color-mix(in srgb, var(--uui-color-text-alt, #888) 18%, transparent) 8px
      );
      color: var(--uui-color-text-alt, #666);
      border-color: var(--uui-color-border-emphasis, #bbb);
      font-size: 12px;
    }
```

- [ ] **Step 2: Create the row badge**

Create `src/start-nodes/uap-start-node-badge.element.ts`:

```ts
import { html, css, nothing, customElement, property } from '@umbraco-cms/backoffice/external/lit';
import type { TemplateResult } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import type { StartNodeBadge } from './start-node-display.js';

/**
 * The start-node marker next to a node name in the viewers: a flag on a start node, or a badge on a
 * node the subject can only browse through or cannot see at all.
 */
@customElement('uap-start-node-badge')
export class UapStartNodeBadgeElement extends UmbLitElement {
  /** Which marker to show; null shows nothing. */
  @property({ attribute: false }) badge: StartNodeBadge = null;

  /** The localization controller for this element. */
  #localize = new UmbLocalizationController(this);

  override render(): TemplateResult | typeof nothing {
    switch (this.badge) {
      case 'startNode':
        return html`<umb-icon class="flag" name="icon-flag" title=${this.#localize.term('uap_startNodes_badgeStartNode')}></umb-icon>`;
      case 'pathOnly':
        return html`<span class="badge" title=${this.#localize.term('uap_startNodes_badgePathOnlyTitle')}>
          <umb-icon name="icon-navigation"></umb-icon>${this.#localize.term('uap_startNodes_badgePathOnly')}</span>`;
      case 'notVisible':
        return html`<span class="badge" title=${this.#localize.term('uap_startNodes_badgeNotVisibleTitle')}>
          <umb-icon name="icon-eye"></umb-icon>${this.#localize.term('uap_startNodes_badgeNotVisible')}</span>`;
      default:
        return nothing;
    }
  }

  static override styles = css`
    :host { display: inline-flex; align-items: center; flex-shrink: 0; }
    .flag { color: var(--uui-color-interactive, #3544b1); }
    .badge {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      margin-left: 6px;
      padding: 0 6px;
      border-radius: 4px;
      border: 1px solid var(--uui-color-border, #ddd);
      background: var(--uui-color-surface-alt, #f3f3f5);
      color: var(--uui-color-text-alt, #666);
      font-size: 11px;
      line-height: 18px;
      white-space: nowrap;
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'uap-start-node-badge': UapStartNodeBadgeElement;
  }
}
```

Before writing it, confirm `icon-flag`, `icon-navigation`, `icon-eye` and `icon-lock` exist in the v17 icon registry (`D:\github\UmbracoVersions\v17\Umbraco-CMS\src\Umbraco.Web.UI.Client\src\packages\core\icon-registry\icons.ts`); pick the closest existing name if one is missing.

- [ ] **Step 3: Create the strip**

Create `src/start-nodes/uap-start-node-strip.element.ts`:

```ts
import { html, css, nothing, customElement, property } from '@umbraco-cms/backoffice/external/lit';
import type { TemplateResult } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import type { StartNodeSourceItem, StartNodesResponse } from '../models/permission.models.js';
import { sourceNodeLabel, sourceStatusTerm, subjectStateTerm } from './start-node-display.js';

/**
 * The start-node strip shown under the subject picker in both viewers: the subject's effective
 * start nodes, and one line per configured start node explaining where it comes from and whether it
 * counts.
 */
@customElement('uap-start-node-strip')
export class UapStartNodeStripElement extends UmbLitElement {
  /** The subject's start nodes; null while loading or after a failure. */
  @property({ attribute: false }) data: StartNodesResponse | null = null;

  /** True while the start nodes load for a newly selected subject. */
  @property({ type: Boolean }) loading = false;

  /** True when the start nodes could not be loaded. */
  @property({ type: Boolean }) error = false;

  /** Whether the subject is a single user group (shows the combination note) or a user. */
  @property({ attribute: 'subject-kind' }) subjectKind: 'role' | 'user' = 'user';

  /** The localization controller for this element. */
  #localize = new UmbLocalizationController(this);

  override render(): TemplateResult | typeof nothing {
    if (this.error) {
      return html`<div class="strip"><span class="muted">${this.#localize.term('uap_startNodes_error')}</span></div>`;
    }
    if (this.loading || !this.data) {
      return this.loading
        ? html`<div class="strip"><span class="muted">${this.#localize.term('uap_startNodes_loading')}</span></div>`
        : nothing;
    }

    const data = this.data;
    const stateTerm = subjectStateTerm(data.state);
    const showSources = data.state !== 'NotApplicable' && data.sources.length > 0;

    return html`
      <div class="strip">
        <div class="effective">
          <span class="label">${this.#localize.term('uap_startNodes_label')}:</span>
          ${data.startNodes.map((n) => html`<span class="chip"><umb-icon name="icon-flag"></umb-icon>${n.name}</span>`)}
          ${stateTerm ? html`<span class="state">${this.#localize.term(stateTerm)}</span>` : nothing}
        </div>
        ${showSources ? html`<ul class="sources">${data.sources.map((s) => this.#renderSource(s))}</ul>` : nothing}
        ${this.subjectKind === 'role' && data.state !== 'NotApplicable'
          ? html`<p class="note">${this.#localize.term('uap_startNodes_roleModeNote')}</p>`
          : nothing}
      </div>
    `;
  }

  /**
   * Renders one source line: who configures it, which node, and what became of it.
   * @param source The source line.
   * @returns The list item.
   */
  #renderSource(source: StartNodeSourceItem): TemplateResult {
    const rootLabel = this.#localize.term('uap_startNodes_rootLabel');
    const who = source.kind === 'User'
      ? this.#localize.term('uap_startNodes_userSpecific')
      : (source.userGroupName ?? source.userGroupAlias ?? '');
    const node = sourceNodeLabel(source, rootLabel);
    const status = sourceStatusTerm(source, rootLabel);
    const counts = source.status === 'InUse';
    return html`
      <li class=${counts ? 'in-use' : 'not-counted'}>
        <strong>${who}</strong>${node ? html` \u2192 ${node}` : nothing}
        <span class="status">(${this.#localize.term(status.key, ...status.args)})</span>
      </li>
    `;
  }

  static override styles = css`
    :host { display: block; }
    .strip {
      padding: 8px 12px;
      margin-bottom: 12px;
      border: 1px solid var(--uui-color-border, #ddd);
      border-radius: 6px;
      background: var(--uui-color-surface-alt, #f7f7f9);
      font-size: 13px;
    }
    .effective { display: flex; flex-wrap: wrap; align-items: center; gap: 6px; }
    .label { font-weight: 600; }
    .chip {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      padding: 0 8px;
      border-radius: 10px;
      background: var(--uui-color-surface, #fff);
      border: 1px solid var(--uui-color-border, #ddd);
    }
    .chip umb-icon { color: var(--uui-color-interactive, #3544b1); }
    .state, .muted, .status, .note { color: var(--uui-color-text-alt, #666); }
    .sources { margin: 6px 0 0; padding-left: 18px; }
    .sources li.not-counted { color: var(--uui-color-text-alt, #666); }
    .note { margin: 6px 0 0; font-style: italic; }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    'uap-start-node-strip': UapStartNodeStripElement;
  }
}
```

- [ ] **Step 4: Add the blocked mode to the reasoning dialog**

In `uap-reasoning-dialog.element.ts`:
- extend the model import with `StartNodeAccess, StartNodesResponse`, and add `import { blockedBannerTerm, blockedNoteTerm, isBlocked } from '../../start-nodes/start-node-display.js';`
- add properties after `virtualRootLabel`:

```ts
  /**
   * The cell's start-node access. Anything but `Accessible` puts the dialog in blocked mode: a grey
   * banner explaining that start nodes stop the subject, the start nodes as a note, and the path
   * table greyed out as a preview of what would apply inside a start node.
   */
  @property() startNodeAccess: StartNodeAccess = 'Accessible';

  /** The subject's start nodes, listed in blocked mode; null when not loaded. */
  @property({ attribute: false }) startNodes: StartNodesResponse | null = null;
```

- in `render()`, wrap the loaded table so blocked mode greys it and adds the heading. Replace the `? html\`` branch that starts with `<div class="dialog-table-wrap">` with:

```ts
              ? html`
                  <div class=${isBlocked(this.startNodeAccess) ? 'blocked-preview' : ''}>
                    ${isBlocked(this.startNodeAccess)
                      ? html`<p class="blocked-heading">${this.#localize.term('uap_startNodes_ifInsideHeading')}</p>`
                      : nothing}
                    <div class="dialog-table-wrap">
                      <table class="dialog-table">
                        <thead>
                          <tr>
                            <th class="dialog-node-header">${this.#localize.term('uap_contentNodeHeader')}</th>
                            <th class="dialog-security-header">${this.#localize.term('uap_dialogSecurityHeader')}</th>
                          </tr>
                        </thead>
                        <tbody>
                          ${this.path.map((n, i) => this.#renderPathRow(n, i))}
                        </tbody>
                      </table>
                    </div>
                    ${this.#renderSuppressedSection()}
                  </div>
                `
```

- at the top of `#renderBanner()`, after `if (!this.effectivePerm) return html\`\`;`, add:

```ts
    if (isBlocked(this.startNodeAccess)) {
      const contentPath = this.path
        .filter((n) => n.key !== VIRTUAL_ROOT_NODE_KEY)
        .map((n) => n.name)
        .join(' > ');
      return html`
        <div class="effective-banner result-blocked">
          <umb-icon class="banner-icon" name="icon-lock"></umb-icon>
          <span class="banner-text">${this.#localize.term(
            blockedBannerTerm(this.startNodeAccess), this.subjectName, this.verbLabel, contentPath || this.nodeName)}</span>
        </div>
      `;
    }
```

- at the top of `#renderInfoNote()`, after the `loading` guard, add:

```ts
    if (isBlocked(this.startNodeAccess)) {
      const note = blockedNoteTerm(this.startNodes);
      return html`
        <div class="info-note">
          <span class="info-note-icon">ⓘ</span>
          <span class="info-note-text">${this.#localize.term(note.key, ...note.args)}</span>
        </div>
      `;
    }
```

- add to `styles`, after the `.result-deny .banner-icon` rule:

```css
    .effective-banner.result-blocked {
      background: color-mix(in srgb, var(--uui-color-text-alt, #888) 10%, transparent);
      border-left: 4px solid var(--uui-color-text-alt, #888);
    }
    .result-blocked .banner-icon { color: var(--uui-color-text-alt, #666); }
    .blocked-heading {
      margin: 0 0 8px;
      font-weight: 600;
      color: var(--uui-color-text-alt, #666);
    }
    .blocked-preview .dialog-table-wrap,
    .blocked-preview .suppressed-section {
      opacity: 0.55;
      filter: grayscale(1);
    }
```

(Check the suppressed section's real class name in `#renderSuppressedSection` and use it in the last selector.)

- [ ] **Step 5: Type-check**

Run: `npx tsc --noEmit`
Expected: no errors.

- [ ] **Step 6: Leave uncommitted**

No commit.

---

### Task 11: Wire the Access Viewer

**Files:**
- Modify: `src/Umbraco.Community.AdvancedPermissions.Client/src/access-viewer/uap-access-viewer-root.element.ts`

**Interfaces:**
- Consumes: `getStartNodes` (Task 7), display rules (Task 8), components (Task 10).

- [ ] **Step 1: Imports and state**

Add imports:

```ts
import { getStartNodes } from '../api/advanced-permissions.api.js';
import type { StartNodeAccess, StartNodesResponse } from '../models/permission.models.js';
import { isBlocked, rowBadge } from '../start-nodes/start-node-display.js';
import '../start-nodes/uap-start-node-badge.element.js';
import '../start-nodes/uap-start-node-strip.element.js';
```

(Merge `getStartNodes` into the existing import from `advanced-permissions.api.js`.) Add to `ViewerTreeNode`:

```ts
  /** Whether the subject can reach the node through its start nodes (null = not yet loaded). */
  startNodeAccess: StartNodeAccess | null;
```

and set `startNodeAccess: null` in both places that build nodes (`#loadTree` and `#toggleExpand`). Add state after `_refreshPhase`:

```ts
  /** The subject's start nodes, for the strip and the blocked dialog; null until loaded. */
  @state() private _startNodes: StartNodesResponse | null = null;
  /** True while the start nodes load for a newly selected subject. */
  @state() private _startNodesLoading = false;
  /** True when the start nodes could not be loaded. */
  @state() private _startNodesError = false;
```

- [ ] **Step 2: Load the start nodes**

Add the method:

```ts
  /**
   * Loads the subject's start nodes for the strip and the blocked dialog. The grid does not depend
   * on it: each node's own effective response carries its start-node access.
   * @param signal Aborts when the selection changes.
   * @param background True for a live refresh: keep the current strip until the new one arrives.
   * @returns True when the start nodes loaded (or the load was superseded).
   */
  async #loadStartNodes(signal: AbortSignal, background = false): Promise<boolean> {
    const subject = this._activeSubject === 'role'
      ? (this._selectedRole ? { roleAlias: this._selectedRole.alias } : null)
      : (this._selectedUser ? { userKey: this._selectedUser.unique } : null);
    if (!subject) return true;
    if (!background) {
      this._startNodes = null;
      this._startNodesLoading = true;
    }
    this._startNodesError = false;
    try {
      const result = await getStartNodes(subject, signal);
      if (signal.aborted) return true;
      this._startNodes = result;
      return true;
    } catch {
      if (signal.aborted) return true;
      this._startNodesError = true;
      return false;
    } finally {
      if (!signal.aborted) this._startNodesLoading = false;
    }
  }
```

Call it:
- in `#loadTree`, right after the new `AbortController` is stored: `void this.#loadStartNodes(controller.signal);`
- in `#reloadEffective`, right after its new `AbortController` is stored, so a live or manual refresh also reloads the strip: `const startNodesLoad = this.#loadStartNodes(controller.signal, background);` and, where the method computes `complete`, AND in its result: `complete = (await startNodesLoad) && complete;` (place it before the method decides whether the refresh completed).
- in `#onClearSelection` (and wherever the selection is cleared): `this._startNodes = null; this._startNodesError = false;`

- [ ] **Step 3: Store each node's access**

In `#loadEffective`, after `node.effectivePerms = permsMap;`, add:

```ts
      node.startNodeAccess = result.startNodeAccess;
```

- [ ] **Step 4: Render the badge and the blocked cell**

In `#renderRow`, replace `<span class="node-name">${node.name}</span>` with:

```ts
            <span class="node-name ${node.startNodeAccess === 'NotVisible' ? 'muted' : ''}">${node.name}</span>
            <uap-start-node-badge .badge=${rowBadge(node.key, node.startNodeAccess, this._startNodes)}></uap-start-node-badge>
```

In `#renderEffectiveCell`, after the loading branch, add:

```ts
    if (isBlocked(node.startNodeAccess)) {
      return html`
        <td class="perm-td" @click=${() => this.#openReasoning(node, verb)}>
          <uap-perm-block blocked blocked-title=${this.#localize.term('uap_startNodes_blockedCellTitle')}></uap-perm-block>
        </td>
      `;
    }
```

Add to the styles: `.node-name.muted { color: var(--uui-color-text-alt, #888); }`

- [ ] **Step 5: Render the strip and feed the dialog**

In `render()`, inside `<uap-selection-panel>`, right after the `_error` line, add:

```ts
          ${this._activeSubject
            ? html`<uap-start-node-strip
                .data=${this._startNodes}
                ?loading=${this._startNodesLoading}
                ?error=${this._startNodesError}
                subject-kind=${this._activeSubject}></uap-start-node-strip>`
            : nothing}
```

On `<uap-reasoning-dialog>`, add:

```ts
        .startNodeAccess=${this._reasoningNode?.startNodeAccess ?? 'Accessible'}
        .startNodes=${this._startNodes}
```

- [ ] **Step 6: Type-check, test and build**

Run (Client folder): `npx tsc --noEmit && npm test && npm run build`
Expected: no type errors; all Vitest suites pass; Vite build writes to `../Umbraco.Community.AdvancedPermissions/wwwroot/App_Plugins/...`.

- [ ] **Step 7: Leave uncommitted**

No commit. (The rebuilt `wwwroot` bundle is part of the diff, as in earlier features.)

---

### Task 12: Wire the Insert Options Viewer

**Files:**
- Modify: `src/Umbraco.Community.AdvancedPermissions.Client/src/doc-type-permissions/uap-doc-type-create-audit-root.element.ts`

**Interfaces:**
- Consumes: as Task 11.

- [ ] **Step 1: Imports, node field, state and loading**

Repeat Task 11 Steps 1-3 in this element, with these differences:
- the node interface is `AuditTreeNode` (add `startNodeAccess: StartNodeAccess | null;`, set `null` where nodes are built in `#loadTree` and `#toggleExpand`);
- the subject is already available as `this.#subject` (`{ userKey } | { roleAlias } | null`), so `#loadStartNodes` uses `const subject = this.#subject;`;
- in `#loadAuditForNode`, after `node.auditResults = map;`, add `node.startNodeAccess = result.startNodeAccess;`;
- the refresh method that re-runs `#loadAuditBatch` over the tree (around line 291) is where `#loadStartNodes(controller.signal, background)` joins the `complete` result.

- [ ] **Step 2: Badge and blocked cell**

In `#renderRow`, replace the node name span as in Task 11 Step 4. In `#renderCell`, after the `if (!node.auditResults)` loading branch, add:

```ts
    if (isBlocked(node.startNodeAccess)) {
      return html`
        <td class="perm-td" @click=${() => void this.#openReasoning(node, ctKey)}>
          <uap-perm-block blocked blocked-title=${this.#localize.term('uap_startNodes_blockedCellTitle')}></uap-perm-block>
        </td>
      `;
    }
```

Add the `.node-name.muted` style.

- [ ] **Step 3: Strip and dialog**

Add the strip inside `<uap-selection-panel>` after the `_error` line, exactly as Task 11 Step 5. On `<uap-reasoning-dialog>` add `.startNodeAccess=${this._reasoningNode?.startNodeAccess ?? 'Accessible'}` and `.startNodes=${this._startNodes}`. Keep `.outsideAllowed` as it is; in blocked mode the dialog's start-node note takes priority because `#renderInfoNote` checks blocked first.

- [ ] **Step 4: Type-check, test and build**

Run: `npx tsc --noEmit && npm test && npm run build`
Expected: clean.

- [ ] **Step 5: Leave uncommitted**

No commit.

---

### Task 13: Help, project docs and end-to-end verification

**Files:**
- Modify: `src/Umbraco.Community.AdvancedPermissions.Client/help-docs/en/access-viewer.md`, `insert-options-viewer.md`, `concepts.md`
- Modify: `src/Umbraco.Community.AdvancedPermissions.Client/help-docs/nl/access-viewer.md`, `insert-options-viewer.md`, `concepts.md`
- Modify: `CLAUDE.md` (Management API endpoint table)

- [ ] **Step 1: Help text (English)**

In `en/access-viewer.md`, add before the final "See the **Concepts** tab" line:

```markdown
## Start nodes

The viewer also takes the subject's **start nodes** into account, because Umbraco blocks every node outside them before any permission is checked.

- The **start-node strip** under the picker shows the start nodes in effect and, line by line, where each one comes from: which user group, or the user's own account. A line that doesn't count says why.
- A node the subject can't reach shows **blocked** cells (grey, hatched, with a lock) instead of allow or deny:
  - **Path only**: an ancestor of a start node. It's shown in the tree so the user can browse down, but it can't be opened.
  - **Not visible**: outside every start node, so hidden.
- Click a blocked cell to see why, and what the permissions would be if the node were inside a start node.
- With a user group selected, the viewer uses that group's own start node. A user gets the combined start nodes of all their groups, so pick a user to see the real result.
```

In `en/insert-options-viewer.md`, add under "Good to know":

```markdown
- Creating under a node needs access to that node. Rows outside the subject's start nodes show **blocked** cells; the start-node strip under the picker shows which start nodes apply and where they come from.
```

In `en/concepts.md`, add a section:

```markdown
## Start nodes

Start nodes decide which part of the content tree a user can reach at all. Umbraco checks them **before** any permission.

- **A user group** has one start node, root access, or none. A group without a start node gives **no** content access.
- **Several groups** combine: the user can reach every group's start node, and a higher start node covers a lower one on the same branch.
- **Start nodes and permissions are separate.** A permission from one group also applies inside a start node that another group provides.
- **A start node set on the user account** replaces any group start node on the same branch, even root access. It can only narrow access, never widen it.
- **Administrators** reach everything only because their group has root access. There is no other exception.
- **Deleted or trashed start nodes** are ignored. If a user's own start node is deleted, the user falls back to the group start nodes.
- Some pickers can be configured to ignore start nodes, and backoffice search applies them slightly differently. The viewers show the tree and editor rules.
```

- [ ] **Step 2: Help text (Dutch)**

Add the same three sections to the `nl` files, in Dutch:

`nl/access-viewer.md`:

```markdown
## Startnodes

De viewer houdt ook rekening met de **startnodes** van het onderwerp, omdat Umbraco elke node daarbuiten blokkeert voordat er naar machtigingen wordt gekeken.

- De **startnodestrook** onder de kiezer toont de geldende startnodes en per regel waar elke startnode vandaan komt: welke gebruikersgroep, of het eigen account van de gebruiker. Een regel die niet meetelt, zegt waarom.
- Een node die het onderwerp niet kan bereiken, toont **geblokkeerde** cellen (grijs, gearceerd, met een slotje) in plaats van toestaan of weigeren:
  - **Alleen pad**: een bovenliggende node van een startnode. Hij staat in de boom zodat de gebruiker naar beneden kan navigeren, maar kan niet worden geopend.
  - **Niet zichtbaar**: buiten alle startnodes, dus verborgen.
- Klik op een geblokkeerde cel om te zien waarom, en wat de machtigingen zouden zijn als de node binnen een startnode viel.
- Met een gebruikersgroep geselecteerd gebruikt de viewer de eigen startnode van die groep. Een gebruiker krijgt de gecombineerde startnodes van al zijn groepen; kies dus een gebruiker om het echte resultaat te zien.
```

`nl/insert-options-viewer.md`, under "Goed om te weten" (or the file's equivalent heading):

```markdown
- Aanmaken onder een node vereist toegang tot die node. Rijen buiten de startnodes van het onderwerp tonen **geblokkeerde** cellen; de startnodestrook onder de kiezer toont welke startnodes gelden en waar ze vandaan komen.
```

`nl/concepts.md`:

```markdown
## Startnodes

Startnodes bepalen welk deel van de contentboom een gebruiker überhaupt kan bereiken. Umbraco controleert ze **vóór** elke machtiging.

- **Een gebruikersgroep** heeft één startnode, roottoegang of geen. Een groep zonder startnode geeft **geen** toegang tot content.
- **Meerdere groepen** worden gecombineerd: de gebruiker bereikt de startnode van elke groep, en een hogere startnode dekt een lagere op dezelfde tak.
- **Startnodes en machtigingen staan los van elkaar.** Een machtiging uit de ene groep geldt ook binnen een startnode die een andere groep levert.
- **Een startnode op het gebruikersaccount** vervangt elke groepsstartnode op dezelfde tak, zelfs roottoegang. Hij kan toegang alleen beperken, nooit uitbreiden.
- **Beheerders** bereiken alles alleen omdat hun groep roottoegang heeft. Er is geen andere uitzondering.
- **Verwijderde startnodes of startnodes in de prullenbak** worden genegeerd. Als de eigen startnode van een gebruiker wordt verwijderd, valt de gebruiker terug op de startnodes van de groepen.
- Sommige pickers kunnen zo worden ingesteld dat ze startnodes negeren, en zoeken in de backoffice past ze iets anders toe. De viewers tonen de regels van de boom en de editor.
```

- [ ] **Step 3: Project docs**

In `CLAUDE.md`, add to the Management API table:

```markdown
| GET | `/start-nodes?userKey=` | A user's effective start nodes and where each comes from |
| GET | `/start-nodes/by-role?roleAlias=` | One user group's own start node |
```

and note in the `/effective` rows that the response carries `startNodeAccess`.

- [ ] **Step 4: Full automated run**

Run (repo root): `dotnet build && dotnet test`
Run (Client folder): `npx tsc --noEmit && npm test && npm run build`
Expected: everything passes. Record the test counts for the report.

- [ ] **Step 5: Manual verification on the v17 test site**

Use the `running-umbraco-instances` skill if the site loops on login. Start it with `preview_start` (`Umbraco TestSite`, `https://localhost:7067/umbraco`). In the backoffice, set up:
- user group **A** with start node *Home*, user group **B** with start node *Shop* (a sibling), user group **C** with no start node;
- user **U1** in A and B; user **U2** in A with a user-level start node below *Home*.

Check, with screenshots as proof:
1. Access Viewer, U1: the strip shows *Home* and *Shop*, A and B "in use", user-specific "none". The website root row shows "Path only" with blocked cells; a third sibling shows "Not visible"; nodes under *Home* show normal allow/deny.
2. Click a blocked cell: grey banner with a lock, the start-node note, the greyed table under "If this node were inside a start node:".
3. U2: A's line says "replaced by the user's own start node …"; *Home* itself is now path only.
4. User group mode, C: "No start nodes, so no content access." and every row blocked. All Users group: "Start nodes don't apply…", no blocked cells.
5. Insert Options Viewer, U1 with any document type: the same rows are blocked; clicking one shows the blocked dialog.
6. **Live refresh (review focus 5):** keep the Access Viewer open on U1, change group B's start node in a second tab and save. Within a few seconds, without reloading, the strip and the rows reflect the new start node. If they still show the old one, the event ordering is wrong; stop and investigate before claiming done.
7. Light and dark theme: blocked cells, badges and the strip stay readable.

- [ ] **Step 6: Report and leave uncommitted**

Report what changed per file, the test counts, and the screenshots. Do not commit; ask the user whether to commit.
