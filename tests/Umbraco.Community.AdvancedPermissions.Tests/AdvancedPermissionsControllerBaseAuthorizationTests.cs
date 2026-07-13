using Microsoft.AspNetCore.Authorization;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Community.AdvancedPermissions.Controllers;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Verifies the authorization topology across the management-API controllers.
/// </summary>
/// <remarks>
/// Most endpoints power the package's management UIs (Permissions Editor, Access Viewer,
/// Doc-type editor) which live behind the Users section, and the mutating endpoints
/// (PUT/DELETE /permissions) are privilege-escalation primitives. Those must stay gated on
/// <see cref="AuthorizationPolicies.SectionAccessUsers"/>.
///
/// The one exception is <see cref="AdvancedPermissionsEffectiveController.GetEffectiveForUser"/>
/// (<c>GET /effective</c>): it is the endpoint the replacement document permission condition calls
/// to decide whether the current user may act on a content node. Gating it on Users-section access
/// meant any content editor without that section got a 403, the condition denied every action, and
/// all content became read-only. It must remain backoffice-authenticated but NOT Users-section-gated.
///
/// The gate was previously an <see cref="AuthorizeAttribute"/> on
/// <see cref="AdvancedPermissionsControllerBase"/>, which inherits to every endpoint and cannot be
/// removed by a derived controller — hence it is now applied per management controller/action
/// instead. These reflection tests assert that topology so a forgotten attribute fails the build.
/// </remarks>
public sealed class AdvancedPermissionsControllerBaseAuthorizationTests
{
    /// <summary>
    /// The base controller must NOT carry the <see cref="AuthorizationPolicies.SectionAccessUsers"/>
    /// policy: a base-class <see cref="AuthorizeAttribute"/> inherits to every endpoint and cannot be
    /// removed downstream, which is exactly what forced the backoffice-scoped effective endpoint to be
    /// Users-section-gated. The gate now lives on the individual management controllers/actions.
    /// </summary>
    [Fact]
    public void ControllerBase_DoesNotApplySectionAccessUsersPolicy() =>
        Assert.False(
            TypeIsSectionGated(typeof(AdvancedPermissionsControllerBase)),
            "AdvancedPermissionsControllerBase must not carry the SectionAccessUsers policy, or it would inherit to every endpoint (including the ones content editors need).");

    /// <summary>
    /// Every endpoint must still require an authenticated backoffice user. That gate
    /// (<see cref="AuthorizationPolicies.BackOfficeAccess"/>) comes from
    /// <c>ManagementApiControllerBase</c>; this test guards against the base being changed in a way
    /// that drops it and accidentally exposes endpoints anonymously.
    /// </summary>
    [Fact]
    public void ControllerBase_StillRequiresBackOfficeAccess()
    {
        var policies = typeof(AdvancedPermissionsControllerBase)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Select(a => a.Policy)
            .ToList();

        Assert.Contains(AuthorizationPolicies.BackOfficeAccess, policies);
    }

    /// <summary>
    /// The management controllers must be gated on Users-section access: they power the package's
    /// management UIs and (for the permission controller) expose mutating endpoints.
    /// </summary>
    /// <param name="controllerType">The management controller under test.</param>
    [Theory]
    [InlineData(typeof(AdvancedPermissionsMetaController))]
    [InlineData(typeof(AdvancedPermissionsPermissionController))]
    [InlineData(typeof(AdvancedPermissionsTreeController))]
    [InlineData(typeof(DocTypePermissionsController))]
    public void ManagementControllers_AreSectionGated(Type controllerType) =>
        Assert.True(
            TypeIsSectionGated(controllerType),
            $"{controllerType.Name} must apply the SectionAccessUsers policy so its management/mutating endpoints stay behind the Users section.");

    /// <summary>
    /// The by-role effective endpoint is consumed only by the Access Viewer (a management UI), so it
    /// stays gated on Users-section access.
    /// </summary>
    [Fact]
    public void EffectiveForRole_IsSectionGated() =>
        Assert.True(
            MethodIsSectionGated(typeof(AdvancedPermissionsEffectiveController), nameof(AdvancedPermissionsEffectiveController.GetEffectiveForRole)),
            "GET /effective/by-role is a management (Access Viewer) endpoint and must stay Users-section-gated.");

    /// <summary>
    /// The core fix: <c>GET /effective</c> (the current user's effective permissions at a node) must be
    /// reachable by any authenticated backoffice user, because the document permission condition calls it
    /// to gate content-editing actions. It must NOT be Users-section-gated at either the method or the
    /// controller level.
    /// </summary>
    [Fact]
    public void EffectiveForUser_IsNotSectionGated() =>
        Assert.False(
            MethodIsSectionGated(typeof(AdvancedPermissionsEffectiveController), nameof(AdvancedPermissionsEffectiveController.GetEffectiveForUser)),
            "GET /effective must be backoffice-scoped (not Users-section-gated) so content editors without Users-section access can still edit content.");

    /// <summary>
    /// The concrete controllers all derive from the base, so the shared routing/feature attributes
    /// (and backoffice-access gate) apply to every endpoint by inheritance.
    /// </summary>
    /// <param name="controllerType">The concrete controller under test.</param>
    [Theory]
    [InlineData(typeof(AdvancedPermissionsMetaController))]
    [InlineData(typeof(AdvancedPermissionsPermissionController))]
    [InlineData(typeof(AdvancedPermissionsEffectiveController))]
    [InlineData(typeof(AdvancedPermissionsTreeController))]
    [InlineData(typeof(DocTypePermissionsController))]
    public void ConcreteControllers_DeriveFromBase(Type controllerType) =>
        Assert.True(
            typeof(AdvancedPermissionsControllerBase).IsAssignableFrom(controllerType),
            $"{controllerType.Name} must derive from AdvancedPermissionsControllerBase so the shared backoffice-access gate applies.");

    /// <summary>
    /// Determines whether a controller type carries the
    /// <see cref="AuthorizationPolicies.SectionAccessUsers"/> policy (including via inheritance).
    /// </summary>
    /// <param name="type">The controller type to inspect.</param>
    /// <returns><c>true</c> if the type is gated on Users-section access; otherwise <c>false</c>.</returns>
    private static bool TypeIsSectionGated(Type type) =>
        type.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Any(a => a.Policy == AuthorizationPolicies.SectionAccessUsers);

    /// <summary>
    /// Determines whether an action is gated on Users-section access, considering both the action's own
    /// attributes and its declaring controller's attributes (a controller-level gate applies to all actions).
    /// </summary>
    /// <param name="controllerType">The controller declaring the action.</param>
    /// <param name="methodName">The action method name.</param>
    /// <returns><c>true</c> if the action is gated on Users-section access; otherwise <c>false</c>.</returns>
    private static bool MethodIsSectionGated(Type controllerType, string methodName)
    {
        var method = controllerType.GetMethod(methodName)
            ?? throw new InvalidOperationException($"Action '{methodName}' not found on {controllerType.Name}.");

        var methodGated = method
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Any(a => a.Policy == AuthorizationPolicies.SectionAccessUsers);

        return methodGated || TypeIsSectionGated(controllerType);
    }
}
