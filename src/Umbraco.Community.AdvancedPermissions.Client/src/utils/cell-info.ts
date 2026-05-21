import type { PermissionState, PermissionScope } from '../models/permission.models.js';
import { decomposeEntries } from './decompose-entries.js';

/**
 * Visual layout description for a permission cell.
 *
 * - `split=false` → render one uniform block with class `nodeClass`.
 * - `split=true` → render two halves: left=`nodeClass`, right=`descClass`.
 */
export interface CellInfo {
  split: boolean;
  nodeClass: 'allow' | 'deny' | 'inherit';
  descClass: 'allow' | 'deny' | 'inherit';
}

/**
 * Computes the cell rendering shape for a set of stored entries.
 *
 * @param entries The stored entries (after filtering to the relevant verb).
 * @returns A `CellInfo` describing whether to render uniform or split, and which classes to use.
 */
export function getCellInfo(
  entries: ReadonlyArray<{ state: PermissionState; scope: PermissionScope }>,
): CellInfo {
  if (entries.length === 0) {
    return { split: false, nodeClass: 'inherit', descClass: 'inherit' };
  }

  const d = decomposeEntries(entries);

  if (d.sameAsNode) {
    return { split: false, nodeClass: d.nodeState, descClass: d.nodeState };
  }

  return { split: true, nodeClass: d.nodeState, descClass: d.descState };
}
