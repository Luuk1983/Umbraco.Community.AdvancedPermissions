using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.Core.Services;

/// <summary>
/// Pure, stateless resolution algorithm shared by the node-level permission resolver
/// (default <see cref="PermissionState.Deny"/>) and the document-type permission resolver
/// (default <see cref="PermissionState.Allow"/>).
/// </summary>
/// <remarks>
/// <para>
/// The engine walks the provided path from target node upward to root for each role,
/// finding the first applicable entry. "Applicable" depends on the entry's
/// <see cref="PermissionScope"/>:
/// <list type="bullet">
///   <item><see cref="PermissionScope.ThisNodeOnly"/> — applies only at depth 0 (target node itself)</item>
///   <item><see cref="PermissionScope.ThisNodeAndDescendants"/> — applies at any depth</item>
///   <item><see cref="PermissionScope.DescendantsOnly"/> — applies only at depth &gt; 0</item>
/// </list>
/// </para>
/// <para>
/// If no entry is found in the content path, virtual-root entries
/// (<c>NodeKey = VirtualRootNodeKey</c>) are checked as a fallback. These represent the role's
/// default permissions.
/// </para>
/// <para>
/// After collecting one result per role, the priority order determines the final outcome:
/// <list type="number">
///   <item>Explicit Deny from any role (entry on target node itself)</item>
///   <item>Explicit Allow from any role (entry on target node itself)</item>
///   <item>Implicit Deny from any role (inherited from ancestor or root-level entry)</item>
///   <item>Implicit Allow from any role (inherited from ancestor or root-level entry)</item>
///   <item>No opinion from any role → the supplied <c>defaultState</c></item>
/// </list>
/// </para>
/// </remarks>
public static class ResolutionEngine
{
    /// <summary>
    /// Resolves the effective permission for a single verb given a path, the user's roles,
    /// and pre-filtered entries.
    /// </summary>
    /// <param name="pathFromRoot">
    /// Ordered list of node keys from root down to (and including) the target node.
    /// </param>
    /// <param name="roleAliases">All role aliases belonging to the user, including <c>$everyone</c>.</param>
    /// <param name="entries">
    /// Entries to consider. The caller must pre-filter to the verb (and any other dimensions
    /// like ContentTypeKey) it cares about. The engine itself is verb-agnostic.
    /// </param>
    /// <param name="verb">The verb being resolved. Used only as a label on the output.</param>
    /// <param name="defaultState">
    /// The fallback state when no role has any opinion. Existing node-level resolution uses
    /// <see cref="PermissionState.Deny"/>; doc-type create-restrictions use
    /// <see cref="PermissionState.Allow"/>.
    /// </param>
    /// <returns>
    /// The effective permission together with the full reasoning chain for the Access Viewer.
    /// </returns>
    public static EffectivePermission Resolve(
        IReadOnlyList<Guid> pathFromRoot,
        IReadOnlyList<string> roleAliases,
        IReadOnlyList<ResolutionEntry> entries,
        string verb,
        PermissionState defaultState)
    {
        var roleResults = new List<RolePermissionResult>(roleAliases.Count);

        foreach (var role in roleAliases)
        {
            var result = ResolveForRole(pathFromRoot, entries, role);
            if (result is not null)
            {
                roleResults.Add(result);
            }
        }

        return BuildEffectivePermission(verb, roleResults, defaultState);
    }

    /// <summary>
    /// Resolves the permission for a single role by walking the path from target to root,
    /// then falling back to virtual-root entries (<c>NodeKey = VirtualRootNodeKey</c>).
    /// </summary>
    /// <param name="pathFromRoot">The path from root to target (inclusive).</param>
    /// <param name="entries">The pre-filtered entries to consider.</param>
    /// <param name="roleAlias">The role to resolve for.</param>
    /// <returns>The resolved result for this role, or <see langword="null"/> if the role has no opinion.</returns>
    private static RolePermissionResult? ResolveForRole(
        IReadOnlyList<Guid> pathFromRoot,
        IReadOnlyList<ResolutionEntry> entries,
        string roleAlias)
    {
        var targetIndex = pathFromRoot.Count - 1;

        // Walk from target node upward to root.
        // depth = 0 means we are at the target node itself.
        for (var i = targetIndex; i >= 0; i--)
        {
            var nodeKey = pathFromRoot[i];
            var depth = targetIndex - i;

            foreach (var entry in entries)
            {
                if (entry.NodeKey != nodeKey)
                {
                    continue;
                }

                if (!string.Equals(entry.RoleAlias, roleAlias, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!EntryAppliesAtDepth(entry.Scope, depth))
                {
                    continue;
                }

                return new RolePermissionResult(
                    RoleAlias: roleAlias,
                    State: entry.State,
                    IsExplicit: depth == 0,
                    SourceNodeKey: nodeKey,
                    SourceScope: entry.Scope);
            }
        }

        // Fall back to virtual-root entries (the role's default).
        foreach (var entry in entries)
        {
            if (entry.NodeKey != AdvancedPermissionsConstants.VirtualRootNodeKey)
            {
                continue;
            }

            if (!string.Equals(entry.RoleAlias, roleAlias, StringComparison.Ordinal))
            {
                continue;
            }

            return new RolePermissionResult(
                RoleAlias: roleAlias,
                State: entry.State,
                IsExplicit: false,
                SourceNodeKey: AdvancedPermissionsConstants.VirtualRootNodeKey,
                SourceScope: entry.Scope);
        }

        return null;
    }

    /// <summary>
    /// Determines whether a permission entry with the given scope applies at the specified depth.
    /// </summary>
    /// <param name="scope">The scope of the entry.</param>
    /// <param name="depth">Distance from the entry's node to the target (0 = target itself).</param>
    /// <returns><see langword="true"/> if the entry applies at this depth; otherwise <see langword="false"/>.</returns>
    private static bool EntryAppliesAtDepth(PermissionScope scope, int depth) =>
        scope switch
        {
            PermissionScope.ThisNodeOnly => depth == 0,
            PermissionScope.ThisNodeAndDescendants => true,
            PermissionScope.DescendantsOnly => depth > 0,
            _ => false,
        };

    /// <summary>
    /// Builds the final <see cref="EffectivePermission"/> from the collected role results
    /// by applying the priority order. Falls back to <paramref name="defaultState"/> when no role
    /// expressed an opinion.
    /// </summary>
    /// <param name="verb">The permission verb (label only).</param>
    /// <param name="roleResults">Per-role results collected during resolution.</param>
    /// <param name="defaultState">Default state when no role has any opinion.</param>
    /// <returns>The effective permission for the verb.</returns>
    private static EffectivePermission BuildEffectivePermission(
        string verb,
        IReadOnlyList<RolePermissionResult> roleResults,
        PermissionState defaultState)
    {
        var explicitDenies = roleResults.Where(r => r.State == PermissionState.Deny && r.IsExplicit).ToList();
        var explicitAllows = roleResults.Where(r => r.State == PermissionState.Allow && r.IsExplicit).ToList();
        var implicitDenies = roleResults.Where(r => r.State == PermissionState.Deny && !r.IsExplicit).ToList();
        var implicitAllows = roleResults.Where(r => r.State == PermissionState.Allow && !r.IsExplicit).ToList();

        if (explicitDenies.Count > 0)
        {
            return new EffectivePermission(
                Verb: verb,
                IsAllowed: false,
                IsExplicit: true,
                Reasoning: BuildReasoning(explicitDenies, explicitAllows, implicitDenies, implicitAllows));
        }

        if (explicitAllows.Count > 0)
        {
            return new EffectivePermission(
                Verb: verb,
                IsAllowed: true,
                IsExplicit: true,
                Reasoning: BuildReasoning(explicitDenies, explicitAllows, implicitDenies, implicitAllows));
        }

        if (implicitDenies.Count > 0)
        {
            return new EffectivePermission(
                Verb: verb,
                IsAllowed: false,
                IsExplicit: false,
                Reasoning: BuildReasoning(explicitDenies, explicitAllows, implicitDenies, implicitAllows));
        }

        if (implicitAllows.Count > 0)
        {
            return new EffectivePermission(
                Verb: verb,
                IsAllowed: true,
                IsExplicit: false,
                Reasoning: BuildReasoning(explicitDenies, explicitAllows, implicitDenies, implicitAllows));
        }

        // No role had an opinion — use caller's default.
        return new EffectivePermission(
            Verb: verb,
            IsAllowed: defaultState == PermissionState.Allow,
            IsExplicit: false,
            Reasoning: []);
    }

    /// <summary>
    /// Builds the reasoning list from all collected role results, ordered by priority:
    /// explicit denies → explicit allows → implicit denies → implicit allows.
    /// </summary>
    /// <param name="explicitDenies">Roles with an explicit deny on the target node.</param>
    /// <param name="explicitAllows">Roles with an explicit allow on the target node.</param>
    /// <param name="implicitDenies">Roles with an inherited deny.</param>
    /// <param name="implicitAllows">Roles with an inherited allow.</param>
    /// <returns>An ordered list of reasoning entries for the Access Viewer.</returns>
    private static IReadOnlyList<PermissionReasoning> BuildReasoning(
        IEnumerable<RolePermissionResult> explicitDenies,
        IEnumerable<RolePermissionResult> explicitAllows,
        IEnumerable<RolePermissionResult> implicitDenies,
        IEnumerable<RolePermissionResult> implicitAllows)
    {
        var ordered = explicitDenies
            .Concat(explicitAllows)
            .Concat(implicitDenies)
            .Concat(implicitAllows);

        return ordered
            .Select(r => new PermissionReasoning(
                ContributingRole: r.RoleAlias,
                State: r.State,
                IsExplicit: r.IsExplicit,
                SourceNodeKey: r.SourceNodeKey,
                SourceScope: r.SourceScope,
                IsFromGroupDefault: r.SourceNodeKey == AdvancedPermissionsConstants.VirtualRootNodeKey))
            .ToList();
    }
}
