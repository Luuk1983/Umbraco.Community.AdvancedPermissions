using System.Security.Cryptography;
using System.Text;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.Core.Concurrency;

/// <summary>
/// Computes a concurrency stamp over the stored permission entries for one node and user group.
/// </summary>
/// <remarks>
/// <para>
/// The stamp is what lets the server tell "this client is writing back a picture it loaded before
/// somebody else changed it" from an ordinary save. It is a hash rather than a row version because
/// it needs no schema change and behaves identically on SQLite and SQL Server, and because the
/// unit it has to describe is a *set* of rows, which no single row's version can stand for.
/// </para>
/// <para>
/// Only content is hashed, never identity: two entries that say the same thing produce the same
/// stamp even though their row ids differ, because re-saving the identical permission must not
/// look like a change to anybody else.
/// </para>
/// <para>
/// The canonical form is part of the wire contract — the client compares a stamp it was handed
/// earlier — so changing it rejects saves that are valid. <c>PermissionStampTests</c> holds a
/// golden vector that fails if this changes by accident.
/// </para>
/// </remarks>
public static class PermissionStamp
{
    /// <summary>
    /// The separator between fields within one entry. Each entry's verb is length-prefixed
    /// before this separator is ever written, so an occurrence of this character inside a verb
    /// cannot be misread as field structure — the separator only has to be improbable in
    /// practice, never impossible, because the length prefix already makes the canonical string
    /// uniquely decodable.
    /// </summary>
    private const char FieldSeparator = '\u001f';

    /// <summary>
    /// The separator between entries. As with <see cref="FieldSeparator"/>, the per-entry length
    /// prefix makes the canonical string uniquely decodable regardless of what an entry's verb
    /// contains, so this separator does not need to be forbidden in verbs — only unlikely.
    /// </summary>
    private const char EntrySeparator = '\u001e';

    /// <summary>
    /// Computes the stamp for a set of entries.
    /// </summary>
    /// <param name="entries">
    /// The stored entries for one node and user group, in any order. An empty set is valid and
    /// hashes to one fixed value.
    /// </param>
    /// <returns>The stamp, as lowercase hex.</returns>
    public static string Compute(IEnumerable<AdvancedPermissionEntry> entries) =>
        ComputeCore(entries.Select(e => (e.Verb, (int)e.State, (int)e.Scope, e.IsPriorityOverride)));

    /// <summary>
    /// Computes the stamp for a set of document-type entries.
    /// </summary>
    /// <remarks>
    /// The stamp deliberately covers the same four fields as the node-level overload — verb, state,
    /// scope and priority override — and not the node, user group or document type. Those three
    /// identify which set is being stamped and are already fixed by the caller, so the value a
    /// client was handed over the wire (computed from response models) matches the one computed
    /// here from stored rows. A distinct name rather than an overload of <c>Compute</c>, so an empty
    /// collection expression (<c>Compute([])</c>) stays unambiguous.
    /// </remarks>
    /// <param name="entries">
    /// The stored entries for one node, user group and document type triple, in any order.
    /// </param>
    /// <returns>The stamp, as lowercase hex.</returns>
    public static string ComputeForDocType(IEnumerable<DocTypePermissionEntry> entries) =>
        ComputeCore(entries.Select(e => (e.Verb, (int)e.State, (int)e.Scope, e.IsPriorityOverride)));

    /// <summary>
    /// Computes the stamp from entries whose state and scope are already strings, as the API
    /// response models carry them.
    /// </summary>
    /// <param name="entries">Verb, state name, scope name and priority-override flag per entry.</param>
    /// <returns>The stamp, as lowercase hex.</returns>
    /// <exception cref="ArgumentException">A state or scope name is not a known value.</exception>
    public static string ComputeFromNames(IEnumerable<(string Verb, string State, string Scope, bool IsPriorityOverride)> entries) =>
        ComputeCore(entries.Select(e =>
        {
            if (!Enum.TryParse<PermissionState>(e.State, ignoreCase: true, out var state))
            {
                throw new ArgumentException($"'{e.State}' is not a valid permission state.", nameof(entries));
            }

            if (!Enum.TryParse<PermissionScope>(e.Scope, ignoreCase: true, out var scope))
            {
                throw new ArgumentException($"'{e.Scope}' is not a valid permission scope.", nameof(entries));
            }

            return (e.Verb, (int)state, (int)scope, e.IsPriorityOverride);
        }));

    /// <summary>
    /// The one canonicalisation, shared by both entry points so the two can never disagree.
    /// </summary>
    /// <param name="entries">Verb, state ordinal, scope ordinal and priority-override flag per entry.</param>
    /// <returns>The stamp, as lowercase hex.</returns>
    private static string ComputeCore(IEnumerable<(string Verb, int State, int Scope, bool IsPriorityOverride)> entries)
    {
        var canonical = new StringBuilder();

        // Ordered on every field the stamp covers, so two sets that differ only in the order they
        // came back from the database — which EF does not promise to keep stable — hash the same.
        var ordered = entries
            .OrderBy(e => e.Verb, StringComparer.Ordinal)
            .ThenBy(e => e.State)
            .ThenBy(e => e.Scope)
            .ThenBy(e => e.IsPriorityOverride);

        foreach (var entry in ordered)
        {
            // The verb is length-prefixed, and that is not decoration. It is the only
            // variable-length field, and without the prefix a verb containing a separator
            // character could be read as structure: a single entry whose verb embedded the
            // right bytes would canonicalise identically to two unrelated entries, and two
            // different permission pictures would hash the same — silently defeating the
            // concurrency check this whole function exists to provide.
            canonical
                .Append(entry.Verb.Length).Append(FieldSeparator)
                .Append(entry.Verb).Append(FieldSeparator)
                .Append(entry.State).Append(FieldSeparator)
                .Append(entry.Scope).Append(FieldSeparator)
                .Append(entry.IsPriorityOverride ? '1' : '0').Append(EntrySeparator);
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));
        return Convert.ToHexStringLower(hash);
    }
}
