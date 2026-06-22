using Umbraco.AI.Core.Tools.Scopes;

namespace Umbraco.Community.AdvancedPermissions.AI.Scopes;

/// <summary>Read-only Umbraco AI tool scope for querying Advanced Permissions (access explanations and audits).</summary>
[AIToolScope("advanced-permissions:read", Icon = "icon-lock", Domain = "Advanced Permissions")]
public sealed class AdvancedPermissionsReadScope : AIToolScopeBase;
