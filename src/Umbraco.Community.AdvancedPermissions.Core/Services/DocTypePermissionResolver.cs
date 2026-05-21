using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.Core.Services;

/// <summary>
/// Thin adapter over <see cref="ResolutionEngine"/> that pre-filters doc-type entries by verb
/// and content-type-key, then delegates the algorithm. Default state is <see cref="PermissionState.Allow"/>
/// — by default any doc-type is creatable; entries narrow by exception.
/// </summary>
public sealed class DocTypePermissionResolver : IDocTypePermissionResolver
{
    /// <inheritdoc />
    public EffectivePermission Resolve(DocTypePermissionResolutionContext context, string verb)
    {
        var entries = FilterByVerbAndContentType(context.StoredEntries, verb, context.ContentTypeKey);

        return ResolutionEngine.Resolve(
            pathFromRoot: context.PathFromRoot,
            roleAliases: context.RoleAliases,
            entries: entries,
            verb: verb,
            defaultState: PermissionState.Allow);
    }

    /// <summary>
    /// Projects domain entries matching the requested verb and content-type-key into the
    /// verb-agnostic <see cref="ResolutionEntry"/> shape consumed by the engine.
    /// </summary>
    /// <param name="entries">All doc-type entries available in the resolution context.</param>
    /// <param name="verb">The verb to filter by.</param>
    /// <param name="contentTypeKey">The content-type-key to filter by.</param>
    /// <returns>A list of resolution entries matching both filters.</returns>
    private static IReadOnlyList<ResolutionEntry> FilterByVerbAndContentType(
        IReadOnlyList<DocTypePermissionEntry> entries,
        string verb,
        Guid contentTypeKey)
    {
        var result = new List<ResolutionEntry>(entries.Count);

        foreach (var entry in entries)
        {
            if (entry.ContentTypeKey != contentTypeKey)
            {
                continue;
            }

            if (!string.Equals(entry.Verb, verb, StringComparison.Ordinal))
            {
                continue;
            }

            result.Add(new ResolutionEntry(
                NodeKey: entry.NodeKey,
                RoleAlias: entry.RoleAlias,
                State: entry.State,
                Scope: entry.Scope));
        }

        return result;
    }
}
