using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.Core.Services;

/// <summary>
/// Resolves effective permissions by applying the advanced security inheritance and priority rules.
/// This is a thin adapter over <see cref="ResolutionEngine"/> that pre-filters the supplied
/// entries by verb and delegates the actual algorithm to the engine.
/// </summary>
/// <remarks>
/// Default state for node-level permissions is <see cref="PermissionState.Deny"/> — the
/// existing safe-by-default behavior. See <see cref="ResolutionEngine"/> for the full algorithm.
/// </remarks>
public sealed class PermissionResolver : IPermissionResolver
{
    /// <inheritdoc />
    public EffectivePermission Resolve(PermissionResolutionContext context, string verb)
    {
        var entries = FilterByVerb(context.StoredEntries, verb);

        return ResolutionEngine.Resolve(
            pathFromRoot: context.PathFromRoot,
            roleAliases: context.RoleAliases,
            entries: entries,
            verb: verb,
            defaultState: PermissionState.Deny);
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
    /// Projects domain entries matching the requested verb into the verb-agnostic
    /// <see cref="ResolutionEntry"/> shape consumed by the engine.
    /// </summary>
    /// <param name="entries">All entries available in the resolution context.</param>
    /// <param name="verb">The verb to filter by.</param>
    /// <returns>A list of resolution entries for that verb only.</returns>
    private static IReadOnlyList<ResolutionEntry> FilterByVerb(
        IReadOnlyList<AdvancedPermissionEntry> entries,
        string verb)
    {
        var result = new List<ResolutionEntry>(entries.Count);

        foreach (var entry in entries)
        {
            if (!string.Equals(entry.Verb, verb, StringComparison.Ordinal))
            {
                continue;
            }

            result.Add(new ResolutionEntry(
                NodeKey: entry.NodeKey,
                RoleAlias: entry.RoleAlias,
                State: entry.State,
                Scope: entry.Scope,
                IsPriorityOverride: entry.IsPriorityOverride));
        }

        return result;
    }
}
