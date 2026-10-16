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
