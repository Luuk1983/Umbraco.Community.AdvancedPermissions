using Umbraco.Cms.Api.Common.OpenApi;

namespace Umbraco.Community.AdvancedPermissions.Api.Swagger;

/// <summary>
/// Scopes the default <see cref="SchemaIdHandler"/> behaviour to types in this package's
/// namespace so their generated OpenAPI schema IDs stay unprefixed (e.g.
/// <c>PermissionEntryResponseModel</c>) and do not collide with Umbraco's own schemas.
/// </summary>
public class AdvancedPermissionsSchemaIdHandler : SchemaIdHandler
{
    /// <inheritdoc />
    public override bool CanHandle(Type type)
        => type.Namespace?.StartsWith("Umbraco.Community.AdvancedPermissions") ?? false;
}
