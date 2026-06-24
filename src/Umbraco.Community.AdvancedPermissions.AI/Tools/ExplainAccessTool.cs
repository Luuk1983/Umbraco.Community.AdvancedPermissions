using Umbraco.AI.Core.Tools;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.AI.Services;
using Umbraco.Community.AdvancedPermissions.Core.Constants;
using Umbraco.Community.AdvancedPermissions.Core.Interfaces;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Tools;

/// <summary>
/// The single, parameterized "explain effective access" tool. It answers "what is the effective
/// permission decision, and why" for a content node, for one of four subjects selected by
/// <see cref="ExplainAccessArgs.Subject"/>: the current backoffice user, a specific user, a single role,
/// or all assignable roles. The three former tools (<c>uap_explain_user_access</c>,
/// <c>uap_explain_role_access</c>, <c>uap_who_can</c>) collapse into this one because they are the same
/// operation differing only by subject. Every result is projected through <see cref="IPermissionPresenter"/>
/// so the model only ever sees friendly labels — never role aliases, verb identifiers, scope/state enum
/// names, or node GUIDs.
/// </summary>
/// <param name="permissions">The permission service used to resolve effective node permissions.</param>
/// <param name="pathResolver">The resolver that builds the root-to-node ancestor key path.</param>
/// <param name="presenter">The presenter that maps raw permission data to friendly labels.</param>
/// <param name="userGroupService">The Umbraco user group service used to enumerate assignable roles for the all-roles subject.</param>
/// <param name="backOfficeSecurityAccessor">Accessor used to resolve the current backoffice user for the current-user subject.</param>
/// <param name="docTypePermissions">The doc-type permission service used to resolve "Insert Options" (type-create) decisions.</param>
/// <param name="contentTypeService">The Umbraco content-type service used to enumerate candidate document types and resolve allowed children.</param>
/// <param name="entityService">The Umbraco entity service used to resolve the parent node's content type for the structural allowed-children check.</param>
[AITool("uap_explain_access", "Explain access", ScopeId = "advanced-permissions:read")]
public sealed class ExplainAccessTool(
    IAdvancedPermissionService permissions,
    IContentPathResolver pathResolver,
    IPermissionPresenter presenter,
    IUserGroupService userGroupService,
    IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
    IDocTypePermissionService docTypePermissions,
    IContentTypeService contentTypeService,
    IEntityService entityService)
    : AIToolBase<ExplainAccessArgs>
{
    /// <summary>The page size used when enumerating user groups, mirroring the roles metadata endpoint.</summary>
    private const int PageSize = 100;

    /// <inheritdoc />
    public override string Description =>
        "Answer 'what is the effective permission decision, and why' for a content node. " +
        "Set `subject`: current-user (the editor asking about themselves — 'why can't I publish/delete/edit this?'), " +
        "user (a specific user), role (a user group or 'All Users' — 'what can Editors do here?'), " +
        "or all-roles ('who can publish/edit/delete here?'). " +
        "Call this FIRST whenever someone can't perform an action or the editor looks restricted: " +
        "fields read-only / can't save, can't trash/delete, Publish/Unpublish disabled, can't move/copy/sort/rollback, " +
        "can't set notifications / culture & hostnames / public access, or Create is missing. " +
        "A permission Deny is a common, invisible cause — don't attribute a block to a structural reason " +
        "(root node, content type) before checking this. " +
        "Returns each action's Allowed/Denied result with the reason (which role and node; explicit vs inherited). " +
        "Set aspect=type-create for 'why can't I create/insert document type X here?', 'what document types can I create here?', " +
        "or 'who can create type X here?' — this covers the Insert Options / allowed-child-types restrictions " +
        "(for type-create, nodeKey is the PARENT node; set contentTypeKey to focus a single document type, or omit it for the full roster).";

    /// <inheritdoc />
    protected override async Task<object> ExecuteAsync(ExplainAccessArgs args, CancellationToken cancellationToken = default)
    {
        var path = pathResolver.GetPathFromRoot(args.NodeKey);

        return args.Aspect == ExplainAspect.TypeCreate
            ? await ExplainTypeCreateAsync(args, path, cancellationToken)
            : await ExplainNodeAsync(args, path, cancellationToken);
    }

    /// <summary>
    /// Dispatches the node-permission aspect (the default, unchanged behaviour) to the appropriate
    /// subject handler.
    /// </summary>
    /// <param name="args">The tool arguments.</param>
    /// <param name="path">The resolved root-to-node ancestor key path.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>A friendly explanation/verdict/roster, or an <see cref="AccessError"/>.</returns>
    private async Task<object> ExplainNodeAsync(
        ExplainAccessArgs args,
        IReadOnlyList<Guid> path,
        CancellationToken cancellationToken) =>
        args.Subject switch
        {
            ExplainSubject.CurrentUser => await ExplainCurrentUserAsync(args, path, cancellationToken),
            ExplainSubject.User => await ExplainUserAsync(args, path, cancellationToken),
            ExplainSubject.Role => await ExplainRoleAsync(args, path, cancellationToken),
            ExplainSubject.AllRoles => await ExplainAllRolesAsync(args, path, cancellationToken),
            _ => new AccessError("Unknown subject."),
        };

    /// <summary>
    /// Resolves the current backoffice user from the security accessor and explains their access, or
    /// returns a friendly error when no user is in context.
    /// </summary>
    /// <param name="args">The tool arguments.</param>
    /// <param name="path">The resolved root-to-node ancestor key path.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>A friendly explanation/verdict, or an <see cref="AccessError"/>.</returns>
    private async Task<object> ExplainCurrentUserAsync(
        ExplainAccessArgs args,
        IReadOnlyList<Guid> path,
        CancellationToken cancellationToken)
    {
        var userKey = backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Key;
        if (userKey is null)
        {
            return new AccessError("Could not determine the current user.");
        }

        return await ResolveForUserAsync(userKey.Value, args, path, cancellationToken);
    }

    /// <summary>
    /// Explains a specific user's access, or returns a friendly error when the user key is missing.
    /// </summary>
    /// <param name="args">The tool arguments.</param>
    /// <param name="path">The resolved root-to-node ancestor key path.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>A friendly explanation/verdict, or an <see cref="AccessError"/>.</returns>
    private async Task<object> ExplainUserAsync(
        ExplainAccessArgs args,
        IReadOnlyList<Guid> path,
        CancellationToken cancellationToken)
    {
        if (args.UserKey is null)
        {
            return new AccessError("A user key is required when explaining access for a specific user.");
        }

        return await ResolveForUserAsync(args.UserKey.Value, args, path, cancellationToken);
    }

    /// <summary>
    /// Resolves and presents a single user's effective access: one verdict per verb, or a single verdict
    /// when a verb is focused.
    /// </summary>
    /// <param name="userKey">The user to resolve for.</param>
    /// <param name="args">The tool arguments.</param>
    /// <param name="path">The resolved root-to-node ancestor key path.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>A friendly verdict or explanation.</returns>
    private async Task<object> ResolveForUserAsync(
        Guid userKey,
        ExplainAccessArgs args,
        IReadOnlyList<Guid> path,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(args.Verb))
        {
            var single = await permissions.ResolveAsync(userKey, args.NodeKey, path, args.Verb, cancellationToken);
            return Format(await presenter.ToVerdictAsync(single, cancellationToken), args.ResponseFormat);
        }

        var all = await permissions.ResolveAllAsync(userKey, args.NodeKey, path, null, cancellationToken);
        return Format(await presenter.ToExplanationAsync(all, args.NodeKey, cancellationToken), args.ResponseFormat);
    }

    /// <summary>
    /// Explains a single role's access, or returns a friendly error when the role alias is missing.
    /// </summary>
    /// <param name="args">The tool arguments.</param>
    /// <param name="path">The resolved root-to-node ancestor key path.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>A friendly explanation/verdict, or an <see cref="AccessError"/>.</returns>
    private async Task<object> ExplainRoleAsync(
        ExplainAccessArgs args,
        IReadOnlyList<Guid> path,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(args.RoleAlias))
        {
            return new AccessError("A role is required when explaining access for a specific role.");
        }

        var verbs = string.IsNullOrWhiteSpace(args.Verb) ? null : new[] { args.Verb };
        var resolved = await permissions.ResolveForRoleAsync(args.RoleAlias, args.NodeKey, path, verbs, cancellationToken);

        if (!string.IsNullOrWhiteSpace(args.Verb) && resolved.TryGetValue(args.Verb, out var single))
        {
            return Format(await presenter.ToVerdictAsync(single, cancellationToken), args.ResponseFormat);
        }

        return Format(await presenter.ToExplanationAsync(resolved, args.NodeKey, cancellationToken), args.ResponseFormat);
    }

    /// <summary>
    /// Builds a "who can do this here" roster across every assignable role. With a focused verb it returns
    /// a single <see cref="AccessRoster"/>; without one it returns a per-action <see cref="AccessRosterReport"/>.
    /// </summary>
    /// <param name="args">The tool arguments.</param>
    /// <param name="path">The resolved root-to-node ancestor key path.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>An <see cref="AccessRoster"/> (single verb) or <see cref="AccessRosterReport"/> (all verbs).</returns>
    private async Task<object> ExplainAllRolesAsync(
        ExplainAccessArgs args,
        IReadOnlyList<Guid> path,
        CancellationToken cancellationToken)
    {
        var verbs = string.IsNullOrWhiteSpace(args.Verb) ? null : new[] { args.Verb };
        var node = presenter.GetNodeName(args.NodeKey);

        // verb -> (allowed roles, denied roles), preserving first-seen verb order.
        var rosters = new Dictionary<string, (List<string> Allowed, List<string> Denied)>(StringComparer.Ordinal);
        var verbOrder = new List<string>();

        foreach (var roleAlias in await GetRoleAliasesAsync())
        {
            var resolved = await permissions.ResolveForRoleAsync(roleAlias, args.NodeKey, path, verbs, cancellationToken);
            if (resolved.Count == 0)
            {
                continue;
            }

            var displayName = await presenter.GetRoleDisplayNameAsync(roleAlias, cancellationToken);
            foreach (var permission in resolved.Values)
            {
                if (!rosters.TryGetValue(permission.Verb, out var bucket))
                {
                    bucket = (new List<string>(), new List<string>());
                    rosters[permission.Verb] = bucket;
                    verbOrder.Add(permission.Verb);
                }

                (permission.IsAllowed ? bucket.Allowed : bucket.Denied).Add(displayName);
            }
        }

        var actions = verbOrder
            .Select(verb => new AccessRoster(
                presenter.GetVerbDisplayName(verb),
                node,
                rosters[verb].Allowed,
                rosters[verb].Denied))
            .ToList();

        // A focused verb yields exactly one roster — return it directly for a tighter answer.
        if (!string.IsNullOrWhiteSpace(args.Verb))
        {
            return actions.Count == 1
                ? actions[0]
                : new AccessRoster(presenter.GetVerbDisplayName(args.Verb), node, [], []);
        }

        return new AccessRosterReport(node, actions);
    }

    /// <summary>
    /// Enumerates all assignable role aliases: the virtual <c>$everyone</c> role first, followed by every
    /// Umbraco user group alias. Mirrors the roles metadata endpoint.
    /// </summary>
    /// <returns>The ordered list of role aliases.</returns>
    private async Task<IReadOnlyList<string>> GetRoleAliasesAsync()
    {
        var aliases = new List<string> { AdvancedPermissionsConstants.EveryoneRoleAlias };

        var skip = 0;
        while (true)
        {
            var page = await userGroupService.GetAllAsync(skip, PageSize);
            foreach (var group in page.Items)
            {
                aliases.Add(group.Alias);
            }

            skip += PageSize;
            if (skip >= page.Total)
            {
                break;
            }
        }

        return aliases;
    }

    /// <summary>
    /// Applies the requested response format to a single verdict: <see cref="ExplainResponseFormat.Concise"/>
    /// keeps only the highest-priority reason; <see cref="ExplainResponseFormat.Detailed"/> keeps the full chain.
    /// </summary>
    /// <param name="verdict">The friendly verdict to format.</param>
    /// <param name="format">The requested response format.</param>
    /// <returns>The formatted verdict.</returns>
    private static AccessVerdict Format(AccessVerdict verdict, ExplainResponseFormat format) =>
        format == ExplainResponseFormat.Detailed || verdict.Reasons.Count <= 1
            ? verdict
            : verdict with { Reasons = [verdict.Reasons[0]] };

    /// <summary>
    /// Applies the requested response format to a full explanation by formatting each contained verdict.
    /// </summary>
    /// <param name="explanation">The friendly explanation to format.</param>
    /// <param name="format">The requested response format.</param>
    /// <returns>The formatted explanation.</returns>
    private static AccessExplanation Format(AccessExplanation explanation, ExplainResponseFormat format) =>
        format == ExplainResponseFormat.Detailed
            ? explanation
            : explanation with { Permissions = explanation.Permissions.Select(v => Format(v, format)).ToList() };

    // ----------------------------------------------------------------------------------------------
    // Type-create ("Insert Options") aspect.
    //
    // Mirrors the doc-type permission system used by the backoffice Create menu: the verb is
    // Umb.Document.CreateOfType, it DEFAULTS to Allow (entries narrow by exception), and it is keyed
    // per (parent node, role, content type). A document type can additionally be structurally
    // "Not applicable" when Umbraco's own allowed-children configuration disallows it under the parent
    // — independent of any permission entry. Element types are never creatable content and are excluded.
    // ----------------------------------------------------------------------------------------------

    /// <summary>
    /// Resolves the type-create ("Insert Options") aspect: whether the chosen subject may create document
    /// types under the parent node identified by <see cref="ExplainAccessArgs.NodeKey"/>. With a focused
    /// <see cref="ExplainAccessArgs.ContentTypeKey"/> a single verdict/roster is returned; without one the
    /// full per-type roster for the node is returned.
    /// </summary>
    /// <param name="args">The tool arguments.</param>
    /// <param name="path">The resolved root-to-parent ancestor key path (the path passed to the resolver).</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>A friendly type-create verdict/explanation/roster, or an <see cref="AccessError"/>.</returns>
    private async Task<object> ExplainTypeCreateAsync(
        ExplainAccessArgs args,
        IReadOnlyList<Guid> path,
        CancellationToken cancellationToken)
    {
        // The path resolver returns an empty list for the virtual root or an unresolvable node; the
        // resolver expects at least the virtual-root sentinel, matching the controller's behaviour.
        var resolvePath = path.Count > 0 ? path : [AdvancedPermissionsConstants.VirtualRootNodeKey];
        var allowedChildren = GetAllowedChildrenKeys(args.NodeKey);

        return args.Subject switch
        {
            ExplainSubject.AllRoles => await TypeCreateAllRolesAsync(args, resolvePath, allowedChildren, cancellationToken),
            ExplainSubject.Role => await TypeCreateForRoleAsync(args, resolvePath, allowedChildren, cancellationToken),
            ExplainSubject.User => await TypeCreateForUserSubjectAsync(args, args.UserKey, resolvePath, allowedChildren, cancellationToken),
            ExplainSubject.CurrentUser => await TypeCreateForCurrentUserAsync(args, resolvePath, allowedChildren, cancellationToken),
            _ => new AccessError("Unknown subject."),
        };
    }

    /// <summary>
    /// Resolves the type-create aspect for the current backoffice user, or returns a friendly error when
    /// no user is in context.
    /// </summary>
    /// <param name="args">The tool arguments.</param>
    /// <param name="path">The resolved root-to-parent ancestor key path.</param>
    /// <param name="allowedChildren">The set of structurally allowed child content-type keys under the parent.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>A friendly verdict/explanation, or an <see cref="AccessError"/>.</returns>
    private async Task<object> TypeCreateForCurrentUserAsync(
        ExplainAccessArgs args,
        IReadOnlyList<Guid> path,
        ISet<Guid> allowedChildren,
        CancellationToken cancellationToken)
    {
        var userKey = backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Key;
        if (userKey is null)
        {
            return new AccessError("Could not determine the current user.");
        }

        return await TypeCreateForUserAsync(userKey.Value, args, path, allowedChildren, cancellationToken);
    }

    /// <summary>
    /// Validates the user key for the user subject and resolves the type-create aspect for that user.
    /// </summary>
    /// <param name="args">The tool arguments.</param>
    /// <param name="userKey">The user key supplied on the arguments.</param>
    /// <param name="path">The resolved root-to-parent ancestor key path.</param>
    /// <param name="allowedChildren">The set of structurally allowed child content-type keys under the parent.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>A friendly verdict/explanation, or an <see cref="AccessError"/>.</returns>
    private async Task<object> TypeCreateForUserSubjectAsync(
        ExplainAccessArgs args,
        Guid? userKey,
        IReadOnlyList<Guid> path,
        ISet<Guid> allowedChildren,
        CancellationToken cancellationToken)
    {
        if (userKey is null)
        {
            return new AccessError("A user key is required when explaining access for a specific user.");
        }

        return await TypeCreateForUserAsync(userKey.Value, args, path, allowedChildren, cancellationToken);
    }

    /// <summary>
    /// Resolves the type-create aspect for a specific user. With a focused content type returns a single
    /// verdict; without one returns the per-type roster for the parent node.
    /// </summary>
    /// <param name="userKey">The user to resolve for.</param>
    /// <param name="args">The tool arguments.</param>
    /// <param name="path">The resolved root-to-parent ancestor key path.</param>
    /// <param name="allowedChildren">The set of structurally allowed child content-type keys under the parent.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>A <see cref="TypeCreateVerdict"/> (focused) or <see cref="TypeCreateExplanation"/> (roster).</returns>
    private async Task<object> TypeCreateForUserAsync(
        Guid userKey,
        ExplainAccessArgs args,
        IReadOnlyList<Guid> path,
        ISet<Guid> allowedChildren,
        CancellationToken cancellationToken)
    {
        if (args.ContentTypeKey is { } focused)
        {
            var permission = await docTypePermissions.ResolveCreateAsync(userKey, args.NodeKey, path, focused, cancellationToken);
            return await presenter.ToTypeCreateVerdictAsync(focused, permission, allowedChildren.Contains(focused), cancellationToken);
        }

        var verdicts = new List<TypeCreateVerdict>();
        foreach (var candidate in GetCandidateContentTypes())
        {
            var permission = await docTypePermissions.ResolveCreateAsync(userKey, args.NodeKey, path, candidate.Key, cancellationToken);
            verdicts.Add(await presenter.ToTypeCreateVerdictAsync(candidate.Key, permission, allowedChildren.Contains(candidate.Key), cancellationToken));
        }

        return new TypeCreateExplanation(presenter.GetNodeName(args.NodeKey), verdicts);
    }

    /// <summary>
    /// Resolves the type-create aspect for a single role (the role plus <c>$everyone</c>). With a focused
    /// content type returns a single verdict; without one returns the per-type roster for the parent node.
    /// </summary>
    /// <param name="args">The tool arguments.</param>
    /// <param name="path">The resolved root-to-parent ancestor key path.</param>
    /// <param name="allowedChildren">The set of structurally allowed child content-type keys under the parent.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>A <see cref="TypeCreateVerdict"/> (focused) or <see cref="TypeCreateExplanation"/> (roster), or an <see cref="AccessError"/>.</returns>
    private async Task<object> TypeCreateForRoleAsync(
        ExplainAccessArgs args,
        IReadOnlyList<Guid> path,
        ISet<Guid> allowedChildren,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(args.RoleAlias))
        {
            return new AccessError("A role is required when explaining access for a specific role.");
        }

        IReadOnlyList<string> roleAliases = [args.RoleAlias, AdvancedPermissionsConstants.EveryoneRoleAlias];

        if (args.ContentTypeKey is { } focused)
        {
            var permission = await docTypePermissions.ResolveCreateForRolesAsync(roleAliases, path, focused, cancellationToken);
            return await presenter.ToTypeCreateVerdictAsync(focused, permission, allowedChildren.Contains(focused), cancellationToken);
        }

        var verdicts = new List<TypeCreateVerdict>();
        foreach (var candidate in GetCandidateContentTypes())
        {
            var permission = await docTypePermissions.ResolveCreateForRolesAsync(roleAliases, path, candidate.Key, cancellationToken);
            verdicts.Add(await presenter.ToTypeCreateVerdictAsync(candidate.Key, permission, allowedChildren.Contains(candidate.Key), cancellationToken));
        }

        return new TypeCreateExplanation(presenter.GetNodeName(args.NodeKey), verdicts);
    }

    /// <summary>
    /// Resolves the type-create aspect across every assignable role. With a focused content type returns a
    /// single <see cref="TypeCreateRoster"/> (who can create that type here); without one returns a
    /// per-type <see cref="TypeCreateRosterReport"/>.
    /// </summary>
    /// <param name="args">The tool arguments.</param>
    /// <param name="path">The resolved root-to-parent ancestor key path.</param>
    /// <param name="allowedChildren">The set of structurally allowed child content-type keys under the parent.</param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>A <see cref="TypeCreateRoster"/> (focused) or <see cref="TypeCreateRosterReport"/> (all types).</returns>
    private async Task<object> TypeCreateAllRolesAsync(
        ExplainAccessArgs args,
        IReadOnlyList<Guid> path,
        ISet<Guid> allowedChildren,
        CancellationToken cancellationToken)
    {
        var node = presenter.GetNodeName(args.NodeKey);
        var roleAliases = await GetRoleAliasesAsync();

        // Precompute friendly role display names once.
        var displayNames = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var alias in roleAliases)
        {
            displayNames[alias] = await presenter.GetRoleDisplayNameAsync(alias, cancellationToken);
        }

        var candidates = args.ContentTypeKey is { } focused
            ? [focused]
            : GetCandidateContentTypes().Select(ct => ct.Key).ToList();

        var rosters = new List<TypeCreateRoster>(candidates.Count);
        foreach (var contentTypeKey in candidates)
        {
            var name = presenter.GetContentTypeName(contentTypeKey);

            if (!allowedChildren.Contains(contentTypeKey))
            {
                rosters.Add(new TypeCreateRoster(name, node, IsApplicable: false, [], []));
                continue;
            }

            var allowed = new List<string>();
            var denied = new List<string>();
            foreach (var alias in roleAliases)
            {
                // Resolve per role with just that role + $everyone, matching the single-role semantics.
                IReadOnlyList<string> roles = alias == AdvancedPermissionsConstants.EveryoneRoleAlias
                    ? [AdvancedPermissionsConstants.EveryoneRoleAlias]
                    : [alias, AdvancedPermissionsConstants.EveryoneRoleAlias];

                var permission = await docTypePermissions.ResolveCreateForRolesAsync(roles, path, contentTypeKey, cancellationToken);
                (permission.IsAllowed ? allowed : denied).Add(displayNames[alias]);
            }

            rosters.Add(new TypeCreateRoster(name, node, IsApplicable: true, allowed, denied));
        }

        // A focused type yields exactly one roster — return it directly for a tighter answer.
        return args.ContentTypeKey is not null && rosters.Count == 1
            ? rosters[0]
            : new TypeCreateRosterReport(node, rosters);
    }

    /// <summary>
    /// Enumerates the candidate document types for the type-create aspect: every non-element content type,
    /// ordered by name, matching the doc-type editor and audit endpoints.
    /// </summary>
    /// <returns>The candidate content types.</returns>
    private IReadOnlyList<IContentType> GetCandidateContentTypes() =>
        contentTypeService.GetAll()
            .Where(ct => !ct.IsElement)
            .OrderBy(ct => ct.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// Resolves the set of document-type keys structurally allowed as children of the supplied parent,
    /// mirroring the management API's <c>AuditForNode</c> logic. For the virtual root (or an unresolvable
    /// node) the allowed set is the document types flagged <c>IsAllowedAsRoot</c>; under a content node it
    /// is the parent content type's configured allowed-children list.
    /// </summary>
    /// <param name="parentNodeKey">The parent node key, or the virtual-root sentinel.</param>
    /// <returns>A set of content-type keys structurally permitted under the parent.</returns>
    private ISet<Guid> GetAllowedChildrenKeys(Guid parentNodeKey)
    {
        if (parentNodeKey == AdvancedPermissionsConstants.VirtualRootNodeKey)
        {
            return contentTypeService.GetAll()
                .Where(ct => !ct.IsElement && ct.AllowedAsRoot)
                .Select(ct => ct.Key)
                .ToHashSet();
        }

        var parent = entityService.Get(parentNodeKey, UmbracoObjectTypes.Document);
        if (parent is not IContentEntitySlim slim)
        {
            return new HashSet<Guid>();
        }

        var parentContentType = contentTypeService.Get(slim.ContentTypeAlias);
        if (parentContentType?.AllowedContentTypes is null)
        {
            return new HashSet<Guid>();
        }

        return parentContentType.AllowedContentTypes
            .Select(c => c.Key)
            .Where(k => k != Guid.Empty)
            .ToHashSet();
    }
}
