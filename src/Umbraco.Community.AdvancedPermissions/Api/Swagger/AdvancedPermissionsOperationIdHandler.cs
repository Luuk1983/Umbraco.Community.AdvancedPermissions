using Asp.Versioning;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Api.Common.OpenApi;

namespace Umbraco.Community.AdvancedPermissions.Api.Swagger;

/// <summary>
/// Shortens generated OpenAPI operation IDs for this package so that tools like
/// hey-api/openapi-ts produce readable method names (e.g. <c>getRoles</c>) instead
/// of the full-path default (e.g. <c>getUmbracoManagementApiV1AdvancedPermissionsRoles</c>).
/// Scoped via <see cref="CanHandle"/> so it does not affect other Umbraco endpoints.
/// </summary>
public class AdvancedPermissionsOperationIdHandler(IOptions<ApiVersioningOptions> apiVersioningOptions)
    : OperationIdHandler(apiVersioningOptions)
{
    /// <inheritdoc />
    protected override bool CanHandle(ApiDescription apiDescription,
        ControllerActionDescriptor controllerActionDescriptor)
        => controllerActionDescriptor.ControllerTypeInfo.Namespace?.StartsWith(
            "Umbraco.Community.AdvancedPermissions",
            StringComparison.InvariantCultureIgnoreCase) is true;

    /// <inheritdoc />
    public override string Handle(ApiDescription apiDescription)
        => $"{apiDescription.ActionDescriptor.RouteValues["action"]}";
}
