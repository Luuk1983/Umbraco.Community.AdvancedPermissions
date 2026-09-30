using System.Reflection;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.Core.Tests.Concurrency;

/// <summary>
/// Tests that the published contract stays compatible with an implementation written before the
/// stamped save overloads existed: one that supplies only the original, unstamped member.
/// </summary>
/// <remarks>
/// <para>
/// The contract package shipped the unstamped <c>SaveAsync</c> / <c>SaveEntriesAsync</c> /
/// <c>SaveEditorEntriesAsync</c> members before concurrency checking existed. Concurrency checking
/// is delivered as a separate, stamped overload of each, with a default implementation that forwards
/// to the original and ignores the stamp. These tests pin both halves of that bargain: an old
/// implementer still compiles and still works (it is written here as a fake that implements
/// <em>only</em> the original member), and it gets no concurrency check, which is exactly what the
/// <see cref="ObsoleteAttribute"/> on the original member says.
/// </para>
/// <para>
/// The content and library-element families share one node-keyed contract
/// (<see cref="INodePermissionRepository"/> and <see cref="INodePermissionService"/>); their own
/// interfaces are empty markers over it. The obsolete members therefore live on the shared contract,
/// and the fakes here cover the shared contract itself and both markers to prove the members reach
/// every family that inherits them. The document-type family has its own contract and is covered
/// separately.
/// </para>
/// </remarks>
public sealed class ObsoleteSaveOverloadCompatibilityTests
{
    /// <summary>The entry tuple shape the doc-type interfaces accept.</summary>
    private static readonly (string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride) SampleDocTypeEntry =
        ("Umb.Document.CreateOfType", PermissionState.Allow, PermissionScope.ThisNodeOnly, false);

    /// <summary>The entry tuple shape the node-keyed interfaces accept.</summary>
    private static readonly (string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride) SampleEntry =
        ("Umb.Document.Read", PermissionState.Allow, PermissionScope.ThisNodeOnly, false);

    /// <summary>
    /// Calling the stamped overload on an implementer that has only the original member must write
    /// through that member. A stale-looking stamp must not stop the write: the default
    /// implementation does not know how to check one, and pretending otherwise would be worse than
    /// saying so. Run for the shared contract and for both families' marker interfaces.
    /// </summary>
    /// <param name="implementer">The legacy repository fake to exercise.</param>
    [Theory]
    [InlineData(typeof(LegacyNodeRepository))]
    [InlineData(typeof(LegacyContentRepository))]
    [InlineData(typeof(LegacyElementRepository))]
    public async Task NodeRepository_StampedOverload_OnImplementerWithOnlyTheOriginalMember_WritesWithoutAnyCheck(Type implementer)
    {
        var fake = (LegacyNodeRepository)Activator.CreateInstance(implementer)!;
        INodePermissionRepository repository = fake;
        var nodeKey = Guid.NewGuid();

        await repository.SaveAsync(nodeKey, "editors", [SampleEntry], "a-stamp-that-matches-nothing");

        var write = Assert.Single(fake.Writes);
        Assert.Equal(nodeKey, write.NodeKey);
        Assert.Equal("editors", write.RoleAlias);
        Assert.Equal(new[] { SampleEntry }, write.Entries);
    }

    /// <summary>
    /// The same guarantee for the node-keyed service contract: the stamped overload reaches an old
    /// implementer's original member and the write happens.
    /// </summary>
    /// <param name="implementer">The legacy service fake to exercise.</param>
    [Theory]
    [InlineData(typeof(LegacyNodeService))]
    [InlineData(typeof(LegacyContentService))]
    [InlineData(typeof(LegacyElementService))]
    public async Task NodeService_StampedOverload_OnImplementerWithOnlyTheOriginalMember_WritesWithoutAnyCheck(Type implementer)
    {
        var fake = (LegacyNodeService)Activator.CreateInstance(implementer)!;
        INodePermissionService service = fake;
        var nodeKey = Guid.NewGuid();

        await service.SaveEntriesAsync(nodeKey, "editors", [SampleEntry], "a-stamp-that-matches-nothing");

        var write = Assert.Single(fake.Writes);
        Assert.Equal(nodeKey, write.NodeKey);
        Assert.Equal("editors", write.RoleAlias);
        Assert.Equal(new[] { SampleEntry }, write.Entries);
    }

    /// <summary>
    /// The doc-type repository contract has the same bargain: an implementer with only the original
    /// <c>SaveAsync</c> still writes when the stamped overload is called, with no check.
    /// </summary>
    [Fact]
    public async Task DocTypeRepository_StampedOverload_OnImplementerWithOnlyTheOriginalMember_WritesWithoutAnyCheck()
    {
        var fake = new LegacyDocTypeRepository();
        IDocTypePermissionRepository repository = fake;
        var nodeKey = Guid.NewGuid();
        var contentTypeKey = Guid.NewGuid();

        await repository.SaveAsync(nodeKey, "editors", contentTypeKey, [SampleDocTypeEntry], "a-stamp-that-matches-nothing");

        var write = Assert.Single(fake.Writes);
        Assert.Equal(nodeKey, write.NodeKey);
        Assert.Equal("editors", write.RoleAlias);
        Assert.Equal(contentTypeKey, write.ContentTypeKey);
        Assert.Equal(new[] { SampleDocTypeEntry }, write.Entries);
    }

    /// <summary>
    /// The doc-type service contract has the same bargain: the stamped <c>SaveEditorEntriesAsync</c>
    /// reaches an old implementer's original member and the write happens.
    /// </summary>
    [Fact]
    public async Task DocTypeService_StampedOverload_OnImplementerWithOnlyTheOriginalMember_WritesWithoutAnyCheck()
    {
        var fake = new LegacyDocTypeService();
        IDocTypePermissionService service = fake;
        var nodeKey = Guid.NewGuid();
        var contentTypeKey = Guid.NewGuid();

        await service.SaveEditorEntriesAsync(nodeKey, "editors", contentTypeKey, [SampleDocTypeEntry], "a-stamp-that-matches-nothing");

        var write = Assert.Single(fake.Writes);
        Assert.Equal(nodeKey, write.NodeKey);
        Assert.Equal("editors", write.RoleAlias);
        Assert.Equal(contentTypeKey, write.ContentTypeKey);
        Assert.Equal(new[] { SampleDocTypeEntry }, write.Entries);
    }

    /// <summary>
    /// The cancellation token must survive the trip through the default implementation, or an old
    /// implementer would silently lose the caller's ability to cancel.
    /// </summary>
    [Fact]
    public async Task NodeContracts_StampedOverload_ForwardCancellationTokenToTheOriginalMember()
    {
        var repository = new LegacyNodeRepository();
        var service = new LegacyNodeService();
        using var source = new CancellationTokenSource();

        await ((INodePermissionRepository)repository).SaveAsync(Guid.NewGuid(), "editors", [SampleEntry], null, source.Token);
        await ((INodePermissionService)service).SaveEntriesAsync(Guid.NewGuid(), "editors", [SampleEntry], null, source.Token);

        Assert.Equal(source.Token, Assert.Single(repository.Writes).CancellationToken);
        Assert.Equal(source.Token, Assert.Single(service.Writes).CancellationToken);
    }

    /// <summary>
    /// The cancellation token must survive the default implementation on the doc-type contracts too.
    /// </summary>
    [Fact]
    public async Task DocTypeContracts_StampedOverload_ForwardCancellationTokenToTheOriginalMember()
    {
        var repository = new LegacyDocTypeRepository();
        var service = new LegacyDocTypeService();
        using var source = new CancellationTokenSource();

        await ((IDocTypePermissionRepository)repository).SaveAsync(Guid.NewGuid(), "editors", Guid.NewGuid(), [SampleDocTypeEntry], null, source.Token);
        await ((IDocTypePermissionService)service).SaveEditorEntriesAsync(Guid.NewGuid(), "editors", Guid.NewGuid(), [SampleDocTypeEntry], null, source.Token);

        Assert.Equal(source.Token, Assert.Single(repository.Writes).CancellationToken);
        Assert.Equal(source.Token, Assert.Single(service.Writes).CancellationToken);
    }

    /// <summary>
    /// Every unstamped member must be marked obsolete, must not be an error (that would break the
    /// very implementers this exists to protect), and must say in words a new implementer cannot
    /// misread that it performs no concurrency check and which member to implement instead.
    /// </summary>
    /// <param name="interfaceType">The interface declaring the member.</param>
    /// <param name="methodName">The name of the overloaded save method.</param>
    [Theory]
    [InlineData(typeof(INodePermissionRepository), "SaveAsync")]
    [InlineData(typeof(INodePermissionService), "SaveEntriesAsync")]
    [InlineData(typeof(IDocTypePermissionRepository), "SaveAsync")]
    [InlineData(typeof(IDocTypePermissionService), "SaveEditorEntriesAsync")]
    public void UnstampedOverload_IsObsoleteWithAWarningThatCannotBeMisread(Type interfaceType, string methodName)
    {
        var original = FindOverload(interfaceType, methodName, stamped: false);

        var obsolete = original.GetCustomAttribute<ObsoleteAttribute>();

        Assert.NotNull(obsolete);
        Assert.False(obsolete.IsError, "An error-level [Obsolete] would break the implementers this protects.");
        Assert.Contains("NO concurrency check", obsolete.Message);
        Assert.Contains("silently overwrite", obsolete.Message);
        Assert.Contains("expectedStamp", obsolete.Message);
        Assert.Contains(methodName, obsolete.Message);
    }

    /// <summary>
    /// The stamped overload is the one to implement, so it must not itself be obsolete.
    /// </summary>
    /// <param name="interfaceType">The interface declaring the member.</param>
    /// <param name="methodName">The name of the overloaded save method.</param>
    [Theory]
    [InlineData(typeof(INodePermissionRepository), "SaveAsync")]
    [InlineData(typeof(INodePermissionService), "SaveEntriesAsync")]
    [InlineData(typeof(IDocTypePermissionRepository), "SaveAsync")]
    [InlineData(typeof(IDocTypePermissionService), "SaveEditorEntriesAsync")]
    public void StampedOverload_IsNotObsolete(Type interfaceType, string methodName)
    {
        var stamped = FindOverload(interfaceType, methodName, stamped: true);

        Assert.Null(stamped.GetCustomAttribute<ObsoleteAttribute>());
    }

    /// <summary>
    /// The stamped overload's <c>expectedStamp</c> parameter must be required, not optional. An
    /// optional one would make a call that omits it ambiguous between the two overloads.
    /// </summary>
    /// <param name="interfaceType">The interface declaring the member.</param>
    /// <param name="methodName">The name of the overloaded save method.</param>
    [Theory]
    [InlineData(typeof(INodePermissionRepository), "SaveAsync")]
    [InlineData(typeof(INodePermissionService), "SaveEntriesAsync")]
    [InlineData(typeof(IDocTypePermissionRepository), "SaveAsync")]
    [InlineData(typeof(IDocTypePermissionService), "SaveEditorEntriesAsync")]
    public void StampedOverload_ExpectedStampParameter_IsRequired(Type interfaceType, string methodName)
    {
        var stamped = FindOverload(interfaceType, methodName, stamped: true);

        var parameter = stamped.GetParameters().Single(p => p.Name == "expectedStamp");

        Assert.False(parameter.IsOptional, "An optional expectedStamp would make calls ambiguous.");
    }

    /// <summary>
    /// The per-family marker interfaces must stay empty over the shared node-keyed contract. If one
    /// declared its own save member, an implementer of that family could supply only that and bypass
    /// the obsolete warning and the default implementation this suite pins on the shared contract.
    /// </summary>
    /// <param name="marker">The family's marker interface.</param>
    /// <param name="sharedContract">The shared contract it must inherit from.</param>
    [Theory]
    [InlineData(typeof(IAdvancedPermissionRepository), typeof(INodePermissionRepository))]
    [InlineData(typeof(IElementPermissionRepository), typeof(INodePermissionRepository))]
    [InlineData(typeof(IAdvancedPermissionService), typeof(INodePermissionService))]
    [InlineData(typeof(IElementNodePermissionService), typeof(INodePermissionService))]
    public void FamilyMarkerInterface_DeclaresNoMembersOfItsOwn_AndInheritsTheSharedContract(Type marker, Type sharedContract)
    {
        Assert.Contains(sharedContract, marker.GetInterfaces());
        Assert.Empty(marker.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
    }

    /// <summary>
    /// A control for the "our implementations do not rely on the default" tests elsewhere: an
    /// implementer that supplies only the original member is mapped to the interface's own default
    /// method for the stamped overload, so a test asserting the opposite for a real implementation
    /// can genuinely fail.
    /// </summary>
    /// <param name="implementer">The legacy implementer type.</param>
    /// <param name="contract">The interface it implements.</param>
    /// <param name="methodName">The overloaded save method's name.</param>
    [Theory]
    [InlineData(typeof(LegacyNodeRepository), typeof(INodePermissionRepository), "SaveAsync")]
    [InlineData(typeof(LegacyContentRepository), typeof(INodePermissionRepository), "SaveAsync")]
    [InlineData(typeof(LegacyElementRepository), typeof(INodePermissionRepository), "SaveAsync")]
    [InlineData(typeof(LegacyNodeService), typeof(INodePermissionService), "SaveEntriesAsync")]
    [InlineData(typeof(LegacyContentService), typeof(INodePermissionService), "SaveEntriesAsync")]
    [InlineData(typeof(LegacyElementService), typeof(INodePermissionService), "SaveEntriesAsync")]
    [InlineData(typeof(LegacyDocTypeRepository), typeof(IDocTypePermissionRepository), "SaveAsync")]
    [InlineData(typeof(LegacyDocTypeService), typeof(IDocTypePermissionService), "SaveEditorEntriesAsync")]
    public void LegacyImplementer_StampedOverload_MapsToTheInterfaceDefault(Type implementer, Type contract, string methodName)
    {
        var map = implementer.GetInterfaceMap(contract);
        var index = Array.FindIndex(map.InterfaceMethods, m => IsMatch(m, methodName, stamped: true));

        Assert.True(index >= 0);
        Assert.Equal(contract, map.TargetMethods[index].DeclaringType);
    }

    /// <summary>Finds the stamped or unstamped overload of a save method on an interface.</summary>
    /// <param name="interfaceType">The interface to search.</param>
    /// <param name="methodName">The method name.</param>
    /// <param name="stamped">Whether to find the overload taking an expected stamp.</param>
    /// <returns>The single matching method.</returns>
    private static MethodInfo FindOverload(Type interfaceType, string methodName, bool stamped) =>
        interfaceType.GetMethods().Single(m => IsMatch(m, methodName, stamped));

    /// <summary>Whether a method is the stamped or unstamped overload of the named save method.</summary>
    /// <param name="method">The method to test.</param>
    /// <param name="methodName">The method name.</param>
    /// <param name="stamped">Whether the stamped overload is wanted.</param>
    /// <returns><see langword="true"/> if the method is the wanted overload.</returns>
    private static bool IsMatch(MethodInfo method, string methodName, bool stamped) =>
        method.Name == methodName
        && method.GetParameters().Any(p => p.Name == "expectedStamp") == stamped;

    /// <summary>One write captured by a fake.</summary>
    /// <param name="NodeKey">The node key written.</param>
    /// <param name="RoleAlias">The user group alias written.</param>
    /// <param name="Entries">The entries written.</param>
    /// <param name="CancellationToken">The token the write was called with.</param>
    private sealed record CapturedWrite(
        Guid NodeKey,
        string RoleAlias,
        IReadOnlyList<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries,
        CancellationToken CancellationToken);

    /// <summary>One doc-type write captured by a fake.</summary>
    /// <param name="NodeKey">The node key written.</param>
    /// <param name="RoleAlias">The user group alias written.</param>
    /// <param name="ContentTypeKey">The document type key written.</param>
    /// <param name="Entries">The entries written.</param>
    /// <param name="CancellationToken">The token the write was called with.</param>
    private sealed record CapturedDocTypeWrite(
        Guid NodeKey,
        string RoleAlias,
        Guid ContentTypeKey,
        IReadOnlyList<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries,
        CancellationToken CancellationToken);

    /// <summary>
    /// A minimal node-keyed repository written the way an implementer would have written it before
    /// the stamped overload existed: only the original <c>SaveAsync</c>, no stamp anywhere. Every
    /// other member is unused by these tests.
    /// </summary>
    private record LegacyNodeRepository : INodePermissionRepository
    {
        /// <summary>Gets the writes received through the original member.</summary>
        public List<CapturedWrite> Writes { get; } = [];

        /// <inheritdoc />
        public Task SaveAsync(
            Guid nodeKey,
            string roleAlias,
            IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> entries,
            CancellationToken cancellationToken = default)
        {
            Writes.Add(new CapturedWrite(nodeKey, roleAlias, entries.ToList(), cancellationToken));
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<AdvancedPermissionEntry>> GetByNodeAndRoleAsync(Guid nodeKey, string roleAlias, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task<IReadOnlyList<AdvancedPermissionEntry>> GetByNodeAsync(Guid nodeKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task<IReadOnlyList<AdvancedPermissionEntry>> GetByRoleAsync(string roleAlias, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task<IReadOnlyList<AdvancedPermissionEntry>> GetByNodesAsync(IEnumerable<Guid> nodeKeys, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task<IReadOnlyList<AdvancedPermissionEntry>> GetByRolesAndNodesAsync(IEnumerable<string> roleAliases, IEnumerable<Guid> nodeKeys, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task<IReadOnlyList<AdvancedPermissionEntry>> GetByNodesAndRoleAsync(IEnumerable<Guid> nodeKeys, string roleAlias, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task SaveManyAsync(
            IEnumerable<(Guid NodeKey, string RoleAlias, IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries, string? ExpectedStamp)> batch,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task DeleteAsync(Guid nodeKey, string roleAlias, string verb, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task DeleteAllForNodeAsync(Guid nodeKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task DeleteAllForRoleAsync(string roleAlias, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    /// <summary>
    /// A legacy repository declared against the content family's marker interface, proving the
    /// obsolete member and its default implementation reach that family through the shared contract.
    /// </summary>
    private sealed record LegacyContentRepository : LegacyNodeRepository, IAdvancedPermissionRepository;

    /// <summary>
    /// A legacy repository declared against the library-element family's marker interface, proving
    /// the obsolete member and its default implementation reach that family through the shared
    /// contract.
    /// </summary>
    private sealed record LegacyElementRepository : LegacyNodeRepository, IElementPermissionRepository;

    /// <summary>
    /// A minimal node-keyed service written the way an implementer would have written it before the
    /// stamped overload existed: only the original <c>SaveEntriesAsync</c>, no stamp anywhere. Every
    /// other member is unused by these tests.
    /// </summary>
    private record LegacyNodeService : INodePermissionService
    {
        /// <summary>Gets the writes received through the original member.</summary>
        public List<CapturedWrite> Writes { get; } = [];

        /// <inheritdoc />
        public Task SaveEntriesAsync(
            Guid nodeKey,
            string roleAlias,
            IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> entries,
            CancellationToken cancellationToken = default)
        {
            Writes.Add(new CapturedWrite(nodeKey, roleAlias, entries.ToList(), cancellationToken));
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<EffectivePermission> ResolveAsync(Guid userKey, Guid nodeKey, IReadOnlyList<Guid> pathFromRoot, string verb, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task<IReadOnlyDictionary<string, EffectivePermission>> ResolveAllAsync(Guid userKey, Guid nodeKey, IReadOnlyList<Guid> pathFromRoot, IEnumerable<string>? verbs = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task<IReadOnlyList<AdvancedPermissionEntry>> GetEntriesAsync(Guid nodeKey, string roleAlias, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task<IReadOnlyList<AdvancedPermissionEntry>> GetEntriesByNodesAndRoleAsync(IEnumerable<Guid> nodeKeys, string roleAlias, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task<IReadOnlyList<AdvancedPermissionEntry>> GetEntriesByNodeAsync(Guid nodeKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task<IReadOnlyDictionary<string, EffectivePermission>> ResolveForRoleAsync(string roleAlias, Guid nodeKey, IReadOnlyList<Guid> pathFromRoot, IEnumerable<string>? verbs = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task DeleteEntryAsync(Guid nodeKey, string roleAlias, string verb, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task SaveManyAsync(
            IReadOnlyList<(Guid NodeKey, string RoleAlias, IReadOnlyList<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries, string? ExpectedStamp)> batch,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    /// <summary>
    /// A legacy service declared against the content family's marker interface, proving the obsolete
    /// member and its default implementation reach that family through the shared contract.
    /// </summary>
    private sealed record LegacyContentService : LegacyNodeService, IAdvancedPermissionService;

    /// <summary>
    /// A legacy service declared against the library-element family's marker interface, proving the
    /// obsolete member and its default implementation reach that family through the shared contract.
    /// </summary>
    private sealed record LegacyElementService : LegacyNodeService, IElementNodePermissionService;

    /// <summary>
    /// A minimal doc-type repository written the way an implementer would have written it before the
    /// stamped overload existed: only the original <c>SaveAsync</c>, no stamp anywhere. Every other
    /// member is unused by these tests.
    /// </summary>
    private sealed record LegacyDocTypeRepository : IDocTypePermissionRepository
    {
        /// <summary>Gets the writes received through the original member.</summary>
        public List<CapturedDocTypeWrite> Writes { get; } = [];

        /// <inheritdoc />
        public Task SaveAsync(
            Guid nodeKey,
            string roleAlias,
            Guid contentTypeKey,
            IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> entries,
            CancellationToken cancellationToken = default)
        {
            Writes.Add(new CapturedDocTypeWrite(nodeKey, roleAlias, contentTypeKey, entries.ToList(), cancellationToken));
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<DocTypePermissionEntry>> GetByRoleAsync(string roleAlias, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task<IReadOnlyList<DocTypePermissionEntry>> GetByRoleAndContentTypeAsync(string roleAlias, Guid contentTypeKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task SaveManyAsync(
            IEnumerable<(Guid NodeKey, string RoleAlias, Guid ContentTypeKey, IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries, string? ExpectedStamp)> batch,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task DeleteAllForNodeAsync(Guid nodeKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task DeleteAllForContentTypeAsync(Guid contentTypeKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task DeleteAllForRoleAsync(string roleAlias, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task<IReadOnlyList<DocTypePermissionEntry>> GetByContentTypeAndNodesAsync(Guid contentTypeKey, IEnumerable<Guid> nodeKeys, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    /// <summary>
    /// A minimal doc-type service written the way an implementer would have written it before the
    /// stamped overload existed: only the original <c>SaveEditorEntriesAsync</c>, no stamp anywhere.
    /// Every other member is unused by these tests.
    /// </summary>
    private sealed record LegacyDocTypeService : IDocTypePermissionService
    {
        /// <summary>Gets the writes received through the original member.</summary>
        public List<CapturedDocTypeWrite> Writes { get; } = [];

        /// <inheritdoc />
        public Task SaveEditorEntriesAsync(
            Guid nodeKey,
            string roleAlias,
            Guid contentTypeKey,
            IEnumerable<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> entries,
            CancellationToken cancellationToken = default)
        {
            Writes.Add(new CapturedDocTypeWrite(nodeKey, roleAlias, contentTypeKey, entries.ToList(), cancellationToken));
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<EffectivePermission> ResolveCreateAsync(Guid userKey, Guid parentNodeKey, IReadOnlyList<Guid> parentPathFromRoot, Guid contentTypeKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task<EffectivePermission> ResolveCreateForRolesAsync(IReadOnlyList<string> roleAliases, IReadOnlyList<Guid> parentPathFromRoot, Guid contentTypeKey, string verb = AdvancedPermissionsConstants.VerbCreateOfType, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task<IReadOnlyList<DocTypePermissionEntry>> GetEditorEntriesAsync(string roleAlias, Guid contentTypeKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        /// <inheritdoc />
        public Task SaveManyAsync(
            IReadOnlyList<(Guid NodeKey, string RoleAlias, Guid ContentTypeKey, IReadOnlyList<(string Verb, PermissionState State, PermissionScope Scope, bool IsPriorityOverride)> Entries, string? ExpectedStamp)> batch,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
