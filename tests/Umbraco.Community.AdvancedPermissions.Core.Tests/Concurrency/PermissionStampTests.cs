using Umbraco.Community.AdvancedPermissions.Core.Concurrency;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.Core.Tests.Concurrency;

/// <summary>
/// Tests for <see cref="PermissionStamp"/>, the concurrency stamp used to detect that stored
/// permission entries moved underneath a client between load and save.
/// </summary>
public sealed class PermissionStampTests
{
    /// <summary>Builds an entry with the fields the stamp is computed from.</summary>
    /// <param name="verb">The permission verb.</param>
    /// <param name="state">The permission state.</param>
    /// <param name="scope">The permission scope.</param>
    /// <param name="isPriorityOverride">Whether the entry is a priority override.</param>
    /// <returns>The entry.</returns>
    private static AdvancedPermissionEntry Entry(
        string verb,
        PermissionState state = PermissionState.Allow,
        PermissionScope scope = PermissionScope.ThisNodeOnly,
        bool isPriorityOverride = false) =>
        new(
            Guid.NewGuid(),
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "editors",
            verb,
            state,
            scope,
            isPriorityOverride);

    /// <summary>An empty set must hash to one fixed, non-empty value rather than to nothing.</summary>
    [Fact]
    public void Compute_EmptySet_ReturnsStableNonEmptyStamp()
    {
        var first = PermissionStamp.Compute([]);
        var second = PermissionStamp.Compute([]);

        Assert.False(string.IsNullOrWhiteSpace(first));
        Assert.Equal(first, second);
    }

    /// <summary>The stamp must not depend on the order entries arrive in.</summary>
    [Fact]
    public void Compute_ReorderedEntries_ReturnsSameStamp()
    {
        var a = Entry("Umb.Document.Read");
        var b = Entry("Umb.Document.Publish", PermissionState.Deny);

        Assert.Equal(PermissionStamp.Compute([a, b]), PermissionStamp.Compute([b, a]));
    }

    /// <summary>The row id is identity, not content, so it must not affect the stamp.</summary>
    [Fact]
    public void Compute_DifferentIdsSameContent_ReturnsSameStamp()
    {
        var a = Entry("Umb.Document.Read");
        var b = Entry("Umb.Document.Read");

        Assert.NotEqual(a.Id, b.Id);
        Assert.Equal(PermissionStamp.Compute([a]), PermissionStamp.Compute([b]));
    }

    /// <summary>Every field the stamp claims to cover must actually change it.</summary>
    /// <param name="verb">The verb to use.</param>
    /// <param name="state">The state to use.</param>
    /// <param name="scope">The scope to use.</param>
    /// <param name="isPriorityOverride">The priority-override flag to use.</param>
    [Theory]
    [InlineData("Umb.Document.Publish", PermissionState.Allow, PermissionScope.ThisNodeOnly, false)]
    [InlineData("Umb.Document.Read", PermissionState.Deny, PermissionScope.ThisNodeOnly, false)]
    [InlineData("Umb.Document.Read", PermissionState.Allow, PermissionScope.DescendantsOnly, false)]
    [InlineData("Umb.Document.Read", PermissionState.Allow, PermissionScope.ThisNodeOnly, true)]
    public void Compute_AnyFieldChanged_ReturnsDifferentStamp(
        string verb,
        PermissionState state,
        PermissionScope scope,
        bool isPriorityOverride)
    {
        var baseline = PermissionStamp.Compute([Entry("Umb.Document.Read")]);
        var changed = PermissionStamp.Compute([Entry(verb, state, scope, isPriorityOverride)]);

        Assert.NotEqual(baseline, changed);
    }

    /// <summary>
    /// A golden vector. The stamp crosses the wire and is compared against a value the client
    /// was handed earlier, so an accidental change to the canonical form is a breaking change:
    /// it would reject saves that are perfectly valid. This test is the tripwire for that.
    /// </summary>
    [Fact]
    public void Compute_KnownInput_MatchesGoldenVector()
    {
        var entries = new[]
        {
            Entry("Umb.Document.Read"),
            Entry("Umb.Document.Publish", PermissionState.Deny, PermissionScope.ThisNodeAndDescendants, true),
        };

        Assert.Equal("9f85a7d0029547aeb1e563976fed94d204495c1e6ae8357137dbd0df172676dd", PermissionStamp.Compute(entries));
    }

    /// <summary>
    /// A golden vector for the empty set: its stamp is the SHA-256 of the empty string, spelled out
    /// here as a literal rather than computed, so it is an independent statement of the value and not a
    /// copy of the implementation.
    /// </summary>
    /// <remarks>
    /// The client hard-codes this same value (<c>EMPTY_SET_STAMP</c> in <c>src/live/stamp.ts</c>) and
    /// sends it as the expected stamp for a node with no stored entries. It has to, because the
    /// client cannot call <c>PermissionStamp.Compute</c>, and it used to send no stamp at all for such
    /// a node - which the server reads as "skip the concurrency check", so two editors saving to a
    /// node that started empty silently overwrote each other. Those are two independent definitions of
    /// one constant; nothing else pins either of them, and if the server's empty-set hash ever moved
    /// (a version prefix, a different canonical form) the client would still send the old value and
    /// every first save to an empty node would be refused as a conflict nobody caused. The populated
    /// golden vector above cannot see this: it never exercises the empty case, so a change that only
    /// touched the empty set - for instance a special case returning a different constant - would
    /// leave it green.
    /// </remarks>
    [Fact]
    public void Compute_EmptySet_MatchesGoldenVector_TheSha256OfTheEmptyString()
    {
        const string sha256OfEmptyString = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

        Assert.Equal(sha256OfEmptyString, PermissionStamp.Compute([]));
        Assert.Equal(sha256OfEmptyString, PermissionStamp.ComputeForDocType([]));
        Assert.Equal(sha256OfEmptyString, PermissionStamp.ComputeFromNames([]));
    }

    /// <summary>
    /// Reproduces the field-injection collision the stamp must not have: a single entry whose
    /// verb happens to contain the raw separator characters must not canonicalize to the same
    /// string as an unrelated two-entry set that "looks like" the same bytes once joined without
    /// a length prefix.
    /// </summary>
    [Fact]
    public void Compute_VerbContainingSeparators_DoesNotCollideWithMultipleEntries()
    {
        var collidingEntry = Entry(
            "P\u001f1\u001f1\u001f0\u001eQ",
            PermissionState.Deny,
            PermissionScope.ThisNodeAndDescendants,
            isPriorityOverride: true);

        var unrelatedEntries = new[]
        {
            Entry("P", PermissionState.Allow, PermissionScope.ThisNodeOnly, isPriorityOverride: false),
            Entry("Q", PermissionState.Deny, PermissionScope.ThisNodeAndDescendants, isPriorityOverride: true),
        };

        Assert.NotEqual(PermissionStamp.Compute([collidingEntry]), PermissionStamp.Compute(unrelatedEntries));
    }

    /// <summary>
    /// Locks in that entries are hashed as a multiset, not a set: a duplicated entry (same
    /// content, different <see cref="AdvancedPermissionEntry.Id"/>) must still change the stamp
    /// relative to the single entry, so a future "distinct by content" refactor cannot make an
    /// added duplicate row disappear from the stamp unnoticed.
    /// </summary>
    [Fact]
    public void Compute_DuplicateEntries_DiffersFromSingleEntry()
    {
        var a = Entry("Umb.Document.Read");
        var aDuplicate = Entry("Umb.Document.Read");

        Assert.NotEqual(PermissionStamp.Compute([a]), PermissionStamp.Compute([a, aDuplicate]));
    }

    /// <summary>
    /// The doc-type entry point must agree with the node one for the same content, and with the
    /// string-based one the wire uses. The repository computes a triple's current stamp with
    /// <see cref="PermissionStamp.ComputeForDocType"/> from stored rows and compares it with the
    /// stamp the client was handed, which the controller computed from response models; if the
    /// three ever disagreed, every doc-type save would be refused as a conflict nobody caused.
    /// </summary>
    [Fact]
    public void ComputeForDocType_AgreesWithComputeAndComputeFromNames_ForTheSameContent()
    {
        var docTypeEntries = new[]
        {
            new DocTypePermissionEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "editors", "Umb.Document.CreateOfType", PermissionState.Deny, PermissionScope.ThisNodeAndDescendants, true),
            new DocTypePermissionEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "editors", "Umb.Document.CreateOfType", PermissionState.Allow, PermissionScope.DescendantsOnly, false),
        };
        var nodeEntries = docTypeEntries
            .Select(e => Entry(e.Verb, e.State, e.Scope, e.IsPriorityOverride))
            .ToArray();
        var named = docTypeEntries.Select(e => (e.Verb, e.State.ToString(), e.Scope.ToString(), e.IsPriorityOverride));

        var stamp = PermissionStamp.ComputeForDocType(docTypeEntries);

        Assert.Equal(PermissionStamp.Compute(nodeEntries), stamp);
        Assert.Equal(PermissionStamp.ComputeFromNames(named), stamp);
        Assert.Equal(PermissionStamp.Compute([]), PermissionStamp.ComputeForDocType([]));
    }
}
