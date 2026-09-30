using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Community.AdvancedPermissions.ServerEvents;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests that this package's server-event sources are delivered only to users who may already see
/// the permission data they describe - and that the authorizer claims every source the package has.
/// </summary>
/// <remarks>
/// Core delivers no source that no authorizer claims, so the authorizer's source list is the whole
/// access story and a source missing from it disables its feature completely and silently: no error is
/// raised anywhere, the screen simply never updates. The coverage tests below therefore do not compare
/// the authorizer with <see cref="AdvancedPermissionsServerEvents.AllSources"/> alone - both are
/// written by hand, so a source dropped from both would pass. They compare against the source
/// constants found by reflection, and against the literal wire strings the client is written against.
/// </remarks>
public sealed class AdvancedPermissionsEventAuthorizerTests
{
    /// <summary>
    /// The wire strings the client subscribes to. A literal list, deliberately not derived from the
    /// constants under test: it is the contract, so removing or renaming a source must fail here.
    /// </summary>
    private static readonly string[] ExpectedSources =
    [
        "AdvancedPermissions:NodePermissions",
        "AdvancedPermissions:ElementPermissions",
        "AdvancedPermissions:DocTypePermissions",
        "AdvancedPermissions:ElementTypePermissions",
        "AdvancedPermissions:Access",
    ];

    /// <summary>Finds every event-source constant declared on <see cref="AdvancedPermissionsServerEvents"/>.</summary>
    /// <returns>The declared source strings.</returns>
    private static IReadOnlyList<string> DeclaredSourceConstants() =>
        typeof(AdvancedPermissionsServerEvents)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f is { IsLiteral: true, IsInitOnly: false } && f.FieldType == typeof(string) && f.Name.EndsWith("Source", StringComparison.Ordinal))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

    /// <summary>Creates the authorizer under test over a substitute authorization service.</summary>
    /// <returns>The authorizer.</returns>
    private static AdvancedPermissionsEventAuthorizer CreateAuthorizer() =>
        new(Substitute.For<IAuthorizationService>());

    /// <summary>
    /// The authorizer must claim every source the package declares. Compared against the constants found
    /// by reflection, so a source added without being claimed fails here even if it was also left out of
    /// <see cref="AdvancedPermissionsServerEvents.AllSources"/>.
    /// </summary>
    [Fact]
    public void AuthorizableEventSources_CoversEverySourceConstantDeclared()
    {
        var declared = DeclaredSourceConstants();

        Assert.NotEmpty(declared);
        Assert.Equal(
            declared.OrderBy(s => s, StringComparer.Ordinal),
            CreateAuthorizer().AuthorizableEventSources.OrderBy(s => s, StringComparer.Ordinal));
    }

    /// <summary>
    /// The authorizer's list is exactly the five wire strings the client subscribes to. Removing a
    /// source from the package - from the constants, from <c>AllSources</c> or from the authorizer -
    /// makes this fail, which is the point: a missing source is otherwise silent.
    /// </summary>
    [Fact]
    public void AuthorizableEventSources_IsExactlyTheWireContract() =>
        Assert.Equal(
            ExpectedSources.OrderBy(s => s, StringComparer.Ordinal),
            CreateAuthorizer().AuthorizableEventSources.OrderBy(s => s, StringComparer.Ordinal));

    /// <summary>
    /// <c>AllSources</c> is what the authorizer hands out, so it must list every declared constant. A
    /// constant added to the class but not to the list would be published by a handler and claimed by
    /// nobody.
    /// </summary>
    [Fact]
    public void AllSources_ListsEveryDeclaredSourceConstant() =>
        Assert.Equal(
            DeclaredSourceConstants().OrderBy(s => s, StringComparer.Ordinal),
            AdvancedPermissionsServerEvents.AllSources.OrderBy(s => s, StringComparer.Ordinal));

    /// <summary>
    /// A source listed twice would make the authorizer's claim look complete while hiding a missing one,
    /// so the list must have no duplicates.
    /// </summary>
    [Fact]
    public void AuthorizableEventSources_HasNoDuplicates()
    {
        var sources = CreateAuthorizer().AuthorizableEventSources.ToList();

        Assert.Equal(sources.Count, sources.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>A principal that satisfies the Users-section policy receives every source.</summary>
    /// <remarks>
    /// Stubs the interface's own three-parameter <c>AuthorizeAsync</c> overload directly, with an
    /// explicit matcher on every parameter. The two-parameter form used by the production code is an
    /// extension method that forwards to this one with <c>resource: null</c>; stubbing through the
    /// extension call directly hits an <c>AmbiguousArgumentsException</c> in NSubstitute, because the
    /// <see cref="ClaimsPrincipal"/> argument and the <c>null</c> resource argument are both literally
    /// <c>null</c> and indistinguishable to the matcher.
    /// </remarks>
    /// <param name="source">Each source the package publishes.</param>
    [Theory]
    [MemberData(nameof(Sources))]
    public async Task AuthorizeAsync_UsersSectionPrincipal_IsAuthorized(string source)
    {
        var authorizationService = Substitute.For<IAuthorizationService>();
        authorizationService
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), AuthorizationPolicies.SectionAccessUsers)
            .Returns(AuthorizationResult.Success());

        var authorizer = new AdvancedPermissionsEventAuthorizer(authorizationService);

        Assert.True(await authorizer.AuthorizeAsync(new ClaimsPrincipal(), source));
    }

    /// <summary>A principal without Users-section access receives nothing, from any source.</summary>
    /// <remarks>
    /// See the remarks on <see cref="AuthorizeAsync_UsersSectionPrincipal_IsAuthorized"/> for why this
    /// stubs the three-parameter overload explicitly.
    /// </remarks>
    /// <param name="source">Each source the package publishes.</param>
    [Theory]
    [MemberData(nameof(Sources))]
    public async Task AuthorizeAsync_WithoutUsersSection_IsRefused(string source)
    {
        var authorizationService = Substitute.For<IAuthorizationService>();
        authorizationService
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), AuthorizationPolicies.SectionAccessUsers)
            .Returns(AuthorizationResult.Failed());

        var authorizer = new AdvancedPermissionsEventAuthorizer(authorizationService);

        Assert.False(await authorizer.AuthorizeAsync(new ClaimsPrincipal(), source));
    }

    /// <summary>Every source, as theory data.</summary>
    /// <returns>One case per source string.</returns>
    public static TheoryData<string> Sources() => [.. ExpectedSources];
}
