import type { AnyTreeNode } from '../utils/tree-ops.js';

/**
 * Whether any node in the given subtree is one the caller says is holding something that must
 * not be thrown away.
 * @param nodes The subtree's nodes.
 * @param holdsWork Tells whether a node, by key, holds unsaved work.
 * @returns `true` if a node in the subtree, at any depth, holds work.
 */
function subtreeHoldsWork<T extends AnyTreeNode>(nodes: T[], holdsWork: (key: string) => boolean): boolean {
  return nodes.some((n) => holdsWork(n.key) || subtreeHoldsWork((n.children ?? []) as T[], holdsWork));
}

/**
 * Drops the cached children of every collapsed node, so expanding it again fetches them fresh.
 *
 * A tree that reloads only what is on screen leaves the children of a collapsed node behind, still
 * showing whatever they held when it was collapsed. Expanding a node reuses those children when it
 * has them, so the user would see values, and would edit against stamps, that predate every
 * change since — or, after a user-group switch blanks entries, no values at all next to stamps that
 * still describe the previous group. Removing the cache is the one fix that cannot be wrong: the
 * next expand asks the server, which is the same request a first expand makes.
 *
 * The exception is a subtree holding unsaved work. Removing a node the user has an edit on would
 * leave that edit pointing at a node the tree can no longer find, and a save skips such an edit
 * without saying so. Those subtrees are kept whole (down to the held node, at any depth), and the
 * caller is expected to refresh them along with the expanded ones; siblings without work are still
 * pruned.
 *
 * Immutable, and the input array is returned untouched when there was nothing to prune, so a
 * caller can assign the result to reactive state without provoking a render for nothing.
 * @template T A tree node type extending {@link AnyTreeNode}.
 * @param nodes The tree.
 * @param holdsWork Tells whether a node, by key, holds unsaved work (an edit or a flagged cell).
 * @returns The tree without the stale caches.
 */
export function pruneCollapsedChildren<T extends AnyTreeNode>(nodes: T[], holdsWork: (key: string) => boolean): T[] {
  let changed = false;

  const next = nodes.map((node) => {
    const children = node.children as T[] | undefined;
    if (!children) return node;

    if (!node.expanded && !subtreeHoldsWork(children, holdsWork)) {
      changed = true;
      // Rest-destructured rather than set to `undefined`: `children` is optional under
      // `exactOptionalPropertyTypes`, so absent and undefined are different things.
      const { children: _dropped, ...rest } = node;
      return rest as T;
    }

    const pruned = pruneCollapsedChildren(children, holdsWork);
    if (pruned === children) return node;

    changed = true;
    return { ...node, children: pruned } as T;
  });

  return changed ? next : nodes;
}
