/**
 * Minimal tree-node shape consumed by the tree utility helpers. Each editor extends this with
 * its own per-node data fields (entries, effective permissions, etc.); the helpers only touch
 * the structure fields below.
 */
export interface AnyTreeNode {
  key: string;
  expanded?: boolean;
  loading?: boolean;
  children?: this[];
}

/**
 * Immutably replaces the node with the given key by merging in the supplied changes.
 *
 * Walks the tree recursively until it finds a node whose `key` matches. The matched node is
 * replaced with a new object (so Lit detects the change), and the parent array is replaced as
 * well. Returns the new tree array. Returns the input array if no match was found.
 *
 * @template T A tree node type extending {@link AnyTreeNode}.
 * @param nodes The current tree.
 * @param key The key of the node to update.
 * @param changes Partial node to merge into the matched node.
 * @returns A new tree array reflecting the change, or the input array if no match was found.
 */
export function updateNode<T extends AnyTreeNode>(
  nodes: T[],
  key: string,
  changes: Partial<T>,
): T[] {
  let mutated = false;
  const next = nodes.map((node) => {
    if (node.key === key) {
      mutated = true;
      return { ...node, ...changes } as T;
    }
    if (node.children && node.children.length > 0) {
      const updatedChildren = updateNode(node.children as T[], key, changes);
      if (updatedChildren !== node.children) {
        mutated = true;
        return { ...node, children: updatedChildren } as T;
      }
    }
    return node;
  });
  return mutated ? next : nodes;
}

/**
 * Recursively searches a tree for a node with the given key.
 *
 * @template T A tree node type extending {@link AnyTreeNode}.
 * @param nodes The tree to search.
 * @param key The key to look for.
 * @returns The matched node, or `null` if not found.
 */
export function findNode<T extends AnyTreeNode>(nodes: T[], key: string): T | null {
  for (const node of nodes) {
    if (node.key === key) return node;
    if (node.children && node.children.length > 0) {
      const found = findNode(node.children as T[], key);
      if (found) return found;
    }
  }
  return null;
}
