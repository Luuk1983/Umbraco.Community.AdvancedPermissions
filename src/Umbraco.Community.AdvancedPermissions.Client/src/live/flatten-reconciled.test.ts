import { describe, expect, it } from 'vitest';
import { flattenReconciled } from './flatten-reconciled.js';
import type { CellEntry } from './conflict.js';
import type { PermissionEntry } from '../models/permission.models.js';

/**
 * Builds one cell entry.
 * @param state Allow or Deny.
 * @returns The entry.
 */
function cell(state: 'Allow' | 'Deny'): CellEntry {
  return { state, scope: 'ThisNodeOnly', isPriorityOverride: false };
}

/**
 * Builds one stored entry as the server would return it.
 * @param verb The verb the entry is for.
 * @param state Allow or Deny.
 * @returns The entry.
 */
function stored(verb: string, state: 'Allow' | 'Deny'): PermissionEntry {
  return { id: `server-${verb}`, nodeKey: 'node', roleAlias: 'editors', verb, state, scope: 'ThisNodeOnly', isPriorityOverride: false };
}

describe('flattenReconciled', () => {
  it('flattens the per-verb merge into entries for the node and user group', () => {
    const result = flattenReconciled('node', 'editors', new Map([['Read', [cell('Allow')]]]), []);

    expect(result).toHaveLength(1);
    expect(result[0]).toMatchObject({ nodeKey: 'node', roleAlias: 'editors', verb: 'Read', state: 'Allow' });
  });

  it('carries over stored entries for a verb the merge has no key for, instead of dropping them', () => {
    // A verb the grid has no column for — a save rebuilds the node from these entries, so losing
    // one here would delete it from the server the next time somebody saves the node.
    const result = flattenReconciled(
      'node',
      'editors',
      new Map([['Read', [cell('Allow')]]]),
      [stored('Read', 'Allow'), stored('Archive', 'Deny')],
    );

    expect(result.map((e) => e.verb).sort()).toEqual(['Archive', 'Read']);
    expect(result.find((e) => e.verb === 'Archive')).toMatchObject({ id: 'server-Archive', state: 'Deny' });
  });

  it('does not let the stored entries override a verb the merge has decided', () => {
    // The merge holds a frozen baseline for a conflicted verb; the stored value must not replace it.
    const result = flattenReconciled(
      'node',
      'editors',
      new Map([['Read', [cell('Allow')]]]),
      [stored('Read', 'Deny')],
    );

    expect(result).toHaveLength(1);
    expect(result[0]).toMatchObject({ verb: 'Read', state: 'Allow' });
  });

  it('does not resurrect stored entries for a verb the merge maps to an empty cell', () => {
    // An empty cell is a decision ("nothing stored for this verb"), not an absent key.
    const result = flattenReconciled('node', 'editors', new Map([['Read', []]]), [stored('Read', 'Deny')]);

    expect(result).toEqual([]);
  });
});
