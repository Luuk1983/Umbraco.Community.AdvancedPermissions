import type { PermissionState, PermissionScope } from '../models/permission.models.js';
import { decomposeEntries } from './decompose-entries.js';

/**
 * Visual layout description for a permission cell.
 *
 * - `split=false` → render one uniform block with class `nodeClass`.
 * - `split=true` → render two halves: left=`nodeClass`, right=`descClass`.
 *
 * `nodeOverride`/`descOverride` mark each side as priority-override. When set, that side is
 * themed gold (the allow/deny colour is replaced; the ✓/✗ icon still conveys the state).
 */
export interface CellInfo {
  split: boolean;
  nodeClass: 'allow' | 'deny' | 'inherit';
  descClass: 'allow' | 'deny' | 'inherit';
  nodeOverride?: boolean;
  descOverride?: boolean;
}

/**
 * Computes the cell rendering shape for a set of stored entries.
 *
 * @param entries The stored entries (after filtering to the relevant verb).
 * @returns A `CellInfo` describing whether to render uniform or split, which classes to use,
 *          and which side(s) carry the priority-override flag.
 */
export function getCellInfo(
  entries: ReadonlyArray<{ state: PermissionState; scope: PermissionScope; isPriorityOverride?: boolean }>,
): CellInfo {
  if (entries.length === 0) {
    return { split: false, nodeClass: 'inherit', descClass: 'inherit' };
  }

  const d = decomposeEntries(entries);

  if (d.sameAsNode) {
    return {
      split: false,
      nodeClass: d.nodeState,
      descClass: d.nodeState,
      nodeOverride: d.nodeIsPriorityOverride,
      descOverride: d.nodeIsPriorityOverride,
    };
  }

  return {
    split: true,
    nodeClass: d.nodeState,
    descClass: d.descState,
    nodeOverride: d.nodeIsPriorityOverride,
    descOverride: d.descIsPriorityOverride,
  };
}
