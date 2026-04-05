namespace LP.Umbraco.AdvancedPermissions.Core.Models;

/// <summary>
/// Defines the scope at which a permission entry applies within the content tree.
/// </summary>
public enum PermissionScope
{
    /// <summary>
    /// The permission applies only to the specific node on which it is set.
    /// For inheritance purposes, this node is invisible to descendants — they walk past it
    /// as if the entry does not exist.
    /// </summary>
    ThisNodeOnly = 1,

    /// <summary>
    /// The permission applies to the node itself and all its descendants.
    /// </summary>
    ThisNodeAndDescendants = 2,

    /// <summary>
    /// The permission applies only to descendants of the node, not to the node itself.
    /// This allows setting a different state for a node vs. its subtree.
    /// </summary>
    DescendantsOnly = 3,
}
