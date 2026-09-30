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
/// Gated on the same <c>SectionAccessUsers</c> policy the mutating controllers already require, so
/// the events tell a user nothing they could not already read. There is no second place where this
/// can be got wrong: core delivers no source that no authorizer claims, so this class is the whole
/// access story for the feature.
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
