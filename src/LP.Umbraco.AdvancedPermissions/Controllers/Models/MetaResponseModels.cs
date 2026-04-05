namespace LP.Umbraco.AdvancedPermissions.Controllers.Models;

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
public sealed record RoleResponseModel(string Alias, string Name, bool IsEveryone);
