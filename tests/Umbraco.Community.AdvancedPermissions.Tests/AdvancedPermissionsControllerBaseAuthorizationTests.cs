using Microsoft.AspNetCore.Authorization;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Community.AdvancedPermissions.Controllers;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Verifies the authorization gate on the management-API controller base.
/// </summary>
/// <remarks>
/// The mutating endpoints (PUT/DELETE /permissions) are an obvious privilege-escalation
/// primitive without a section-level authorization check: any authenticated backoffice
/// user could grant themselves arbitrary permissions by calling the API directly,
/// bypassing the UI. These tests assert the <see cref="AuthorizeAttribute"/> with the
/// <see cref="AuthorizationPolicies.SectionAccessUsers"/> policy is present on the base
/// class so it inherits to every endpoint.
/// </remarks>
public sealed class AdvancedPermissionsControllerBaseAuthorizationTests
{
    /// <summary>
    /// The controller base must apply the <see cref="AuthorizationPolicies.SectionAccessUsers"/>
    /// policy via an <see cref="AuthorizeAttribute"/>. This matches the section that hosts the
    /// Permissions Editor UI: a user who can't see the Users section also can't edit advanced
    /// permissions via the API.
    /// </summary>
    /// <remarks>
    /// The base class <c>ManagementApiControllerBase</c> already carries other Authorize
    /// attributes (BackOfficeAccess, UmbracoFeatureEnabled), so multiple Authorize
    /// attributes are inherited. The test asserts the SectionAccessUsers policy is among
    /// them rather than assuming a single attribute.
    /// </remarks>
    [Fact]
    public void ControllerBase_AppliesSectionAccessUsersPolicy()
    {
        var policies = typeof(AdvancedPermissionsControllerBase)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Select(a => a.Policy)
            .ToList();

        Assert.Contains(AuthorizationPolicies.SectionAccessUsers, policies);
    }

    /// <summary>
    /// The four concrete controllers all derive from the base, so the authorization
    /// attribute applies to every endpoint by inheritance. This test guards against a
    /// future refactor where a controller is changed to derive from
    /// <c>ManagementApiControllerBase</c> directly and silently loses the gate.
    /// </summary>
    [Theory]
    [InlineData(typeof(AdvancedPermissionsMetaController))]
    [InlineData(typeof(AdvancedPermissionsPermissionController))]
    [InlineData(typeof(AdvancedPermissionsEffectiveController))]
    [InlineData(typeof(AdvancedPermissionsTreeController))]
    public void ConcreteControllers_DeriveFromBase(Type controllerType) =>
        Assert.True(
            typeof(AdvancedPermissionsControllerBase).IsAssignableFrom(controllerType),
            $"{controllerType.Name} must derive from AdvancedPermissionsControllerBase so the section-access authorization gate applies.");
}
