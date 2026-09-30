import { describe, expect, it } from 'vitest';
import { applyFreshReads } from './apply-fresh.js';
import type { PermissionEntry } from '../models/permission.models.js';

/** The node shape the editors hold, reduced to what the function touches. */
interface Node {
  key: string;
  name: string;
  entries: PermissionEntry[];
  stamp: string;
  children?: Node[];
}

/**
 * Builds a stored entry.
 * @param nodeKey The node it belongs to.
 * @returns The entry.
 */
function stored(nodeKey: string): PermissionEntry {
  return { id: `${nodeKey}-1`, nodeKey, roleAlias: 'editors', verb: 'Read', state: 'Allow', scope: 'ThisNodeOnly', isPriorityOverride: false };
}

describe('applyFreshReads', () => {
  it('replaces entries and stamp together, for a node at any depth', () => {
    const tree: Node[] = [
      { key: 'a', name: 'A', entries: [], stamp: 'old-a', children: [{ key: 'b', name: 'B', entries: [], stamp: 'old-b' }] },
    ];
    const fresh = new Map([
      ['a', { entries: [stored('a')], stamp: 'new-a' }],
      ['b', { entries: [stored('b')], stamp: 'new-b' }],
    ]);

    const next = applyFreshReads(tree, fresh);

    expect(next[0]?.entries).toEqual([stored('a')]);
    expect(next[0]?.stamp).toBe('new-a');
    expect(next[0]?.children?.[0]?.entries).toEqual([stored('b')]);
    expect(next[0]?.children?.[0]?.stamp).toBe('new-b');
  });

  it('leaves a node the read had nothing for exactly as it was, rather than blanking it', () => {
    // I1b: nothing may be blanked before the read is known to have worked, and a node the read does
    // not mention is not one it has an opinion on.
    const tree: Node[] = [{ key: 'a', name: 'A', entries: [stored('a')], stamp: 'old-a' }];

    const next = applyFreshReads(tree, new Map());

    expect(next[0]).toEqual(tree[0]);
  });

  it('keeps everything else about a node, including its children when the read has none for them', () => {
    const tree: Node[] = [{ key: 'a', name: 'Alpha', entries: [], stamp: 's', children: [{ key: 'b', name: 'B', entries: [], stamp: 'sb' }] }];

    const next = applyFreshReads(tree, new Map([['a', { entries: [stored('a')], stamp: 'new' }]]));

    expect(next[0]?.name).toBe('Alpha');
    expect(next[0]?.children).toEqual(tree[0]?.children);
  });

  it('does not modify the tree it was given', () => {
    const tree: Node[] = [{ key: 'a', name: 'A', entries: [], stamp: 'old' }];

    applyFreshReads(tree, new Map([['a', { entries: [stored('a')], stamp: 'new' }]]));

    expect(tree[0]?.stamp).toBe('old');
    expect(tree[0]?.entries).toEqual([]);
  });
});
