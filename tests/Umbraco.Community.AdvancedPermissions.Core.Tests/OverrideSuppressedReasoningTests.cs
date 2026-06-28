using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Models;
using Umbraco.Community.AdvancedPermissions.Core.Services;

namespace Umbraco.Community.AdvancedPermissions.Core.Tests;

/// <summary>
/// Guards the data the Access Viewer's reasoning popup needs to explain a priority override:
/// when an override flips a would-be Deny to Allow, the suppressed Deny must appear in
/// <see cref="EffectivePermission.SuppressedReasoning"/> — both when the rules sit directly on the
/// target node and when they are inherited from an ancestor. Also documents the "vacuous override"
/// case, where the override wins without suppressing any Deny (so there is nothing to explain).
/// </summary>
public class OverrideSuppressedReasoningTests
{
    private readonly PermissionResolver _resolver = new();

    private static AdvancedPermissionEntry Entry(
        Guid nodeKey,
        string roleAlias,
        string verb,
        PermissionState state,
        PermissionScope scope,
        bool isPriorityOverride = false)
        => new(Guid.NewGuid(), nodeKey, roleAlias, verb, state, scope) { IsPriorityOverride = isPriorityOverride };

    /// <summary>
    /// Override and suppressed Deny both explicit on the target node: SuppressedReasoning carries the Deny.
    /// </summary>
    [Fact]
    public void DirectNode_OverrideSuppressesExplicitDeny_PopulatesSuppressedReasoning()
    {
        var root = Guid.NewGuid();
        var target = Guid.NewGuid();
        var deny = Entry(target, "admins", AdvancedPermissionsConstants.VerbDelete, PermissionState.Deny, PermissionScope.ThisNodeOnly);
        var allowOverride = Entry(target, "$everyone", AdvancedPermissionsConstants.VerbDelete, PermissionState.Allow, PermissionScope.ThisNodeOnly, isPriorityOverride: true);

        var ctx = new PermissionResolutionContext(
            TargetNodeKey: target,
            PathFromRoot: [root, target],
            RoleAliases: ["admins", "$everyone"],
            StoredEntries: [deny, allowOverride]);

        var result = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbDelete);

        Assert.True(result.IsAllowed);
        Assert.True(result.WasPriorityOverrideActive);
        Assert.NotNull(result.SuppressedReasoning);
        Assert.Contains(result.SuppressedReasoning!, r => r.ContributingRole == "admins" && r.State == PermissionState.Deny);
    }

    /// <summary>
    /// The scenario users expect to see explained: on a parent, $everyone has Allow+override and another
    /// role has a Deny — both reaching descendants. At a child the override flips the inherited Deny to
    /// Allow, and the suppressed Deny must be present so the popup can show "would have been Deny".
    /// Both rules must be on the SAME verb for the override to suppress the Deny.
    /// </summary>
    [Fact]
    public void Child_InheritedOverrideSuppressesInheritedDeny_PopulatesSuppressedReasoning()
    {
        var root = Guid.NewGuid();
        var products = Guid.NewGuid();
        var child = Guid.NewGuid();

        var everyoneAllowDesc = Entry(products, "$everyone", AdvancedPermissionsConstants.VerbDelete, PermissionState.Allow, PermissionScope.DescendantsOnly, isPriorityOverride: true);
        var adminsDenyDesc = Entry(products, "admins", AdvancedPermissionsConstants.VerbDelete, PermissionState.Deny, PermissionScope.DescendantsOnly);

        var ctx = new PermissionResolutionContext(
            TargetNodeKey: child,
            PathFromRoot: [root, products, child],
            RoleAliases: ["admins", "$everyone"],
            StoredEntries: [everyoneAllowDesc, adminsDenyDesc]);

        var result = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbDelete);

        Assert.True(result.IsAllowed);
        Assert.True(result.WasPriorityOverrideActive);
        Assert.NotNull(result.SuppressedReasoning);
        Assert.Contains(result.SuppressedReasoning!, r => r.ContributingRole == "admins" && r.State == PermissionState.Deny);
    }

    /// <summary>
    /// "Vacuous override": the flagged Allow wins over another role's Allow (its group default) — no Deny
    /// is involved. The override is reported active (so the UI badge fires), but nothing it suppressed was
    /// a Deny, so there is no would-be-Deny for the popup to explain. This is the shape produced when the
    /// override and a Deny are configured on DIFFERENT verbs.
    /// </summary>
    [Fact]
    public void Child_OverrideBeatsOnlyAllows_NoSuppressedDeny()
    {
        var root = Guid.NewGuid();
        var products = Guid.NewGuid();
        var child = Guid.NewGuid();

        var everyoneAllowDesc = Entry(products, "$everyone", AdvancedPermissionsConstants.VerbDelete, PermissionState.Allow, PermissionScope.DescendantsOnly, isPriorityOverride: true);
        var adminsDefaultAllow = Entry(AdvancedPermissionsConstants.VirtualRootNodeKey, "admins", AdvancedPermissionsConstants.VerbDelete, PermissionState.Allow, PermissionScope.ThisNodeAndDescendants);

        var ctx = new PermissionResolutionContext(
            TargetNodeKey: child,
            PathFromRoot: [root, products, child],
            RoleAliases: ["admins", "$everyone"],
            StoredEntries: [everyoneAllowDesc, adminsDefaultAllow]);

        var result = _resolver.Resolve(ctx, AdvancedPermissionsConstants.VerbDelete);

        Assert.True(result.IsAllowed);
        Assert.True(result.WasPriorityOverrideActive);
        Assert.DoesNotContain(result.SuppressedReasoning ?? [], r => r.State == PermissionState.Deny);
    }
}
