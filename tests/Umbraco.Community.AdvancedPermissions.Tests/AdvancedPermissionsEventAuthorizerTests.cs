using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Community.AdvancedPermissions.ServerEvents;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests that this package's server-event sources are delivered only to users who may already see
/// the permission data they describe.
/// </summary>
public sealed class AdvancedPermissionsEventAuthorizerTests
{
    /// <summary>The authorizer must claim every source the package publishes.</summary>
    /// <remarks>
    /// A source no authorizer claims is never delivered by core, so a source missing from this
    /// list disables its feature completely and silently.
    /// </remarks>
    [Fact]
    public void AuthorizableEventSources_CoversEverySourceThePackagePublishes()
    {
        var authorizer = new AdvancedPermissionsEventAuthorizer(Substitute.For<IAuthorizationService>());

        Assert.Equal(
            AdvancedPermissionsServerEvents.AllSources.OrderBy(s => s, StringComparer.Ordinal),
            authorizer.AuthorizableEventSources.OrderBy(s => s, StringComparer.Ordinal));
    }

    /// <summary>A principal that satisfies the Users-section policy receives the source.</summary>
    /// <remarks>
    /// Stubs the interface's own three-parameter <c>AuthorizeAsync</c> overload directly, with an
    /// explicit matcher on every parameter. The two-parameter form used by the production code is
    /// an extension method that forwards to this one with <c>resource: null</c>; stubbing through
    /// the extension call directly hits an <c>AmbiguousArgumentsException</c> in NSubstitute,
    /// because the <see cref="ClaimsPrincipal"/> argument and the <c>null</c> resource argument are
    /// both literally <c>null</c> and indistinguishable to the matcher.
    /// </remarks>
    [Fact]
    public async Task AuthorizeAsync_UsersSectionPrincipal_IsAuthorized()
    {
        var authorizationService = Substitute.For<IAuthorizationService>();
        authorizationService
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), AuthorizationPolicies.SectionAccessUsers)
            .Returns(AuthorizationResult.Success());

        var authorizer = new AdvancedPermissionsEventAuthorizer(authorizationService);

        Assert.True(await authorizer.AuthorizeAsync(
            new ClaimsPrincipal(),
            AdvancedPermissionsServerEvents.NodePermissionsSource));
    }

    /// <summary>A principal without Users-section access receives nothing.</summary>
    /// <remarks>
    /// See the remarks on <see cref="AuthorizeAsync_UsersSectionPrincipal_IsAuthorized"/> for why
    /// this stubs the three-parameter overload explicitly rather than the two-parameter extension
    /// method the plan originally called out.
    /// </remarks>
    [Fact]
    public async Task AuthorizeAsync_WithoutUsersSection_IsRefused()
    {
        var authorizationService = Substitute.For<IAuthorizationService>();
        authorizationService
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), AuthorizationPolicies.SectionAccessUsers)
            .Returns(AuthorizationResult.Failed());

        var authorizer = new AdvancedPermissionsEventAuthorizer(authorizationService);

        Assert.False(await authorizer.AuthorizeAsync(
            new ClaimsPrincipal(),
            AdvancedPermissionsServerEvents.AccessSource));
    }
}
