using Umbraco.AI.Core;
using Umbraco.AI.Core.RuntimeContext;
using Umbraco.Community.AdvancedPermissions.AI.Context;

namespace Umbraco.Community.AdvancedPermissions.AI.Tests;

/// <summary>
/// Unit tests for <see cref="AdvancedPermissionsGroundingContributor"/>. They exercise the contributor
/// through the real <see cref="AIRuntimeContext"/> type (constructed from request items and primed via
/// <see cref="AIRuntimeContext.SetValue(string, object?)"/>), asserting the verified-safe defensive
/// pattern: gate on a document entity context, append-only into
/// <see cref="AIRuntimeContext.SystemMessageParts"/>, and never touch the
/// <see cref="AIRuntimeContext.Variables"/> or <see cref="AIRuntimeContext.Data"/> bags.
/// </summary>
public sealed class AdvancedPermissionsGroundingContributorTests
{
    /// <summary>The system under test.</summary>
    private readonly AdvancedPermissionsGroundingContributor _contributor = new();

    /// <summary>
    /// Builds an empty <see cref="AIRuntimeContext"/> with no request items, ready to be primed with
    /// <see cref="AIRuntimeContext.SetValue(string, object?)"/>.
    /// </summary>
    /// <returns>A fresh runtime context.</returns>
    private static AIRuntimeContext NewContext() => new([]);

    /// <summary>
    /// When the focused entity type is <c>document</c> (the value the built-in entity contributor stores
    /// before ours runs), the contributor appends exactly one grounding line that mentions the package.
    /// </summary>
    [Fact]
    public void Contribute_DocumentContext_AppendsGroundingLine()
    {
        var context = NewContext();
        context.SetValue(Constants.ContextKeys.EntityType, "document");

        _contributor.Contribute(context);

        Assert.Single(context.SystemMessageParts);
        Assert.Contains("Advanced Permissions", context.SystemMessageParts[0]);
        Assert.Contains("uap_explain_access", context.SystemMessageParts[0]);
        Assert.Contains("uap_audit_permissions", context.SystemMessageParts[0]);
    }

    /// <summary>
    /// When the focused entity type is not a document (e.g. <c>media</c>), the contributor must not
    /// contribute anything — the gate keeps the line off non-document conversations.
    /// </summary>
    [Fact]
    public void Contribute_NonDocumentContext_AppendsNothing()
    {
        var context = NewContext();
        context.SetValue(Constants.ContextKeys.EntityType, "media");

        _contributor.Contribute(context);

        Assert.Empty(context.SystemMessageParts);
    }

    /// <summary>
    /// When no entity type has been set (e.g. a non-entity conversation, or the entity contributor did
    /// not run), the contributor must not contribute anything.
    /// </summary>
    [Fact]
    public void Contribute_NoEntityType_AppendsNothing()
    {
        var context = NewContext();

        _contributor.Contribute(context);

        Assert.Empty(context.SystemMessageParts);
    }

    /// <summary>
    /// The contributor is append-only: even on a document context it must leave the
    /// <see cref="AIRuntimeContext.Variables"/> and <see cref="AIRuntimeContext.Data"/> bags untouched
    /// (it must not call <see cref="AIRuntimeContext.SetValue(string, object?)"/> itself). The only
    /// <c>Data</c> entry present is the one the test primed.
    /// </summary>
    [Fact]
    public void Contribute_DocumentContext_DoesNotWriteVariablesOrData()
    {
        var context = NewContext();
        context.SetValue(Constants.ContextKeys.EntityType, "document");
        var dataCountBefore = context.Data.Count;

        _contributor.Contribute(context);

        Assert.Empty(context.Variables);
        Assert.Equal(dataCountBefore, context.Data.Count);
    }
}
