using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Umbraco.Cms.Core.ServerEvents;
using Umbraco.Cms.Web.Common.Authorization;

namespace Umbraco.Community.AdvancedPermissions.ServerEvents;

/// <summary>
/// Decides who receives this package's server events.
/// </summary>
/// <remarks>
/// <para>
/// One authorizer for every source the package publishes. A source no authorizer claims is silently
/// never delivered - no error is raised anywhere - so a source missing from
/// <see cref="AuthorizableEventSources"/> disables its feature with nothing to say so. That is why
/// there is a single authorizer claiming <see cref="AdvancedPermissionsServerEvents.AllSources"/>
/// rather than one per family: there is no second list that can fall out of step with the first.
/// </para>
/// <para>
/// Gated on the same <c>SectionAccessUsers</c> policy the mutating controllers already require, so
/// the events tell a user nothing they could not already read. This class is the whole access story
/// for the feature.
/// </para>
/// <para>
/// Content editors without Users-section access therefore receive nothing at all. Pushing
/// permission changes into an ordinary editor's own session is a larger question about what an
/// editor may learn of the permission tree, and belongs with the client-side effective-permission
/// cache work rather than here.
/// </para>
/// </remarks>
/// <param name="authorizationService">Used to evaluate the Users-section policy.</param>
public sealed class AdvancedPermissionsEventAuthorizer(IAuthorizationService authorizationService)
    : IEventSourceAuthorizer
{
    /// <inheritdoc />
    public IEnumerable<string> AuthorizableEventSources => AdvancedPermissionsServerEvents.AllSources;

    /// <inheritdoc />
    public async Task<bool> AuthorizeAsync(ClaimsPrincipal principal, string eventSource)
    {
        var result = await authorizationService.AuthorizeAsync(principal, AuthorizationPolicies.SectionAccessUsers);
        return result.Succeeded;
    }
}
