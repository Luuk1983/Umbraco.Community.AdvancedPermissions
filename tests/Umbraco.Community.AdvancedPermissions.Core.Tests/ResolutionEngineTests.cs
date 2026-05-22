using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Models;
using Umbraco.Community.AdvancedPermissions.Core.Services;

namespace Umbraco.Community.AdvancedPermissions.Core.Tests;

/// <summary>
/// Unit tests for the pure <see cref="ResolutionEngine"/>. These verify the algorithm in
/// isolation from the existing <see cref="PermissionResolver"/> wrapper, and exercise the
/// default-state parameter that distinguishes node-level permissions (default Deny) from
/// doc-type permissions (default Allow).
/// </summary>
public class ResolutionEngineTests
{
    private static ResolutionEntry Entry(
        Guid nodeKey,
        string roleAlias,
        PermissionState state,
        PermissionScope scope)
        => new(nodeKey, roleAlias, state, scope);

    // ───────────────────────────────────────────────────────────────────────
    // Default-state behavior (the new knob)
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// With no entries and default = Deny, the result is denied (existing behavior).
    /// </summary>
    [Fact]
    public void Resolve_NoEntries_DefaultDeny_ReturnsDeny()
    {
        var root = Guid.NewGuid();
        var target = Guid.NewGuid();

        var result = ResolutionEngine.Resolve(
            pathFromRoot: [root, target],
            roleAliases: ["editors"],
            entries: [],
            verb: "TestVerb",
            defaultState: PermissionState.Deny);

        Assert.False(result.IsAllowed);
        Assert.False(result.IsExplicit);
        Assert.Empty(result.Reasoning);
    }

    /// <summary>
    /// With no entries and default = Allow, the result is allowed (new doc-type behavior).
    /// </summary>
    [Fact]
    public void Resolve_NoEntries_DefaultAllow_ReturnsAllow()
    {
        var root = Guid.NewGuid();
        var target = Guid.NewGuid();

        var result = ResolutionEngine.Resolve(
            pathFromRoot: [root, target],
            roleAliases: ["editors"],
            entries: [],
            verb: "Umb.Document.CreateOfType",
            defaultState: PermissionState.Allow);

        Assert.True(result.IsAllowed);
        Assert.False(result.IsExplicit);
        Assert.Empty(result.Reasoning);
    }

    /// <summary>
    /// An explicit Deny still wins over default Allow.
    /// </summary>
    [Fact]
    public void Resolve_ExplicitDeny_DefaultAllow_DenyStillWins()
    {
        var root = Guid.NewGuid();
        var target = Guid.NewGuid();

        var deny = Entry(target, "editors", PermissionState.Deny, PermissionScope.ThisNodeOnly);

        var result = ResolutionEngine.Resolve(
            pathFromRoot: [root, target],
            roleAliases: ["editors"],
            entries: [deny],
            verb: "Umb.Document.CreateOfType",
            defaultState: PermissionState.Allow);

        Assert.False(result.IsAllowed);
        Assert.True(result.IsExplicit);
    }

    /// <summary>
    /// "Globally deny, allow under subtree" — the multi-site pattern that motivated the design.
    /// Virtual-root Deny + scoped Allow at an ancestor on the path → Allow wins (path beats virtual-root).
    /// </summary>
    [Fact]
    public void Resolve_VirtualRootDeny_PlusScopedAllow_AllowsUnderScope()
    {
        var root = Guid.NewGuid();
        var siteA = Guid.NewGuid();
        var pageUnderA = Guid.NewGuid();

        var globalDeny = Entry(
            AdvancedPermissionsConstants.VirtualRootNodeKey,
            "editors",
            PermissionState.Deny,
            PermissionScope.ThisNodeAndDescendants);

        var scopedAllow = Entry(siteA, "editors", PermissionState.Allow, PermissionScope.ThisNodeAndDescendants);

        // Resolving for a page under Site A
        var resultUnderA = ResolutionEngine.Resolve(
            pathFromRoot: [root, siteA, pageUnderA],
            roleAliases: ["editors"],
            entries: [globalDeny, scopedAllow],
            verb: "Umb.Document.CreateOfType",
            defaultState: PermissionState.Allow);

        // Path entry (siteA Allow) wins over virtual-root Deny
        Assert.True(resultUnderA.IsAllowed);
        Assert.False(resultUnderA.IsExplicit);

        // Resolving for a node outside Site A — only virtual-root Deny applies
        var siteB = Guid.NewGuid();
        var pageUnderB = Guid.NewGuid();
        var resultUnderB = ResolutionEngine.Resolve(
            pathFromRoot: [root, siteB, pageUnderB],
            roleAliases: ["editors"],
            entries: [globalDeny, scopedAllow],
            verb: "Umb.Document.CreateOfType",
            defaultState: PermissionState.Allow);

        Assert.False(resultUnderB.IsAllowed);
        Assert.False(resultUnderB.IsExplicit);
    }

    // ───────────────────────────────────────────────────────────────────────
    // Multi-group resolution with default Allow
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// User in two groups: groupA denies, groupB allows. Deny wins regardless of default.
    /// </summary>
    [Fact]
    public void Resolve_MultiGroup_DenyBeatsAllow_RegardlessOfDefault()
    {
        var root = Guid.NewGuid();
        var target = Guid.NewGuid();

        var denyFromA = Entry(target, "groupA", PermissionState.Deny, PermissionScope.ThisNodeOnly);
        var allowFromB = Entry(target, "groupB", PermissionState.Allow, PermissionScope.ThisNodeOnly);

        var result = ResolutionEngine.Resolve(
            pathFromRoot: [root, target],
            roleAliases: ["groupA", "groupB"],
            entries: [denyFromA, allowFromB],
            verb: "Umb.Document.CreateOfType",
            defaultState: PermissionState.Allow);

        Assert.False(result.IsAllowed);
        Assert.True(result.IsExplicit);
    }

    /// <summary>
    /// User in two groups, one with no opinion: the other group's entry decides.
    /// Default Allow does NOT kick in just because some group is silent.
    /// </summary>
    [Fact]
    public void Resolve_MultiGroup_SilentGroupDoesNotTriggerDefault()
    {
        var root = Guid.NewGuid();
        var target = Guid.NewGuid();

        // Only groupA has a Deny entry; groupB has nothing
        var denyFromA = Entry(target, "groupA", PermissionState.Deny, PermissionScope.ThisNodeOnly);

        var result = ResolutionEngine.Resolve(
            pathFromRoot: [root, target],
            roleAliases: ["groupA", "groupB"],
            entries: [denyFromA],
            verb: "Umb.Document.CreateOfType",
            defaultState: PermissionState.Allow);

        // groupA explicit Deny wins; groupB silence does not invoke default
        Assert.False(result.IsAllowed);
        Assert.True(result.IsExplicit);
    }

    // ───────────────────────────────────────────────────────────────────────
    // Scope rules carry over unchanged
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// ThisNodeOnly on parent is invisible to the child — child falls through to default.
    /// </summary>
    [Fact]
    public void Resolve_ThisNodeOnlyOnAncestor_InvisibleToDescendant_FallsThroughToDefault()
    {
        var root = Guid.NewGuid();
        var parent = Guid.NewGuid();
        var child = Guid.NewGuid();

        var denyOnParentOnly = Entry(parent, "editors", PermissionState.Deny, PermissionScope.ThisNodeOnly);

        var result = ResolutionEngine.Resolve(
            pathFromRoot: [root, parent, child],
            roleAliases: ["editors"],
            entries: [denyOnParentOnly],
            verb: "Umb.Document.CreateOfType",
            defaultState: PermissionState.Allow);

        // ThisNodeOnly on parent invisible to child → no opinion → default Allow
        Assert.True(result.IsAllowed);
        Assert.False(result.IsExplicit);
        Assert.Empty(result.Reasoning);
    }

    /// <summary>
    /// DescendantsOnly on parent applies to child but not to parent itself.
    /// </summary>
    [Fact]
    public void Resolve_DescendantsOnly_AppliesToChild_NotParent()
    {
        var root = Guid.NewGuid();
        var parent = Guid.NewGuid();
        var child = Guid.NewGuid();

        var denyDescendants = Entry(parent, "editors", PermissionState.Deny, PermissionScope.DescendantsOnly);

        var childResult = ResolutionEngine.Resolve(
            pathFromRoot: [root, parent, child],
            roleAliases: ["editors"],
            entries: [denyDescendants],
            verb: "Umb.Document.CreateOfType",
            defaultState: PermissionState.Allow);

        Assert.False(childResult.IsAllowed);
        Assert.False(childResult.IsExplicit);

        var parentResult = ResolutionEngine.Resolve(
            pathFromRoot: [root, parent],
            roleAliases: ["editors"],
            entries: [denyDescendants],
            verb: "Umb.Document.CreateOfType",
            defaultState: PermissionState.Allow);

        // DescendantsOnly does not apply at depth 0 → falls through to default Allow
        Assert.True(parentResult.IsAllowed);
        Assert.False(parentResult.IsExplicit);
    }

    // ───────────────────────────────────────────────────────────────────────
    // Reasoning still flows through the engine
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reasoning lists all contributing roles in priority order.
    /// </summary>
    [Fact]
    public void Resolve_Reasoning_DenyBeforeAllow_BothListed()
    {
        var root = Guid.NewGuid();
        var target = Guid.NewGuid();

        var denyFromA = Entry(target, "groupA", PermissionState.Deny, PermissionScope.ThisNodeOnly);
        var allowFromB = Entry(target, "groupB", PermissionState.Allow, PermissionScope.ThisNodeOnly);

        var result = ResolutionEngine.Resolve(
            pathFromRoot: [root, target],
            roleAliases: ["groupA", "groupB"],
            entries: [denyFromA, allowFromB],
            verb: "TestVerb",
            defaultState: PermissionState.Deny);

        Assert.Equal(2, result.Reasoning.Count);
        Assert.Equal(PermissionState.Deny, result.Reasoning[0].State);
        Assert.Equal(PermissionState.Allow, result.Reasoning[1].State);
    }

    /// <summary>
    /// Virtual-root entries are flagged as IsFromGroupDefault in the reasoning chain.
    /// </summary>
    [Fact]
    public void Resolve_VirtualRootEntry_ReasoningIsMarkedAsGroupDefault()
    {
        var root = Guid.NewGuid();
        var target = Guid.NewGuid();

        var virtualRootAllow = Entry(
            AdvancedPermissionsConstants.VirtualRootNodeKey,
            "editors",
            PermissionState.Allow,
            PermissionScope.ThisNodeAndDescendants);

        var result = ResolutionEngine.Resolve(
            pathFromRoot: [root, target],
            roleAliases: ["editors"],
            entries: [virtualRootAllow],
            verb: "Umb.Document.CreateOfType",
            defaultState: PermissionState.Allow);

        Assert.Single(result.Reasoning);
        Assert.True(result.Reasoning[0].IsFromGroupDefault);
        Assert.Equal(AdvancedPermissionsConstants.VirtualRootNodeKey, result.Reasoning[0].SourceNodeKey);
    }
}
