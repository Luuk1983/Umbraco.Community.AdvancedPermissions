using Umbraco.Community.AdvancedPermissions.AI.Models;
using Umbraco.Community.AdvancedPermissions.Core.Models;

namespace Umbraco.Community.AdvancedPermissions.AI.Services;

/// <summary>
/// Computes, by deterministic simulation, the concrete permission-configuration changes that would flip
/// a single denied verb to allowed for a given subject at a content node.
/// </summary>
/// <remarks>
/// <para>
/// The service exists to replace the copilot's guesswork with ground truth. The LLM, left to itself,
/// will happily claim that "adding an Allow" fixes a denial — which is wrong when the cause is an
/// explicit same-node Deny (only a priority-override Allow or removing the Deny works there). This
/// service generates a small, case-specific set of candidate mutations and <b>validates each one by
/// re-resolving the package's pure resolver</b> against the mutated entry set, keeping only the
/// candidates that actually flip the verdict to Allowed.
/// </para>
/// <para>
/// It is strictly read-and-simulate: it reads the current entries once via the repository, applies
/// candidate mutations to an in-memory copy, and re-resolves via
/// <see cref="Core.Interfaces.IPermissionResolver"/> (never the cached
/// <see cref="Core.Interfaces.IAdvancedPermissionService"/>, whose L2 cache would ignore the mutations).
/// It never writes, deletes, or persists anything.
/// </para>
/// </remarks>
public interface IPermissionRemediationService
{
    /// <summary>
    /// Suggests the confirmed changes that would make <paramref name="verb"/> allowed for the supplied
    /// role set at the target node, ranked least-privileged-first.
    /// </summary>
    /// <param name="nodeKey">The target content node the verb is resolved at.</param>
    /// <param name="pathFromRoot">
    /// The ordered node keys from root down to (and including) the target node — the same path the
    /// verdict being explained was resolved with.
    /// </param>
    /// <param name="roleAliases">
    /// The exact role set the verdict used. For a user/current-user subject this is the user's group
    /// aliases plus <c>$everyone</c>; for a single-role subject it is that role alias only (no
    /// <c>$everyone</c>), mirroring the node-level resolution semantics.
    /// </param>
    /// <param name="verb">The single verb to remediate.</param>
    /// <param name="defaultState">
    /// The resolver's default state for this aspect — <see cref="PermissionState.Deny"/> for node-level
    /// permissions.
    /// </param>
    /// <param name="cancellationToken">Token to support cancellation.</param>
    /// <returns>
    /// The confirmed remediation options (possibly empty when the baseline is already Allowed or no
    /// bounded candidate flips it). Each option is guaranteed to flip the verdict to Allowed.
    /// </returns>
    Task<IReadOnlyList<RemediationOption>> SuggestAsync(
        Guid nodeKey,
        IReadOnlyList<Guid> pathFromRoot,
        IReadOnlyList<string> roleAliases,
        string verb,
        PermissionState defaultState,
        CancellationToken cancellationToken = default);
}
