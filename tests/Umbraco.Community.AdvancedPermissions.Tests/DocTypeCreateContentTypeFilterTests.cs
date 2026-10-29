using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.Filters;
using Umbraco.Community.AdvancedPermissions.Abstractions.Constants;
using Umbraco.Community.AdvancedPermissions.Abstractions.Interfaces;
using Umbraco.Community.AdvancedPermissions.Abstractions.Models;
using Umbraco.Community.AdvancedPermissions.Filters;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests the Library side of <see cref="DocTypeCreateContentTypeFilter"/>: the parent-aware
/// <c>FilterAllowedInLibraryAsync(contentTypes, parentKey)</c> overload Umbraco calls since 18.1
/// (<see href="https://github.com/umbraco/Umbraco-CMS/pull/23161">PR #23161</see>). The single-argument
/// overload is obsolete, so the filter must implement the parent-aware one.
/// </summary>
public sealed class DocTypeCreateContentTypeFilterTests
{
    private readonly IDocTypePermissionService _docTypePermissionService = Substitute.For<IDocTypePermissionService>();
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor = Substitute.For<IBackOfficeSecurityAccessor>();
    private readonly IEntityService _entityService = Substitute.For<IEntityService>();
    private readonly IContentTypeFilter _sut;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocTypeCreateContentTypeFilterTests"/> class with a
    /// signed-in backoffice user in the <c>editor</c> group.
    /// </summary>
    public DocTypeCreateContentTypeFilterTests()
    {
        var group = Substitute.For<IReadOnlyUserGroup>();
        group.Alias.Returns("editor");
        var user = Substitute.For<IUser>();
        user.Groups.Returns([group]);
        var security = Substitute.For<IBackOfficeSecurity>();
        security.CurrentUser.Returns(user);
        _backOfficeSecurityAccessor.BackOfficeSecurity.Returns(security);

        _sut = new DocTypeCreateContentTypeFilter(
            _docTypePermissionService,
            _backOfficeSecurityAccessor,
            _entityService,
            NullLogger<DocTypeCreateContentTypeFilter>.Instance);
    }

    /// <summary>
    /// Element types the user may not create are dropped from the Library create list, resolved
    /// against the element-type verb rather than the document one.
    /// </summary>
    /// <param name="hasParent">Whether the create happens inside a folder (<see langword="true"/>) or at the Library root.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FilterAllowedInLibraryAsync_WithParentKey_DropsDeniedElementTypes(bool hasParent)
    {
        var allowed = StubContentType(Guid.NewGuid());
        var denied = StubContentType(Guid.NewGuid());
        StubCreateOfType(allowed.Key, isAllowed: true);
        StubCreateOfType(denied.Key, isAllowed: false);

        var result = await _sut.FilterAllowedInLibraryAsync([allowed, denied], hasParent ? Guid.NewGuid() : null);

        Assert.Equal([allowed], result);
    }

    /// <summary>
    /// Library element-type entries are section-global and live on the virtual root, so a folder key
    /// still resolves against the virtual root. Making it folder-aware is tracked separately (issue #54).
    /// </summary>
    [Fact]
    public async Task FilterAllowedInLibraryAsync_WithParentKey_ResolvesAgainstVirtualRoot()
    {
        var contentType = StubContentType(Guid.NewGuid());
        StubCreateOfType(contentType.Key, isAllowed: true);

        await _sut.FilterAllowedInLibraryAsync([contentType], Guid.NewGuid());

        await _docTypePermissionService.Received(1).ResolveCreateForRolesAsync(
            Arg.Is<IReadOnlyList<string>>(roles => roles.Contains("editor") && roles.Contains(AdvancedPermissionsConstants.EveryoneRoleAlias)),
            Arg.Is<IReadOnlyList<Guid>>(path => path.SequenceEqual(new[] { AdvancedPermissionsConstants.VirtualRootNodeKey })),
            contentType.Key,
            AdvancedPermissionsConstants.VerbElementCreateOfType,
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Makes the doc-type service answer the element create-of-type check for one content type.
    /// </summary>
    /// <param name="contentTypeKey">The content type the answer is for.</param>
    /// <param name="isAllowed">Whether creating it is allowed.</param>
    private void StubCreateOfType(Guid contentTypeKey, bool isAllowed) =>
        _docTypePermissionService
            .ResolveCreateForRolesAsync(
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<IReadOnlyList<Guid>>(),
                contentTypeKey,
                AdvancedPermissionsConstants.VerbElementCreateOfType,
                Arg.Any<CancellationToken>())
            .Returns(new EffectivePermission(AdvancedPermissionsConstants.VerbElementCreateOfType, isAllowed, IsExplicit: !isAllowed, Reasoning: []));

    /// <summary>
    /// Creates a content type substitute with the given key.
    /// </summary>
    /// <param name="key">The content type key.</param>
    /// <returns>The substitute.</returns>
    private static IContentTypeComposition StubContentType(Guid key)
    {
        var contentType = Substitute.For<IContentTypeComposition>();
        contentType.Key.Returns(key);
        return contentType;
    }
}
