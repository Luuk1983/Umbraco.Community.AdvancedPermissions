import type { PermissionState, PermissionScope } from '../models/permission.models.js';

/** Pending entries for a verb: empty array means "inherit/clear all entries for this verb". */
export type PendingVerbEntries = Array<{ state: PermissionState; scope: PermissionScope }>;

/**
 * Inverse of `decomposeEntries`: compose dialog state back into the 0, 1, or 2 stored entries
 * a verb needs given the chosen node/descendant state pair.
 *
 * @param nodeState The chosen state for the node itself.
 * @param descState The chosen state for descendants when not the same as node.
 * @param sameAsNode If true, descendants follow whatever `nodeState` says.
 * @returns 0–2 entries representing the user's intent in the backend scope model.
 */
export function composeEntries(
  nodeState: 'inherit' | 'allow' | 'deny',
  descState: 'inherit' | 'allow' | 'deny',
  sameAsNode: boolean,
): PendingVerbEntries {
  const effectiveDesc = sameAsNode ? nodeState : descState;

  // Both inherit → no entries
  if (nodeState === 'inherit' && effectiveDesc === 'inherit') return [];

  // Both same → single ThisNodeAndDescendants entry
  if (nodeState === effectiveDesc) {
    const state: PermissionState = nodeState === 'allow' ? 'Allow' : 'Deny';
    return [{ state, scope: 'ThisNodeAndDescendants' }];
  }

  // Different states → up to 2 entries
  const result: PendingVerbEntries = [];
  if (nodeState !== 'inherit') {
    result.push({ state: nodeState === 'allow' ? 'Allow' : 'Deny', scope: 'ThisNodeOnly' });
  }
  if (effectiveDesc !== 'inherit') {
    result.push({ state: effectiveDesc === 'allow' ? 'Allow' : 'Deny', scope: 'DescendantsOnly' });
  }
  return result;
}
