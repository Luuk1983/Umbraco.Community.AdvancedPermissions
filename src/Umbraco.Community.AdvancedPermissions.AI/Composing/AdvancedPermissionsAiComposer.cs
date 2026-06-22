using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.AdvancedPermissions.AI.Services;

namespace Umbraco.Community.AdvancedPermissions.AI.Composing;

/// <summary>
/// Registers the Advanced Permissions AI companion's services with Umbraco. The copilot tools
/// and tool scope are auto-discovered by Umbraco AI via their attributes, so only the package's
/// own services need explicit registration here.
/// </summary>
public sealed class AdvancedPermissionsAiComposer : IComposer
{
    /// <inheritdoc />
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddSingleton<IPermissionAuditAnalyzer, PermissionAuditAnalyzer>();
        builder.Services.AddSingleton<IContentPathResolver, ContentPathResolver>();
    }
}
