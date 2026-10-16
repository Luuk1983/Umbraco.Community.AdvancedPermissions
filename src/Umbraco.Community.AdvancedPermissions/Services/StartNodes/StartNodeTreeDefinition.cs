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
