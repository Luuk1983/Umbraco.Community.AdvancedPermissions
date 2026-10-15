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
