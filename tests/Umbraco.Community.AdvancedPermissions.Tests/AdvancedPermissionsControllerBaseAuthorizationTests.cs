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
/// No endpoint is exempt. Until v19 the element effective controller had a current-user action that the
/// replacement element/element-folder conditions called, and it had to stay reachable without Users-section
/// access. Those conditions now call Umbraco's own <c>GET /user/current/permissions/element</c>, which is
/// routed through <c>IElementPermissionService</c> (issue #53), exactly as the document condition uses the
/// native per-document endpoint, so that action was removed.
///
/// The gate was previously an <see cref="AuthorizeAttribute"/> on
/// <see cref="AdvancedPermissionsControllerBase"/>, which inherits to every endpoint and cannot be
/// removed by a derived controller — hence it is now applied per management controller instead.
/// These reflection tests assert that topology so a forgotten attribute fails the build.
/// </remarks>
public sealed class AdvancedPermissionsControllerBaseAuthorizationTests
{
    /// <summary>
    /// The base controller must NOT carry the <see cref="AuthorizationPolicies.SectionAccessUsers"/>
    /// policy: a base-class <see cref="AuthorizeAttribute"/> inherits to every endpoint and cannot be
    /// removed downstream, which once forced a backoffice-scoped endpoint to be Users-section-gated. The gate now
    /// lives on the individual management controllers.
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
    [InlineData(typeof(ElementPermissionsEffectiveController))]
    [InlineData(typeof(AdvancedPermissionsStartNodesController))]
    public void ManagementControllers_AreSectionGated(Type controllerType) =>
        Assert.True(
            TypeIsSectionGated(controllerType),
            $"{controllerType.Name} must apply the SectionAccessUsers policy so its management/mutating endpoints stay behind the Users section.");

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
    [InlineData(typeof(AdvancedPermissionsStartNodesController))]
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
}
