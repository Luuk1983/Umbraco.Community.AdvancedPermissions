namespace UmbracoAdvancedSecurity.Controllers.Models;

/// <summary>
/// Represents a single permission verb available in the Advanced Security system.
/// </summary>
/// <param name="Verb">The full verb string, e.g. <c>Umb.Document.Read</c>.</param>
/// <param name="DisplayName">A human-readable name for the verb, e.g. <c>Read</c>.</param>
public sealed record VerbResponseModel(string Verb, string DisplayName);

/// <summary>
/// Represents a role (user group or the virtual <c>$everyone</c> role) available for permission assignment.
/// </summary>
/// <param name="Alias">The role alias used in permission entries.</param>
/// <param name="Name">The display name of the role.</param>
/// <param name="IsEveryone">Whether this is the virtual Everyone role.</param>
/// <param name="DefaultVerbs">
/// The default permission verbs for this role at the virtual root level.
/// For <c>$everyone</c> this comes from <see cref="UmbracoAdvancedSecurity.Core.Constants.AdvancedSecurityConstants.EveryoneDefaultVerbs"/>.
/// For user groups this comes from the Umbraco group's native permission settings.
/// </param>
public sealed record RoleResponseModel(string Alias, string Name, bool IsEveryone, IReadOnlyList<string> DefaultVerbs);
