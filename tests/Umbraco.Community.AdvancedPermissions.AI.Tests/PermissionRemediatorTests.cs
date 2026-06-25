using NSubstitute;
using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.AI.Services;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;
using Umbraco.Community.AdvancedPermissions.Core.Services;

namespace Umbraco.Community.AdvancedPermissions.AI.Tests;

/// <summary>
/// Unit tests for <see cref="PermissionRemediator"/>. The repository is mocked to supply the stored
/// entries; the <b>real</b> <see cref="PermissionResolver"/>/<see cref="ResolutionEngine"/> is used —
/// that is the whole point of the simulation, so it is never mocked. Each test asserts the CONFIRMED
/// candidate set: a candidate is returned only when re-resolving the mutated entries actually flips the
/// verdict to Allowed, and rejected otherwise.
/// </summary>
public sealed class PermissionRemediatorTests
{
    /// <summary>The verb under test throughout (node-level, default Deny).</summary>
    private const string Verb = AdvancedPermissionsConstants.VerbDelete;

    /// <summary>The mocked repository supplying the current stored entries.</summary>
    private readonly IAdvancedPermissionRepository _repository = Substitute.For<IAdvancedPermissionRepository>();

    /// <summary>The real, pure resolver — never mocked.</summary>
    private readonly IPermissionResolver _resolver = new PermissionResolver();

    /// <summary>Builds the system under test over the current mocks.</summary>
    /// <returns>A fresh remediator.</returns>
    private PermissionRemediator CreateSut() => new(_resolver, _repository);

    /// <summary>Creates an entry with a generated id.</summary>
    /// <param name="nodeKey">The node the entry applies to.</param>
    /// <param name="role">The role alias.</param>
    /// <param name="state">Allow or Deny.</param>
    /// <param name="scope">The scope; defaults to ThisNodeAndDescendants.</param>
    /// <param name="priority">Whether the entry is a priority override.</param>
    /// <returns>A new entry for <see cref="Verb"/>.</returns>
    private static AdvancedPermissionEntry Entry(
        Guid nodeKey,
        string role,
        PermissionState state,
        PermissionScope scope = PermissionScope.ThisNodeAndDescendants,
        bool priority = false)
        => new(Guid.NewGuid(), nodeKey, role, Verb, state, scope, priority);

    /// <summary>Configures the repository to return the given entries for any roles+nodes read.</summary>
    /// <param name="entries">The stored entries to return.</param>
    private void SetupEntries(params AdvancedPermissionEntry[] entries) =>
        _repository
            .GetByRolesAndNodesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(entries);

    /// <summary>
    /// THE BUG-FIX TEST. Explicit Deny on the node for the user's role: a plain Allow does NOT flip it
    /// (the simulation must reject it), but removing the Deny and adding a priority-override Allow both
    /// do — and only those two are returned.
    /// </summary>
    [Fact]
    public async Task ExplicitDeny_PlainAllowRejected_RemoveAndOverrideReturned()
    {
        var root = Guid.NewGuid();
        var target = Guid.NewGuid();
        // An explicit Deny on the node suppresses an underlying implicit Allow (the All Users default).
        // The Deny is meaningful precisely because something would otherwise grant access — so removing
        // it reveals that Allow, which is what makes "remove the Deny" a genuine fix here.
        SetupEntries(
            Entry(target, "editors", PermissionState.Deny, PermissionScope.ThisNodeOnly),
            Entry(AdvancedPermissionsConstants.VirtualRootNodeKey, AdvancedPermissionsConstants.EveryoneRoleAlias, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants));

        var options = await CreateSut().SuggestAsync(
            target, [root, target], ["editors", AdvancedPermissionsConstants.EveryoneRoleAlias],
            Verb, PermissionState.Deny, CancellationToken.None);

        // A plain Allow cannot beat a same-node explicit Deny — must NOT be offered.
        Assert.DoesNotContain(options, o => o.Kind == RemediationActionKind.AddAllowOnNode);

        // Both valid fixes returned.
        Assert.Contains(options, o => o.Kind == RemediationActionKind.RemoveDeny);
        Assert.Contains(options, o => o.Kind == RemediationActionKind.AddPriorityOverrideAllow);
    }

    /// <summary>
    /// Override-vs-override: a competing priority-override Deny on the node defeats any priority-override
    /// Allow (Deny wins among flagged survivors), so the override-Allow candidate is rejected. Removing
    /// the contributing denies is still valid.
    /// </summary>
    [Fact]
    public async Task OverrideVsOverride_PriorityOverrideAllowRejected()
    {
        var root = Guid.NewGuid();
        var target = Guid.NewGuid();
        // $everyone holds a priority-override Deny on the node, over an underlying implicit Allow.
        SetupEntries(
            Entry(target, AdvancedPermissionsConstants.EveryoneRoleAlias, PermissionState.Deny, PermissionScope.ThisNodeOnly, priority: true),
            Entry(AdvancedPermissionsConstants.VirtualRootNodeKey, "editors", PermissionState.Allow, PermissionScope.ThisNodeAndDescendants));

        var options = await CreateSut().SuggestAsync(
            target, [root, target], ["editors", AdvancedPermissionsConstants.EveryoneRoleAlias],
            Verb, PermissionState.Deny, CancellationToken.None);

        // A priority-override Allow cannot beat a competing priority-override Deny → rejected.
        Assert.DoesNotContain(options, o => o.Kind == RemediationActionKind.AddPriorityOverrideAllow);
        // A plain Allow certainly cannot.
        Assert.DoesNotContain(options, o => o.Kind == RemediationActionKind.AddAllowOnNode);
        // Removing the contributing Deny reveals the underlying Allow and flips it.
        Assert.Contains(options, o => o.Kind == RemediationActionKind.RemoveDeny);
    }

    /// <summary>
    /// Implicit Deny (inherited from an ancestor): a plain explicit Allow on the node flips it — that is
    /// the cheapest fix and must be returned.
    /// </summary>
    [Fact]
    public async Task ImplicitDeny_AddPlainAllowOnNode_Returned()
    {
        var root = Guid.NewGuid();
        var parent = Guid.NewGuid();
        var target = Guid.NewGuid();
        // editors denied via inheritance from the parent.
        SetupEntries(Entry(parent, "editors", PermissionState.Deny, PermissionScope.ThisNodeAndDescendants));

        var options = await CreateSut().SuggestAsync(
            target, [root, parent, target], ["editors", AdvancedPermissionsConstants.EveryoneRoleAlias],
            Verb, PermissionState.Deny, CancellationToken.None);

        Assert.Contains(options, o => o.Kind == RemediationActionKind.AddAllowOnNode);
        // The cheapest option must rank first.
        Assert.Equal(RemediationActionKind.AddAllowOnNode, options[0].Kind);
    }

    /// <summary>
    /// Default Deny (no entries at all): adding a plain Allow on the node flips it, and no removal is
    /// offered because there is nothing to remove.
    /// </summary>
    [Fact]
    public async Task DefaultDeny_AddAllowReturned_NoRemovalOffered()
    {
        var root = Guid.NewGuid();
        var target = Guid.NewGuid();
        SetupEntries();

        var options = await CreateSut().SuggestAsync(
            target, [root, target], ["editors", AdvancedPermissionsConstants.EveryoneRoleAlias],
            Verb, PermissionState.Deny, CancellationToken.None);

        Assert.Contains(options, o => o.Kind == RemediationActionKind.AddAllowOnNode);
        Assert.DoesNotContain(options, o => o.Kind == RemediationActionKind.RemoveDeny);
    }

    /// <summary>
    /// Multi-deny: two roles both hold a contributing explicit Deny on the node. The removal candidate
    /// must remove BOTH together — removing only one does not flip the verdict, so a partial removal must
    /// never be returned. The returned removal lists both roles.
    /// </summary>
    [Fact]
    public async Task MultiDeny_RemovalRemovesAllContributingDenies()
    {
        var root = Guid.NewGuid();
        var target = Guid.NewGuid();
        // Two roles each hold an explicit Deny on the node, suppressing an underlying inherited Allow.
        // Only removing BOTH denies reveals the Allow; removing one leaves the other still denying.
        SetupEntries(
            Entry(target, "editors", PermissionState.Deny, PermissionScope.ThisNodeOnly),
            Entry(target, AdvancedPermissionsConstants.EveryoneRoleAlias, PermissionState.Deny, PermissionScope.ThisNodeOnly),
            Entry(root, "editors", PermissionState.Allow, PermissionScope.ThisNodeAndDescendants));

        var options = await CreateSut().SuggestAsync(
            target, [root, target], ["editors", AdvancedPermissionsConstants.EveryoneRoleAlias],
            Verb, PermissionState.Deny, CancellationToken.None);

        var removal = Assert.Single(options, o => o.Kind == RemediationActionKind.RemoveDeny);
        Assert.Contains("editors", removal.RemovedRoleAliases);
        Assert.Contains(AdvancedPermissionsConstants.EveryoneRoleAlias, removal.RemovedRoleAliases);
        Assert.Equal(2, removal.RemovedRoleAliases.Count);
    }

    /// <summary>
    /// An add-at-node candidate is never emitted with the DescendantsOnly scope, because such an entry
    /// does not apply at the node itself (depth 0) and so cannot flip the node's own verdict.
    /// </summary>
    [Fact]
    public async Task AddAtNode_NeverUsesDescendantsOnlyScope()
    {
        var root = Guid.NewGuid();
        var target = Guid.NewGuid();
        SetupEntries(Entry(target, "editors", PermissionState.Deny, PermissionScope.ThisNodeOnly));

        var options = await CreateSut().SuggestAsync(
            target, [root, target], ["editors", AdvancedPermissionsConstants.EveryoneRoleAlias],
            Verb, PermissionState.Deny, CancellationToken.None);

        Assert.DoesNotContain(
            options,
            o => o.NodeKey == target && o.Scope == PermissionScope.DescendantsOnly);
    }

    /// <summary>
    /// When the baseline already resolves to Allowed, there is nothing to remediate — the service returns
    /// an empty list and offers no changes.
    /// </summary>
    [Fact]
    public async Task BaselineAlreadyAllowed_ReturnsEmpty()
    {
        var root = Guid.NewGuid();
        var target = Guid.NewGuid();
        SetupEntries(Entry(target, "editors", PermissionState.Allow, PermissionScope.ThisNodeOnly));

        var options = await CreateSut().SuggestAsync(
            target, [root, target], ["editors", AdvancedPermissionsConstants.EveryoneRoleAlias],
            Verb, PermissionState.Deny, CancellationToken.None);

        Assert.Empty(options);
    }

    /// <summary>
    /// The service reads the repository exactly once and never re-reads per candidate — the single read
    /// is reused across all simulated re-resolutions.
    /// </summary>
    [Fact]
    public async Task ReadsRepositoryOnce()
    {
        var root = Guid.NewGuid();
        var target = Guid.NewGuid();
        SetupEntries(Entry(target, "editors", PermissionState.Deny, PermissionScope.ThisNodeOnly));

        await CreateSut().SuggestAsync(
            target, [root, target], ["editors", AdvancedPermissionsConstants.EveryoneRoleAlias],
            Verb, PermissionState.Deny, CancellationToken.None);

        await _repository.Received(1).GetByRolesAndNodesAsync(
            Arg.Any<IEnumerable<string>>(), Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Role subject semantics: with a single-role set excluding <c>$everyone</c>, an inherited Deny that
    /// belongs to that role is flipped by a plain on-node Allow.
    /// </summary>
    [Fact]
    public async Task RoleSubject_SingleRoleSet_ImplicitDenyFixedByAllow()
    {
        var root = Guid.NewGuid();
        var parent = Guid.NewGuid();
        var target = Guid.NewGuid();
        SetupEntries(Entry(parent, "editors", PermissionState.Deny, PermissionScope.ThisNodeAndDescendants));

        var options = await CreateSut().SuggestAsync(
            target, [root, parent, target], ["editors"],
            Verb, PermissionState.Deny, CancellationToken.None);

        Assert.Contains(options, o => o.Kind == RemediationActionKind.AddAllowOnNode);
    }
}
