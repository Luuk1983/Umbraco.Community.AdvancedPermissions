using Umbraco.AI.Core;
using Umbraco.AI.Core.RuntimeContext;

namespace Umbraco.Community.AdvancedPermissions.AI.Context;

/// <summary>
/// An <see cref="IAIRuntimeContextContributor"/> that, on document-context conversations, prepends a
/// short grounding line to the copilot's system prompt. The line teaches the model two things: (1) that
/// this site uses the Advanced Permissions package, so an action that appears blocked or read-only may be
/// caused by a permission <c>Deny</c> rather than a structural reason — and that it should reach for the
/// Advanced Permissions tools (<c>uap_explain_access</c>, <c>uap_audit_permissions</c>) to check before
/// concluding otherwise; and (2) how the package's model works, so simple "how do I…" questions can be
/// answered inline (the how-to is grounding, not a tool call).
/// </summary>
/// <remarks>
/// <para>
/// This contributor follows a strict defensive pattern, because Umbraco AI 1.14.0 runs every contributor
/// on every agent run with <b>no</b> try/catch around them — an exception thrown here would abort the
/// whole agent run:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <b>Gated on document context.</b> It only contributes when the focused entity is a document. The
/// built-in <c>SerializedEntityContributor</c> stores the focused entity's type under
/// <see cref="Constants.ContextKeys.EntityType"/> before this contributor runs (this one is appended to
/// the collection), so the gate reads that value and compares it to the document entity type. When the
/// value is absent or anything other than a document, nothing is contributed.
/// </description>
/// </item>
/// <item><description><b>Append-only.</b> It only adds to <see cref="AIRuntimeContext.SystemMessageParts"/>; it never writes <see cref="AIRuntimeContext.Variables"/> or <see cref="AIRuntimeContext.Data"/>.</description></item>
/// <item><description><b>Never throws.</b> The whole method body is wrapped in a catch-all so a bug here can never break an agent run.</description></item>
/// <item><description><b>Cheap.</b> It contributes a single static string; no I/O and no service calls (the contract is synchronous).</description></item>
/// </list>
/// </remarks>
internal sealed class AdvancedPermissionsGroundingContributor : IAIRuntimeContextContributor
{
    /// <summary>
    /// The entity-type value the built-in document entity adapter stores in the runtime context's data
    /// bag (<c>DocumentEntityAdapter.EntityType</c>). The grounding line is contributed only when the
    /// focused entity matches this value.
    /// </summary>
    private const string DocumentEntityType = "document";

    /// <summary>
    /// The grounding line prepended to the system prompt on document conversations. Kept concise: it
    /// summarises the permission model, nudges the copilot toward the Advanced Permissions read tools when
    /// something looks blocked or read-only, and supplies enough detail to answer "how do I…" questions
    /// inline.
    /// </summary>
    private const string GroundingMessage =
        "This Umbraco site uses the Advanced Permissions package: access is governed by explicit " +
        "Allow/Deny rules per user group (including the special 'All Users' group) on content nodes, " +
        "with scopes (this node / this node and descendants / descendants only), tree inheritance, and " +
        "optional priority overrides; document-type 'Insert Options' control which document types can be " +
        "created where. If a user cannot perform an action or the editor appears read-only, a permission " +
        "Deny is a likely cause — use the Advanced Permissions tools (uap_explain_access, " +
        "uap_audit_permissions) to check before concluding the cause is structural. These rules are " +
        "configured per user group and node in the Permissions Editor.";

    /// <inheritdoc />
    public void Contribute(AIRuntimeContext context)
    {
        try
        {
            // Gate (a): only contribute on document conversations. The entity-type value is set by the
            // built-in entity contributor before this one runs; absent/non-document → contribute nothing.
            var entityType = context.GetValue<string>(Constants.ContextKeys.EntityType);
            if (!string.Equals(entityType, DocumentEntityType, StringComparison.Ordinal))
            {
                return;
            }

            // Append-only (b): never write Variables/Data.
            context.SystemMessageParts.Add(GroundingMessage);
        }
        catch
        {
            // Never throw (c): Umbraco AI has no try/catch around contributors, so a failure here would
            // abort the entire agent run. Grounding is a best-effort nudge; swallow everything.
        }
    }
}
