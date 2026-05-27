import type { PermissionState, PermissionScope } from '../models/permission.models.js';

/** Decomposed dialog state from stored entries. */
export interface DecomposedPermission {
  nodeState: 'inherit' | 'allow' | 'deny';
  descState: 'inherit' | 'allow' | 'deny';
  sameAsNode: boolean;
  /** Whether the node-side entry (ThisNodeOnly / ThisNodeAndDescendants) is flagged. */
  nodeIsPriorityOverride: boolean;
  /** Whether the descendant-side entry (DescendantsOnly) is flagged. */
  descIsPriorityOverride: boolean;
}

/**
 * Decompose stored entries (using backend scope model) into a UI state.
 * Handles all combinations of scopes and supports both single and dual entries per verb.
 *
 * Each side's priority-override flag is read independently: the node side from the
 * ThisNodeOnly / ThisNodeAndDescendants entry, the descendant side from the DescendantsOnly entry.
 */
export function decomposeEntries(
  entries: ReadonlyArray<{ state: PermissionState; scope: PermissionScope; isPriorityOverride?: boolean }>,
): DecomposedPermission {
  let nodeState: 'inherit' | 'allow' | 'deny' = 'inherit';
  let descState: 'inherit' | 'allow' | 'deny' = 'inherit';
  let nodeIsPriorityOverride = false;
  let descIsPriorityOverride = false;

  for (const e of entries) {
    const s = e.state === 'Allow' ? ('allow' as const) : ('deny' as const);
    const flag = e.isPriorityOverride === true;
    switch (e.scope) {
      case 'ThisNodeAndDescendants':
        // A single TND entry governs both sides with one rule, so both flags reflect it.
        nodeState = s;
        descState = s;
        nodeIsPriorityOverride = flag;
        descIsPriorityOverride = flag;
        break;
      case 'ThisNodeOnly':
        nodeState = s;
        nodeIsPriorityOverride = flag;
        break;
      case 'DescendantsOnly':
        descState = s;
        descIsPriorityOverride = flag;
        break;
    }
  }

  // "Same as node" requires both the state AND the override flag to match — otherwise the two
  // sides are distinct rules and must be shown (and re-saved) separately.
  const sameAsNode = nodeState === descState && nodeIsPriorityOverride === descIsPriorityOverride;
  return { nodeState, descState, sameAsNode, nodeIsPriorityOverride, descIsPriorityOverride };
}
