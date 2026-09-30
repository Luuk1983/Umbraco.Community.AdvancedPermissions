using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Umbraco.Community.AdvancedPermissions.Controllers;
using Umbraco.Community.AdvancedPermissions.Controllers.Models;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Runs the shared save-endpoint concurrency contract (<see cref="NodeBatchSaveTestsBase{TService}"/>)
/// against the content permission controller.
/// </summary>
public sealed class BatchSavePermissionsTests : NodeBatchSaveTestsBase<IAdvancedPermissionService>
{
    /// <inheritdoc />
    protected override string ValidVerb => AdvancedPermissionsConstants.VerbRead;

    /// <inheritdoc />
    protected override string OtherValidVerb => AdvancedPermissionsConstants.VerbDelete;

    /// <inheritdoc />
    protected override string ForeignVerb => AdvancedPermissionsConstants.VerbElementRead;

    /// <inheritdoc />
    protected override Task<IActionResult> SaveAsync(IAdvancedPermissionService service, SavePermissionsRequestModel request) =>
        BuildController(service).SavePermissions(request, CancellationToken.None);

    /// <inheritdoc />
    protected override Task<IActionResult> BatchSaveAsync(IAdvancedPermissionService service, BatchSavePermissionsRequestModel request) =>
        BuildController(service).BatchSavePermissions(request, CancellationToken.None);

    /// <summary>
    /// Builds the controller under test with substitutes for every collaborator besides the
    /// permission service, which each test configures directly.
    /// </summary>
    /// <param name="service">The permission service substitute.</param>
    /// <returns>The controller.</returns>
    private static AdvancedPermissionsPermissionController BuildController(IAdvancedPermissionService service) =>
        new(service,
            Substitute.For<IAdvancedPermissionRepository>(),
            Substitute.For<global::Umbraco.Cms.Core.Services.IEntityService>());
}
