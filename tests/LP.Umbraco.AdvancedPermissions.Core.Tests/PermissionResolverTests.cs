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

    /// <summary>Creates an entry with a generated id.</summary>
    private static AdvancedPermissionEntry Entry(
        Guid nodeKey,
        string roleAlias,
        string verb,
        PermissionState state,
        PermissionScope scope,
        Guid? id = null)
        => new(id ?? Guid.NewGuid(), nodeKey, roleAlias, verb, state, scope);

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

        var denyOnOverview = Entry(newsOverview, "editors", AdvancedPermissionsConstants.VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeOnly);
        var allowDescendants = Entry(newsOverview, "editors", AdvancedPermissionsConstants.VerbDelete, PermissionState.Allow, PermissionScope.DescendantsOnly);

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
        var parentDeny = Entry(parent, "editors", AdvancedPermissionsConstants.VerbRead, PermissionState.Deny, PermissionScope.ThisNodeAndDescendants);
        // Child explicitly allows Read
        var childAllow = Entry(child, "editors", AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.ThisNodeOnly);

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

        var denyFromEveryone = Entry(target, "$everyone", AdvancedPermissionsConstants.VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeOnly);
        var allowFromEditors = Entry(target, "editors", AdvancedPermissionsConstants.VerbDelete, PermissionState.Allow, PermissionScope.ThisNodeOnly);

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
        var denyFromRoleA = Entry(parent, "roleA", AdvancedPermissionsConstants.VerbRead, PermissionState.Deny, PermissionScope.ThisNodeAndDescendants);

        // Role B allows Read via virtual-root entry (VirtualRootNodeKey = default for all nodes)
        var roleBDefault = Entry(AdvancedPermissionsConstants.VirtualRootNodeKey, "roleB", AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants);

        var ctx = new PermissionResolutionContext(
            TargetNodeKey: child,
            PathFromRoot: [root, parent, child],
            RoleAliases: ["roleA", "roleB"],
            StoredEntries: [denyFromRoleA, roleBDefault]);

        var result = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbRead);

        // Implicit Deny from roleA beats Implicit Allow from roleB's virtual-root default
        Assert.False(result.IsAllowed);
        Assert.False(result.IsExplicit);
    }

    // ───────────────────────────────────────────────────────────────────────
    // Virtual-root entries (default permissions, NodeKey = VirtualRootNodeKey)
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Virtual-root entries (NodeKey = VirtualRootNodeKey) act as implicit Allow entries.
    /// They provide an allow when no path entry overrides them.
    /// </summary>
    [Fact]
    public void Resolve_GroupDefault_ProvidesImplicitAllow_WhenNoStoredEntries()
    {
        var root = Guid.NewGuid();
        var target = Guid.NewGuid();

        // Virtual-root entries (VirtualRootNodeKey) act as global defaults
        var editorReadDefault = Entry(AdvancedPermissionsConstants.VirtualRootNodeKey, "editors", AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants);
        var editorCreateDefault = Entry(AdvancedPermissionsConstants.VirtualRootNodeKey, "editors", AdvancedPermissionsConstants.VerbCreate, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants);

        var ctx = new PermissionResolutionContext(
            TargetNodeKey: target,
            PathFromRoot: [root, target],
            RoleAliases: ["editors"],
            StoredEntries: [editorReadDefault, editorCreateDefault]);

        var readResult = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbRead);
        Assert.True(readResult.IsAllowed);
        Assert.False(readResult.IsExplicit); // from virtual-root entry = implicit

        var deleteResult = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbDelete);
        Assert.False(deleteResult.IsAllowed); // no virtual-root entry for Delete, no path entry → deny
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
    /// The Everyone role virtual-root entry for Read should give implicit allow on any node.
    /// </summary>
    [Fact]
    public void Resolve_EveryoneDefaultRead_IsImplicitAllow()
    {
        var root = Guid.NewGuid();
        var target = Guid.NewGuid();

        // Virtual-root entry (VirtualRootNodeKey) for $everyone acts as the global default
        var everyoneDefault = Entry(AdvancedPermissionsConstants.VirtualRootNodeKey, AdvancedPermissionsConstants.EveryoneRoleAlias, AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants);

        var ctx = new PermissionResolutionContext(
            TargetNodeKey: target,
            PathFromRoot: [root, target],
            RoleAliases: [AdvancedPermissionsConstants.EveryoneRoleAlias],
            StoredEntries: [everyoneDefault]);

        var result = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbRead);

        Assert.True(result.IsAllowed);
        Assert.False(result.IsExplicit); // from virtual-root entry = implicit
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
        var everyoneDeny = Entry(parent, "$everyone", AdvancedPermissionsConstants.VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeAndDescendants);
        // Editors explicitly allow Delete on child
        var editorsAllow = Entry(child, "editors", AdvancedPermissionsConstants.VerbDelete, PermissionState.Allow, PermissionScope.ThisNodeOnly);

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

        // Virtual-root entries (VirtualRootNodeKey) act as global defaults for editors
        var editorReadDefault = Entry(AdvancedPermissionsConstants.VirtualRootNodeKey, "editors", AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants);
        var editorCreateDefault = Entry(AdvancedPermissionsConstants.VirtualRootNodeKey, "editors", AdvancedPermissionsConstants.VerbCreate, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants);

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
    /// When a virtual-root entry (VirtualRootNodeKey) provides the allow, the reasoning should mark it
    /// as from a group default (IsFromGroupDefault = true, SourceNodeKey = VirtualRootNodeKey).
    /// </summary>
    [Fact]
    public void Resolve_GroupDefault_ReasoningIsMarkedAsFromGroupDefault()
    {
        var root = Guid.NewGuid();
        var target = Guid.NewGuid();

        // Virtual-root entry (VirtualRootNodeKey) acts as global default
        var editorDefault = Entry(AdvancedPermissionsConstants.VirtualRootNodeKey, "editors", AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants);

        var ctx = new PermissionResolutionContext(
            TargetNodeKey: target,
            PathFromRoot: [root, target],
            RoleAliases: ["editors"],
            StoredEntries: [editorDefault]);

        var result = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbRead);

        Assert.Single(result.Reasoning);
        Assert.True(result.Reasoning[0].IsFromGroupDefault);
        Assert.Equal(AdvancedPermissionsConstants.VirtualRootNodeKey, result.Reasoning[0].SourceNodeKey);
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

        var adminAllow = Entry(target, "admins", AdvancedPermissionsConstants.VerbDelete, PermissionState.Allow, PermissionScope.ThisNodeOnly);
        var everyoneDeny = Entry(target, "$everyone", AdvancedPermissionsConstants.VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeOnly);

        var ctx = new PermissionResolutionContext(
            TargetNodeKey: target,
            PathFromRoot: [root, target],
            RoleAliases: ["$everyone", "admins"],
            StoredEntries: [adminAllow, everyoneDeny]);

        var result = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbDelete);

        Assert.False(result.IsAllowed);
        Assert.True(result.IsExplicit);
    }

    // ───────────────────────────────────────────────────────────────────────
    // Nearest-ancestor wins
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// When the same role has DescendantsOnly entries at two different ancestors,
    /// the closer ancestor (lower depth) takes precedence — the walk stops at the first match.
    /// Grandparent Allow + Parent Deny → child resolves to Deny.
    /// </summary>
    [Fact]
    public void Resolve_NearestAncestorWins_CloserDescendantsOnlyBeatsGrandparent()
    {
        var root = Guid.NewGuid();
        var grandparent = Guid.NewGuid();
        var parent = Guid.NewGuid();
        var child = Guid.NewGuid();

        // Grandparent says Allow for all descendants
        var grandparentAllow = Entry(grandparent, "editors", AdvancedPermissionsConstants.VerbRead,
            PermissionState.Allow, PermissionScope.DescendantsOnly);
        // Parent overrides: Deny for all its descendants
        var parentDeny = Entry(parent, "editors", AdvancedPermissionsConstants.VerbRead,
            PermissionState.Deny, PermissionScope.DescendantsOnly);

        var ctx = new PermissionResolutionContext(
            TargetNodeKey: child,
            PathFromRoot: [root, grandparent, parent, child],
            RoleAliases: ["editors"],
            StoredEntries: [grandparentAllow, parentDeny]);

        var result = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbRead);

        // Walk finds parent (depth 1 from child) before grandparent (depth 2) → Deny
        Assert.False(result.IsAllowed);
        Assert.False(result.IsExplicit); // inherited from parent, not on child itself
    }

    /// <summary>
    /// A ThisNodeAndDescendants entry at a closer ancestor beats a conflicting entry at a farther ancestor.
    /// </summary>
    [Fact]
    public void Resolve_NearestAncestorWins_CloserThisNodeAndDescendantsBeatsGrandparent()
    {
        var root = Guid.NewGuid();
        var grandparent = Guid.NewGuid();
        var parent = Guid.NewGuid();
        var child = Guid.NewGuid();

        // Grandparent denies
        var grandparentDeny = Entry(grandparent, "editors", AdvancedPermissionsConstants.VerbUpdate,
            PermissionState.Deny, PermissionScope.ThisNodeAndDescendants);
        // Parent allows (closer) — should win
        var parentAllow = Entry(parent, "editors", AdvancedPermissionsConstants.VerbUpdate,
            PermissionState.Allow, PermissionScope.ThisNodeAndDescendants);

        var ctx = new PermissionResolutionContext(
            TargetNodeKey: child,
            PathFromRoot: [root, grandparent, parent, child],
            RoleAliases: ["editors"],
            StoredEntries: [grandparentDeny, parentAllow]);

        var result = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbUpdate);

        // Parent (depth 1) found before grandparent (depth 2) → Allow wins
        Assert.True(result.IsAllowed);
        Assert.False(result.IsExplicit);
    }

    /// <summary>
    /// Path entries always beat virtual-root (group-default) entries.
    /// Even an implicit Deny from a path ancestor beats an implicit Allow from the virtual-root.
    /// </summary>
    [Fact]
    public void Resolve_PathEntryBeatsVirtualRoot()
    {
        var root = Guid.NewGuid();
        var parent = Guid.NewGuid();
        var child = Guid.NewGuid();

        // Virtual-root says Allow for the role (group default)
        var virtualRootAllow = Entry(AdvancedPermissionsConstants.VirtualRootNodeKey, "editors",
            AdvancedPermissionsConstants.VerbDelete, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants);
        // Parent in the real path says Deny (overrides the virtual-root default)
        var parentDeny = Entry(parent, "editors",
            AdvancedPermissionsConstants.VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeAndDescendants);

        var ctx = new PermissionResolutionContext(
            TargetNodeKey: child,
            PathFromRoot: [root, parent, child],
            RoleAliases: ["editors"],
            StoredEntries: [virtualRootAllow, parentDeny]);

        var result = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbDelete);

        // Path entry (parent, Deny) found during walk → virtual-root never checked → Deny
        Assert.False(result.IsAllowed);
        Assert.False(result.IsExplicit);
    }

    // ───────────────────────────────────────────────────────────────────────
    // Three-role priority — all four priority levels
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Full four-tier priority test with three roles:
    /// roleA=ExplicitDeny, roleB=ExplicitAllow, roleC=ImplicitAllow → ExplicitDeny wins.
    /// </summary>
    [Fact]
    public void Resolve_ThreeRoles_ExplicitDenyWinsOverExplicitAllowAndImplicitAllow()
    {
        var root = Guid.NewGuid();
        var parent = Guid.NewGuid();
        var target = Guid.NewGuid();

        var explicitDeny  = Entry(target, "roleA", AdvancedPermissionsConstants.VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeOnly);
        var explicitAllow = Entry(target, "roleB", AdvancedPermissionsConstants.VerbDelete, PermissionState.Allow, PermissionScope.ThisNodeOnly);
        var implicitAllow = Entry(parent, "roleC", AdvancedPermissionsConstants.VerbDelete, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants);

        var ctx = new PermissionResolutionContext(
            TargetNodeKey: target,
            PathFromRoot: [root, parent, target],
            RoleAliases: ["roleA", "roleB", "roleC"],
            StoredEntries: [explicitDeny, explicitAllow, implicitAllow]);

        var result = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbDelete);

        Assert.False(result.IsAllowed);
        Assert.True(result.IsExplicit);
    }

    /// <summary>
    /// Three roles where no role has explicit entries:
    /// roleA=ImplicitDeny, roleB=ImplicitAllow, roleC=ImplicitAllow → ImplicitDeny wins.
    /// </summary>
    [Fact]
    public void Resolve_ThreeRoles_ImplicitDenyBeatsMultipleImplicitAllows()
    {
        var root = Guid.NewGuid();
        var parent = Guid.NewGuid();
        var child = Guid.NewGuid();

        var implicitDeny   = Entry(parent, "roleA", AdvancedPermissionsConstants.VerbPublish, PermissionState.Deny,  PermissionScope.ThisNodeAndDescendants);
        var implicitAllow1 = Entry(AdvancedPermissionsConstants.VirtualRootNodeKey, "roleB", AdvancedPermissionsConstants.VerbPublish, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants);
        var implicitAllow2 = Entry(AdvancedPermissionsConstants.VirtualRootNodeKey, "roleC", AdvancedPermissionsConstants.VerbPublish, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants);

        var ctx = new PermissionResolutionContext(
            TargetNodeKey: child,
            PathFromRoot: [root, parent, child],
            RoleAliases: ["roleA", "roleB", "roleC"],
            StoredEntries: [implicitDeny, implicitAllow1, implicitAllow2]);

        var result = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbPublish);

        Assert.False(result.IsAllowed);
        Assert.False(result.IsExplicit);
    }

    /// <summary>
    /// Three roles where only two have any opinion; the third has no entries.
    /// The third role having no opinion must not affect the outcome.
    /// </summary>
    [Fact]
    public void Resolve_ThreeRoles_RoleWithNoOpinionIsIgnored()
    {
        var root = Guid.NewGuid();
        var target = Guid.NewGuid();

        var allowFromA = Entry(target, "roleA", AdvancedPermissionsConstants.VerbCreate, PermissionState.Allow, PermissionScope.ThisNodeOnly);
        // roleB and roleC have no entries for this verb

        var ctx = new PermissionResolutionContext(
            TargetNodeKey: target,
            PathFromRoot: [root, target],
            RoleAliases: ["roleA", "roleB", "roleC"],
            StoredEntries: [allowFromA]);

        var result = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbCreate);

        Assert.True(result.IsAllowed);
        Assert.True(result.IsExplicit);
    }

    // ───────────────────────────────────────────────────────────────────────
    // Reasoning completeness — all contributing roles appear
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// When multiple roles all contribute to the same outcome, all appear in the reasoning list.
    /// Two roles both explicitly allow → both appear in reasoning (priority 2 wins over 3+4).
    /// </summary>
    [Fact]
    public void Resolve_Reasoning_AllContributingRolesListed()
    {
        var root = Guid.NewGuid();
        var target = Guid.NewGuid();

        var allowA = Entry(target, "roleA", AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.ThisNodeOnly);
        var allowB = Entry(target, "roleB", AdvancedPermissionsConstants.VerbRead, PermissionState.Allow, PermissionScope.ThisNodeOnly);

        var ctx = new PermissionResolutionContext(
            TargetNodeKey: target,
            PathFromRoot: [root, target],
            RoleAliases: ["roleA", "roleB"],
            StoredEntries: [allowA, allowB]);

        var result = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbRead);

        Assert.True(result.IsAllowed);
        Assert.Equal(2, result.Reasoning.Count);
        Assert.Contains(result.Reasoning, r => r.ContributingRole == "roleA");
        Assert.Contains(result.Reasoning, r => r.ContributingRole == "roleB");
    }

    /// <summary>
    /// When an explicit deny wins, it appears first in reasoning, followed by the overridden explicit allow.
    /// Reasoning order: explicit denies → explicit allows → implicit denies → implicit allows.
    /// </summary>
    [Fact]
    public void Resolve_Reasoning_OrderedByPriority_DenyBeforeAllow()
    {
        var root = Guid.NewGuid();
        var target = Guid.NewGuid();

        var explicitDeny  = Entry(target, "roleA", AdvancedPermissionsConstants.VerbDelete, PermissionState.Deny,  PermissionScope.ThisNodeOnly);
        var explicitAllow = Entry(target, "roleB", AdvancedPermissionsConstants.VerbDelete, PermissionState.Allow, PermissionScope.ThisNodeOnly);

        var ctx = new PermissionResolutionContext(
            TargetNodeKey: target,
            PathFromRoot: [root, target],
            RoleAliases: ["roleA", "roleB"],
            StoredEntries: [explicitDeny, explicitAllow]);

        var result = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbDelete);

        Assert.Equal(2, result.Reasoning.Count);
        // Deny entry must appear first
        Assert.Equal(PermissionState.Deny,  result.Reasoning[0].State);
        Assert.Equal(PermissionState.Allow, result.Reasoning[1].State);
    }

    // ───────────────────────────────────────────────────────────────────────
    // Data-driven: effective permission resolution matrix
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Shared test data for the resolution matrix. Each row is:
    /// (scenarioName, entries, pathLength, targetIndex, roles, expectedIsAllowed, expectedIsExplicit)
    ///
    /// Path is always [node0, node1, ..., node(pathLength-1)]; target = node[targetIndex].
    /// Entries use node index (-1 = virtual root).
    /// </summary>
    public static IEnumerable<object[]> ResolutionMatrixData()
    {
        // ── No-entry baseline ────────────────────────────────────────────────
        yield return Row("No entries → safe deny",
            entries: [],
            pathLen: 2, targetIdx: 1, roles: ["$everyone"],
            allow: false, isExplicit: false);

        // ── Single-entry, all three scopes, at target (depth 0) ─────────────
        yield return Row("ThisNodeOnly Allow at target → allow (explicit)",
            entries: [(1, "editors", PermissionState.Allow, PermissionScope.ThisNodeOnly)],
            pathLen: 2, targetIdx: 1, roles: ["editors"],
            allow: true, isExplicit: true);

        yield return Row("ThisNodeOnly Deny at target → deny (explicit)",
            entries: [(1, "editors", PermissionState.Deny, PermissionScope.ThisNodeOnly)],
            pathLen: 2, targetIdx: 1, roles: ["editors"],
            allow: false, isExplicit: true);

        yield return Row("DescendantsOnly Allow at target itself → deny (scope mismatch)",
            entries: [(1, "editors", PermissionState.Allow, PermissionScope.DescendantsOnly)],
            pathLen: 2, targetIdx: 1, roles: ["editors"],
            allow: false, isExplicit: false);

        yield return Row("ThisNodeAndDescendants Allow at target → allow (explicit)",
            entries: [(1, "editors", PermissionState.Allow, PermissionScope.ThisNodeAndDescendants)],
            pathLen: 2, targetIdx: 1, roles: ["editors"],
            allow: true, isExplicit: true);

        // ── Ancestor entries propagating to child (depth 1) ─────────────────
        yield return Row("ThisNodeAndDescendants Allow at parent → child allow (implicit)",
            entries: [(0, "editors", PermissionState.Allow, PermissionScope.ThisNodeAndDescendants)],
            pathLen: 2, targetIdx: 1, roles: ["editors"],
            allow: true, isExplicit: false);

        yield return Row("ThisNodeOnly Allow at parent → child deny (scope invisible)",
            entries: [(0, "editors", PermissionState.Allow, PermissionScope.ThisNodeOnly)],
            pathLen: 2, targetIdx: 1, roles: ["editors"],
            allow: false, isExplicit: false);

        yield return Row("DescendantsOnly Allow at parent → child allow (implicit)",
            entries: [(0, "editors", PermissionState.Allow, PermissionScope.DescendantsOnly)],
            pathLen: 2, targetIdx: 1, roles: ["editors"],
            allow: true, isExplicit: false);

        yield return Row("DescendantsOnly Deny at parent → child deny (implicit)",
            entries: [(0, "editors", PermissionState.Deny, PermissionScope.DescendantsOnly)],
            pathLen: 2, targetIdx: 1, roles: ["editors"],
            allow: false, isExplicit: false);

        // ── Split entry: Deny(ThisNodeOnly) + Allow(DescendantsOnly) at parent ─
        yield return Row("Split entry at parent: child (depth 1) resolves Allow",
            entries:
            [
                (0, "editors", PermissionState.Deny,  PermissionScope.ThisNodeOnly),
                (0, "editors", PermissionState.Allow, PermissionScope.DescendantsOnly),
            ],
            pathLen: 2, targetIdx: 1, roles: ["editors"],
            allow: true, isExplicit: false);

        // ── Virtual-root (group default) fallback ────────────────────────────
        yield return Row("Virtual-root Allow → implicit allow when no path entries",
            entries: [(-1, "editors", PermissionState.Allow, PermissionScope.ThisNodeAndDescendants)],
            pathLen: 2, targetIdx: 1, roles: ["editors"],
            allow: true, isExplicit: false);

        yield return Row("Path entry beats virtual-root: path Deny overrides virtual-root Allow",
            entries:
            [
                (-1, "editors", PermissionState.Allow, PermissionScope.ThisNodeAndDescendants),
                (0,  "editors", PermissionState.Deny,  PermissionScope.ThisNodeAndDescendants),
            ],
            pathLen: 2, targetIdx: 1, roles: ["editors"],
            allow: false, isExplicit: false);

        // ── Priority ordering (two roles) ────────────────────────────────────
        yield return Row("ExplicitDeny beats ExplicitAllow (different roles, same depth)",
            entries:
            [
                (1, "$everyone", PermissionState.Deny,  PermissionScope.ThisNodeOnly),
                (1, "editors",   PermissionState.Allow, PermissionScope.ThisNodeOnly),
            ],
            pathLen: 2, targetIdx: 1, roles: ["$everyone", "editors"],
            allow: false, isExplicit: true);

        yield return Row("ExplicitDeny beats ImplicitAllow (explicit deny at target, implicit allow from parent — no explicit allow present)",
            entries:
            [
                (1, "roleA", PermissionState.Deny,  PermissionScope.ThisNodeOnly),
                (0, "roleB", PermissionState.Allow, PermissionScope.ThisNodeAndDescendants),
            ],
            pathLen: 2, targetIdx: 1, roles: ["roleA", "roleB"],
            allow: false, isExplicit: true);

        yield return Row("Nothing set for any role → effective deny (default deny, not explicit)",
            entries: [],
            pathLen: 3, targetIdx: 2, roles: ["groupA", "groupB"],
            allow: false, isExplicit: false);

        yield return Row("ExplicitAllow beats ImplicitDeny (explicit at target, implicit from parent)",
            entries:
            [
                (0, "editors", PermissionState.Deny,  PermissionScope.ThisNodeAndDescendants),
                (1, "editors", PermissionState.Allow, PermissionScope.ThisNodeOnly),
            ],
            pathLen: 2, targetIdx: 1, roles: ["editors"],
            allow: true, isExplicit: true);

        yield return Row("ImplicitDeny beats ImplicitAllow (deny from path, allow from virtual-root)",
            entries:
            [
                (0,  "roleA", PermissionState.Deny,  PermissionScope.ThisNodeAndDescendants),
                (-1, "roleB", PermissionState.Allow, PermissionScope.ThisNodeAndDescendants),
            ],
            pathLen: 2, targetIdx: 1, roles: ["roleA", "roleB"],
            allow: false, isExplicit: false);

        // ── Nearest ancestor wins ─────────────────────────────────────────────
        yield return Row("Nearest ancestor wins: parent DescendantsOnly Deny beats grandparent Allow",
            entries:
            [
                (0, "editors", PermissionState.Allow, PermissionScope.DescendantsOnly),
                (1, "editors", PermissionState.Deny,  PermissionScope.DescendantsOnly),
            ],
            pathLen: 3, targetIdx: 2, roles: ["editors"],
            allow: false, isExplicit: false);

        yield return Row("Nearest ancestor wins: parent ThisNodeAndDescendants Allow beats grandparent Deny",
            entries:
            [
                (0, "editors", PermissionState.Deny,  PermissionScope.ThisNodeAndDescendants),
                (1, "editors", PermissionState.Allow, PermissionScope.ThisNodeAndDescendants),
            ],
            pathLen: 3, targetIdx: 2, roles: ["editors"],
            allow: true, isExplicit: false);

        // ── Three-role scenarios ─────────────────────────────────────────────
        yield return Row("Three roles: ExplicitDeny beats ExplicitAllow and ImplicitAllow",
            entries:
            [
                (2, "roleA", PermissionState.Deny,  PermissionScope.ThisNodeOnly),
                (2, "roleB", PermissionState.Allow, PermissionScope.ThisNodeOnly),
                (0, "roleC", PermissionState.Allow, PermissionScope.ThisNodeAndDescendants),
            ],
            pathLen: 3, targetIdx: 2, roles: ["roleA", "roleB", "roleC"],
            allow: false, isExplicit: true);

        yield return Row("Three roles: ImplicitDeny beats two ImplicitAllows",
            entries:
            [
                (1,  "roleA", PermissionState.Deny,  PermissionScope.ThisNodeAndDescendants),
                (-1, "roleB", PermissionState.Allow, PermissionScope.ThisNodeAndDescendants),
                (-1, "roleC", PermissionState.Allow, PermissionScope.ThisNodeAndDescendants),
            ],
            pathLen: 3, targetIdx: 2, roles: ["roleA", "roleB", "roleC"],
            allow: false, isExplicit: false);

        yield return Row("Three roles, one has no opinion: remaining ExplicitAllow wins",
            entries:
            [
                (2, "roleA", PermissionState.Allow, PermissionScope.ThisNodeOnly),
                // roleB and roleC have no entries
            ],
            pathLen: 3, targetIdx: 2, roles: ["roleA", "roleB", "roleC"],
            allow: true, isExplicit: true);

        // ── Multi-group user (two groups with conflicting entries) ────────────
        yield return Row("User in two groups: implicit Deny (group A at depth 2) beats implicit Allow (group B at depth 1)",
            entries:
            [
                (0, "groupA", PermissionState.Deny,  PermissionScope.ThisNodeAndDescendants),
                (1, "groupB", PermissionState.Allow, PermissionScope.ThisNodeAndDescendants),
            ],
            pathLen: 3, targetIdx: 2, roles: ["groupA", "groupB"],
            allow: false, isExplicit: false);

        yield return Row("User in two groups: explicit Allow from group B at target beats implicit Deny from group A",
            entries:
            [
                (0, "groupA", PermissionState.Deny,  PermissionScope.ThisNodeAndDescendants),
                (2, "groupB", PermissionState.Allow, PermissionScope.ThisNodeOnly),
            ],
            pathLen: 3, targetIdx: 2, roles: ["groupA", "groupB"],
            allow: true, isExplicit: true);

        yield return Row("User in two groups: both explicitly allow → allow",
            entries:
            [
                (2, "groupA", PermissionState.Allow, PermissionScope.ThisNodeOnly),
                (2, "groupB", PermissionState.Allow, PermissionScope.ThisNodeOnly),
            ],
            pathLen: 3, targetIdx: 2, roles: ["groupA", "groupB"],
            allow: true, isExplicit: true);
    }

    /// <summary>
    /// Data-driven resolution matrix: verifies IsAllowed and IsExplicit for each scenario defined
    /// in <see cref="ResolutionMatrixData"/>. The scenario name is included in failure messages.
    /// </summary>
    [Theory]
    [MemberData(nameof(ResolutionMatrixData))]
    public void ResolutionMatrix(
        string scenarioName,
        List<(int nodeIdx, string role, PermissionState state, PermissionScope scope)> entrySpecs,
        int pathLen,
        int targetIdx,
        List<string> roles,
        bool expectedAllow,
        bool expectedExplicit)
    {
        var nodes = Enumerable.Range(0, pathLen).Select(_ => Guid.NewGuid()).ToList();
        var target = nodes[targetIdx];

        var entries = entrySpecs.Select(e =>
        {
            var nodeKey = e.nodeIdx == -1
                ? AdvancedPermissionsConstants.VirtualRootNodeKey
                : nodes[e.nodeIdx];
            return Entry(nodeKey, e.role, AdvancedPermissionsConstants.VerbDelete, e.state, e.scope);
        }).ToList();

        var ctx = new PermissionResolutionContext(
            TargetNodeKey: target,
            PathFromRoot: nodes,
            RoleAliases: roles,
            StoredEntries: entries);

        var result = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbDelete);

        Assert.True(result.IsAllowed == expectedAllow,
            $"Scenario '{scenarioName}': expected IsAllowed={expectedAllow} but got {result.IsAllowed}");
        Assert.True(result.IsExplicit == expectedExplicit,
            $"Scenario '{scenarioName}': expected IsExplicit={expectedExplicit} but got {result.IsExplicit}");
    }

    // ───────────────────────────────────────────────────────────────────────
    // Helper: builds a [Theory] row from named parameters
    // ───────────────────────────────────────────────────────────────────────

    private static object[] Row(
        string name,
        (int nodeIdx, string role, PermissionState state, PermissionScope scope)[] entries,
        int pathLen,
        int targetIdx,
        string[] roles,
        bool allow,
        bool isExplicit)
        => [name, entries.ToList(), pathLen, targetIdx, roles.ToList(), allow, isExplicit];
}
