using Umbraco.Community.AdvancedPermissions.Controllers;
using Umbraco.Community.AdvancedPermissions.Controllers.Models;
using Umbraco.Community.AdvancedPermissions.Core.Concurrency;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.Tests;

/// <summary>
/// Tests that the stamp a read endpoint hands out is the stamp a save will be checked against.
/// </summary>
public sealed class PermissionStampEndpointTests
{
    /// <summary>
    /// A tree node's stamp must be computed from exactly the entries it reports, so that a client
    /// that saves back what it was given, unchanged, is never told it conflicts.
    /// </summary>
    [Fact]
    public void TreeNodeResponseModel_CarriesStampOfItsOwnEntries()
    {
        var nodeKey = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var entries = new List<PermissionEntryResponseModel>
        {
            new(Guid.NewGuid(), nodeKey, "editors", "Umb.Document.Read", "Allow", "ThisNodeOnly", false),
        };

        var stamp = entries.ComputeFromResponse();
        var model = new TreeNodeResponseModel(nodeKey, "Products", "icon-document", false, entries, stamp);

        Assert.Equal(stamp, model.Stamp);
        Assert.NotEqual(PermissionStamp.Compute([]), model.Stamp);
    }

    /// <summary>A node with no entries still carries the well-known empty stamp, never null.</summary>
    [Fact]
    public void TreeNodeResponseModel_NoEntries_CarriesEmptyStamp()
    {
        var nodeKey = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var stamp = Array.Empty<PermissionEntryResponseModel>().ComputeFromResponse();
        var model = new TreeNodeResponseModel(nodeKey, "Empty", null, false, [], stamp);

        Assert.Equal(PermissionStamp.Compute([]), model.Stamp);
    }

    /// <summary>
    /// The two entry points into the stamp must never disagree: <see cref="PermissionStamp.Compute(IEnumerable{AdvancedPermissionEntry})"/>
    /// operates on domain entries with enum State/Scope, while <see cref="PermissionStampExtensions.ComputeFromResponse(IEnumerable{PermissionEntryResponseModel})"/>
    /// operates on API response models whose State/Scope are strings. If the same logical entry
    /// set ever hashed differently through the two paths, the server would reject saves that are
    /// perfectly valid — a save that round-trips exactly what a read handed out — and nothing else
    /// in the suite would catch it.
    /// </summary>
    [Fact]
    public void ComputeFromNames_AgreesWithCompute_ForTheSameLogicalEntries()
    {
        var nodeKey = Guid.Parse("44444444-4444-4444-4444-444444444444");

        var domainEntries = new List<AdvancedPermissionEntry>
        {
            new(Guid.NewGuid(), nodeKey, "editors", "Umb.Document.Read", PermissionState.Allow, PermissionScope.ThisNodeOnly, false),
            new(Guid.NewGuid(), nodeKey, "editors", "Umb.Document.Publish", PermissionState.Deny, PermissionScope.ThisNodeAndDescendants, true),
        };

        var responseEntries = new List<PermissionEntryResponseModel>
        {
            new(Guid.NewGuid(), nodeKey, "editors", "Umb.Document.Read", "Allow", "ThisNodeOnly", false),
            new(Guid.NewGuid(), nodeKey, "editors", "Umb.Document.Publish", "Deny", "ThisNodeAndDescendants", true),
        };

        Assert.Equal(PermissionStamp.Compute(domainEntries), responseEntries.ComputeFromResponse());
    }
}
