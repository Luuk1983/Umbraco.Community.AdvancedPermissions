import { describe, expect, it } from 'vitest';
import { withCellEdit, withVerbEdit, withoutVerbs } from './pending-changes.js';
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

describe('withVerbEdit', () => {
  it('records an edit that differs from what is stored', () => {
    const next = withVerbEdit(new Map(), 'n1', 'Read', [entry('Deny')], [entry('Allow')], false);

    expect(next.get('n1')?.get('Read')).toEqual([entry('Deny')]);
  });

  it('records nothing when the applied value equals what is stored, so no no-op edit can go stale', () => {
    // C2: Apply on a cell that already holds the value used to leave a pending entry equal to the
    // baseline, which a later refresh would silently write back over a colleague's change.
    const next = withVerbEdit(new Map(), 'n1', 'Read', [entry('Allow')], [entry('Allow')], false);

    expect(next.has('n1')).toBe(false);
  });

  it('drops an earlier edit when the user changes it and then changes it back', () => {
    const afterFirst = withVerbEdit(new Map(), 'n1', 'Read', [entry('Deny')], [entry('Allow')], false);
    const afterSecond = withVerbEdit(afterFirst, 'n1', 'Read', [entry('Allow')], [entry('Allow')], false);

    expect(afterSecond.has('n1')).toBe(false);
  });

  it('treats the same entries in a different order as equal', () => {
    const stored = [entry('Allow', 'ThisNodeOnly'), entry('Deny', 'DescendantsOnly')];
    const applied = [entry('Deny', 'DescendantsOnly'), entry('Allow', 'ThisNodeOnly')];

    expect(withVerbEdit(new Map(), 'n1', 'Read', applied, stored, false).has('n1')).toBe(false);
  });

  it('keeps the node when only one of its verbs is dropped', () => {
    const start = withVerbEdit(new Map(), 'n1', 'Write', [entry('Deny')], [entry('Allow')], false);
    const next = withVerbEdit(start, 'n1', 'Read', [entry('Allow')], [entry('Allow')], false);

    expect([...(next.get('n1')?.keys() ?? [])]).toEqual(['Write']);
  });

  it('keeps an edit equal to the stored value when the cell is flagged, because it is what the conflict is about', () => {
    // The baseline of a flagged cell is frozen at the pre-conflict value. Re-applying that value is
    // the user's choice to overwrite the colleague's, and the entry has to stay so the dialog can
    // say so. Dropping it would leave a flag with nothing behind it.
    const next = withVerbEdit(new Map(), 'n1', 'Read', [entry('Allow')], [entry('Allow')], true);

    expect(next.get('n1')?.get('Read')).toEqual([entry('Allow')]);
  });

  it('does not modify the map it was given', () => {
    const before = withVerbEdit(new Map(), 'n1', 'Read', [entry('Deny')], [entry('Allow')], false);
    withVerbEdit(before, 'n1', 'Read', [entry('Allow')], [entry('Allow')], false);

    expect(before.get('n1')?.get('Read')).toEqual([entry('Deny')]);
  });

  it('records an applied "inherit" (no entries) when something is stored, and drops it when nothing is', () => {
    expect(withVerbEdit(new Map(), 'n1', 'Read', [], [entry('Allow')], false).get('n1')?.get('Read')).toEqual([]);
    expect(withVerbEdit(new Map(), 'n1', 'Read', [], [], false).has('n1')).toBe(false);
  });
});

describe('withCellEdit', () => {
  it('records an edit that differs from what is stored', () => {
    const next = withCellEdit(new Map(), 'n1', [entry('Deny')], [entry('Allow')], false);

    expect(next.get('n1')).toEqual([entry('Deny')]);
  });

  it('records nothing when the applied value equals what is stored', () => {
    expect(withCellEdit(new Map(), 'n1', [entry('Allow')], [entry('Allow')], false).has('n1')).toBe(false);
  });

  it('drops an earlier edit when the user changes it back', () => {
    const first = withCellEdit(new Map(), 'n1', [entry('Deny')], [entry('Allow')], false);

    expect(withCellEdit(first, 'n1', [entry('Allow')], [entry('Allow')], false).has('n1')).toBe(false);
  });

  it('keeps an edit equal to the stored value when the cell is flagged', () => {
    expect(withCellEdit(new Map(), 'n1', [entry('Allow')], [entry('Allow')], true).get('n1')).toEqual([entry('Allow')]);
  });

  it('leaves other nodes alone and does not modify its input', () => {
    const before = new Map([['n2', [entry('Deny')]]]);
    const next = withCellEdit(before, 'n1', [entry('Allow')], [entry('Deny')], false);

    expect(next.get('n2')).toEqual([entry('Deny')]);
    expect(before.has('n1')).toBe(false);
  });
});

describe('withoutVerbs', () => {
  const start = (): ReturnType<typeof withVerbEdit<CellEntry>> => {
    const a = withVerbEdit(new Map(), 'n1', 'Read', [entry('Deny')], [entry('Allow')], false);
    const b = withVerbEdit(a, 'n1', 'Write', [entry('Deny')], [entry('Allow')], false);
    return withVerbEdit(b, 'n2', 'Read', [entry('Deny')], [entry('Allow')], false);
  };

  it('removes the named verbs and keeps the rest of the node', () => {
    const next = withoutVerbs(start(), 'n1', ['Read']);

    expect([...(next.get('n1')?.keys() ?? [])]).toEqual(['Write']);
    expect(next.has('n2')).toBe(true);
  });

  it('removes the node when its last pending verb goes', () => {
    const next = withoutVerbs(start(), 'n1', ['Read', 'Write']);

    expect(next.has('n1')).toBe(false);
    expect(next.has('n2')).toBe(true);
  });

  it('leaves everything alone for a verb or node that is not pending', () => {
    const next = withoutVerbs(start(), 'n3', ['Read']);

    expect([...next.keys()]).toEqual(['n1', 'n2']);
    expect([...(withoutVerbs(start(), 'n1', ['Publish']).get('n1')?.keys() ?? [])]).toEqual(['Read', 'Write']);
  });

  it('does not modify the map it was given', () => {
    const before = start();
    withoutVerbs(before, 'n1', ['Read']);

    expect(before.get('n1')?.has('Read')).toBe(true);
  });
});
