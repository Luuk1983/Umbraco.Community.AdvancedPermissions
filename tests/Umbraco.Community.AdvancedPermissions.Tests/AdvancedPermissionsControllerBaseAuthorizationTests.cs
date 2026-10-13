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
/// That includes <see cref="AdvancedPermissionsEffectiveController"/> (<c>GET /effective</c> and
/// <c>GET /effective/by-role</c>), which is consumed only by the Access Viewer. It used to be the data
/// source of the replacement document permission condition, which forced <c>GET /effective</c> to stay
/// ungated so content editors without Users-section access could still edit (issue #41). Since the
/// Umbraco floor is 17.4.0, the condition uses Umbraco's native per-document current-user endpoint
/// (<c>GET /user/current/permissions/document</c>) instead. That endpoint is routed through
/// <c>IContentPermissionService</c> and is not Users-section-gated, so the package's own endpoint no
/// longer needs the exception.
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
    /// removed downstream, so any future endpoint content editors need could never be opened up. The gate
    /// lives on the individual management controllers/actions instead.
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
    [InlineData(typeof(AdvancedPermissionsEffectiveController))]
    [InlineData(typeof(AdvancedPermissionsTreeController))]
    [InlineData(typeof(DocTypePermissionsController))]
    public void ManagementControllers_AreSectionGated(Type controllerType) =>
        Assert.True(
            TypeIsSectionGated(controllerType),
            $"{controllerType.Name} must apply the SectionAccessUsers policy so its management/mutating endpoints stay behind the Users section.");

    /// <summary>
    /// <c>GET /effective</c> resolves the permissions of an arbitrary user, so it is a management (Access
    /// Viewer) endpoint and must be Users-section-gated. It no longer backs the document permission
    /// condition (that uses Umbraco's native per-document current-user endpoint), so content editors
    /// without Users-section access do not need it.
    /// </summary>
    [Fact]
    public void EffectiveForUser_IsSectionGated() =>
        Assert.True(
            MethodIsSectionGated(typeof(AdvancedPermissionsEffectiveController), nameof(AdvancedPermissionsEffectiveController.GetEffectiveForUser)),
            "GET /effective resolves any user's permissions and must be Users-section-gated.");

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
