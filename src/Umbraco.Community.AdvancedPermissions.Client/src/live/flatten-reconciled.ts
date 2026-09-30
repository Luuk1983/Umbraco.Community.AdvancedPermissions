import type { PermissionEntry } from '../models/permission.models.js';
import type { CellEntry } from './conflict.js';

/**
 * Flattens the per-verb merge that `reconcileNode` returns back into the flat entry list a tree
 * node holds, without losing stored entries the merge has no opinion on.
 *
 * The merge only carries the verbs the caller asked it to compare, which is the verbs the grid has
 * a column for. A stored entry for any other verb — a verb registered on the server that this
 * client build has no column for, or one that was unregistered while entries for it remained —
 * would vanish if the list were rebuilt from the merge alone. That is not cosmetic: a save rebuilds
 * a node's entire entry list from `node.entries`, so an entry dropped here is deleted from the
 * server by the next save of a node the user never touched that verb on. Those entries are carried
 * over from the fresh read, unchanged.
 *
 * "Has no opinion" means the verb has no key in the merge. A key mapped to an empty cell is an
 * opinion — nothing is stored for that verb, or the baseline is frozen empty for a conflicted one —
 * and the fresh read is not consulted for it.
 *
 * The synthesised `id` on entries built from the merge is display-only, the same convention the
 * editors use wherever they fabricate entries client-side; carried-over entries keep the server's.
 * @param nodeKey The API node key (the real key, or the virtual-root sentinel).
 * @param roleAlias The user group these entries belong to.
 * @param merged The per-verb entries to flatten, as returned in `ReconcileNodeResult.nextBase`.
 * @param freshEntries The node's freshly-read entries, the source for verbs `merged` lacks.
 * @returns The flat entry list.
 */
export function flattenReconciled(
  nodeKey: string,
  roleAlias: string,
  merged: ReadonlyMap<string, ReadonlyArray<CellEntry>>,
  freshEntries: ReadonlyArray<PermissionEntry>,
): PermissionEntry[] {
  const result: PermissionEntry[] = [];

  for (const [verb, cellEntries] of merged) {
    for (const e of cellEntries) {
      result.push({
        id: `${nodeKey}-${verb}-${result.length}`,
        nodeKey,
        roleAlias,
        verb,
        state: e.state,
        scope: e.scope,
        isPriorityOverride: e.isPriorityOverride,
      });
    }
  }

  for (const e of freshEntries) {
    if (!merged.has(e.verb)) result.push(e);
  }

  return result;
}
