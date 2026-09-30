import { describe, expect, it } from 'vitest';
import { conflictKey, parseConflictKey, cellsEqual, isStalePending, EMPTY_SET_STAMP } from './doc-type-concurrency.js';
import type { CellEntry } from '../live/conflict.js';

/**
 * Builds one cell entry.
 * @param state Allow or Deny.
 * @param scope How far the entry reaches.
 * @returns The entry.
 */
function entry(state: 'Allow' | 'Deny', scope: CellEntry['scope'] = 'ThisNodeAndDescendants'): CellEntry {
  return { state, scope, isPriorityOverride: false };
}

describe('EMPTY_SET_STAMP', () => {
  it('is the SHA-256 of the empty string, which is what the server stamps an empty set with', async () => {
    const digest = await globalThis.crypto.subtle.digest('SHA-256', new Uint8Array());
    const hex = [...new Uint8Array(digest)].map((b) => b.toString(16).padStart(2, '0')).join('');

    expect(EMPTY_SET_STAMP).toBe(hex);
  });
});

describe('conflictKey', () => {
  it('differs for two document types on the same node and verb', () => {
    const a = conflictKey('node-1', '11111111-1111-1111-1111-111111111111', 'Umb.Document.CreateOfType');
    const b = conflictKey('node-1', '22222222-2222-2222-2222-222222222222', 'Umb.Document.CreateOfType');

    expect(a).not.toBe(b);
  });

  it('does not depend on the casing of the document type key', () => {
    expect(conflictKey('n', 'ABCDEF00-0000-0000-0000-000000000000', 'v')).toBe(
      conflictKey('n', 'abcdef00-0000-0000-0000-000000000000', 'v'),
    );
  });

  it('round-trips through parseConflictKey', () => {
    const key = conflictKey('virtual-root', 'ABCDEF00-0000-0000-0000-000000000000', 'Umb.Document.CreateOfType');

    expect(parseConflictKey(key)).toEqual({
      nodeKey: 'virtual-root',
      contentTypeKey: 'abcdef00-0000-0000-0000-000000000000',
      verb: 'Umb.Document.CreateOfType',
    });
  });
});

describe('cellsEqual', () => {
  it('treats the same entries in a different order as equal', () => {
    const a = [entry('Allow', 'ThisNodeOnly'), entry('Deny', 'DescendantsOnly')];
    const b = [entry('Deny', 'DescendantsOnly'), entry('Allow', 'ThisNodeOnly')];

    expect(cellsEqual(a, b)).toBe(true);
  });

  it('treats a different state as different', () => {
    expect(cellsEqual([entry('Allow')], [entry('Deny')])).toBe(false);
  });

  it('treats nothing stored and an entry as different', () => {
    expect(cellsEqual([], [entry('Allow')])).toBe(false);
  });
});

describe('isStalePending', () => {
  it('is true when the edit is the loaded value and the server has since moved', () => {
    // Applying the value a cell already had leaves a pending entry equal to the baseline; the
    // colleague then writes Deny. Nothing of the user's is at stake, so the entry must go.
    expect(isStalePending([entry('Allow')], [entry('Allow')], [entry('Deny')])).toBe(true);
  });

  it('is false when the edit differs from the loaded value, because that is a real conflict', () => {
    expect(isStalePending([entry('Allow')], [entry('Deny', 'ThisNodeOnly')], [entry('Deny')])).toBe(false);
  });

  it('is false when the server has not moved', () => {
    expect(isStalePending([entry('Allow')], [entry('Deny')], [entry('Allow')])).toBe(false);
  });
});
