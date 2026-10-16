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
