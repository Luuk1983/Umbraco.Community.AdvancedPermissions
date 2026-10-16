namespace Umbraco.Community.AdvancedPermissions.Services.StartNodes;

/// <summary>
/// Works out which nodes of the content tree or the Library a user or a single user group can reach
/// through start nodes, and explains where those start nodes come from. Feeds the viewers only;
/// enforcement does not use it.
/// </summary>
public interface IStartNodeAccessService
{
    /// <summary>
    /// Builds the start-node snapshot for a user, from Umbraco's own combination of the user's group
    /// and user-level start nodes (the exact value enforcement uses).
    /// </summary>
    /// <param name="userKey">The user's key.</param>
    /// <param name="tree">Which start nodes to read: content or Library.</param>
    /// <returns>The snapshot, or <see langword="null"/> when the user does not exist.</returns>
    Task<StartNodeSnapshot?> GetForUserAsync(Guid userKey, StartNodeTree tree);

    /// <summary>
    /// Builds the start-node snapshot for one user group on its own. The All Users group yields
    /// <see cref="StartNodeSnapshot.NotApplicableFor"/>.
    /// </summary>
    /// <param name="userGroupAlias">The user group alias.</param>
    /// <param name="tree">Which start nodes to read: content or Library.</param>
    /// <returns>The snapshot, or <see langword="null"/> when the group does not exist.</returns>
    Task<StartNodeSnapshot?> GetForUserGroupAsync(string userGroupAlias, StartNodeTree tree);

    /// <summary>
    /// Gets what classifying a node needs for a user, cached per user and tree so a viewer's rows
    /// share one load of the user's start nodes.
    /// </summary>
    /// <param name="userKey">The user's key.</param>
    /// <param name="tree">Which start nodes to read: content or Library.</param>
    /// <returns>The scope, or <see langword="null"/> when the user does not exist (not cached).</returns>
    Task<StartNodeScope?> GetScopeForUserAsync(Guid userKey, StartNodeTree tree);

    /// <summary>
    /// Gets what classifying a node needs for one user group on its own, cached per group and tree.
    /// The All Users group yields <see cref="StartNodeScope.NotApplicableFor"/>.
    /// </summary>
    /// <param name="userGroupAlias">The user group alias.</param>
    /// <param name="tree">Which start nodes to read: content or Library.</param>
    /// <returns>The scope, or <see langword="null"/> when the group does not exist (not cached).</returns>
    Task<StartNodeScope?> GetScopeForUserGroupAsync(string userGroupAlias, StartNodeTree tree);

    /// <summary>Classifies one node for the subject a scope describes.</summary>
    /// <param name="scope">The subject's scope.</param>
    /// <param name="nodePath">The node's materialised path; <c>-1</c> is the tree root.</param>
    /// <returns>Whether the subject can reach the node.</returns>
    StartNodeAccess Classify(StartNodeScope scope, string nodePath);
}
