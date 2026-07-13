using Microsoft.AspNetCore.Authorization;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Community.AdvancedPermissions.Controllers;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Verifies the authorization topology across the management-API controllers.
/// </summary>
/// <remarks>
/// Most endpoints power the package's management UIs (Permissions Editor, Access Viewer, Library editors,
/// Doc-type editor) which live behind the Users section, and the mutating endpoints (PUT/DELETE
/// permissions) are privilege-escalation primitives. Those must stay gated on
/// <see cref="AuthorizationPolicies.SectionAccessUsers"/>.
///
/// The one exception is <see cref="ElementPermissionsEffectiveController.GetEffectiveForCurrentUser"/>
/// (<c>GET /element/effective/current-user</c>): it is the seam the replacement element/element-folder
/// permission conditions call to decide whether the current user may act on a library node. Gating it on
/// Users-section access meant any content editor without that section got a 403, the conditions denied
/// every action, and library content became read-only. It must remain backoffice-authenticated but NOT
/// Users-section-gated.
///
/// (Documents are unaffected in v18: their condition uses Umbraco's native per-document current-user
/// endpoint, which is routed through <c>IContentPermissionService</c> and is not Users-section-gated.)
///
/// The gate was previously an <see cref="AuthorizeAttribute"/> on
/// <see cref="AdvancedPermissionsControllerBase"/>, which inherits to every endpoint and cannot be
/// removed by a derived controller — hence it is now applied per management controller/action instead.
/// These reflection tests assert that topology so a forgotten attribute fails the build.
/// </remarks>
public sealed class AdvancedPermissionsControllerBaseAuthorizationTests
{
    /// <summary>
    /// The base controller must NOT carry the <see cref="AuthorizationPolicies.SectionAccessUsers"/>
    /// policy: a base-class <see cref="AuthorizeAttribute"/> inherits to every endpoint and cannot be
    /// removed downstream, which is exactly what forced the backoffice-scoped current-user endpoint to be
    /// Users-section-gated. The gate now lives on the individual management controllers/actions.
    /// </summary>
    [Fact]
    public void ControllerBase_DoesNotApplySectionAccessUsersPolicy() =>
        Assert.False(
            TypeIsSectionGated(typeof(AdvancedPermissionsControllerBase)),
            "AdvancedPermissionsControllerBase must not carry the SectionAccessUsers policy, or it would inherit to every endpoint (including the ones content editors need).");

    /// <summary>
    /// Every endpoint must still require an authenticated backoffice user. That gate
    /// (<see cref="AuthorizationPolicies.BackOfficeAccess"/>) comes from <c>ManagementApiControllerBase</c>;
    /// this test guards against the base being changed in a way that drops it and accidentally exposes
    /// endpoints anonymously.
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
    /// management UIs and expose the mutating (save/delete) endpoints.
    /// </summary>
    /// <param name="controllerType">The management controller under test.</param>
    [Theory]
    [InlineData(typeof(AdvancedPermissionsMetaController))]
    [InlineData(typeof(AdvancedPermissionsPermissionController))]
    [InlineData(typeof(AdvancedPermissionsTreeController))]
    [InlineData(typeof(AdvancedPermissionsEffectiveController))]
    [InlineData(typeof(DocTypePermissionsController))]
    [InlineData(typeof(ElementPermissionsMetaController))]
    [InlineData(typeof(ElementPermissionsPermissionController))]
    [InlineData(typeof(ElementPermissionsTreeController))]
    public void ManagementControllers_AreSectionGated(Type controllerType) =>
        Assert.True(
            TypeIsSectionGated(controllerType),
            $"{controllerType.Name} must apply the SectionAccessUsers policy so its management/mutating endpoints stay behind the Users section.");

    /// <summary>
    /// The arbitrary-user and arbitrary-role element effective endpoints are consumed only by the Library
    /// access viewer (a management UI), so they stay gated on Users-section access.
    /// </summary>
    /// <param name="methodName">The action under test.</param>
    [Theory]
    [InlineData(nameof(ElementPermissionsEffectiveController.GetEffectiveForUser))]
    [InlineData(nameof(ElementPermissionsEffectiveController.GetEffectiveForRole))]
    public void ElementEffective_ManagementActions_AreSectionGated(string methodName) =>
        Assert.True(
            MethodIsSectionGated(typeof(ElementPermissionsEffectiveController), methodName),
            $"ElementPermissionsEffectiveController.{methodName} is a management (Library access viewer) endpoint and must stay Users-section-gated.");

    /// <summary>
    /// The core fix: <c>GET /element/effective/current-user</c> must be reachable by any authenticated
    /// backoffice user, because the element and element-folder permission conditions call it to gate
    /// library-editing actions. It must NOT be Users-section-gated at either the method or the controller
    /// level.
    /// </summary>
    [Fact]
    public void ElementEffective_CurrentUser_IsNotSectionGated() =>
        Assert.False(
            MethodIsSectionGated(typeof(ElementPermissionsEffectiveController), nameof(ElementPermissionsEffectiveController.GetEffectiveForCurrentUser)),
            "GET /element/effective/current-user must be backoffice-scoped (not Users-section-gated) so content editors without Users-section access can still edit library content.");

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
    [InlineData(typeof(ElementPermissionsMetaController))]
    [InlineData(typeof(ElementPermissionsPermissionController))]
    [InlineData(typeof(ElementPermissionsTreeController))]
    [InlineData(typeof(ElementPermissionsEffectiveController))]
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
