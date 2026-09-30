import { describe, expect, it } from 'vitest';
import { planSaveConflicts, type SaveConflictNode } from './save-conflicts.js';
import type { CellEntry } from './conflict.js';

/**
 * Builds one cell entry.
 * @param state Allow or Deny.
 * @param scope How far the entry reaches.
 * @returns The entry.
 */
function entry(state: 'Allow' | 'Deny', scope: CellEntry['scope'] = 'ThisNodeOnly'): CellEntry {
  return { state, scope, isPriorityOverride: false };
}

/**
 * Builds a refused node with sensible defaults, overridable per test.
 * @param overrides The parts a test cares about.
 * @returns The node.
 */
function node(overrides: Partial<SaveConflictNode>): SaveConflictNode {
  return {
    key: 'n1',
    base: new Map(),
    pending: undefined,
    theirs: new Map(),
    flagged: new Set(),
    heldStamp: 'held',
    currentStamp: 'current',
    ...overrides,
  };
}

describe('planSaveConflicts', () => {
  it('finds no conflict when the colleague changed a verb the user did not touch, and says to retry', () => {
    // I5: the user edited Read; a colleague changed Write on the same node. The server refuses the
    // node as a whole, but nothing the user edited moved. Flagging Read would make the dialog claim
    // a conflict with nothing stored differently, and "Load stored values" would then discard an
    // edit nobody contested.
    const plan = planSaveConflicts([
      node({
        base: new Map([['Read', [entry('Allow')]], ['Write', [entry('Allow')]]]),
        pending: new Map([['Read', [entry('Deny')]]]),
        theirs: new Map([['Read', [entry('Allow')]], ['Write', [entry('Deny')]]]),
      }),
    ]);

    expect(plan.nodes[0]?.conflictedVerbs).toEqual([]);
    expect(plan.retry).toBe(true);
    expect(plan.nodes[0]?.nextBase.get('Write')).toEqual([entry('Deny')]);
    expect(plan.nodes[0]?.adoptedStamp).toBe('current');
  });

  it('flags only the verb that genuinely moved on both sides', () => {
    const plan = planSaveConflicts([
      node({
        base: new Map([['Read', [entry('Allow')]], ['Write', [entry('Allow')]]]),
        pending: new Map([['Read', [entry('Deny')]], ['Write', [entry('Deny')]]]),
        theirs: new Map([['Read', [entry('Allow')]], ['Write', []]]),
      }),
    ]);

    expect(plan.nodes[0]?.conflictedVerbs).toEqual(['Write']);
    expect(plan.retry).toBe(false);
    // A flagged verb holds the node's stamp back, so a plain save of it is refused again.
    expect(plan.nodes[0]?.adoptedStamp).toBeUndefined();
  });

  it('asks for a dialog when any node in the batch has a genuine conflict, and still refreshes the clean ones', () => {
    const plan = planSaveConflicts([
      node({
        key: 'contested',
        base: new Map([['Read', [entry('Allow')]]]),
        pending: new Map([['Read', [entry('Deny')]]]),
        theirs: new Map([['Read', []]]),
      }),
      node({
        key: 'clean',
        base: new Map([['Read', [entry('Allow')]], ['Write', [entry('Allow')]]]),
        pending: new Map([['Read', [entry('Deny')]]]),
        theirs: new Map([['Read', [entry('Allow')]], ['Write', [entry('Deny')]]]),
        currentStamp: 'clean-current',
      }),
    ]);

    expect(plan.retry).toBe(false);
    expect(plan.nodes.find((n) => n.key === 'contested')?.conflictedVerbs).toEqual(['Read']);
    const clean = plan.nodes.find((n) => n.key === 'clean');
    expect(clean?.conflictedVerbs).toEqual([]);
    expect(clean?.adoptedStamp).toBe('clean-current');
  });

  it('keeps a verb flagged from an earlier pass flagged, even though its values now agree', () => {
    const plan = planSaveConflicts([
      node({
        base: new Map([['Read', [entry('Allow')]]]),
        pending: new Map([['Read', [entry('Deny')]]]),
        theirs: new Map([['Read', [entry('Allow')]]]),
        flagged: new Set(['Read']),
      }),
    ]);

    expect(plan.nodes[0]?.conflictedVerbs).toEqual(['Read']);
    expect(plan.retry).toBe(false);
  });

  it('reports a pending entry equal to the baseline as stale, and does not treat it as a conflict', () => {
    const plan = planSaveConflicts([
      node({
        base: new Map([['Read', [entry('Allow')]]]),
        pending: new Map([['Read', [entry('Allow')]]]),
        theirs: new Map([['Read', [entry('Deny')]]]),
      }),
    ]);

    expect(plan.nodes[0]?.stalePendingVerbs).toEqual(['Read']);
    expect(plan.nodes[0]?.conflictedVerbs).toEqual([]);
    expect(plan.nodes[0]?.nextBase.get('Read')).toEqual([entry('Deny')]);
    expect(plan.retry).toBe(true);
  });

  it('does not retry when the server sent back the very stamp the editor already held', () => {
    // A retry under the same stamp would be refused the same way, forever. That can only be a
    // server that is refusing a save it should have accepted, and looping on it helps nobody.
    const plan = planSaveConflicts([
      node({
        base: new Map([['Read', [entry('Allow')]]]),
        pending: new Map([['Read', [entry('Deny')]]]),
        theirs: new Map([['Read', [entry('Allow')]]]),
        heldStamp: 'same',
        currentStamp: 'same',
      }),
    ]);

    expect(plan.nodes[0]?.conflictedVerbs).toEqual([]);
    expect(plan.retry).toBe(false);
    expect(plan.stalled).toBe(true);
  });

  it('is not stalled when there is a conflict to show', () => {
    const plan = planSaveConflicts([
      node({
        base: new Map([['Read', [entry('Allow')]]]),
        pending: new Map([['Read', [entry('Deny')]]]),
        theirs: new Map([['Read', []]]),
        heldStamp: 'same',
        currentStamp: 'same',
      }),
    ]);

    expect(plan.stalled).toBe(false);
    expect(plan.retry).toBe(false);
  });

  it('returns an empty plan that retries nothing for an empty conflict list', () => {
    const plan = planSaveConflicts([]);

    expect(plan.nodes).toEqual([]);
    expect(plan.retry).toBe(false);
    expect(plan.stalled).toBe(true);
  });
});
