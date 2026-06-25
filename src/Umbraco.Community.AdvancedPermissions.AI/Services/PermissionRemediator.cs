using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Services;

/// <summary>
/// Default <see cref="IPermissionRemediationService"/>. Computes confirmed denial-to-allow remediations
/// by simulating a small, case-specific set of candidate entry mutations against the package's pure
/// resolver and keeping only the mutations that actually flip the resolved verdict to Allowed.
/// </summary>
/// <remarks>
/// <para>
/// Correctness rests on four invariants, each guarded explicitly here:
/// </para>
/// <list type="number">
/// <item><description>
/// <b>Cache bypass.</b> Every re-resolution runs through <see cref="IPermissionResolver.Resolve"/>
/// directly on a freshly built <see cref="PermissionResolutionContext"/> — never the cached
/// <see cref="IAdvancedPermissionService"/>, whose L2 cache would ignore the in-memory mutations.
/// </description></item>
/// <item><description>
/// <b>Role-set fidelity.</b> The caller supplies the exact role set the verdict used; this service
/// resolves with that identical set so its baseline matches ground truth.
/// </description></item>
/// <item><description>
/// <b>Whole-deny removal.</b> The remove candidate removes every contributing same-tier Deny together,
/// then re-resolves to confirm the flip — a partial removal would fail validation and be dropped.
/// </description></item>
/// <item><description>
/// <b>Validate, never assume.</b> No candidate is trusted; each is kept only if its re-resolution
/// returns Allowed. This is what defeats the LLM's "a plain Allow beats a Deny" misconception and the
/// override-vs-override trap (a competing priority-override Deny still wins).
/// </description></item>
/// </list>
/// </remarks>
/// <param name="resolver">The pure resolver used to re-resolve each candidate mutation.</param>
/// <param name="repository">The repository read once to load the current entries for the roles+path.</param>
public sealed class PermissionRemediator(
    IPermissionResolver resolver,
    IAdvancedPermissionRepository repository)
    : IPermissionRemediationService
{
    /// <summary>The maximum number of confirmed options returned, keeping the answer focused.</summary>
    private const int MaxOptions = 8;

    /// <summary>
    /// Defense-in-depth hard cap on total re-resolutions per call, so a pathological role/entry set can
    /// never make the simulation unbounded.
    /// </summary>
    private const int MaxResolves = 64;

    /// <inheritdoc />
    public async Task<IReadOnlyList<RemediationOption>> SuggestAsync(
        Guid nodeKey,
        IReadOnlyList<Guid> pathFromRoot,
        IReadOnlyList<string> roleAliases,
        string verb,
        PermissionState defaultState,
        CancellationToken cancellationToken = default)
    {
        if (roleAliases.Count == 0 || pathFromRoot.Count == 0)
        {
            return [];
        }

        cancellationToken.ThrowIfCancellationRequested();

        // ── ONE read. Reused across every candidate re-resolution — no per-candidate DB access. ──
        var nodeKeys = new List<Guid>(pathFromRoot) { AdvancedPermissionsConstants.VirtualRootNodeKey };
        var stored = await repository.GetByRolesAndNodesAsync(roleAliases, nodeKeys, cancellationToken);

        // Local, mutable working copy filtered to the single verb in play. All mutations operate on
        // copies of this list; the original is never modified and never re-fetched.
        var baseline = stored
            .Where(e => string.Equals(e.Verb, verb, StringComparison.Ordinal))
            .ToList();

        var resolveBudget = MaxResolves;

        // Baseline must be Denied for there to be anything to remediate.
        var baselineResult = Resolve(baseline, nodeKey, pathFromRoot, roleAliases, verb, defaultState, ref resolveBudget);
        if (baselineResult is null || baselineResult.IsAllowed)
        {
            return [];
        }

        var candidates = BuildCandidates(baselineResult, baseline, nodeKey, roleAliases, verb);

        var confirmed = new List<RemediationOption>();
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (resolveBudget <= 0 || confirmed.Count >= MaxOptions)
            {
                break;
            }

            var mutated = candidate.Apply(baseline);
            var result = Resolve(mutated, nodeKey, pathFromRoot, roleAliases, verb, defaultState, ref resolveBudget);
            if (result is { IsAllowed: true })
            {
                confirmed.Add(candidate.Option);
            }
        }

        // Rank least-privileged-first; the enum is declared in that order.
        return confirmed
            .OrderBy(o => (int)o.Kind)
            .ToList();
    }

    /// <summary>
    /// Resolves a single verb against a working entry set, decrementing the shared re-resolve budget.
    /// Returns <see langword="null"/> once the budget is exhausted so callers stop generating work.
    /// </summary>
    /// <param name="entries">The (possibly mutated) entries to resolve against.</param>
    /// <param name="nodeKey">The target node key.</param>
    /// <param name="pathFromRoot">The root-to-target path.</param>
    /// <param name="roleAliases">The role set to resolve with.</param>
    /// <param name="verb">The verb being resolved.</param>
    /// <param name="defaultState">The resolver's default state for the aspect.</param>
    /// <param name="budget">The remaining re-resolve budget, decremented by one on each call.</param>
    /// <returns>The effective permission, or <see langword="null"/> when the budget is exhausted.</returns>
    private EffectivePermission? Resolve(
        IReadOnlyList<AdvancedPermissionEntry> entries,
        Guid nodeKey,
        IReadOnlyList<Guid> pathFromRoot,
        IReadOnlyList<string> roleAliases,
        string verb,
        PermissionState defaultState,
        ref int budget)
    {
        if (budget <= 0)
        {
            return null;
        }

        budget--;

        // Default state is carried only as a label here: the node-level resolver is hard-wired to default
        // Deny (see PermissionResolver). The parameter is honoured for forward-compatibility and to keep
        // the simulation explicit about the aspect it is resolving.
        _ = defaultState;

        var context = new PermissionResolutionContext(
            TargetNodeKey: nodeKey,
            PathFromRoot: pathFromRoot,
            RoleAliases: roleAliases,
            StoredEntries: entries);

        return resolver.Resolve(context, verb);
    }

    /// <summary>
    /// Classifies the baseline denial from its reasoning chain and produces the case-specific candidate
    /// mutations to validate, in roughly least-privileged-first generation order.
    /// </summary>
    /// <param name="baseline">The baseline (Denied) effective permission.</param>
    /// <param name="entries">The current verb-filtered entries.</param>
    /// <param name="nodeKey">The target node key.</param>
    /// <param name="roleAliases">The role set in play.</param>
    /// <param name="verb">The verb being remediated.</param>
    /// <returns>The candidate mutations to validate.</returns>
    private static IReadOnlyList<Candidate> BuildCandidates(
        EffectivePermission baseline,
        IReadOnlyList<AdvancedPermissionEntry> entries,
        Guid nodeKey,
        IReadOnlyList<string> roleAliases,
        string verb)
    {
        var candidates = new List<Candidate>();

        // The role the new (plain) Allow entries target: the first non-everyone role if present, else the
        // first role overall. Keeps a suggestion specific to the asker's group rather than widening All Users.
        var addRole = roleAliases.FirstOrDefault(r => r != AdvancedPermissionsConstants.EveryoneRoleAlias)
            ?? roleAliases[0];

        // The role a priority-override Allow targets. The engine returns only the FIRST applicable entry
        // per role per node, so an override Allow added for a role that already holds a Deny on the node
        // would never be seen — it must target a role with no conflicting same-node Deny. Prefer such a
        // role; fall back to addRole (validation will reject it if it cannot win).
        var nodeDenyRoles = entries
            .Where(e => e.NodeKey == nodeKey && e.State == PermissionState.Deny)
            .Select(e => e.RoleAlias)
            .ToHashSet(StringComparer.Ordinal);
        var overrideRole = roleAliases.FirstOrDefault(r => !nodeDenyRoles.Contains(r)) ?? addRole;

        // Did an explicit Deny on the target node decide this? IsExplicit means the deciding tier was the
        // explicit (depth-0) tier; an explicit Deny reasoning line confirms a Deny sits on the node.
        var explicitDenyReasons = baseline.Reasoning
            .Where(r => r is { IsExplicit: true, State: PermissionState.Deny })
            .ToList();
        var isExplicitDeny = baseline.IsExplicit && explicitDenyReasons.Count > 0;

        if (isExplicitDeny)
        {
            // (a) Remove ALL contributing explicit-Deny entries on the node, together.
            var denyRoles = explicitDenyReasons
                .Select(r => r.ContributingRole)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var denyEntriesToRemove = entries
                .Where(e => e.NodeKey == nodeKey
                            && e.State == PermissionState.Deny
                            && denyRoles.Contains(e.RoleAlias, StringComparer.Ordinal))
                .ToList();

            if (denyEntriesToRemove.Count > 0)
            {
                candidates.Add(Candidate.Remove(
                    new RemediationOption(
                        RemediationActionKind.RemoveDeny,
                        denyRoles[0],
                        verb,
                        nodeKey,
                        Scope: null,
                        RemovedRoleAliases: denyRoles),
                    denyEntriesToRemove));
            }

            // (b) A plain Allow on the node — included so the simulation explicitly REJECTS it (it cannot
            // beat a same-node explicit Deny). This is the bug the feature fixes: never offered unless a
            // re-resolve proves it flips, which here it never will.
            candidates.Add(Candidate.Add(
                new RemediationOption(RemediationActionKind.AddAllowOnNode, addRole, verb, nodeKey, PermissionScope.ThisNodeOnly, []),
                NewAllow(nodeKey, addRole, verb, PermissionScope.ThisNodeOnly, priority: false)));

            // (c) A priority-override Allow on the node — the only addition that can beat a same-node Deny
            // (and is itself defeated by a competing priority-override Deny, which validation will catch).
            candidates.Add(Candidate.Add(
                new RemediationOption(RemediationActionKind.AddPriorityOverrideAllow, overrideRole, verb, nodeKey, PermissionScope.ThisNodeOnly, []),
                NewAllow(nodeKey, overrideRole, verb, PermissionScope.ThisNodeOnly, priority: true)));

            return candidates;
        }

        // No explicit Deny on the node. Either an implicit (inherited/ancestor) Deny decided it, or no
        // role had any opinion (default Deny). Distinguish by whether any Deny reasoning exists.
        var implicitDenyReasons = baseline.Reasoning
            .Where(r => r.State == PermissionState.Deny)
            .ToList();

        // (1) Cheapest universally-applicable add: a plain explicit Allow on the node.
        candidates.Add(Candidate.Add(
            new RemediationOption(RemediationActionKind.AddAllowOnNode, addRole, verb, nodeKey, PermissionScope.ThisNodeOnly, []),
            NewAllow(nodeKey, addRole, verb, PermissionScope.ThisNodeOnly, priority: false)));

        if (implicitDenyReasons.Count > 0)
        {
            // (2) Add an Allow on the nearest contributing ancestor with a scope that reaches the node.
            // The nearest contributing ancestor is the source node of an implicit Deny reasoning line
            // that lies on the path (not the virtual root); pick the deepest such node on the path.
            foreach (var ancestorKey in NearestContributingAncestors(implicitDenyReasons))
            {
                candidates.Add(Candidate.Add(
                    new RemediationOption(RemediationActionKind.AddAllowOnAncestor, addRole, verb, ancestorKey, PermissionScope.ThisNodeAndDescendants, []),
                    NewAllow(ancestorKey, addRole, verb, PermissionScope.ThisNodeAndDescendants, priority: false)));
            }

            // (3) Remove/neutralize ALL contributing implicit Deny entries together.
            var denyRoles = implicitDenyReasons
                .Select(r => r.ContributingRole)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var denySources = implicitDenyReasons
                .Select(r => r.SourceNodeKey)
                .ToHashSet();

            var denyEntriesToRemove = entries
                .Where(e => e.State == PermissionState.Deny
                            && denySources.Contains(e.NodeKey)
                            && denyRoles.Contains(e.RoleAlias, StringComparer.Ordinal))
                .ToList();

            if (denyEntriesToRemove.Count > 0)
            {
                candidates.Add(Candidate.Remove(
                    new RemediationOption(RemediationActionKind.RemoveDeny, denyRoles[0], verb, nodeKey, Scope: null, RemovedRoleAliases: denyRoles),
                    denyEntriesToRemove));
            }

            // (4) Priority-override Allow on the node — heavier fallback that also flips an implicit Deny.
            candidates.Add(Candidate.Add(
                new RemediationOption(RemediationActionKind.AddPriorityOverrideAllow, overrideRole, verb, nodeKey, PermissionScope.ThisNodeOnly, []),
                NewAllow(nodeKey, overrideRole, verb, PermissionScope.ThisNodeOnly, priority: true)));
        }

        // Default-Deny case (no Deny reasoning): only the plain on-node Allow is generated above, and no
        // removal is offered because there is nothing to remove.
        return candidates;
    }

    /// <summary>
    /// Resolves the source node keys of the implicit Deny reasoning lines that sit on the content path
    /// (i.e. real ancestors, excluding the virtual root), ordered nearest-first by their position in the
    /// reasoning chain. Each is a viable place to add a reaching Allow.
    /// </summary>
    /// <param name="implicitDenyReasons">The implicit Deny reasoning lines.</param>
    /// <returns>The distinct contributing ancestor node keys.</returns>
    private static IReadOnlyList<Guid> NearestContributingAncestors(IReadOnlyList<PermissionReasoning> implicitDenyReasons) =>
        implicitDenyReasons
            .Where(r => r.SourceNodeKey != AdvancedPermissionsConstants.VirtualRootNodeKey)
            .Select(r => r.SourceNodeKey)
            .Distinct()
            .ToList();

    /// <summary>Builds a new Allow entry for a candidate addition.</summary>
    /// <param name="nodeKey">The node the entry is set on.</param>
    /// <param name="roleAlias">The role the entry is for.</param>
    /// <param name="verb">The verb the entry grants.</param>
    /// <param name="scope">The scope of the entry.</param>
    /// <param name="priority">Whether the entry carries the priority-override flag.</param>
    /// <returns>The new entry.</returns>
    private static AdvancedPermissionEntry NewAllow(
        Guid nodeKey,
        string roleAlias,
        string verb,
        PermissionScope scope,
        bool priority)
        => new(Guid.NewGuid(), nodeKey, roleAlias, verb, PermissionState.Allow, scope, priority);

    /// <summary>
    /// A single candidate mutation: the friendly-projectable <see cref="RemediationOption"/> it
    /// represents, plus a function that applies the mutation to a copy of the baseline entry list.
    /// </summary>
    /// <param name="Option">The option this candidate would yield if confirmed.</param>
    /// <param name="Apply">Applies the mutation to a copy of the baseline entries.</param>
    private sealed record Candidate(
        RemediationOption Option,
        Func<IReadOnlyList<AdvancedPermissionEntry>, IReadOnlyList<AdvancedPermissionEntry>> Apply)
    {
        /// <summary>Builds an "add an entry" candidate.</summary>
        /// <param name="option">The option this candidate yields.</param>
        /// <param name="entry">The entry to add.</param>
        /// <returns>The candidate.</returns>
        public static Candidate Add(RemediationOption option, AdvancedPermissionEntry entry) =>
            new(option, baseline => [.. baseline, entry]);

        /// <summary>Builds a "remove these entries" candidate.</summary>
        /// <param name="option">The option this candidate yields.</param>
        /// <param name="toRemove">The entries to remove (matched by id).</param>
        /// <returns>The candidate.</returns>
        public static Candidate Remove(RemediationOption option, IReadOnlyList<AdvancedPermissionEntry> toRemove)
        {
            var ids = toRemove.Select(e => e.Id).ToHashSet();
            return new Candidate(option, baseline => baseline.Where(e => !ids.Contains(e.Id)).ToList());
        }
    }
}
