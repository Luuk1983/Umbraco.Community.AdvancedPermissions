using Microsoft.Extensions.DependencyInjection;
using Umbraco.AI.Extensions;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.AdvancedPermissions.AI.Context;
using Umbraco.Community.AdvancedPermissions.AI.Services;

namespace Umbraco.Community.AdvancedPermissions.AI.Composing;

/// <summary>
/// Registers the Advanced Permissions AI companion's services with Umbraco. The copilot tools
/// and tool scope are auto-discovered by Umbraco AI via their attributes, so only the package's
/// own services and the runtime-context grounding contributor need explicit registration here.
/// </summary>
public sealed class AdvancedPermissionsAiComposer : IComposer
{
    /// <inheritdoc />
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddSingleton<IPermissionAuditAnalyzer, PermissionAuditAnalyzer>();
        builder.Services.AddSingleton<IContentPathResolver, ContentPathResolver>();
        builder.Services.AddSingleton<IPermissionPresenter, PermissionPresenter>();
        builder.Services.AddSingleton<IPermissionRemediationService, PermissionRemediator>();

        // Append the grounding contributor after Umbraco AI's built-in contributors so the focused
        // entity's type is already in the context's data bag when ours reads it to gate on documents.
        builder.AIRuntimeContextContributors().Append<AdvancedPermissionsGroundingContributor>();
    }
}
