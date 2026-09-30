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
  it('reports conflict when the server holds the same value the editor holds, because both sides moved', () => {
    // Value equality is not authorship. `ServerEvent` carries no identity, and a permission cell
    // has only about seven possible values, so a colleague landing on exactly the value this
    // editor has pending is routine, not a coincidence to be waved through. Somebody else wrote
    // to a cell being edited here; that is worth saying whichever value they picked.
    expect(classifyCell({ base: [entry('Allow')], mine: [entry('Deny')], theirs: [entry('Deny')] }))
      .toBe('conflict');
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
    // Same cell in a different order: nothing moved.
    expect(classifyCell({ base: [a, b], mine: [a, b], theirs: [b, a] })).toBe('no-change');
  });

  it('treats an empty cell and a populated one as different', () => {
    expect(classifyCell({ base: [], mine: [], theirs: [entry('Allow')] })).toBe('refresh');
  });

  it('reports no-change when the server was reverted to what was loaded, even with a pending change', () => {
    // The stored value is what this editor loaded, so there is nothing new to say about the cell.
    // Whether an earlier conflict on it survives is decided by the caller, not here.
    expect(classifyCell({ base: [entry('Allow')], mine: [entry('Deny')], theirs: [entry('Allow')] }))
      .toBe('no-change');
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
