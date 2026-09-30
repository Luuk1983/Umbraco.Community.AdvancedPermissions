namespace Umbraco.Community.AdvancedPermissions.Core.Exceptions;

/// <summary>
/// The document-type counterpart of <see cref="PermissionConcurrencyException"/>: thrown by a
/// repository write when the entries for a node, user group and document type triple no longer
/// match the stamp the writer expected.
/// </summary>
/// <remarks>
/// A separate type because a conflict here is identified by a triple and carries document-type
/// entries; folding both into one exception would make every catch site check which kind it was
/// holding. The guarantees are the same: raised inside the write transaction, nothing written for
/// any triple in the batch, and every conflicted triple listed. A class rather than a record
/// because an exception has to derive from <see cref="Exception"/>.
/// </remarks>
/// <param name="conflicts">Every triple whose stored entries did not match, at least one.</param>
/// <exception cref="ArgumentException"><paramref name="conflicts"/> is empty.</exception>
public sealed class DocTypePermissionConcurrencyException(IReadOnlyList<DocTypePermissionConflict> conflicts)
    : Exception(BuildMessage(conflicts))
{
    /// <summary>
    /// Gets every triple whose stored entries no longer matched the expected stamp, in the order
    /// the triples appeared in the batch.
    /// </summary>
    public IReadOnlyList<DocTypePermissionConflict> Conflicts { get; } = conflicts;

    /// <summary>
    /// Builds the message, and rejects an empty conflict list for the same reason
    /// <see cref="PermissionConcurrencyException"/> does.
    /// </summary>
    /// <param name="conflicts">The conflicted triples.</param>
    /// <returns>The exception message.</returns>
    /// <exception cref="ArgumentException"><paramref name="conflicts"/> is empty.</exception>
    private static string BuildMessage(IReadOnlyList<DocTypePermissionConflict> conflicts)
    {
        if (conflicts.Count == 0)
        {
            throw new ArgumentException("A concurrency exception must carry at least one conflict.", nameof(conflicts));
        }

        return $"{conflicts.Count} document-type permission set(s) changed since they were read; nothing was written.";
    }
}
