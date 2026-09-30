import { describe, expect, it } from 'vitest';
import { reconcileNode } from './reconcile.js';
import type { CellEntry } from './conflict.js';

/**
 * Builds one cell entry.
 * @param state Allow or Deny.
 * @returns The entry.
 */
function entry(state: 'Allow' | 'Deny'): CellEntry {
  return { state, scope: 'ThisNodeOnly', isPriorityOverride: false };
}

/**
 * These pin the contract the editors' "Load stored values" depends on. Resolving a conflict drops
 * the pending change and clears the flags, then re-reads the server; the frozen baseline must come
 * out of that re-read carrying the server's value, and the node's stamp must be allowed to follow
 * it. If either half stopped being true, entries and stamp would drift apart and the next save
 * would write a stale value under a stamp the server accepts.
 */
describe('reconcileNode after a conflict is resolved', () => {
  it('replaces the frozen baseline with the stored value and lets the stamp follow', () => {
    // Loaded as Allow; somebody else then wrote Deny while the user was editing, so the baseline stayed frozen at Allow.
    const frozenBase = new Map([['Read', [entry('Allow')]]]);
    const theirs = new Map([['Read', [entry('Deny')]]]);

    // The user chose "Load stored values": pending dropped, flag cleared.
    const result = reconcileNode({ base: frozenBase, pending: undefined, theirs, conflicted: new Set() });

    expect(result.nextBase.get('Read')).toEqual([entry('Deny')]);
    expect(result.conflictedVerbs).toEqual([]);
    expect(result.adoptStamp).toBe(true);
  });

  it('still holds the stamp back when another verb on the node is in dispute', () => {
    const base = new Map([
      ['Read', [entry('Allow')]],
      ['Write', [entry('Allow')]],
    ]);
    // Read was resolved (no pending); Write is still being edited and the server moved it too.
    const pending = new Map([['Write', [entry('Deny')]]]);
    const theirs = new Map([
      ['Read', [entry('Deny')]],
      ['Write', []],
    ]);

    const result = reconcileNode({ base, pending, theirs, conflicted: new Set() });

    expect(result.nextBase.get('Read')).toEqual([entry('Deny')]);
    expect(result.nextBase.get('Write')).toEqual([entry('Allow')]);
    expect(result.conflictedVerbs).toEqual(['Write']);
    expect(result.adoptStamp).toBe(false);
  });
});
