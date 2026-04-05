using LP.Umbraco.AdvancedPermissions.Core.Constants;
using LP.Umbraco.AdvancedPermissions.Core.Models;
using LP.Umbraco.AdvancedPermissions.Core.Services;

namespace LP.Umbraco.AdvancedPermissions.Core.Tests;

/// <summary>
/// Unit tests for <see cref="PermissionResolver"/>.
/// Tests are written test-first to define the expected behavior of the resolution algorithm.
/// </summary>
public class PermissionResolverTests
{
    /// <summary>The system under test.</summary>
    private readonly PermissionResolver _resolver = new();

    // ───────────────────────────────────────────────────────────────────────
    // Helpers
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>Creates a context with a simple path [root → target] and the given entries.</summary>
    private static PermissionResolutionContext SimpleContext(
        Guid rootKey,
        Guid targetKey,
        IReadOnlyList<AdvancedPermissionEntry> entries,
        IReadOnlyList<string>? roles = null)
    {
        var roleList = roles ?? [AdvancedPermissionsConstants.EveryoneRoleAlias];
        return new PermissionResolutionContext(
            TargetNodeKey: targetKey,
            PathFromRoot: [rootKey, targetKey],
            RoleAliases: roleList,
            StoredEntries: entries);
    }

    /// <summary>Creates an entry with a given id (default 1).</summary>
    private static AdvancedPermissionEntry Entry(
        Guid? nodeKey,
        string roleAlias,
        string verb,
        PermissionState state,
        PermissionScope scope,
        int id = 1)
        => new(id, nodeKey, roleAlias, verb, state, scope);

    // ───────────────────────────────────────────────────────────────────────
    // Single role, single entry — all three scopes
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// An Allow entry with ThisNodeAndDescendants on the target node should allow.
    /// </summary>
    [Fact]
    public void Resolve_AllowThisNodeAndDescendants_OnTargetNode_ReturnsAllow()
    {
        var root = Guid.NewGuid();
        var target = Guid.NewGuid();
        var entry = Entry(target, "$everyone", AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants);
        var ctx = SimpleContext(root, target, [entry]);

        var result = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbRead);

        Assert.True(result.IsAllowed);
        Assert.True(result.IsExplicit);
    }

    /// <summary>
    /// A Deny entry with ThisNodeAndDescendants on the target node should deny.
    /// </summary>
    [Fact]
    public void Resolve_DenyThisNodeAndDescendants_OnTargetNode_ReturnsDeny()
    {
        var root = Guid.NewGuid();
        var target = Guid.NewGuid();
        var entry = Entry(target, "$everyone", AdvancedPermissionsConstants.VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeAndDescendants);
        var ctx = SimpleContext(root, target, [entry]);

        var result = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbDelete);

        Assert.False(result.IsAllowed);
        Assert.True(result.IsExplicit);
    }

    /// <summary>
    /// A ThisNodeOnly entry on the target node should apply to the target itself.
    /// </summary>
    [Fact]
    public void Resolve_AllowThisNodeOnly_OnTargetNode_ReturnsAllow()
    {
        var root = Guid.NewGuid();
        var target = Guid.NewGuid();
        var entry = Entry(target, "$everyone", AdvancedPermissionsConstants.VerbDelete, PermissionState.Allow, PermissionScope.ThisNodeOnly);
        var ctx = SimpleContext(root, target, [entry]);

        var result = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbDelete);

        Assert.True(result.IsAllowed);
        Assert.True(result.IsExplicit);
    }

    /// <summary>
    /// A DescendantsOnly entry on the target node should NOT apply to the target itself.
    /// The target is at depth 0, and DescendantsOnly requires depth > 0.
    /// </summary>
    [Fact]
    public void Resolve_AllowDescendantsOnly_OnTargetNode_DoesNotApplyToTarget()
    {
        var root = Guid.NewGuid();
        var target = Guid.NewGuid();
        // Only entry: DescendantsOnly Allow on target — should not apply at target itself
        var entry = Entry(target, "$everyone", AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.DescendantsOnly);
        var ctx = SimpleContext(root, target, [entry]);

        var result = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbRead);

        // No applicable entry → implicit deny
        Assert.False(result.IsAllowed);
    }

    // ───────────────────────────────────────────────────────────────────────
    // Inheritance — entry on ancestor affects descendant
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A ThisNodeAndDescendants entry on a parent should apply to a child at depth 1.
    /// </summary>
    [Fact]
    public void Resolve_AllowThisNodeAndDescendants_OnParent_AppliesToChild()
    {
        var root = Guid.NewGuid();
        var parent = Guid.NewGuid();
        var child = Guid.NewGuid();
        var entry = Entry(parent, "$everyone", AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants);

        var ctx = new PermissionResolutionContext(
            TargetNodeKey: child,
            PathFromRoot: [root, parent, child],
            RoleAliases: [AdvancedPermissionsConstants.EveryoneRoleAlias],
            StoredEntries: [entry]);

        var result = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbRead);

        Assert.True(result.IsAllowed);
        Assert.False(result.IsExplicit); // inherited, not on target node
    }

    /// <summary>
    /// A ThisNodeOnly entry on a parent should NOT apply to a child — it is invisible to descendants.
    /// </summary>
    [Fact]
    public void Resolve_AllowThisNodeOnly_OnParent_DoesNotApplyToChild()
    {
        var root = Guid.NewGuid();
        var parent = Guid.NewGuid();
        var child = Guid.NewGuid();
        var entry = Entry(parent, "$everyone", AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.ThisNodeOnly);

        var ctx = new PermissionResolutionContext(
            TargetNodeKey: child,
            PathFromRoot: [root, parent, child],
            RoleAliases: [AdvancedPermissionsConstants.EveryoneRoleAlias],
            StoredEntries: [entry]);

        var result = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbRead);

        // ThisNodeOnly on parent is invisible to child → no opinion → implicit deny
        Assert.False(result.IsAllowed);
        Assert.False(result.IsExplicit);
    }

    /// <summary>
    /// A DescendantsOnly entry on a parent should apply to a child (depth > 0) but not to the parent itself.
    /// </summary>
    [Fact]
    public void Resolve_AllowDescendantsOnly_OnParent_AppliesToChild()
    {
        var root = Guid.NewGuid();
        var parent = Guid.NewGuid();
        var child = Guid.NewGuid();
        var entryOnParent = Entry(parent, "$everyone", AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.DescendantsOnly);

        // Resolve for child
        var childCtx = new PermissionResolutionContext(
            TargetNodeKey: child,
            PathFromRoot: [root, parent, child],
            RoleAliases: [AdvancedPermissionsConstants.EveryoneRoleAlias],
            StoredEntries: [entryOnParent]);

        var childResult = _resolver.Resolve(childCtx, AdvancedPermissionsConstants.VerbRead);
        Assert.True(childResult.IsAllowed);
        Assert.False(childResult.IsExplicit); // inherited

        // Resolve for parent — DescendantsOnly should NOT apply to parent itself
        var parentCtx = new PermissionResolutionContext(
            TargetNodeKey: parent,
            PathFromRoot: [root, parent],
            RoleAliases: [AdvancedPermissionsConstants.EveryoneRoleAlias],
            StoredEntries: [entryOnParent]);

        var parentResult = _resolver.Resolve(parentCtx, AdvancedPermissionsConstants.VerbRead);
        Assert.False(parentResult.IsAllowed); // DescendantsOnly does not apply to parent itself
    }

    // ───────────────────────────────────────────────────────────────────────
    // Dual entries (ThisNodeOnly + DescendantsOnly on same node)
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A node can have two entries for the same verb: Deny+ThisNodeOnly and Allow+DescendantsOnly.
    /// The Deny should apply only to the node itself; the Allow should apply to its children.
    /// </summary>
    [Fact]
    public void Resolve_DualEntries_DenyNodeOnly_AllowDescendantsOnly_CorrectPerDepth()
    {
        var root = Guid.NewGuid();
        var newsOverview = Guid.NewGuid();
        var newsArticle = Guid.NewGuid();

        var denyOnOverview = Entry(newsOverview, "editors", AdvancedPermissionsConstants.VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeOnly, id: 1);
        var allowDescendants = Entry(newsOverview, "editors", AdvancedPermissionsConstants.VerbDelete, PermissionState.Allow, PermissionScope.DescendantsOnly, id: 2);

        var roles = new[] { "editors" };

        // Resolve Delete for news overview itself (depth 0)
        var overviewCtx = new PermissionResolutionContext(
            TargetNodeKey: newsOverview,
            PathFromRoot: [root, newsOverview],
            RoleAliases: roles,
            StoredEntries: [denyOnOverview, allowDescendants]);

        var overviewResult = _resolver.Resolve(overviewCtx, AdvancedPermissionsConstants.VerbDelete);
        Assert.False(overviewResult.IsAllowed, "Overview node should be denied (ThisNodeOnly Deny)");
        Assert.True(overviewResult.IsExplicit);

        // Resolve Delete for news article (depth 1 from overview)
        var articleCtx = new PermissionResolutionContext(
            TargetNodeKey: newsArticle,
            PathFromRoot: [root, newsOverview, newsArticle],
            RoleAliases: roles,
            StoredEntries: [denyOnOverview, allowDescendants]);

        var articleResult = _resolver.Resolve(articleCtx, AdvancedPermissionsConstants.VerbDelete);
        Assert.True(articleResult.IsAllowed, "Article should be allowed (DescendantsOnly Allow)");
        Assert.False(articleResult.IsExplicit); // inherited
    }

    // ───────────────────────────────────────────────────────────────────────
    // Priority: Explicit beats implicit
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// An explicit Allow on the target node should override an inherited Deny from a parent.
    /// (Explicit Allow > Implicit Deny)
    /// </summary>
    [Fact]
    public void Resolve_ExplicitAllow_OverridesInheritedDeny()
    {
        var root = Guid.NewGuid();
        var parent = Guid.NewGuid();
        var child = Guid.NewGuid();

        // Parent denies Read for all descendants
        var parentDeny = Entry(parent, "editors", AdvancedPermissionsConstants.VerbRead, PermissionState.Deny, PermissionScope.ThisNodeAndDescendants, id: 1);
        // Child explicitly allows Read
        var childAllow = Entry(child, "editors", AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.ThisNodeOnly, id: 2);

        var ctx = new PermissionResolutionContext(
            TargetNodeKey: child,
            PathFromRoot: [root, parent, child],
            RoleAliases: ["editors"],
            StoredEntries: [parentDeny, childAllow]);

        var result = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbRead);

        // Explicit Allow on child beats implicit Deny from parent
        Assert.True(result.IsAllowed);
        Assert.True(result.IsExplicit);
    }

    /// <summary>
    /// An explicit Deny on the target node should beat an explicit Allow from another role on the same node.
    /// (Explicit Deny > Explicit Allow)
    /// </summary>
    [Fact]
    public void Resolve_ExplicitDeny_BeatsExplicitAllow_FromDifferentRole()
    {
        var root = Guid.NewGuid();
        var target = Guid.NewGuid();

        var denyFromEveryone = Entry(target, "$everyone", AdvancedPermissionsConstants.VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeOnly, id: 1);
        var allowFromEditors = Entry(target, "editors", AdvancedPermissionsConstants.VerbDelete, PermissionState.Allow, PermissionScope.ThisNodeOnly, id: 2);

        var ctx = new PermissionResolutionContext(
            TargetNodeKey: target,
            PathFromRoot: [root, target],
            RoleAliases: ["$everyone", "editors"],
            StoredEntries: [denyFromEveryone, allowFromEditors]);

        var result = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbDelete);

        // Explicit Deny beats Explicit Allow
        Assert.False(result.IsAllowed);
        Assert.True(result.IsExplicit);
    }

    /// <summary>
    /// An implicit Deny beats an implicit Allow. (Implicit Deny > Implicit Allow)
    /// </summary>
    [Fact]
    public void Resolve_ImplicitDeny_BeatsImplicitAllow()
    {
        var root = Guid.NewGuid();
        var parent = Guid.NewGuid();
        var child = Guid.NewGuid();

        // Role A denies Read via inheritance from parent
        var denyFromRoleA = Entry(parent, "roleA", AdvancedPermissionsConstants.VerbRead, PermissionState.Deny, PermissionScope.ThisNodeAndDescendants, id: 1);

        // Role B allows Read via root-level entry (null NodeKey = virtual root default)
        var roleBDefault = Entry(null, "roleB", AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants, id: 2);

        var ctx = new PermissionResolutionContext(
            TargetNodeKey: child,
            PathFromRoot: [root, parent, child],
            RoleAliases: ["roleA", "roleB"],
            StoredEntries: [denyFromRoleA, roleBDefault]);

        var result = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbRead);

        // Implicit Deny from roleA beats Implicit Allow from roleB's root-level default
        Assert.False(result.IsAllowed);
        Assert.False(result.IsExplicit);
    }

    // ───────────────────────────────────────────────────────────────────────
    // Root-level entries (virtual root defaults, NodeKey = null)
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Root-level entries (NodeKey = null) act as implicit Allow entries at the virtual root.
    /// They provide an allow when no path entry overrides them.
    /// </summary>
    [Fact]
    public void Resolve_GroupDefault_ProvidesImplicitAllow_WhenNoStoredEntries()
    {
        var root = Guid.NewGuid();
        var target = Guid.NewGuid();

        // Root-level entries (null NodeKey) act as virtual root defaults
        var editorReadDefault = Entry(null, "editors", AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants, id: 1);
        var editorCreateDefault = Entry(null, "editors", AdvancedPermissionsConstants.VerbCreate, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants, id: 2);

        var ctx = new PermissionResolutionContext(
            TargetNodeKey: target,
            PathFromRoot: [root, target],
            RoleAliases: ["editors"],
            StoredEntries: [editorReadDefault, editorCreateDefault]);

        var readResult = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbRead);
        Assert.True(readResult.IsAllowed);
        Assert.False(readResult.IsExplicit); // from root-level entry = implicit

        var deleteResult = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbDelete);
        Assert.False(deleteResult.IsAllowed); // no root-level entry for Delete, no path entry → deny
    }

    // ───────────────────────────────────────────────────────────────────────
    // No opinion → implicit deny (safe by default)
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// When no role has any opinion on a verb, the result is an implicit deny.
    /// </summary>
    [Fact]
    public void Resolve_NoOpinionFromAnyRole_ReturnsDeny()
    {
        var root = Guid.NewGuid();
        var target = Guid.NewGuid();

        var ctx = new PermissionResolutionContext(
            TargetNodeKey: target,
            PathFromRoot: [root, target],
            RoleAliases: [AdvancedPermissionsConstants.EveryoneRoleAlias],
            StoredEntries: []);

        var result = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbDelete);

        Assert.False(result.IsAllowed);
        Assert.False(result.IsExplicit);
        Assert.Empty(result.Reasoning);
    }

    // ───────────────────────────────────────────────────────────────────────
    // Everyone role — same priority as other roles
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The Everyone role root-level entry for Read should give implicit allow on any node.
    /// </summary>
    [Fact]
    public void Resolve_EveryoneDefaultRead_IsImplicitAllow()
    {
        var root = Guid.NewGuid();
        var target = Guid.NewGuid();

        // Root-level entry (null NodeKey) for $everyone acts as the virtual root default
        var everyoneDefault = Entry(null, AdvancedPermissionsConstants.EveryoneRoleAlias, AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants, id: 1);

        var ctx = new PermissionResolutionContext(
            TargetNodeKey: target,
            PathFromRoot: [root, target],
            RoleAliases: [AdvancedPermissionsConstants.EveryoneRoleAlias],
            StoredEntries: [everyoneDefault]);

        var result = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbRead);

        Assert.True(result.IsAllowed);
        Assert.False(result.IsExplicit); // from root-level entry = implicit
    }

    /// <summary>
    /// An explicit Allow from another role can override an implicit Deny from Everyone.
    /// ($everyone is not special — it follows the same priority as any other role.)
    /// </summary>
    [Fact]
    public void Resolve_ExplicitAllowFromRole_OverridesImplicitDenyFromEveryone()
    {
        var root = Guid.NewGuid();
        var parent = Guid.NewGuid();
        var child = Guid.NewGuid();

        // Everyone denies Delete at parent (ThisNodeAndDescendants) → implicit at child
        var everyoneDeny = Entry(parent, "$everyone", AdvancedPermissionsConstants.VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeAndDescendants, id: 1);
        // Editors explicitly allow Delete on child
        var editorsAllow = Entry(child, "editors", AdvancedPermissionsConstants.VerbDelete, PermissionState.Allow, PermissionScope.ThisNodeOnly, id: 2);

        var ctx = new PermissionResolutionContext(
            TargetNodeKey: child,
            PathFromRoot: [root, parent, child],
            RoleAliases: ["$everyone", "editors"],
            StoredEntries: [everyoneDeny, editorsAllow]);

        var result = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbDelete);

        // Explicit Allow (editors, depth 0) beats Implicit Deny (everyone, depth 1)
        Assert.True(result.IsAllowed);
        Assert.True(result.IsExplicit);
    }

    // ───────────────────────────────────────────────────────────────────────
    // Deep inheritance (many levels)
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A ThisNodeAndDescendants entry at the root should propagate to a deeply nested node.
    /// </summary>
    [Fact]
    public void Resolve_DeepInheritance_EntryAtRoot_AppliesToDeeplyNestedNode()
    {
        var nodes = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToList();
        var rootKey = nodes[0];
        var deepTarget = nodes[9];

        // Allow Read at root with ThisNodeAndDescendants
        var rootEntry = Entry(rootKey, "editors", AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants);

        var ctx = new PermissionResolutionContext(
            TargetNodeKey: deepTarget,
            PathFromRoot: nodes,
            RoleAliases: ["editors"],
            StoredEntries: [rootEntry]);

        var result = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbRead);

        Assert.True(result.IsAllowed);
        Assert.False(result.IsExplicit); // inherited, not on target itself
    }

    // ───────────────────────────────────────────────────────────────────────
    // ResolveAll — multiple verbs at once
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// ResolveAll resolves multiple verbs in one call and returns results for each.
    /// </summary>
    [Fact]
    public void ResolveAll_ReturnsResultForEachVerb()
    {
        var root = Guid.NewGuid();
        var target = Guid.NewGuid();

        // Root-level entries (null NodeKey) act as virtual root defaults for editors
        var editorReadDefault = Entry(null, "editors", AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants, id: 1);
        var editorCreateDefault = Entry(null, "editors", AdvancedPermissionsConstants.VerbCreate, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants, id: 2);

        var ctx = new PermissionResolutionContext(
            TargetNodeKey: target,
            PathFromRoot: [root, target],
            RoleAliases: ["editors"],
            StoredEntries: [editorReadDefault, editorCreateDefault]);

        var verbs = new[] { AdvancedPermissionsConstants.VerbRead, AdvancedPermissionsConstants.VerbCreate, AdvancedPermissionsConstants.VerbDelete };
        var results = _resolver.ResolveAll(ctx, verbs);

        Assert.Equal(3, results.Count);
        Assert.True(results[AdvancedPermissionsConstants.VerbRead].IsAllowed);
        Assert.True(results[AdvancedPermissionsConstants.VerbCreate].IsAllowed);
        Assert.False(results[AdvancedPermissionsConstants.VerbDelete].IsAllowed);
    }

    // ───────────────────────────────────────────────────────────────────────
    // Reasoning output
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The reasoning list should contain the contributing role(s) explaining the result.
    /// </summary>
    [Fact]
    public void Resolve_ExplicitDeny_ReasoningContainsDenyingRole()
    {
        var root = Guid.NewGuid();
        var target = Guid.NewGuid();

        var entry = Entry(target, "$everyone", AdvancedPermissionsConstants.VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeOnly);
        var ctx = SimpleContext(root, target, [entry]);

        var result = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbDelete);

        Assert.Single(result.Reasoning);
        Assert.Equal("$everyone", result.Reasoning[0].ContributingRole);
        Assert.Equal(PermissionState.Deny, result.Reasoning[0].State);
        Assert.True(result.Reasoning[0].IsExplicit);
    }

    /// <summary>
    /// When a root-level entry (null NodeKey) provides the allow, the reasoning should mark it
    /// as from a group default (IsFromGroupDefault = true, SourceNodeKey = null).
    /// </summary>
    [Fact]
    public void Resolve_GroupDefault_ReasoningIsMarkedAsFromGroupDefault()
    {
        var root = Guid.NewGuid();
        var target = Guid.NewGuid();

        // Root-level entry (null NodeKey) acts as virtual root default
        var editorDefault = Entry(null, "editors", AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants, id: 1);

        var ctx = new PermissionResolutionContext(
            TargetNodeKey: target,
            PathFromRoot: [root, target],
            RoleAliases: ["editors"],
            StoredEntries: [editorDefault]);

        var result = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbRead);

        Assert.Single(result.Reasoning);
        Assert.True(result.Reasoning[0].IsFromGroupDefault);
        Assert.Null(result.Reasoning[0].SourceNodeKey); // null = virtual root
    }

    // ───────────────────────────────────────────────────────────────────────
    // Multiple roles — first applicable entry wins per role
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// When a user has multiple roles and both have opinions, explicit deny beats explicit allow.
    /// </summary>
    [Fact]
    public void Resolve_MultipleRoles_ExplicitDenyBeatsExplicitAllow()
    {
        var root = Guid.NewGuid();
        var target = Guid.NewGuid();

        var adminAllow = Entry(target, "admins", AdvancedPermissionsConstants.VerbDelete, PermissionState.Allow, PermissionScope.ThisNodeOnly, id: 1);
        var everyoneDeny = Entry(target, "$everyone", AdvancedPermissionsConstants.VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeOnly, id: 2);

        var ctx = new PermissionResolutionContext(
            TargetNodeKey: target,
            PathFromRoot: [root, target],
            RoleAliases: ["$everyone", "admins"],
            StoredEntries: [adminAllow, everyoneDeny]);

        var result = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbDelete);

        Assert.False(result.IsAllowed);
        Assert.True(result.IsExplicit);
    }
}
