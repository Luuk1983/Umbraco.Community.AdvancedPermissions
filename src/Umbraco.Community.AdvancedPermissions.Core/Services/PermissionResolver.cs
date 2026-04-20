using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.Core.Services;

/// <summary>
/// Resolves effective permissions by applying the advanced security inheritance and priority rules.
/// This is a pure, stateless service with no I/O dependencies — all data is provided via the context.
/// </summary>
/// <remarks>
/// <para>
/// Resolution walks the path from target node upward to root for each role, finding the first
/// applicable entry. "Applicable" depends on the entry's <see cref="PermissionScope"/>:
/// <list type="bullet">
///   <item><see cref="PermissionScope.ThisNodeOnly"/> — applies only at depth 0 (target node itself)</item>
///   <item><see cref="PermissionScope.ThisNodeAndDescendants"/> — applies at any depth</item>
///   <item><see cref="PermissionScope.DescendantsOnly"/> — applies only at depth &gt; 0</item>
/// </list>
/// </para>
/// <para>
/// A <see cref="PermissionScope.ThisNodeOnly"/> entry found at an ancestor is ignored —
/// it does not block the walk and does not apply to descendants.
/// </para>
/// <para>
/// If no entry is found in the content path, virtual-root entries (<c>NodeKey = VirtualRootNodeKey</c>) are
/// checked as a fallback. These represent the role's default permissions.
/// </para>
/// <para>
/// After collecting one result per role, the priority order determines the final outcome:
/// <list type="number">
///   <item>Explicit Deny from any role (entry on target node itself)</item>
///   <item>Explicit Allow from any role (entry on target node itself)</item>
///   <item>Implicit Deny from any role (inherited from ancestor or root-level entry)</item>
///   <item>Implicit Allow from any role (inherited from ancestor or root-level entry)</item>
///   <item>No opinion from any role → Deny (safe by default)</item>
/// </list>
/// </para>
/// </remarks>
public sealed class PermissionResolver : IPermissionResolver
{
    /// <inheritdoc />
    public EffectivePermission Resolve(PermissionResolutionContext context, string verb)
    {
        var roleResults = new List<RolePermissionResult>(context.RoleAliases.Count);

        foreach (var role in context.RoleAliases)
        {
            var result = ResolveForRole(context, role, verb);
            if (result is not null)
            {
                roleResults.Add(result);
            }
        }

        return BuildEffectivePermission(verb, roleResults);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, EffectivePermission> ResolveAll(
        PermissionResolutionContext context,
        IEnumerable<string> verbs)
    {
        var result = new Dictionary<string, EffectivePermission>(StringComparer.Ordinal);

        foreach (var verb in verbs)
        {
            result[verb] = Resolve(context, verb);
        }

        return result;
    }

    /// <summary>
    /// Resolves the permission for a single role on the given verb by walking the path
    /// from target to root, then falling back to virtual-root entries (<c>NodeKey = VirtualRootNodeKey</c>).
    /// </summary>
    /// <param name="context">The full resolution context.</param>
    /// <param name="roleAlias">The role to resolve for.</param>
    /// <param name="verb">The permission verb to resolve.</param>
    /// <returns>
    /// The resolved result for this role, or <see langword="null"/> if the role has no opinion.
    /// </returns>
    private static RolePermissionResult? ResolveForRole(
        PermissionResolutionContext context,
        string roleAlias,
        string verb)
    {
        var path = context.PathFromRoot;
        var targetIndex = path.Count - 1;

        // Walk from target node upward to root.
        // depth = 0 means we are at the target node itself.
        for (var i = targetIndex; i >= 0; i--)
        {
            var nodeKey = path[i];
            var depth = targetIndex - i; // 0 = target, 1 = parent, 2 = grandparent, …

            // Find entries for this node, role, and verb.
            var entriesAtNode = context.StoredEntries
                .Where(e => e.NodeKey == nodeKey
                         && string.Equals(e.RoleAlias, roleAlias, StringComparison.Ordinal)
                         && string.Equals(e.Verb, verb, StringComparison.Ordinal));

            foreach (var entry in entriesAtNode)
            {
                if (!EntryAppliesAtDepth(entry.Scope, depth))
                {
                    // This scope does not match this depth — skip and continue.
                    continue;
                }

                // Found an applicable entry. This is explicit if it's on the target node.
                return new RolePermissionResult(
                    RoleAlias: roleAlias,
                    State: entry.State,
                    IsExplicit: depth == 0,
                    SourceNodeKey: nodeKey,
                    SourceScope: entry.Scope);
            }
        }

        // No entry found in the content path. Check for virtual-root entries (NodeKey = VirtualRootNodeKey).
        // These act as the virtual-root defaults with implicit (inherited) semantics.
        var rootEntry = context.StoredEntries
            .FirstOrDefault(e => e.NodeKey == AdvancedPermissionsConstants.VirtualRootNodeKey
                              && string.Equals(e.RoleAlias, roleAlias, StringComparison.Ordinal)
                              && string.Equals(e.Verb, verb, StringComparison.Ordinal));

        if (rootEntry is not null)
        {
            return new RolePermissionResult(
                RoleAlias: roleAlias,
                State: rootEntry.State,
                IsExplicit: false,     // Virtual-root entries are always implicit
                SourceNodeKey: AdvancedPermissionsConstants.VirtualRootNodeKey,
                SourceScope: rootEntry.Scope);
        }

        // Role has no opinion on this verb.
        return null;
    }

    /// <summary>
    /// Determines whether a permission entry with the given scope applies at the specified depth.
    /// </summary>
    /// <param name="scope">The scope of the permission entry.</param>
    /// <param name="depth">
    /// The distance from the entry's node to the target node.
    /// 0 means the entry is on the target node itself.
    /// </param>
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
    /// by applying the priority order: explicit deny → explicit allow → implicit deny → implicit allow → deny.
    /// </summary>
    /// <param name="verb">The permission verb.</param>
    /// <param name="roleResults">All per-role results collected during resolution.</param>
    /// <returns>The effective permission for the verb.</returns>
    private static EffectivePermission BuildEffectivePermission(
        string verb,
        IReadOnlyList<RolePermissionResult> roleResults)
    {
        var explicitDenies = roleResults.Where(r => r.State == PermissionState.Deny && r.IsExplicit).ToList();
        var explicitAllows = roleResults.Where(r => r.State == PermissionState.Allow && r.IsExplicit).ToList();
        var implicitDenies = roleResults.Where(r => r.State == PermissionState.Deny && !r.IsExplicit).ToList();
        var implicitAllows = roleResults.Where(r => r.State == PermissionState.Allow && !r.IsExplicit).ToList();

        // Priority 1: Any explicit deny → DENY
        if (explicitDenies.Count > 0)
        {
            return new EffectivePermission(
                Verb: verb,
                IsAllowed: false,
                IsExplicit: true,
                Reasoning: BuildReasoning(explicitDenies, explicitAllows, implicitDenies, implicitAllows));
        }

        // Priority 2: Any explicit allow → ALLOW
        if (explicitAllows.Count > 0)
        {
            return new EffectivePermission(
                Verb: verb,
                IsAllowed: true,
                IsExplicit: true,
                Reasoning: BuildReasoning(explicitDenies, explicitAllows, implicitDenies, implicitAllows));
        }

        // Priority 3: Any implicit deny → DENY
        if (implicitDenies.Count > 0)
        {
            return new EffectivePermission(
                Verb: verb,
                IsAllowed: false,
                IsExplicit: false,
                Reasoning: BuildReasoning(explicitDenies, explicitAllows, implicitDenies, implicitAllows));
        }

        // Priority 4: Any implicit allow → ALLOW
        if (implicitAllows.Count > 0)
        {
            return new EffectivePermission(
                Verb: verb,
                IsAllowed: true,
                IsExplicit: false,
                Reasoning: BuildReasoning(explicitDenies, explicitAllows, implicitDenies, implicitAllows));
        }

        // No opinion from any role → implicit deny (safe by default)
        return new EffectivePermission(
            Verb: verb,
            IsAllowed: false,
            IsExplicit: false,
            Reasoning: []);
    }

    /// <summary>
    /// Builds the reasoning list from all collected role results, ordered by priority.
    /// </summary>
    /// <param name="explicitDenies">Roles with an explicit deny on the target node.</param>
    /// <param name="explicitAllows">Roles with an explicit allow on the target node.</param>
    /// <param name="implicitDenies">Roles with an inherited deny.</param>
    /// <param name="implicitAllows">Roles with an inherited allow or group default.</param>
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
