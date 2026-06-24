using System.Text.Json;
using NSubstitute;
using Umbraco.AI.Core.Tools;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Persistence.Querying;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.AI.Services;
using Umbraco.Community.AdvancedPermissions.AI.Tools;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Tests;

/// <summary>
/// Unit tests for <see cref="AuditPermissionsTool"/>. The tool is invoked through the public
/// <see cref="IAITool.ExecuteAsync(object?, System.Threading.CancellationToken)"/> entry point so the
/// real <see cref="AIToolBase{TArgs}"/> base-class plumbing (argument cast + dispatch) is exercised.
/// A real <see cref="PermissionPresenter"/> wraps mocked Umbraco services so the friendly projection is
/// exercised and the no-raw-identifiers guarantee can be asserted.
/// </summary>
public sealed class AuditPermissionsToolTests
{
    /// <summary>The mocked repository the tool loads role-wide entries from.</summary>
    private readonly IAdvancedPermissionRepository _repository = Substitute.For<IAdvancedPermissionRepository>();

    /// <summary>The mocked analyzer the tool delegates the audit to.</summary>
    private readonly IPermissionAuditAnalyzer _analyzer = Substitute.For<IPermissionAuditAnalyzer>();

    /// <summary>The mocked user group service backing the real presenter.</summary>
    private readonly IUserGroupService _userGroupService = Substitute.For<IUserGroupService>();

    /// <summary>The mocked entity service backing the real presenter.</summary>
    private readonly IEntityService _entityService = Substitute.For<IEntityService>();

    /// <summary>The mocked content-type service backing the real presenter.</summary>
    private readonly IContentTypeService _contentTypeService = Substitute.For<IContentTypeService>();

    /// <summary>
    /// Initializes the test fixture with a benign default user-group page so the real presenter's
    /// role-name lookup does not dereference a null result. Individual tests override it where needed.
    /// </summary>
    public AuditPermissionsToolTests()
    {
        _userGroupService
            .GetAllAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns(new PagedModel<IUserGroup>(0, Array.Empty<IUserGroup>()));
    }

    /// <summary>
    /// The tool loads all entries for the role, hands them to the analyzer, then projects the report
    /// to a friendly report: role becomes display name, verb becomes friendly action, node becomes name,
    /// severity becomes text, and no raw identifiers survive into the serialized output.
    /// </summary>
    [Fact]
    public async Task Audit_ProjectsReportToFriendlyReport_NoRawIdentifiers()
    {
        const string roleAlias = "editors";
        var nodeKey = Guid.NewGuid();

        var editors = Substitute.For<IUserGroup>();
        editors.Alias.Returns("editors");
        editors.Name.Returns("Editors");
        _userGroupService
            .GetAllAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns(new PagedModel<IUserGroup>(1, new[] { editors }));

        var nodeEntity = Substitute.For<IEntitySlim>();
        nodeEntity.Name.Returns("News");
        _entityService.Get(nodeKey, UmbracoObjectTypes.Document).Returns(nodeEntity);

        var entries = new List<AdvancedPermissionEntry>
        {
            new(Guid.NewGuid(), nodeKey, roleAlias, "Umb.Document.Read", PermissionState.Allow, PermissionScope.ThisNodeAndDescendants),
            new(Guid.NewGuid(), nodeKey, roleAlias, "Umb.Document.Delete", PermissionState.Deny, PermissionScope.ThisNodeOnly),
        };

        var rawReport = new AuditReport(
            new[]
            {
                new AuditFinding(
                    "allow-deny-conflict",
                    AuditSeverity.Warning,
                    "Role 'editors' has both Allow and Deny for 'Umb.Document.Delete' on the same node.",
                    nodeKey,
                    roleAlias,
                    "Umb.Document.Delete"),
                new AuditFinding(
                    "everyone-broad-write",
                    AuditSeverity.Risk,
                    "Everyone is allowed 'Umb.Document.Update' across the whole tree from the root.",
                    AdvancedPermissionsConstants.VirtualRootNodeKey,
                    AdvancedPermissionsConstants.EveryoneRoleAlias,
                    "Umb.Document.Update"),
            },
            EntriesAnalyzed: 2);

        _repository.GetByRoleAsync(roleAlias, Arg.Any<CancellationToken>()).Returns(entries);
        _analyzer.Analyze(entries).Returns(rawReport);

        var presenter = new PermissionPresenter(_userGroupService, _entityService, _contentTypeService);
        var tool = new AuditPermissionsTool(_repository, _analyzer, presenter, _entityService);
        var result = await ((IAITool)tool).ExecuteAsync(
            new AuditPermissionsArgs(AuditScope.Role, roleAlias), CancellationToken.None);

        var report = Assert.IsType<FriendlyAuditReport>(result);
        Assert.Equal(2, report.EntriesAnalyzed);
        Assert.Equal(2, report.Findings.Count);

        var conflict = report.Findings.Single(f => f.RuleId == "allow-deny-conflict");
        Assert.Equal("Warning", conflict.Severity);
        Assert.Equal("Editors", conflict.Role);
        Assert.Equal("Delete", conflict.Action);
        Assert.Equal("News", conflict.Node);

        var broad = report.Findings.Single(f => f.RuleId == "everyone-broad-write");
        Assert.Equal("Risk", broad.Severity);
        Assert.Equal("All Users", broad.Role);
        Assert.Equal("Update", broad.Action);
        Assert.Equal("All content (root-level default)", broad.Node);

        // No raw identifiers anywhere in the serialized friendly report.
        var json = JsonSerializer.Serialize(report);
        Assert.DoesNotContain("$everyone", json);
        Assert.DoesNotContain("Umb.Document.", json);
        Assert.DoesNotContain("ThisNodeOnly", json);
        Assert.DoesNotContain("ThisNodeAndDescendants", json);
        Assert.DoesNotContain("DescendantsOnly", json);
        Assert.DoesNotContain(nodeKey.ToString(), json);

        await _repository.Received(1).GetByRoleAsync(roleAlias, Arg.Any<CancellationToken>());
        _analyzer.Received(1).Analyze(entries);
    }

    /// <summary>
    /// Subtree scope gathers the node plus its descendant document keys via the entity service, loads
    /// their stored entries with the bulk <see cref="IAdvancedPermissionRepository.GetByNodesAsync"/> query,
    /// and projects the analyzer's report through the presenter.
    /// </summary>
    [Fact]
    public async Task Audit_Subtree_GathersDescendantsAndAuditsTheirEntries()
    {
        var rootKey = Guid.NewGuid();
        const int rootId = 1234;
        var childKey = Guid.NewGuid();

        _entityService.GetId(rootKey, UmbracoObjectTypes.Document).Returns(Attempt.Succeed(rootId));

        var child = StubEntity(childKey);
        _entityService
            .GetPagedDescendants(
                rootId,
                UmbracoObjectTypes.Document,
                Arg.Any<long>(),
                Arg.Any<int>(),
                out Arg.Any<long>(),
                Arg.Any<IQuery<IUmbracoEntity>?>(),
                Arg.Any<Ordering?>())
            .Returns(callInfo =>
            {
                callInfo[4] = 1L; // totalRecords
                return new[] { child };
            });

        var entries = new List<AdvancedPermissionEntry>
        {
            new(Guid.NewGuid(), childKey, "editors", "Umb.Document.Update", PermissionState.Allow, PermissionScope.ThisNodeOnly),
        };
        var captured = new List<Guid>();
        _repository
            .GetByNodesAsync(Arg.Do<IEnumerable<Guid>>(keys => captured.AddRange(keys)), Arg.Any<CancellationToken>())
            .Returns(entries);

        var rawReport = new AuditReport(
            new[]
            {
                new AuditFinding(
                    "allow-deny-conflict", AuditSeverity.Warning,
                    "Role 'editors' has both Allow and Deny for 'Umb.Document.Update' on the same node.",
                    childKey, "editors", "Umb.Document.Update"),
            },
            EntriesAnalyzed: 1);
        _analyzer.Analyze(entries).Returns(rawReport);

        var presenter = new PermissionPresenter(_userGroupService, _entityService, _contentTypeService);
        var tool = new AuditPermissionsTool(_repository, _analyzer, presenter, _entityService);

        var result = await ((IAITool)tool).ExecuteAsync(
            new AuditPermissionsArgs(AuditScope.Subtree, NodeKey: rootKey), CancellationToken.None);

        var report = Assert.IsType<FriendlyAuditReport>(result);
        Assert.Single(report.Findings);
        Assert.Equal(1, report.EntriesAnalyzed);

        // The node itself plus its descendant must both be passed to the bulk query.
        Assert.Contains(rootKey, captured);
        Assert.Contains(childKey, captured);
        _analyzer.Received(1).Analyze(entries);
        await _repository.DidNotReceive().GetByRoleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// All scope loads the whole stored configuration via the best-available bulk query (descendants of
    /// root plus the virtual-root sentinel) and audits it.
    /// </summary>
    [Fact]
    public async Task Audit_All_AuditsWholeConfiguration()
    {
        var nodeA = Guid.NewGuid();
        var nodeB = Guid.NewGuid();

        _entityService
            .GetPagedDescendants(
                UmbracoObjectTypes.Document,
                Arg.Any<long>(),
                Arg.Any<int>(),
                out Arg.Any<long>(),
                Arg.Any<IQuery<IUmbracoEntity>?>(),
                Arg.Any<Ordering?>(),
                Arg.Any<bool>())
            .Returns(callInfo =>
            {
                callInfo[3] = 2L; // totalRecords
                return new[] { StubEntity(nodeA), StubEntity(nodeB) };
            });

        var entries = new List<AdvancedPermissionEntry>
        {
            new(Guid.NewGuid(), nodeA, "editors", "Umb.Document.Delete", PermissionState.Deny, PermissionScope.ThisNodeOnly),
        };
        var captured = new List<Guid>();
        _repository
            .GetByNodesAsync(Arg.Do<IEnumerable<Guid>>(keys => captured.AddRange(keys)), Arg.Any<CancellationToken>())
            .Returns(entries);

        var rawReport = new AuditReport(
            new[]
            {
                new AuditFinding(
                    "manage-permissions-descendants", AuditSeverity.Risk,
                    "Role 'editors' can manage permissions on this node and all descendants.",
                    nodeA, "editors", "Umb.Document.Permissions"),
            },
            EntriesAnalyzed: 1);
        _analyzer.Analyze(entries).Returns(rawReport);

        var presenter = new PermissionPresenter(_userGroupService, _entityService, _contentTypeService);
        var tool = new AuditPermissionsTool(_repository, _analyzer, presenter, _entityService);

        var result = await ((IAITool)tool).ExecuteAsync(
            new AuditPermissionsArgs(AuditScope.All), CancellationToken.None);

        var report = Assert.IsType<FriendlyAuditReport>(result);
        Assert.Single(report.Findings);

        // Both real nodes plus the virtual-root sentinel must be queried.
        Assert.Contains(nodeA, captured);
        Assert.Contains(nodeB, captured);
        Assert.Contains(AdvancedPermissionsConstants.VirtualRootNodeKey, captured);
        _analyzer.Received(1).Analyze(entries);
    }

    /// <summary>
    /// When <c>SeverityMin</c> is supplied, only findings at or above that severity survive into the
    /// friendly report; lower-severity findings are filtered out before projection.
    /// </summary>
    [Fact]
    public async Task Audit_SeverityMin_FiltersOutLowerSeverityFindings()
    {
        const string roleAlias = "editors";
        var nodeKey = Guid.NewGuid();

        var entries = new List<AdvancedPermissionEntry>
        {
            new(Guid.NewGuid(), nodeKey, roleAlias, "Umb.Document.Read", PermissionState.Allow, PermissionScope.ThisNodeOnly),
        };
        _repository.GetByRoleAsync(roleAlias, Arg.Any<CancellationToken>()).Returns(entries);

        var rawReport = new AuditReport(
            new[]
            {
                new AuditFinding("a-risk", AuditSeverity.Risk, "A risky thing.", nodeKey, roleAlias, "Umb.Document.Update"),
                new AuditFinding("a-warning", AuditSeverity.Warning, "A questionable thing.", nodeKey, roleAlias, "Umb.Document.Delete"),
                new AuditFinding("an-info", AuditSeverity.Info, "Just so you know.", nodeKey, roleAlias, "Umb.Document.Read"),
            },
            EntriesAnalyzed: 1);
        _analyzer.Analyze(entries).Returns(rawReport);

        var presenter = new PermissionPresenter(_userGroupService, _entityService, _contentTypeService);
        var tool = new AuditPermissionsTool(_repository, _analyzer, presenter, _entityService);

        var result = await ((IAITool)tool).ExecuteAsync(
            new AuditPermissionsArgs(AuditScope.Role, roleAlias, SeverityMin: AuditSeverity.Risk),
            CancellationToken.None);

        var report = Assert.IsType<FriendlyAuditReport>(result);
        Assert.Single(report.Findings);
        Assert.Equal("a-risk", report.Findings[0].RuleId);
        Assert.Equal("Risk", report.Findings[0].Severity);
    }

    /// <summary>A missing role alias under role scope returns a friendly error instead of throwing.</summary>
    [Fact]
    public async Task Audit_Role_MissingRoleAlias_ReturnsError()
    {
        var presenter = new PermissionPresenter(_userGroupService, _entityService, _contentTypeService);
        var tool = new AuditPermissionsTool(_repository, _analyzer, presenter, _entityService);

        var result = await ((IAITool)tool).ExecuteAsync(
            new AuditPermissionsArgs(AuditScope.Role), CancellationToken.None);

        Assert.IsType<AccessError>(result);
        await _repository.DidNotReceive().GetByRoleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>A missing node key under subtree scope returns a friendly error instead of throwing.</summary>
    [Fact]
    public async Task Audit_Subtree_MissingNodeKey_ReturnsError()
    {
        var presenter = new PermissionPresenter(_userGroupService, _entityService, _contentTypeService);
        var tool = new AuditPermissionsTool(_repository, _analyzer, presenter, _entityService);

        var result = await ((IAITool)tool).ExecuteAsync(
            new AuditPermissionsArgs(AuditScope.Subtree), CancellationToken.None);

        Assert.IsType<AccessError>(result);
        await _repository.DidNotReceive().GetByNodesAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Creates a stubbed <see cref="IEntitySlim"/> exposing the given key.</summary>
    /// <param name="key">The Guid key the entity should report.</param>
    /// <returns>A substituted <see cref="IEntitySlim"/>.</returns>
    private static IEntitySlim StubEntity(Guid key)
    {
        var e = Substitute.For<IEntitySlim>();
        e.Key.Returns(key);
        return e;
    }
}
