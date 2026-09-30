import type { PermissionEntry } from '../models/permission.models.js';
import type { AnyTreeNode } from '../utils/tree-ops.js';

/** A node's freshly-read entries together with the stamp that describes exactly those entries. */
export interface FreshRead {
  /** The entries as the server holds them. */
  entries: PermissionEntry[];
  /** The concurrency stamp of `entries`. */
  stamp: string;
}

/**
 * Swaps freshly-read entries and stamps into a tree in one step, for the nodes the read covers.
 *
 * A reload used to blank every node's entries in place and refetch them, adopting stamps along the
 * way. If the refetch failed, the grid was left showing everything inherited beside stamps that were
 * valid, and the next save on any node rebuilt its payload from the blanked entries under a matching
 * stamp, wiping the node's other verbs. Doing the swap here, after the whole read has landed, means
 * there is no window in which entries and stamps disagree: a failed read never reaches this function
 * and leaves the tree exactly as it was.
 *
 * A node the read has no entry for is left untouched: the read has no opinion on it. Immutable, and
 * `children` are walked wherever a node has them.
 * @template T A tree node carrying entries and a stamp.
 * @param nodes The tree.
 * @param fresh The freshly-read entries and stamp per node key.
 * @returns A new tree with the fresh values applied.
 */
export function applyFreshReads<T extends AnyTreeNode & { entries: PermissionEntry[]; stamp: string }>(
  nodes: T[],
  fresh: ReadonlyMap<string, FreshRead>,
): T[] {
  return nodes.map((node) => {
    const read = fresh.get(node.key);
    const children = node.children as T[] | undefined;
    return {
      ...node,
      ...(read ? { entries: read.entries, stamp: read.stamp } : {}),
      ...(children ? { children: applyFreshReads(children, fresh) } : {}),
    };
  });
}
