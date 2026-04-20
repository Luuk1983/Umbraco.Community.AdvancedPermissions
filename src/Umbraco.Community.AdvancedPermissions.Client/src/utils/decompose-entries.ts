import type { PermissionState, PermissionScope } from '../models/permission.models.js';

/** Decomposed dialog state from stored entries. */
export interface DecomposedPermission {
  nodeState: 'inherit' | 'allow' | 'deny';
  descState: 'inherit' | 'allow' | 'deny';
  sameAsNode: boolean;
}

/**
 * Decompose stored entries (using backend scope model) into a UI state.
 * Handles all combinations of scopes and supports both single and dual entries per verb.
 */
export function decomposeEntries(
  entries: ReadonlyArray<{ state: PermissionState; scope: PermissionScope }>,
): DecomposedPermission {
  let nodeState: 'inherit' | 'allow' | 'deny' = 'inherit';
  let descState: 'inherit' | 'allow' | 'deny' = 'inherit';

  for (const e of entries) {
    const s = e.state === 'Allow' ? ('allow' as const) : ('deny' as const);
    switch (e.scope) {
      case 'ThisNodeAndDescendants':
        nodeState = s;
        descState = s;
        break;
      case 'ThisNodeOnly':
        nodeState = s;
        break;
      case 'DescendantsOnly':
        descState = s;
        break;
    }
  }

  const sameAsNode = nodeState === descState;
  return { nodeState, descState, sameAsNode };
}
