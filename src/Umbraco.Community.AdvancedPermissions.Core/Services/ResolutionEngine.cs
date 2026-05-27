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
/// <para>
/// <b>Priority override.</b> The walk, scope handling, and the explicit-beats-implicit priority
/// above are unchanged by the override. The override is purely a cross-role tie-breaker applied
/// <em>within the tier that already decides the outcome</em>: once the active tier is selected
/// (explicit if any role has an explicit entry, otherwise implicit), if any candidate in that
/// tier carries <see cref="ResolutionEntry.IsPriorityOverride"/>, only the flagged candidates in
/// that tier are considered. Because an explicit entry on a node always outranks anything
/// inherited, a flagged inherited rule can never beat a non-flagged explicit rule on a descendant
/// — the override is the escape hatch for the otherwise-unbeatable cross-role Explicit Deny, and
/// it respects each rule's scope rather than introducing any new inheritance.
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
    /// The effective permission together with the reasoning chain for the Access Viewer.
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
                    SourceScope: entry.Scope,
                    IsPriorityOverride: entry.IsPriorityOverride);
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
                SourceScope: entry.Scope,
                IsPriorityOverride: entry.IsPriorityOverride);
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
    /// Builds the final <see cref="EffectivePermission"/> from the collected role results.
    /// </summary>
    /// <remarks>
    /// Selects the active tier (explicit if any role has an explicit entry on the target node,
    /// otherwise implicit), then applies the priority-override tie-breaker <em>within</em> that
    /// tier: if any candidate in the active tier is flagged, only flagged candidates count.
    /// Finally Deny beats Allow among the surviving candidates.
    /// </remarks>
    /// <param name="verb">The permission verb (label only).</param>
    /// <param name="roleResults">Per-role results collected during resolution.</param>
    /// <param name="defaultState">Default state when no role has any opinion.</param>
    /// <returns>The effective permission for the verb.</returns>
    private static EffectivePermission BuildEffectivePermission(
        string verb,
        IReadOnlyList<RolePermissionResult> roleResults,
        PermissionState defaultState)
    {
        var explicitCandidates = roleResults.Where(r => r.IsExplicit).ToList();
        var implicitCandidates = roleResults.Where(r => !r.IsExplicit).ToList();

        // Active tier: explicit beats implicit (unchanged from the base algorithm).
        var isExplicitTier = explicitCandidates.Count > 0;
        var activeTier = isExplicitTier ? explicitCandidates : implicitCandidates;

        if (activeTier.Count == 0)
        {
            // No role had an opinion — use caller's default.
            return new EffectivePermission(
                Verb: verb,
                IsAllowed: defaultState == PermissionState.Allow,
                IsExplicit: false,
                Reasoning: []);
        }

        // Priority override: within the active tier, flagged entries take precedence.
        var flaggedInTier = activeTier.Where(r => r.IsPriorityOverride).ToList();
        var overrideActive = flaggedInTier.Count > 0;
        var effectiveTier = overrideActive ? flaggedInTier : activeTier;

        // Deny beats Allow among the surviving candidates.
        var denies = effectiveTier.Where(r => r.State == PermissionState.Deny).ToList();
        var allows = effectiveTier.Where(r => r.State == PermissionState.Allow).ToList();
        var isAllowed = denies.Count == 0;

        // Reasoning lists the winner(s) first: denies (if they won) then allows.
        var reasoning = denies.Concat(allows).Select(MapToReasoning).ToList();

        // Suppressed: the active-tier candidates dropped because the override fired.
        IReadOnlyList<PermissionReasoning>? suppressedReasoning = null;
        if (overrideActive)
        {
            var dropped = activeTier.Where(r => !r.IsPriorityOverride).ToList();
            if (dropped.Count > 0)
            {
                suppressedReasoning = dropped.Select(MapToReasoning).ToList();
            }
        }

        return new EffectivePermission(
            Verb: verb,
            IsAllowed: isAllowed,
            IsExplicit: isExplicitTier,
            Reasoning: reasoning,
            WasPriorityOverrideActive: overrideActive,
            SuppressedReasoning: suppressedReasoning);
    }

    /// <summary>
    /// Maps a <see cref="RolePermissionResult"/> to a <see cref="PermissionReasoning"/> entry.
    /// </summary>
    /// <param name="r">The result to convert.</param>
    /// <returns>The corresponding reasoning entry.</returns>
    private static PermissionReasoning MapToReasoning(RolePermissionResult r) =>
        new(
            ContributingRole: r.RoleAlias,
            State: r.State,
            IsExplicit: r.IsExplicit,
            SourceNodeKey: r.SourceNodeKey,
            SourceScope: r.SourceScope,
            IsFromGroupDefault: r.SourceNodeKey == AdvancedPermissionsConstants.VirtualRootNodeKey,
            IsPriorityOverride: r.IsPriorityOverride);
}
