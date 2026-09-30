import { describe, expect, it } from 'vitest';
import { pruneCollapsedChildren } from './tree-cache.js';

/** A minimal tree node for the tests. */
interface TestNode {
  key: string;
  expanded?: boolean;
  children?: TestNode[];
}

/**
 * Builds a node.
 * @param key The node's key.
 * @param expanded Whether it is expanded.
 * @param children Its cached children, if any.
 * @returns The node.
 */
function node(key: string, expanded: boolean, children?: TestNode[]): TestNode {
  return children ? { key, expanded, children } : { key, expanded };
}

const NO_WORK = (): boolean => false;

describe('pruneCollapsedChildren', () => {
  it('drops the cached children of a collapsed node, so re-expanding refetches them', () => {
    const result = pruneCollapsedChildren([node('a', false, [node('a1', false)])], NO_WORK);

    expect(result[0]).toEqual({ key: 'a', expanded: false });
    expect('children' in result[0]!).toBe(false);
  });

  it('keeps the cached children of an expanded node', () => {
    const tree = [node('a', true, [node('a1', false)])];

    expect(pruneCollapsedChildren(tree, NO_WORK)).toBe(tree);
  });

  it('prunes a collapsed node below an expanded one', () => {
    const result = pruneCollapsedChildren([node('a', true, [node('b', false, [node('b1', false)])])], NO_WORK);

    expect(result[0]!.children![0]).toEqual({ key: 'b', expanded: false });
  });

  it('prunes a collapsed node even when it sits below an expanded node under a collapsed one', () => {
    // 'a' is collapsed, so everything cached under it goes, including the expanded 'b'.
    const result = pruneCollapsedChildren([node('a', false, [node('b', true, [node('b1', false)])])], NO_WORK);

    expect('children' in result[0]!).toBe(false);
  });

  it('keeps a collapsed subtree that holds an unsaved edit, so the edit is not orphaned', () => {
    // Dropping it would leave a pending change for a node the tree can no longer find, and the
    // save would skip it without saying so.
    const tree = [node('a', false, [node('a1', false, [node('a11', false)])])];

    expect(pruneCollapsedChildren(tree, (key) => key === 'a11')).toBe(tree);
  });

  it('keeps the whole path down to held work but still prunes its untouched siblings', () => {
    const tree = [node('a', false, [node('a1', false, [node('a11', false)]), node('a2', false, [node('a21', false)])])];

    const result = pruneCollapsedChildren(tree, (key) => key === 'a11');

    const a = result[0]!;
    expect(a.children!.map((c) => c.key)).toEqual(['a1', 'a2']);
    expect(a.children![0]!.children!.map((c) => c.key)).toEqual(['a11']);
    expect('children' in a.children![1]!).toBe(false);
  });

  it('returns the same array when there is nothing to prune', () => {
    const tree = [node('a', false), node('b', true, [node('b1', false)])];

    expect(pruneCollapsedChildren(tree, NO_WORK)).toBe(tree);
  });

  it('does not mutate its input', () => {
    const tree = [node('a', false, [node('a1', false)])];

    pruneCollapsedChildren(tree, NO_WORK);

    expect(tree[0]!.children).toHaveLength(1);
  });
});
