using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Umbraco.Community.AdvancedPermissions.Controllers;
using Umbraco.Community.AdvancedPermissions.Controllers.Models;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Runs the shared save-endpoint concurrency contract (<see cref="NodeBatchSaveTestsBase{TService}"/>)
/// against the library element permission controller. It exists as its own run, rather than being
/// assumed from the content controller, because the element controller is a separate class with its
/// own verb set: a shared implementation is only proven shared by exercising both.
/// </summary>
public sealed class ElementBatchSavePermissionsTests : NodeBatchSaveTestsBase<IElementNodePermissionService>
{
    /// <inheritdoc />
    protected override string ValidVerb => AdvancedPermissionsConstants.VerbElementRead;

    /// <inheritdoc />
    protected override string OtherValidVerb => AdvancedPermissionsConstants.VerbElementDelete;

    /// <inheritdoc />
    protected override string ForeignVerb => AdvancedPermissionsConstants.VerbRead;

    /// <inheritdoc />
    protected override Task<IActionResult> SaveAsync(IElementNodePermissionService service, SavePermissionsRequestModel request) =>
        BuildController(service).SavePermissions(request, CancellationToken.None);

    /// <inheritdoc />
    protected override Task<IActionResult> BatchSaveAsync(IElementNodePermissionService service, BatchSavePermissionsRequestModel request) =>
        BuildController(service).BatchSavePermissions(request, CancellationToken.None);

    /// <summary>
    /// Builds the controller under test with substitutes for every collaborator besides the
    /// element permission service, which each test configures directly.
    /// </summary>
    /// <param name="service">The element permission service substitute.</param>
    /// <returns>The controller.</returns>
    private static ElementPermissionsPermissionController BuildController(IElementNodePermissionService service) =>
        new(service,
            Substitute.For<IElementPermissionRepository>(),
            Substitute.For<global::Umbraco.Cms.Core.Services.IEntityService>());
}
