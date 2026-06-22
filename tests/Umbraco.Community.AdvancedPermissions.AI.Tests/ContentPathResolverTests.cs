using NSubstitute;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.AI.Services;

namespace Umbraco.Community.AdvancedPermissions.AI.Tests;

/// <summary>
/// Unit tests for <see cref="ContentPathResolver"/>.
/// Tests are written test-first and mirror the materialized-path parsing behavior of the
/// main package's controller base (<c>BuildPathFromRoot</c>): the <c>-1</c> root sentinel is
/// dropped, ids are mapped to keys via <see cref="IEntityService.GetAll(UmbracoObjectTypes, int[])"/>,
/// and root→node order is preserved.
/// </summary>
public sealed class ContentPathResolverTests
{
    /// <summary>The mocked Umbraco entity service the resolver depends on.</summary>
    private readonly IEntityService _entityService = Substitute.For<IEntityService>();

    /// <summary>The system under test.</summary>
    private readonly IContentPathResolver _sut;

    /// <summary>Initializes a new instance of the <see cref="ContentPathResolverTests"/> class.</summary>
    public ContentPathResolverTests()
    {
        _sut = new ContentPathResolver(_entityService);
    }

    /// <summary>Creates a stubbed <see cref="IEntitySlim"/> with the given int id and Guid key.</summary>
    /// <param name="id">The integer id of the entity.</param>
    /// <param name="key">The Guid key of the entity.</param>
    /// <returns>A substituted <see cref="IEntitySlim"/>.</returns>
    private static IEntitySlim StubEntity(int id, Guid key)
    {
        var e = Substitute.For<IEntitySlim>();
        e.Id.Returns(id);
        e.Key.Returns(key);
        return e;
    }

    /// <summary>
    /// Happy path: a materialized path of <c>-1,1057,1099</c> resolves to the ordered
    /// keys <c>[keyRoot, keyTarget]</c> — the <c>-1</c> sentinel dropped, root first, target last.
    /// </summary>
    [Fact]
    public void GetPathFromRoot_ResolvesMaterializedPath_RootFirstTargetLastSentinelDropped()
    {
        var keyRoot = Guid.NewGuid();
        var keyTarget = Guid.NewGuid();
        var rootEntity = StubEntity(1057, keyRoot);
        var targetEntity = StubEntity(1099, keyTarget);

        _entityService
            .GetAllPaths(UmbracoObjectTypes.Document, Arg.Any<Guid[]>())
            .Returns([new TreeEntityPath { Id = 1099, Path = "-1,1057,1099" }]);
        _entityService
            .GetAll(UmbracoObjectTypes.Document, Arg.Any<int[]>())
            .Returns([rootEntity, targetEntity]);

        var result = _sut.GetPathFromRoot(keyTarget);

        Assert.Equal(new[] { keyRoot, keyTarget }, result);
    }

    /// <summary>When the entity service returns no paths, the resolver returns an empty list.</summary>
    [Fact]
    public void GetPathFromRoot_NodeNotFound_ReturnsEmptyList()
    {
        _entityService
            .GetAllPaths(UmbracoObjectTypes.Document, Arg.Any<Guid[]>())
            .Returns([]);

        var result = _sut.GetPathFromRoot(Guid.NewGuid());

        Assert.Empty(result);
    }

    /// <summary>
    /// Ids in the materialized path that do not resolve to an entity via <c>GetAll</c> are
    /// skipped, preserving order for the ids that do resolve. Mirrors <c>BuildPathFromRoot</c>.
    /// </summary>
    [Fact]
    public void GetPathFromRoot_UnresolvableId_IsSkipped()
    {
        var keyRoot = Guid.NewGuid();
        var keyTarget = Guid.NewGuid();
        var rootEntity = StubEntity(1057, keyRoot);
        var targetEntity = StubEntity(1099, keyTarget);

        // Path has three ids but GetAll only maps 1057 and 1099 — 1098 is missing.
        _entityService
            .GetAllPaths(UmbracoObjectTypes.Document, Arg.Any<Guid[]>())
            .Returns([new TreeEntityPath { Id = 1099, Path = "-1,1057,1098,1099" }]);
        _entityService
            .GetAll(UmbracoObjectTypes.Document, Arg.Any<int[]>())
            .Returns([rootEntity, targetEntity]);

        var result = _sut.GetPathFromRoot(keyTarget);

        Assert.Equal(new[] { keyRoot, keyTarget }, result);
    }
}
