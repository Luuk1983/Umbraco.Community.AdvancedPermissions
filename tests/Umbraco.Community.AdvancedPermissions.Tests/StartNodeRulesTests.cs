using Umbraco.Community.AdvancedPermissions.Services.StartNodes;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests <see cref="StartNodeRules"/>: how a node is classified against a subject's effective start
/// nodes, and how each configured start node is explained. The cases mirror Umbraco 18's
/// <c>UserExtensions.CombineStartNodes</c> and <c>ContentPermissions.HasPathAccess</c> (identical in
/// 18.0.0 and 18.2.1), for both the content tree and the Library.
/// </summary>
public sealed class StartNodeRulesTests
{
    /// <summary>The content recycle bin id.</summary>
    private const int ContentBin = -20;

    /// <summary>The Library recycle bin id.</summary>
    private const int LibraryBin = -22;

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
        Assert.Equal(StartNodeAccess.Accessible, StartNodeRules.Classify("-1,30", [-1], ["-1"], ContentBin));

    /// <summary>A start node itself is reachable.</summary>
    [Fact]
    public void Classify_StartNodeItself_IsAccessible() =>
        Assert.Equal(StartNodeAccess.Accessible, StartNodeRules.Classify("-1,10", [10], ["-1,10"], ContentBin));

    /// <summary>A descendant of a start node is reachable.</summary>
    [Fact]
    public void Classify_DescendantOfStartNode_IsAccessible() =>
        Assert.Equal(StartNodeAccess.Accessible, StartNodeRules.Classify("-1,10,20", [10], ["-1,10"], ContentBin));

    /// <summary>An ancestor of a start node is shown for navigation only.</summary>
    [Fact]
    public void Classify_AncestorOfStartNode_IsPathOnly() =>
        Assert.Equal(StartNodeAccess.PathOnly, StartNodeRules.Classify("-1,10", [20], ["-1,10,20"], ContentBin));

    /// <summary>The content root is an ancestor of every start node, so it is path-only without root access.</summary>
    [Fact]
    public void Classify_ContentRootWithoutRootAccess_IsPathOnly() =>
        Assert.Equal(StartNodeAccess.PathOnly, StartNodeRules.Classify("-1", [20], ["-1,10,20"], ContentBin));

    /// <summary>A node on another branch is hidden.</summary>
    [Fact]
    public void Classify_OtherBranch_IsNotVisible() =>
        Assert.Equal(StartNodeAccess.NotVisible, StartNodeRules.Classify("-1,30", [10], ["-1,10"], ContentBin));

    /// <summary>
    /// Review focus 1: node 12 is not an ancestor of start node 123 even though "-1,123" starts with
    /// "-1,12". Path comparison must be comma-bounded.
    /// </summary>
    [Fact]
    public void Classify_SiblingWithPrefixId_IsNotVisible() =>
        Assert.Equal(StartNodeAccess.NotVisible, StartNodeRules.Classify("-1,12", [123], ["-1,123"], ContentBin));

    /// <summary>Without any start node nothing is reachable.</summary>
    [Fact]
    public void Classify_NoStartNodes_IsNotVisible() =>
        Assert.Equal(StartNodeAccess.NotVisible, StartNodeRules.Classify("-1,10", [], [], ContentBin));

    /// <summary>Only root access reaches the recycle bin.</summary>
    [Fact]
    public void Classify_RecycleBinWithoutRootAccess_IsNotVisible() =>
        Assert.Equal(StartNodeAccess.NotVisible, StartNodeRules.Classify("-1,-20,40", [10], ["-1,10"], ContentBin));

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

    /// <summary>A user-level start node replaces any group start node on the same branch, even root.</summary>
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

    /// <summary>
    /// The content root is not a real node, so a caller's path map may omit it. A root-access group
    /// must still be in use rather than reported as a deleted start node.
    /// </summary>
    [Fact]
    public void Attribute_RootGroupWithoutRootInPaths_IsInUse() =>
        Assert.Equal(
            StartNodeSourceStatus.InUse,
            StartNodeRules.Attribute([Group(-1)], [-1], PathsWithoutRoot, ContentBin)[0].Status);

    /// <summary>
    /// A group node under a root-access group is covered by root even when the caller's path map
    /// omits the root, because the root is implied by the effective set.
    /// </summary>
    [Fact]
    public void Attribute_GroupNodeUnderRootGroupWithoutRootInPaths_IsCoveredByRoot()
    {
        var result = StartNodeRules.Attribute([Group(-1), Group(30)], [-1], PathsWithoutRoot, ContentBin);
        Assert.Equal(StartNodeSourceStatus.CoveredBy, result[1].Status);
        Assert.Equal(-1, result[1].RelatedStartNodeId);
    }

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

    /// <summary>Review focus 3: each tree is judged against its own recycle bin only.</summary>
    [Fact]
    public void IsInRecycleBin_UsesTheTreesOwnBin()
    {
        Assert.True(StartNodeRules.IsInRecycleBin("-1,-20,40", ContentBin));
        Assert.False(StartNodeRules.IsInRecycleBin("-1,-20,40", LibraryBin));
        Assert.True(StartNodeRules.IsInRecycleBin("-1,-22,50", LibraryBin));
        Assert.False(StartNodeRules.IsInRecycleBin("-1,-22,50", ContentBin));
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

    /// <summary>The shared tree without the content root, as a caller that omits it would pass.</summary>
    private static readonly IReadOnlyDictionary<int, string> PathsWithoutRoot =
        Paths.Where(pair => pair.Key != -1).ToDictionary(pair => pair.Key, pair => pair.Value);

    /// <summary>Runs the attribution against the shared tree.</summary>
    /// <param name="sources">The configured start nodes.</param>
    /// <param name="effective">Umbraco's combined result.</param>
    /// <returns>One result per source, in source order.</returns>
    private static IReadOnlyList<StartNodeSourceResult> Attribute(IReadOnlyList<StartNodeSource> sources, int[] effective) =>
        StartNodeRules.Attribute(sources, effective, Paths, ContentBin);

    /// <summary>Builds a user-group start-node source.</summary>
    /// <param name="id">The group's start node id, or <see langword="null"/> for none.</param>
    /// <returns>The source.</returns>
    private static StartNodeSource Group(int? id) => new(StartNodeSourceKind.UserGroup, "editors", "Editors", id);

    /// <summary>Builds a user-level start-node source.</summary>
    /// <param name="id">The user's start node id, or <see langword="null"/> for none.</param>
    /// <returns>The source.</returns>
    private static StartNodeSource User(int? id) => new(StartNodeSourceKind.User, null, null, id);
}
