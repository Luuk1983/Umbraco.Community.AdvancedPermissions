import type { PermissionState, PermissionScope } from '../models/permission.models.js';

/** Pending entries for a verb: empty array means "inherit/clear all entries for this verb". */
export type PendingVerbEntries = Array<{ state: PermissionState; scope: PermissionScope; isPriorityOverride: boolean }>;

/**
 * Inverse of `decomposeEntries`: compose dialog state back into the 0, 1, or 2 stored entries
 * a verb needs given the chosen node/descendant state pair.
 *
 * Each composed entry carries its own priority-override flag, so the node rule and the
 * descendant rule can be flagged independently:
 * - uniform case (node === descendants → single `ThisNodeAndDescendants` entry): carries `nodeFlag`.
 * - `ThisNodeOnly` (node-side) entry: carries `nodeFlag`.
 * - `DescendantsOnly` (descendant-side) entry: carries `descFlag`.
 *
 * @param nodeState The chosen state for the node itself.
 * @param descState The chosen state for descendants when not the same as node.
 * @param sameAsNode If true, descendants follow whatever `nodeState` says.
 * @param nodeFlag Whether the node-side entry (ThisNodeOnly / ThisNodeAndDescendants) is flagged.
 * @param descFlag Whether the descendant-side entry (DescendantsOnly) is flagged.
 * @returns 0–2 entries representing the user's intent in the backend scope model.
 */
export function composeEntries(
  nodeState: 'inherit' | 'allow' | 'deny',
  descState: 'inherit' | 'allow' | 'deny',
  sameAsNode: boolean,
  nodeFlag: boolean = false,
  descFlag: boolean = false,
): PendingVerbEntries {
  const effectiveDesc = sameAsNode ? nodeState : descState;
  // When descendants follow the node, they follow its override flag too.
  const effectiveDescFlag = sameAsNode ? nodeFlag : descFlag;

  // Both inherit → no entries
  if (nodeState === 'inherit' && effectiveDesc === 'inherit') return [];

  // Collapse to a single ThisNodeAndDescendants entry ONLY when the node and descendant rules
  // are truly identical — same state AND same override flag. If the override flags differ (e.g.
  // override on the node but not descendants), they are distinct rules and must be stored
  // separately, otherwise the node's override would incorrectly leak onto descendants.
  if (nodeState === effectiveDesc && nodeFlag === effectiveDescFlag) {
    const state: PermissionState = nodeState === 'allow' ? 'Allow' : 'Deny';
    return [{ state, scope: 'ThisNodeAndDescendants', isPriorityOverride: nodeFlag }];
  }

  // Otherwise up to 2 entries, each carrying its own flag.
  const result: PendingVerbEntries = [];
  if (nodeState !== 'inherit') {
    result.push({
      state: nodeState === 'allow' ? 'Allow' : 'Deny',
      scope: 'ThisNodeOnly',
      isPriorityOverride: nodeFlag,
    });
  }
  if (effectiveDesc !== 'inherit') {
    result.push({
      state: effectiveDesc === 'allow' ? 'Allow' : 'Deny',
      scope: 'DescendantsOnly',
      isPriorityOverride: effectiveDescFlag,
    });
  }
  return result;
}
