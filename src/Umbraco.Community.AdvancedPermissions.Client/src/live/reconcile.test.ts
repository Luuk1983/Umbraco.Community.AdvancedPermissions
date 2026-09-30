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

/** No verb on the node is currently flagged. */
const NONE: ReadonlySet<string> = new Set();

describe('reconcileNode', () => {
  it('flags a conflicting verb and withholds its baseline instead of adopting the fresh value', () => {
    const base = new Map([['Read', [entry('Allow')]]]);
    const pending = new Map([['Read', [entry('Deny')]]]);
    const theirs = new Map([['Read', []]]); // server cleared it — disagrees with both base and mine

    const result = reconcileNode({ base, pending, theirs, conflicted: NONE });

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

    const pass1 = reconcileNode({ base: pass1Base, pending, theirs: pass1Theirs, conflicted: NONE });
    expect(pass1.conflictedVerbs).toEqual(['Read']);
    expect(pass1.adoptStamp).toBe(false);

    // Second pass: base is exactly what pass 1 returned, the server reports the same thing again
    // (nobody touched it a second time), and the editor still has not resolved anything.
    const pass2 = reconcileNode({
      base: pass1.nextBase,
      pending,
      theirs: pass1Theirs,
      conflicted: new Set(pass1.conflictedVerbs),
    });

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

    const result = reconcileNode({ base, pending, theirs, conflicted: NONE });

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

    const result = reconcileNode({ base, pending: undefined, theirs, conflicted: NONE });

    expect(result.conflictedVerbs).toEqual([]);
    expect(result.adoptStamp).toBe(true);
    expect(result.nextBase.get('Read')).toEqual([entry('Deny')]);
    expect(result.nextBase.get('Write')).toEqual([entry('Allow')]);
  });
  it('flags a cell when the server lands on exactly the value the editor has pending', () => {
    // Scenario 1 from the two-window reproduction. Base X, ours Y pending, and somebody else
    // saves Y. The editor cannot tell that from its own save by looking at the value, and a
    // permission cell has about seven values, so it is not a rare coincidence. Flag it.
    const base = new Map([['Read', [entry('Allow')]]]);
    const pending = new Map([['Read', [entry('Deny')]]]);
    const theirs = new Map([['Read', [entry('Deny')]]]);

    const result = reconcileNode({ base, pending, theirs, conflicted: NONE });

    expect(result.conflictedVerbs).toEqual(['Read']);
    expect(result.adoptStamp).toBe(false);
    expect(result.nextBase.get('Read')).toEqual([entry('Allow')]);
  });

  it('keeps a conflict flagged when the server is later reverted to what was loaded', () => {
    // Scenario 2 from the two-window reproduction. Pass 1: base X, ours Y, theirs Z is a
    // conflict. Pass 2: the other window changes the cell back to X. On its own that classifies
    // as no-change, but somebody did write here and nobody has acted on it, so the warning must
    // not disappear by itself. Pass 1's output is fed back in as the sticky set, as the editor does.
    const base = new Map([['Read', [entry('Allow')]]]);
    const pending = new Map([['Read', [entry('Deny')]]]);

    const pass1 = reconcileNode({
      base,
      pending,
      theirs: new Map([['Read', [entry('Allow', 'ThisNodeAndDescendants')]]]),
      conflicted: NONE,
    });
    expect(pass1.conflictedVerbs).toEqual(['Read']);
    expect(pass1.adoptStamp).toBe(false);

    const pass2 = reconcileNode({
      base: pass1.nextBase,
      pending,
      theirs: base, // back to what was originally loaded
      conflicted: new Set(pass1.conflictedVerbs),
    });

    expect(pass2.conflictedVerbs).toEqual(['Read']);
    expect(pass2.adoptStamp).toBe(false);
    expect(pass2.nextBase.get('Read')).toEqual([entry('Allow')]);
  });

  it('keeps a conflict flagged when the server later lands on the editor pending value', () => {
    const base = new Map([['Read', [entry('Allow')]]]);
    const pending = new Map([['Read', [entry('Deny')]]]);

    const pass1 = reconcileNode({ base, pending, theirs: new Map([['Read', []]]), conflicted: NONE });
    const pass2 = reconcileNode({
      base: pass1.nextBase,
      pending,
      theirs: new Map([['Read', [entry('Deny')]]]),
      conflicted: new Set(pass1.conflictedVerbs),
    });

    expect(pass2.conflictedVerbs).toEqual(['Read']);
    expect(pass2.adoptStamp).toBe(false);
  });

  it('withholds the stamp while any sticky verb remains, even if every other verb is clean', () => {
    const base = new Map([
      ['Read', [entry('Allow')]],
      ['Write', [entry('Allow')]],
    ]);
    const pending = new Map([['Read', [entry('Deny')]]]);
    const theirs = new Map([
      ['Read', [entry('Allow')]], // reverted, but flagged earlier
      ['Write', [entry('Deny')]], // moved, nothing of ours: adopted
    ]);

    const result = reconcileNode({ base, pending, theirs, conflicted: new Set(['Read']) });

    expect(result.conflictedVerbs).toEqual(['Read']);
    expect(result.adoptStamp).toBe(false);
    expect(result.nextBase.get('Read')).toEqual([entry('Allow')]);
    expect(result.nextBase.get('Write')).toEqual([entry('Deny')]);
  });

  it('does not flag a verb the editor has no pending change for and was not flagged before', () => {
    const base = new Map([['Read', [entry('Allow')]]]);
    const result = reconcileNode({
      base,
      pending: undefined,
      theirs: new Map([['Read', [entry('Deny')]]]),
      conflicted: NONE,
    });

    expect(result.conflictedVerbs).toEqual([]);
    expect(result.adoptStamp).toBe(true);
  });
});
