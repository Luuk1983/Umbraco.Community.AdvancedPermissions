using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Models;
using Umbraco.Community.AdvancedPermissions.Core.Services;

namespace Umbraco.Community.AdvancedPermissions.Core.Tests;

/// <summary>
/// Unit tests for <see cref="DocTypePermissionResolver"/>. Focuses on the doc-type-specific
/// behaviors layered on top of the shared engine: default-Allow, ContentTypeKey matching,
/// and the "deny globally, allow under subtree" patterns the design exists to express.
/// </summary>
public class DocTypePermissionResolverTests
{
    private readonly DocTypePermissionResolver _resolver = new();

    private static DocTypePermissionEntry Entry(
        Guid nodeKey,
        Guid contentTypeKey,
        string roleAlias,
        string verb,
        PermissionState state,
        PermissionScope scope) =>
        new(Guid.NewGuid(), nodeKey, contentTypeKey, roleAlias, verb, state, scope);

    // ───────────────────────────────────────────────────────────────────────
    // Default-Allow
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// No entries → default Allow (the new behavior the entire feature relies on).
    /// </summary>
    [Fact]
    public void Resolve_NoEntries_DefaultAllow()
    {
        var root = Guid.NewGuid();
        var parent = Guid.NewGuid();
        var contentTypeKey = Guid.NewGuid();

        var ctx = new DocTypePermissionResolutionContext(
            ContentTypeKey: contentTypeKey,
            ParentNodeKey: parent,
            PathFromRoot: [root, parent],
            RoleAliases: ["editors"],
            StoredEntries: []);

        var result = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbCreateOfType);

        Assert.True(result.IsAllowed);
    }

    // ───────────────────────────────────────────────────────────────────────
    // Verb isolation (document vs element-type create-filtering share one store)
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Element-type create entries (<c>Umb.Element.CreateOfType</c>) and document create entries
    /// (<c>Umb.Document.CreateOfType</c>) live in the same store keyed by the same content-type. Resolving
    /// one verb must ignore entries written for the other, so library element-type filtering never leaks
    /// into content create-filtering and vice versa.
    /// </summary>
    [Fact]
    public void Resolve_ElementTypeAndDocumentVerbs_AreIsolated()
    {
        var contentTypeKey = Guid.NewGuid();

        // A Deny written for the ELEMENT-TYPE verb only.
        var denyElementCreate = Entry(
            AdvancedPermissionsConstants.VirtualRootNodeKey,
            contentTypeKey,
            AdvancedPermissionsConstants.EveryoneRoleAlias,
            AdvancedPermissionsConstants.VerbElementCreateOfType,
            PermissionState.Deny,
            PermissionScope.ThisNodeAndDescendants);

        var ctx = new DocTypePermissionResolutionContext(
            ContentTypeKey: contentTypeKey,
            ParentNodeKey: AdvancedPermissionsConstants.VirtualRootNodeKey,
            PathFromRoot: [AdvancedPermissionsConstants.VirtualRootNodeKey],
            RoleAliases: [AdvancedPermissionsConstants.EveryoneRoleAlias],
            StoredEntries: [denyElementCreate]);

        // The element-type verb sees the deny.
        Assert.False(_resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbElementCreateOfType).IsAllowed);

        // The document verb does not — it has no matching entry, so it stays at default Allow.
        Assert.True(_resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbCreateOfType).IsAllowed);
    }

    // ───────────────────────────────────────────────────────────────────────
    // ContentTypeKey matching
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A Deny entry for a DIFFERENT doc-type must not apply.
    /// </summary>
    [Fact]
    public void Resolve_EntryForDifferentContentType_DoesNotApply()
    {
        var root = Guid.NewGuid();
        var parent = Guid.NewGuid();
        var newsType = Guid.NewGuid();
        var faqType = Guid.NewGuid();

        var denyNews = Entry(
            AdvancedPermissionsConstants.VirtualRootNodeKey,
            newsType,
            "editors",
            AdvancedPermissionsConstants.VerbCreateOfType,
            PermissionState.Deny,
            PermissionScope.ThisNodeAndDescendants);

        // Resolving for FAQ type — News deny must NOT apply
        var ctx = new DocTypePermissionResolutionContext(
            ContentTypeKey: faqType,
            ParentNodeKey: parent,
            PathFromRoot: [root, parent],
            RoleAliases: ["editors"],
            StoredEntries: [denyNews]);

        var result = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbCreateOfType);

        Assert.True(result.IsAllowed); // default Allow, since the entry is for a different type
    }

    /// <summary>
    /// A Deny entry for the SAME doc-type at virtual root applies as an implicit deny.
    /// </summary>
    [Fact]
    public void Resolve_VirtualRootDenyForType_PreventsCreate()
    {
        var root = Guid.NewGuid();
        var parent = Guid.NewGuid();
        var newsType = Guid.NewGuid();

        var globalDenyNews = Entry(
            AdvancedPermissionsConstants.VirtualRootNodeKey,
            newsType,
            "editors",
            AdvancedPermissionsConstants.VerbCreateOfType,
            PermissionState.Deny,
            PermissionScope.ThisNodeAndDescendants);

        var ctx = new DocTypePermissionResolutionContext(
            ContentTypeKey: newsType,
            ParentNodeKey: parent,
            PathFromRoot: [root, parent],
            RoleAliases: ["editors"],
            StoredEntries: [globalDenyNews]);

        var result = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbCreateOfType);

        Assert.False(result.IsAllowed);
        Assert.False(result.IsExplicit); // virtual root = implicit
    }

    // ───────────────────────────────────────────────────────────────────────
    // The driving use case: global Deny + scoped Allow (multi-site whitelist)
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Globally deny News, but allow under /Site A. Editors creating under Site A succeed;
    /// under any other parent the global deny takes effect.
    /// </summary>
    [Fact]
    public void Resolve_GlobalDenyPlusScopedAllow_ScopedAllowWinsUnderScope()
    {
        var root = Guid.NewGuid();
        var siteA = Guid.NewGuid();
        var pageUnderA = Guid.NewGuid();
        var siteB = Guid.NewGuid();
        var pageUnderB = Guid.NewGuid();
        var newsType = Guid.NewGuid();

        var globalDeny = Entry(
            AdvancedPermissionsConstants.VirtualRootNodeKey,
            newsType,
            "editors",
            AdvancedPermissionsConstants.VerbCreateOfType,
            PermissionState.Deny,
            PermissionScope.ThisNodeAndDescendants);

        var siteAAllow = Entry(
            siteA,
            newsType,
            "editors",
            AdvancedPermissionsConstants.VerbCreateOfType,
            PermissionState.Allow,
            PermissionScope.ThisNodeAndDescendants);

        // Under Site A — path entry wins
        var ctxUnderA = new DocTypePermissionResolutionContext(
            ContentTypeKey: newsType,
            ParentNodeKey: pageUnderA,
            PathFromRoot: [root, siteA, pageUnderA],
            RoleAliases: ["editors"],
            StoredEntries: [globalDeny, siteAAllow]);

        var resultUnderA = _resolver.Resolve(ctxUnderA, AdvancedPermissionsConstants.VerbCreateOfType);
        Assert.True(resultUnderA.IsAllowed);

        // Under Site B — only global deny is relevant
        var ctxUnderB = new DocTypePermissionResolutionContext(
            ContentTypeKey: newsType,
            ParentNodeKey: pageUnderB,
            PathFromRoot: [root, siteB, pageUnderB],
            RoleAliases: ["editors"],
            StoredEntries: [globalDeny, siteAAllow]);

        var resultUnderB = _resolver.Resolve(ctxUnderB, AdvancedPermissionsConstants.VerbCreateOfType);
        Assert.False(resultUnderB.IsAllowed);
    }

    // ───────────────────────────────────────────────────────────────────────
    // Multi-group resolution
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// User belongs to groupA (deny News globally) AND groupB (allow News globally). Deny wins.
    /// </summary>
    [Fact]
    public void Resolve_MultiGroup_DenyBeatsAllow()
    {
        var root = Guid.NewGuid();
        var parent = Guid.NewGuid();
        var newsType = Guid.NewGuid();

        var denyA = Entry(
            AdvancedPermissionsConstants.VirtualRootNodeKey,
            newsType,
            "groupA",
            AdvancedPermissionsConstants.VerbCreateOfType,
            PermissionState.Deny,
            PermissionScope.ThisNodeAndDescendants);

        var allowB = Entry(
            AdvancedPermissionsConstants.VirtualRootNodeKey,
            newsType,
            "groupB",
            AdvancedPermissionsConstants.VerbCreateOfType,
            PermissionState.Allow,
            PermissionScope.ThisNodeAndDescendants);

        var ctx = new DocTypePermissionResolutionContext(
            ContentTypeKey: newsType,
            ParentNodeKey: parent,
            PathFromRoot: [root, parent],
            RoleAliases: ["groupA", "groupB"],
            StoredEntries: [denyA, allowB]);

        var result = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbCreateOfType);

        Assert.False(result.IsAllowed);
    }

    /// <summary>
    /// $everyone virtual-root Deny + Marketing inherited Allow (from a closer ancestor): both
    /// are implicit, and implicit Deny beats implicit Allow per the resolver's precedence rules.
    /// This documents that role-specificity alone does NOT override $everyone — to win, the
    /// role-specific entry must be explicit (on the parent itself) or all-role denies must be absent.
    /// </summary>
    [Fact]
    public void Resolve_EveryoneVirtualRootDeny_RoleInheritedAllow_DenyWins()
    {
        var root = Guid.NewGuid();
        var subtree = Guid.NewGuid();
        var page = Guid.NewGuid();
        var campaignType = Guid.NewGuid();

        var everyoneDeny = Entry(
            AdvancedPermissionsConstants.VirtualRootNodeKey,
            campaignType,
            AdvancedPermissionsConstants.EveryoneRoleAlias,
            AdvancedPermissionsConstants.VerbCreateOfType,
            PermissionState.Deny,
            PermissionScope.ThisNodeAndDescendants);

        var marketingAllowAtSubtree = Entry(
            subtree,
            campaignType,
            "marketing",
            AdvancedPermissionsConstants.VerbCreateOfType,
            PermissionState.Allow,
            PermissionScope.ThisNodeAndDescendants);

        var ctx = new DocTypePermissionResolutionContext(
            ContentTypeKey: campaignType,
            ParentNodeKey: page,
            PathFromRoot: [root, subtree, page],
            RoleAliases: [AdvancedPermissionsConstants.EveryoneRoleAlias, "marketing"],
            StoredEntries: [everyoneDeny, marketingAllowAtSubtree]);

        var result = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbCreateOfType);

        // $everyone implicit Deny + marketing implicit Allow → implicit Deny wins.
        Assert.False(result.IsAllowed);
        Assert.False(result.IsExplicit);
    }

    /// <summary>
    /// $everyone virtual-root Deny + Marketing EXPLICIT Allow (on the parent itself) — the
    /// explicit Allow wins because explicit beats implicit per the precedence rules. This is
    /// the way to express "globally deny, but explicitly allow on this specific parent for
    /// these users".
    /// </summary>
    [Fact]
    public void Resolve_EveryoneVirtualRootDeny_RoleExplicitAllowOnParent_AllowWins()
    {
        var root = Guid.NewGuid();
        var page = Guid.NewGuid();
        var campaignType = Guid.NewGuid();

        var everyoneDeny = Entry(
            AdvancedPermissionsConstants.VirtualRootNodeKey,
            campaignType,
            AdvancedPermissionsConstants.EveryoneRoleAlias,
            AdvancedPermissionsConstants.VerbCreateOfType,
            PermissionState.Deny,
            PermissionScope.ThisNodeAndDescendants);

        var marketingExplicitAllow = Entry(
            page,
            campaignType,
            "marketing",
            AdvancedPermissionsConstants.VerbCreateOfType,
            PermissionState.Allow,
            PermissionScope.ThisNodeOnly);

        var ctx = new DocTypePermissionResolutionContext(
            ContentTypeKey: campaignType,
            ParentNodeKey: page,
            PathFromRoot: [root, page],
            RoleAliases: [AdvancedPermissionsConstants.EveryoneRoleAlias, "marketing"],
            StoredEntries: [everyoneDeny, marketingExplicitAllow]);

        var result = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbCreateOfType);

        // Marketing explicit Allow > $everyone implicit Deny
        Assert.True(result.IsAllowed);
        Assert.True(result.IsExplicit);
    }

    // ───────────────────────────────────────────────────────────────────────
    // Verb filtering
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// An entry for a different verb (e.g. a future DeleteOfType) must not affect the CreateOfType result.
    /// </summary>
    [Fact]
    public void Resolve_EntryForDifferentVerb_DoesNotApply()
    {
        var root = Guid.NewGuid();
        var parent = Guid.NewGuid();
        var newsType = Guid.NewGuid();

        var denyOnDifferentVerb = Entry(
            AdvancedPermissionsConstants.VirtualRootNodeKey,
            newsType,
            "editors",
            "Umb.Document.SomeFutureOfTypeVerb",
            PermissionState.Deny,
            PermissionScope.ThisNodeAndDescendants);

        var ctx = new DocTypePermissionResolutionContext(
            ContentTypeKey: newsType,
            ParentNodeKey: parent,
            PathFromRoot: [root, parent],
            RoleAliases: ["editors"],
            StoredEntries: [denyOnDifferentVerb]);

        var result = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbCreateOfType);

        Assert.True(result.IsAllowed); // entry was for a different verb → default Allow
    }

    // ───────────────────────────────────────────────────────────────────────
    // Priority override — same semantics as node-level: per (verb, parent-node) only,
    // does not inherit, only escapes cross-role Explicit Deny. The "target node" for
    // doc-type resolution is the ParentNodeKey under which content would be created.
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Cross-role override: groupA explicitly denies CreateOfType at the parent; groupB allows
    /// it with priority override at the same parent for the same content type. Override wins.
    /// </summary>
    [Fact]
    public void Resolve_PriorityOverrideAllow_OnParent_BeatsExplicitDeny_FromOtherRole()
    {
        var root = Guid.NewGuid();
        var parent = Guid.NewGuid();
        var newsType = Guid.NewGuid();

        var denyA = Entry(parent, newsType, "groupA", AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Deny, PermissionScope.ThisNodeOnly);
        var allowOverrideB = Entry(parent, newsType, "groupB", AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Allow, PermissionScope.ThisNodeOnly) with { IsPriorityOverride = true };

        var ctx = new DocTypePermissionResolutionContext(
            ContentTypeKey: newsType,
            ParentNodeKey: parent,
            PathFromRoot: [root, parent],
            RoleAliases: ["groupA", "groupB"],
            StoredEntries: [denyA, allowOverrideB]);

        var result = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbCreateOfType);

        Assert.True(result.IsAllowed);
        Assert.True(result.IsExplicit);
    }

    /// <summary>
    /// A priority override on an ancestor parent must NOT activate at a different (deeper)
    /// parent — the flag is node-local.
    /// </summary>
    [Fact]
    public void Resolve_PriorityOverride_OnAncestorParent_DoesNotActivateAtDescendantParent()
    {
        var root = Guid.NewGuid();
        var section = Guid.NewGuid();
        var deeperParent = Guid.NewGuid();
        var newsType = Guid.NewGuid();

        // groupA: Allow + override on the SECTION (inherited/implicit at deeperParent)
        var allowOverrideOnSection = Entry(section, newsType, "groupA", AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants) with { IsPriorityOverride = true };
        // groupB: Deny explicit on deeperParent (no override) — must still win because the
        // explicit tier is selected first, so groupA's flagged-but-implicit Allow never enters
        // the tie-break. Explicit beats implicit.
        var denyOnDeeper = Entry(deeperParent, newsType, "groupB", AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Deny, PermissionScope.ThisNodeOnly);

        var ctx = new DocTypePermissionResolutionContext(
            ContentTypeKey: newsType,
            ParentNodeKey: deeperParent,
            PathFromRoot: [root, section, deeperParent],
            RoleAliases: ["groupA", "groupB"],
            StoredEntries: [allowOverrideOnSection, denyOnDeeper]);

        var result = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbCreateOfType);

        Assert.False(result.IsAllowed);
    }

    /// <summary>
    /// Example C for doc-types — a flagged inherited rule beats a non-flagged inherited rule when
    /// the target parent has no explicit doc-type entry of its own. This makes a descendant-scoped
    /// override on an ancestor meaningful for creates deeper in the tree.
    /// </summary>
    [Fact]
    public void Resolve_PriorityOverride_FlaggedImplicit_BeatsNonFlaggedImplicit_WhenNoExplicit()
    {
        var root = Guid.NewGuid();
        var section = Guid.NewGuid();
        var deeperParent = Guid.NewGuid();
        var newsType = Guid.NewGuid();

        // Both inherited (DescendantsOnly on the section) → both implicit at deeperParent.
        var denyDesc = Entry(section, newsType, "groupA", AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Deny, PermissionScope.DescendantsOnly);
        var allowOverrideDesc = Entry(section, newsType, "groupB", AdvancedPermissionsConstants.VerbCreateOfType, PermissionState.Allow, PermissionScope.DescendantsOnly) with { IsPriorityOverride = true };

        var ctx = new DocTypePermissionResolutionContext(
            ContentTypeKey: newsType,
            ParentNodeKey: deeperParent,
            PathFromRoot: [root, section, deeperParent],
            RoleAliases: ["groupA", "groupB"],
            StoredEntries: [denyDesc, allowOverrideDesc]);

        var result = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbCreateOfType);

        // Implicit tier active; groupB flagged → Allow wins (would have been Deny without the flag).
        Assert.True(result.IsAllowed);
        Assert.True(result.WasPriorityOverrideActive);
    }

    /// <summary>
    /// Default-Allow semantics still apply when no entries match the verb — the override
    /// flag changes priority logic but not the no-entries-default-state behavior.
    /// </summary>
    [Fact]
    public void Resolve_NoPriorityOverrideEntries_DefaultAllowSemanticsUnchanged()
    {
        var root = Guid.NewGuid();
        var parent = Guid.NewGuid();
        var newsType = Guid.NewGuid();

        var ctx = new DocTypePermissionResolutionContext(
            ContentTypeKey: newsType,
            ParentNodeKey: parent,
            PathFromRoot: [root, parent],
            RoleAliases: ["editors"],
            StoredEntries: []);

        var result = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbCreateOfType);

        Assert.True(result.IsAllowed);
    }
}
