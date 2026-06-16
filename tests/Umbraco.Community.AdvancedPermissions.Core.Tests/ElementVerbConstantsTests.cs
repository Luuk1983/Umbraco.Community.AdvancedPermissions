using Umbraco.Community.AdvancedPermissions.Core.Constants;

namespace Umbraco.Community.AdvancedPermissions.Core.Tests;

/// <summary>
/// Tests the element verb constants and the container-to-canonical verb mapping that lets a single
/// stored entry govern both an element folder and the elements beneath it.
/// </summary>
public sealed class ElementVerbConstantsTests
{
    /// <summary>
    /// The canonical element verb set is exactly the nine <c>Umb.Element.*</c> verbs.
    /// </summary>
    [Fact]
    public void ElementVerbs_ContainsTheNineCanonicalVerbs()
    {
        string[] expected =
        [
            "Umb.Element.Read",
            "Umb.Element.Create",
            "Umb.Element.Update",
            "Umb.Element.Delete",
            "Umb.Element.Publish",
            "Umb.Element.Unpublish",
            "Umb.Element.Duplicate",
            "Umb.Element.Move",
            "Umb.Element.Rollback",
        ];

        Assert.Equal(expected.OrderBy(v => v), AdvancedPermissionsConstants.ElementVerbs.OrderBy(v => v));
    }

    /// <summary>
    /// Every element-container (folder) verb maps to the correspondingly-named canonical element verb.
    /// </summary>
    [Theory]
    [InlineData("Umb.ElementContainer.Read", "Umb.Element.Read")]
    [InlineData("Umb.ElementContainer.Create", "Umb.Element.Create")]
    [InlineData("Umb.ElementContainer.Update", "Umb.Element.Update")]
    [InlineData("Umb.ElementContainer.Delete", "Umb.Element.Delete")]
    [InlineData("Umb.ElementContainer.Move", "Umb.Element.Move")]
    public void ElementContainerVerbToCanonical_MapsEachFolderVerbToItsCanonicalElementVerb(
        string containerVerb,
        string expectedCanonical)
    {
        Assert.True(AdvancedPermissionsConstants.ElementContainerVerbToCanonical.TryGetValue(containerVerb, out var mapped));
        Assert.Equal(expectedCanonical, mapped);
    }

    /// <summary>
    /// Every canonical verb a folder verb maps to must be a recognised element verb — guaranteeing the
    /// folder adapter never resolves against an unknown verb.
    /// </summary>
    [Fact]
    public void ElementContainerVerbToCanonical_AllTargetsAreKnownElementVerbs()
    {
        foreach (var canonical in AdvancedPermissionsConstants.ElementContainerVerbToCanonical.Values)
        {
            Assert.Contains(canonical, AdvancedPermissionsConstants.ElementVerbs);
        }
    }

    /// <summary>
    /// The four element-only verbs (Publish/Unpublish/Duplicate/Rollback) have no folder counterpart —
    /// they must not appear as mapping targets.
    /// </summary>
    [Theory]
    [InlineData("Umb.Element.Publish")]
    [InlineData("Umb.Element.Unpublish")]
    [InlineData("Umb.Element.Duplicate")]
    [InlineData("Umb.Element.Rollback")]
    public void ElementContainerVerbToCanonical_DoesNotMapToElementOnlyVerbs(string elementOnlyVerb)
    {
        Assert.DoesNotContain(elementOnlyVerb, AdvancedPermissionsConstants.ElementContainerVerbToCanonical.Values);
    }

    /// <summary>
    /// The Everyone default for elements grants read, mirroring the content default so the library is
    /// browsable out of the box.
    /// </summary>
    [Fact]
    public void EveryoneDefaultElementVerbs_GrantsRead()
    {
        Assert.Contains("Umb.Element.Read", AdvancedPermissionsConstants.EveryoneDefaultElementVerbs);
    }
}
