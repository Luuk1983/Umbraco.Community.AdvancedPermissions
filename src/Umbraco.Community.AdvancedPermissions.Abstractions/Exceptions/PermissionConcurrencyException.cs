namespace Umbraco.Community.AdvancedPermissions.Core.Exceptions;

/// <summary>
/// Thrown by a repository write when the entries it was about to replace no longer match the stamp
/// the writer expected — somebody else changed them after the writer read them.
/// </summary>
/// <remarks>
/// <para>
/// Raised from <em>inside</em> the write transaction, after the current entries were read under the
/// same isolation that protects the delete that would have followed. That is the whole point of it
/// being an exception rather than a pre-check in the caller: a check made before the write is a
/// separate operation, and two writers carrying the same expected stamp can both pass it.
/// </para>
/// <para>
/// When it is thrown nothing was written for any pair in the batch — the write is all or nothing —
/// and <see cref="Conflicts"/> lists every conflicted pair, not just the first, so the person can
/// resolve them in one pass.
/// </para>
/// <para>
/// A class rather than a record: an exception has to derive from <see cref="Exception"/>.
/// </para>
/// </remarks>
/// <param name="conflicts">Every pair whose stored entries did not match, at least one.</param>
/// <exception cref="ArgumentException"><paramref name="conflicts"/> is empty.</exception>
public sealed class PermissionConcurrencyException(IReadOnlyList<PermissionConflict> conflicts)
    : Exception(BuildMessage(conflicts))
{
    /// <summary>
    /// Gets every pair whose stored entries no longer matched the expected stamp, in the order the
    /// pairs appeared in the batch.
    /// </summary>
    public IReadOnlyList<PermissionConflict> Conflicts { get; } = conflicts;

    /// <summary>
    /// Builds the message, and rejects an empty conflict list: an exception that says "there were
    /// conflicts" while carrying none would send a caller down the conflict path with nothing to show.
    /// </summary>
    /// <param name="conflicts">The conflicted pairs.</param>
    /// <returns>The exception message.</returns>
    /// <exception cref="ArgumentException"><paramref name="conflicts"/> is empty.</exception>
    private static string BuildMessage(IReadOnlyList<PermissionConflict> conflicts)
    {
        if (conflicts.Count == 0)
        {
            throw new ArgumentException("A concurrency exception must carry at least one conflict.", nameof(conflicts));
        }

        return $"{conflicts.Count} permission set(s) changed since they were read; nothing was written.";
    }
}
