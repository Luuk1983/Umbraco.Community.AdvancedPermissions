namespace Umbraco.Community.AdvancedPermissions.Controllers.Models;

/// <summary>
/// A single stored doc-type permission entry returned by the API.
/// </summary>
/// <param name="Id">The entry's identifier.</param>
/// <param name="NodeKey">
/// The content node the entry is scoped to. <c>AdvancedPermissionsConstants.VirtualRootNodeKey</c>
/// indicates the virtual root row.
/// </param>
/// <param name="ContentTypeKey">The doc-type the entry concerns.</param>
/// <param name="RoleAlias">The role alias (user group or <c>$everyone</c>).</param>
/// <param name="Verb">The verb (v1: only <c>Umb.Document.CreateOfType</c>).</param>
/// <param name="State">The state, as a string: <c>Allow</c> or <c>Deny</c>.</param>
/// <param name="Scope">The scope, as a string.</param>
/// <param name="IsPriorityOverride">
/// Whether this entry is flagged as a priority override (CSS <c>!important</c>-style escape hatch
/// for cross-role Explicit Deny at the entry's storing node).
/// </param>
public sealed record DocTypePermissionEntryResponseModel(
    Guid Id,
    Guid NodeKey,
    Guid ContentTypeKey,
    string RoleAlias,
    string Verb,
    string State,
    string Scope,
    bool IsPriorityOverride);

/// <summary>
/// Request body for saving a (node, role, content-type) triple's entries.
/// </summary>
/// <param name="NodeKey">
/// The node key the entries apply to. Use
/// <c>AdvancedPermissionsConstants.VirtualRootNodeKey</c> for the virtual-root row.
/// </param>
/// <param name="RoleAlias">The role alias.</param>
/// <param name="ContentTypeKey">The doc-type key.</param>
/// <param name="Entries">The verb-state-scope tuples. Empty list clears all entries for the triple.</param>
/// <param name="ExpectedStamp">
/// The stamp this client was given when it read these entries. When null the concurrency check is
/// skipped, which is how a client written before stamps existed keeps working.
/// </param>
/// <param name="Force">Whether to write through a stale stamp. Only sent after a user confirms.</param>
public sealed record SaveDocTypePermissionsRequestModel(
    Guid NodeKey,
    string RoleAlias,
    Guid ContentTypeKey,
    IReadOnlyList<SavePermissionEntryItem> Entries,
    string? ExpectedStamp = null,
    bool Force = false);

/// <summary>
/// A non-element doc-type that may appear in the editor's "document type" picker.
/// </summary>
/// <param name="Key">The doc-type key.</param>
/// <param name="Alias">The doc-type alias.</param>
/// <param name="Name">The doc-type's display name.</param>
/// <param name="Icon">The doc-type's icon alias, if any.</param>
public sealed record DocTypeListItemModel(
    Guid Key,
    string Alias,
    string Name,
    string? Icon);

/// <summary>
/// Per-doc-type result row for the tree-style "audit for node" endpoint. Includes an
/// <c>IsInAllowedChildren</c> flag so the UI can render `n/a` for doc-types not in the parent's
/// allowed-children list, distinct from a resolver-driven deny.
/// </summary>
public sealed record DocTypeAuditForNodeRowResponseModel(
    Guid ContentTypeKey,
    string ContentTypeAlias,
    string ContentTypeName,
    string? ContentTypeIcon,
    bool IsAllowed,
    bool IsExplicit,
    bool IsInAllowedChildren,
    IReadOnlyList<ReasoningItem> Reasoning,
    bool WasPriorityOverrideActive = false,
    IReadOnlyList<ReasoningItem>? SuppressedReasoning = null);

/// <summary>
/// Top-level response of <c>GET /doc-type-permissions/audit-for-node</c>: the audited node
/// key plus one row per non-element doc type.
/// </summary>
/// <param name="NodeKey">The audited node (or virtual root).</param>
/// <param name="Results">One row per non-element doc-type.</param>
public sealed record DocTypeAuditForNodeResponseModel(
    Guid NodeKey,
    IReadOnlyList<DocTypeAuditForNodeRowResponseModel> Results);

/// <summary>
/// A doc-type permission entry shown along the inheritance path in the reasoning dialog.
/// </summary>
public sealed record DocTypePathEntryResponseModel(
    Guid Id,
    Guid NodeKey,
    Guid ContentTypeKey,
    string RoleAlias,
    string Verb,
    string State,
    string Scope,
    bool IsPriorityOverride);

/// <summary>
/// Response for <c>GET /doc-type-permissions/path-entries</c>: the inheritance path plus all
/// stored doc-type entries along that path filtered to the requested content-type.
/// </summary>
public sealed record DocTypePathEntriesResponseModel(
    IReadOnlyList<PathNodeModel> Path,
    IReadOnlyList<DocTypePathEntryResponseModel> Entries);

/// <summary>
/// One node's stored entries for the (role, content-type) combination selected in the editor,
/// together with the concurrency stamp of that node's own entries.
/// </summary>
/// <remarks>
/// The doc-type counterpart of <see cref="TreeNodeResponseModel"/>'s <c>Stamp</c>. Grouped by node
/// because <c>GetForEditor</c> returns entries for one (role, content-type) pair across every node
/// that has any — the stamp has to describe each node's own set, not the whole result together, or
/// a client saving one node's entries could never tell whether a stale check should fire.
/// </remarks>
/// <param name="NodeKey">
/// The content node the entries are scoped to. <c>AdvancedPermissionsConstants.VirtualRootNodeKey</c>
/// indicates the virtual-root row.
/// </param>
/// <param name="Entries">This node's stored entries for the requested (role, content-type).</param>
/// <param name="Stamp">
/// The concurrency stamp of <paramref name="Entries"/>. A client sends this back when saving this
/// node's triple; the server refuses the save if the stored entries have moved since.
/// </param>
public sealed record DocTypeEditorNodeResponseModel(
    Guid NodeKey,
    IReadOnlyList<DocTypePermissionEntryResponseModel> Entries,
    string Stamp);

/// <summary>
/// A request to replace document-type create permissions for several node, user group and document
/// type triples at once.
/// </summary>
/// <param name="Nodes">The triples to write.</param>
/// <param name="Force">Whether to skip the concurrency check. Only sent after a user confirms.</param>
public sealed record BatchSaveDocTypePermissionsRequestModel(
    IReadOnlyList<BatchSaveDocTypePermissionsNode> Nodes,
    bool Force = false);

/// <summary>
/// One node, user group and document type triple within a batch save.
/// </summary>
/// <param name="NodeKey">The content node key the rule is anchored to.</param>
/// <param name="RoleAlias">The user group alias.</param>
/// <param name="ContentTypeKey">The document type the rule is about.</param>
/// <param name="Entries">The replacement entries. An empty list removes all entries for the triple.</param>
/// <param name="ExpectedStamp">
/// The stamp this client read. Null skips the check for this triple, which is how an older client
/// keeps working.
/// </param>
public sealed record BatchSaveDocTypePermissionsNode(
    Guid NodeKey,
    string RoleAlias,
    Guid ContentTypeKey,
    IReadOnlyList<SavePermissionEntryItem> Entries,
    string? ExpectedStamp = null);

/// <summary>
/// The body of the <c>409 Conflict</c> a document-type batch save is refused with.
/// </summary>
/// <remarks>
/// Documents the shape for OpenAPI and client codegen only. At runtime the endpoint returns a
/// <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails"/> built by <c>ConflictProblemDetails</c>
/// that carries these conflicts as its <c>conflicts</c> extension, alongside <c>type</c>,
/// <c>title</c> and <c>status</c>, so that Umbraco's backoffice interceptor keeps the body.
/// </remarks>
/// <param name="Conflicts">Only the triples whose stored entries moved.</param>
public sealed record BatchSaveDocTypeConflictResponseModel(
    IReadOnlyList<BatchSaveDocTypeConflict> Conflicts);

/// <summary>One triple whose stored entries no longer match what the client loaded.</summary>
/// <param name="NodeKey">The content node key.</param>
/// <param name="RoleAlias">The user group alias.</param>
/// <param name="ContentTypeKey">The document type.</param>
/// <param name="CurrentEntries">What is stored right now.</param>
/// <param name="CurrentStamp">The stamp of <paramref name="CurrentEntries"/>.</param>
public sealed record BatchSaveDocTypeConflict(
    Guid NodeKey,
    string RoleAlias,
    Guid ContentTypeKey,
    IReadOnlyList<DocTypePermissionEntryResponseModel> CurrentEntries,
    string CurrentStamp);

/// <summary>One triple's new stamp after a successful batch save.</summary>
/// <param name="NodeKey">The content node key.</param>
/// <param name="RoleAlias">The user group alias.</param>
/// <param name="ContentTypeKey">The document type.</param>
/// <param name="Stamp">The stamp of what was just written.</param>
public sealed record BatchSavedDocTypeStamp(
    Guid NodeKey,
    string RoleAlias,
    Guid ContentTypeKey,
    string Stamp);
