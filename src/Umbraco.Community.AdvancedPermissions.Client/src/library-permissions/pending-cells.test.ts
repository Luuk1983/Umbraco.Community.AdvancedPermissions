import { describe, expect, it } from 'vitest';
import type { CellEntry } from '../live/conflict.js';
import { sameCell, staleRefreshVerbs } from './pending-cells.js';

/**
 * Builds a cell entry.
 * @param state Allow or Deny.
 * @param scope How far the entry reaches.
 * @param isPriorityOverride Whether the entry is a priority override.
 * @returns The entry.
 */
function entry(
  state: CellEntry['state'],
  scope: CellEntry['scope'] = 'ThisNodeAndDescendants',
  isPriorityOverride = false,
): CellEntry {
  return { state, scope, isPriorityOverride };
}

describe('sameCell', () => {
  it('treats two empty cells as the same, so applying "inherit" over nothing stored is not an edit', () => {
    expect(sameCell([], [])).toBe(true);
  });

  it('treats identical entries as the same, so applying the stored value is not an edit', () => {
    expect(sameCell([entry('Allow')], [entry('Allow')])).toBe(true);
  });

  it('ignores the order entries arrive in', () => {
    const a = [entry('Allow', 'ThisNodeOnly'), entry('Deny', 'DescendantsOnly')];
    const b = [entry('Deny', 'DescendantsOnly'), entry('Allow', 'ThisNodeOnly')];

    expect(sameCell(a, b)).toBe(true);
  });

  it('tells Allow from Deny', () => {
    expect(sameCell([entry('Allow')], [entry('Deny')])).toBe(false);
  });

  it('tells one scope from another', () => {
    expect(sameCell([entry('Allow', 'ThisNodeOnly')], [entry('Allow', 'DescendantsOnly')])).toBe(false);
  });

  it('tells a priority override from an ordinary entry', () => {
    expect(sameCell([entry('Deny', 'ThisNodeOnly', true)], [entry('Deny', 'ThisNodeOnly', false)])).toBe(false);
  });

  it('tells a stored value from nothing stored', () => {
    expect(sameCell([entry('Allow')], [])).toBe(false);
  });

  it('does not collapse two entries into one', () => {
    expect(sameCell([entry('Allow'), entry('Allow')], [entry('Allow')])).toBe(false);
  });
});

describe('staleRefreshVerbs', () => {
  const VERB = 'Umb.Element.Read';
  const cells = (entries: CellEntry[]): Map<string, ReadonlyArray<CellEntry>> => new Map([[VERB, entries]]);
  const noFlags = new Set<string>();

  it('marks a pending entry equal to the baseline as stale once the server has moved', () => {
    // The user "applied" what was already stored; a colleague then changed it. Adopting the
    // colleague's value while keeping this entry would send the old value back under a fresh stamp.
    const result = staleRefreshVerbs({
      base: cells([entry('Allow')]),
      pending: cells([entry('Allow')]),
      theirs: cells([entry('Deny')]),
      flagged: noFlags,
    });

    expect(result).toEqual([VERB]);
  });

  it('keeps a real edit that the server has not touched', () => {
    const result = staleRefreshVerbs({
      base: cells([entry('Allow')]),
      pending: cells([entry('Deny')]),
      theirs: cells([entry('Allow')]),
      flagged: noFlags,
    });

    expect(result).toEqual([]);
  });

  it('keeps a real edit the server has also moved, because that is a conflict for the user to resolve', () => {
    const result = staleRefreshVerbs({
      base: cells([entry('Allow')]),
      pending: cells([entry('Deny')]),
      theirs: cells([entry('Allow', 'ThisNodeOnly')]),
      flagged: new Set([VERB]),
    });

    expect(result).toEqual([]);
  });

  it('never drops a flagged verb, whatever the verdict would have been', () => {
    const result = staleRefreshVerbs({
      base: cells([entry('Allow')]),
      pending: cells([entry('Allow')]),
      theirs: cells([entry('Deny')]),
      flagged: new Set([VERB]),
    });

    expect(result).toEqual([]);
  });

  it('leaves an equal pending entry alone while the server has not moved', () => {
    // Nothing to reconcile yet; sameCell stops such an entry being recorded in the first place.
    const result = staleRefreshVerbs({
      base: cells([entry('Allow')]),
      pending: cells([entry('Allow')]),
      theirs: cells([entry('Allow')]),
      flagged: noFlags,
    });

    expect(result).toEqual([]);
  });
});
