import { describe, expect, it } from 'vitest';
import { reconcileNode } from './reconcile.js';
import type { CellEntry } from './conflict.js';

/**
 * Builds one cell entry.
 * @param state Allow or Deny.
 * @param scope How far the entry reaches.
 * @returns The entry.
 */
function entry(state: 'Allow' | 'Deny', scope: 'ThisNodeOnly' | 'ThisNodeAndDescendants' = 'ThisNodeOnly'): CellEntry {
  return { state, scope, isPriorityOverride: false };
}

describe('reconcileNode', () => {
  it('flags a conflicting verb and withholds its baseline instead of adopting the fresh value', () => {
    const base = new Map([['Read', [entry('Allow')]]]);
    const pending = new Map([['Read', [entry('Deny')]]]);
    const theirs = new Map([['Read', []]]); // server cleared it — disagrees with both base and mine

    const result = reconcileNode({ base, pending, theirs });

    expect(result.conflictedVerbs).toEqual(['Read']);
    expect(result.adoptStamp).toBe(false);
    // Withheld, not adopted: the next pass must still see the pre-collision value as `base`.
    expect(result.nextBase.get('Read')).toEqual([entry('Allow')]);
  });

  it('keeps flagging a conflict across a later, unrelated event where the server did not change again', () => {
    // This is the scenario that reopens the bug this module exists to prevent: the live-events
    // controller reconciles every loaded node on every event, regardless of which key changed.
    // If `base` had advanced to `theirs` after the first pass, a second, unrelated event would
    // see `theirs === base` and `classifyCell` would answer `no-change` before ever looking at
    // `mine` — silently dropping a conflict nobody has resolved, and letting the node's stamp
    // adopt as if nothing were wrong.
    const pending = new Map([['Read', [entry('Deny')]]]);
    const pass1Base = new Map([['Read', [entry('Allow')]]]);
    const pass1Theirs = new Map([['Read', []]]);

    const pass1 = reconcileNode({ base: pass1Base, pending, theirs: pass1Theirs });
    expect(pass1.conflictedVerbs).toEqual(['Read']);
    expect(pass1.adoptStamp).toBe(false);

    // Second pass: base is exactly what pass 1 returned, the server reports the same thing again
    // (nobody touched it a second time), and the editor still has not resolved anything.
    const pass2 = reconcileNode({ base: pass1.nextBase, pending, theirs: pass1Theirs });

    expect(pass2.conflictedVerbs).toEqual(['Read']);
    expect(pass2.adoptStamp).toBe(false);
    expect(pass2.nextBase.get('Read')).toEqual([entry('Allow')]);
  });

  it('adopts the server value for a verb that did not conflict, alongside one that did', () => {
    const base = new Map([
      ['Read', [entry('Allow')]],
      ['Write', [entry('Deny')]],
    ]);
    const pending = new Map([
      ['Read', [entry('Deny')]], // will conflict
      // 'Write' has no pending entry — the editor never touched it.
    ]);
    const theirs = new Map([
      ['Read', []], // moved, disagrees with the editor's pending Deny
      ['Write', [entry('Allow')]], // moved too, but nothing of the editor's to lose
    ]);

    const result = reconcileNode({ base, pending, theirs });

    expect(result.conflictedVerbs).toEqual(['Read']);
    expect(result.adoptStamp).toBe(false);
    expect(result.nextBase.get('Read')).toEqual([entry('Allow')]); // withheld
    expect(result.nextBase.get('Write')).toEqual([entry('Allow')]); // adopted
  });

  it('adopts everything on a clean node with no pending changes at all', () => {
    const base = new Map([
      ['Read', [entry('Allow')]],
      ['Write', []],
    ]);
    const theirs = new Map([
      ['Read', [entry('Deny')]],
      ['Write', [entry('Allow')]],
    ]);

    const result = reconcileNode({ base, pending: undefined, theirs });

    expect(result.conflictedVerbs).toEqual([]);
    expect(result.adoptStamp).toBe(true);
    expect(result.nextBase.get('Read')).toEqual([entry('Deny')]);
    expect(result.nextBase.get('Write')).toEqual([entry('Allow')]);
  });
});
