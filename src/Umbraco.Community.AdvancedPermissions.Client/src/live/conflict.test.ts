import { describe, expect, it } from 'vitest';
import { classifyCell, type CellEntry } from './conflict.js';

/**
 * Builds one cell entry.
 * @param state Allow or Deny.
 * @param scope How far the entry reaches.
 * @returns The entry.
 */
function entry(state: 'Allow' | 'Deny', scope: 'ThisNodeOnly' | 'ThisNodeAndDescendants' = 'ThisNodeOnly'): CellEntry {
  return { state, scope, isPriorityOverride: false };
}

describe('classifyCell', () => {
  it('reports own-write when the server already holds what the editor holds', () => {
    // Asked first, and that order is load-bearing: while a save is landing all three comparisons
    // are true at once, and only this answer avoids reloading over a save still settling.
    expect(classifyCell({ base: [entry('Allow')], mine: [entry('Deny')], theirs: [entry('Deny')] }))
      .toBe('own-write');
  });

  it('reports no-change when the server still holds what was loaded', () => {
    expect(classifyCell({ base: [entry('Allow')], mine: [entry('Deny')], theirs: [entry('Allow')] }))
      .toBe('no-change');
  });

  it('reports refresh when the editor has not touched this cell', () => {
    expect(classifyCell({ base: [entry('Allow')], mine: [entry('Allow')], theirs: [entry('Deny')] }))
      .toBe('refresh');
  });

  it('reports conflict when both sides moved differently', () => {
    expect(classifyCell({ base: [entry('Allow')], mine: [entry('Deny')], theirs: [] }))
      .toBe('conflict');
  });

  it('ignores entry order', () => {
    const a = entry('Allow', 'ThisNodeOnly');
    const b = entry('Deny', 'ThisNodeAndDescendants');
    expect(classifyCell({ base: [a, b], mine: [a, b], theirs: [b, a] })).toBe('own-write');
  });

  it('treats an empty cell and a populated one as different', () => {
    expect(classifyCell({ base: [], mine: [], theirs: [entry('Allow')] })).toBe('refresh');
  });

  it('does nothing when a side has not loaded', () => {
    // A grid mid-load reaches here with undefined. Doing nothing is the only safe answer to a
    // question that cannot be asked.
    expect(classifyCell({ base: undefined, mine: [entry('Allow')], theirs: [] })).toBe('no-change');
  });

  it('distinguishes a priority override from an otherwise identical entry', () => {
    const plain: CellEntry = { state: 'Allow', scope: 'ThisNodeOnly', isPriorityOverride: false };
    const override: CellEntry = { state: 'Allow', scope: 'ThisNodeOnly', isPriorityOverride: true };
    expect(classifyCell({ base: [plain], mine: [plain], theirs: [override] })).toBe('refresh');
  });
});
