import { describe, expect, it } from 'vitest';
import { VIRTUAL_ROOT_NODE_KEY } from '../models/permission.models.js';
import type { DocTypeEditorNode, DocTypePermissionEntry } from '../models/doc-type-permission.models.js';
import type { CellEntry } from '../live/conflict.js';
import {
  ELEMENT_CREATE_OF_TYPE,
  ELEMENT_TYPE_SCOPE,
  EMPTY_SET_STAMP,
  cellOfEntries,
  cellOfState,
  entriesForSave,
  entriesFromNextBase,
  stateOfStored,
  storedFromEditorNodes,
} from './element-type-state.js';

const TYPE_KEY = '11111111-1111-1111-1111-111111111111';
const ROLE = 'editors';

/**
 * Builds a stored doc-type entry.
 * @param verb The verb.
 * @param state Allow or Deny.
 * @param isPriorityOverride Whether the entry is a priority override.
 * @returns The entry.
 */
function stored(
  verb: string,
  state: DocTypePermissionEntry['state'] = 'Allow',
  isPriorityOverride = false,
): DocTypePermissionEntry {
  return {
    id: `${verb}-${state}`,
    nodeKey: VIRTUAL_ROOT_NODE_KEY,
    contentTypeKey: TYPE_KEY,
    roleAlias: ROLE,
    verb,
    state,
    scope: ELEMENT_TYPE_SCOPE,
    isPriorityOverride,
  };
}

describe('EMPTY_SET_STAMP', () => {
  it('is the SHA-256 of an empty canonical string, which is what the server stamps an empty set with', async () => {
    // PermissionStamp.ComputeCore hashes the UTF-8 bytes of its canonical string, and that string
    // is empty when there are no entries. If the server's canonical form ever changes this must
    // change with it, and the stamp golden vector on the server side will have failed first.
    const digest = await crypto.subtle.digest('SHA-256', new Uint8Array(0));
    const hex = [...new Uint8Array(digest)].map((b) => b.toString(16).padStart(2, '0')).join('');

    expect(EMPTY_SET_STAMP).toBe(hex);
  });
});

describe('storedFromEditorNodes', () => {
  it('takes the virtual-root bucket and its own stamp', () => {
    const nodes: DocTypeEditorNode[] = [
      { nodeKey: VIRTUAL_ROOT_NODE_KEY, entries: [stored(ELEMENT_CREATE_OF_TYPE, 'Deny')], stamp: 'abc' },
    ];

    const result = storedFromEditorNodes(nodes);

    expect(result.stamp).toBe('abc');
    expect(result.entries).toHaveLength(1);
  });

  it('ignores buckets anchored anywhere but the virtual root', () => {
    const nodes: DocTypeEditorNode[] = [
      { nodeKey: '22222222-2222-2222-2222-222222222222', entries: [stored(ELEMENT_CREATE_OF_TYPE)], stamp: 'other' },
    ];

    expect(storedFromEditorNodes(nodes).entries).toEqual([]);
  });

  it('gives a type with nothing stored the empty-set stamp, so its first save is still checked', () => {
    // The read returns only nodes that have entries. Mapping "no bucket" to "no stamp" would make
    // the save omit the concurrency check, and two people setting the same type would silently
    // overwrite each other on the very first write.
    const result = storedFromEditorNodes([]);

    expect(result.entries).toEqual([]);
    expect(result.stamp).toBe(EMPTY_SET_STAMP);
    expect(result.stamp).not.toBe('');
  });

  it('matches the virtual-root key regardless of letter case', () => {
    const nodes: DocTypeEditorNode[] = [
      { nodeKey: VIRTUAL_ROOT_NODE_KEY.toUpperCase(), entries: [], stamp: 'abc' },
    ];

    expect(storedFromEditorNodes(nodes).stamp).toBe('abc');
  });
});

describe('stateOfStored and cellOfEntries', () => {
  it('reads no create entry as inherit', () => {
    expect(stateOfStored([])).toEqual({ state: 'inherit', isPriorityOverride: false });
    expect(cellOfEntries([])).toEqual([]);
  });

  it('reads a Deny create entry as deny, carrying its override flag', () => {
    const entries = [stored(ELEMENT_CREATE_OF_TYPE, 'Deny', true)];

    expect(stateOfStored(entries)).toEqual({ state: 'deny', isPriorityOverride: true });
  });

  it('ignores entries for other verbs when reading the create cell', () => {
    const entries = [stored('Umb.Something.Else', 'Deny')];

    expect(stateOfStored(entries)).toEqual({ state: 'inherit', isPriorityOverride: false });
    expect(cellOfEntries(entries)).toEqual([]);
  });
});

describe('cellOfState', () => {
  it('turns inherit into an empty cell', () => {
    expect(cellOfState({ state: 'inherit', isPriorityOverride: true })).toEqual([]);
  });

  it('turns allow and deny into one entry with the section-global scope', () => {
    expect(cellOfState({ state: 'deny', isPriorityOverride: true })).toEqual<CellEntry[]>([
      { state: 'Deny', scope: ELEMENT_TYPE_SCOPE, isPriorityOverride: true },
    ]);
  });
});

describe('entriesForSave', () => {
  it('writes only the create entry when nothing else is stored', () => {
    const result = entriesForSave([], { state: 'allow', isPriorityOverride: false });

    expect(result).toEqual([
      { verb: ELEMENT_CREATE_OF_TYPE, state: 'Allow', scope: ELEMENT_TYPE_SCOPE, isPriorityOverride: false },
    ]);
  });

  it('writes nothing for inherit, so the rule is removed', () => {
    expect(entriesForSave([stored(ELEMENT_CREATE_OF_TYPE)], { state: 'inherit', isPriorityOverride: false })).toEqual([]);
  });

  it('keeps stored entries for verbs this editor has no column for, or the save would delete them', () => {
    const other = stored('Umb.Something.Else', 'Deny', true);

    const result = entriesForSave([stored(ELEMENT_CREATE_OF_TYPE, 'Allow'), other], { state: 'deny', isPriorityOverride: false });

    expect(result).toContainEqual({
      verb: 'Umb.Something.Else',
      state: 'Deny',
      scope: ELEMENT_TYPE_SCOPE,
      isPriorityOverride: true,
    });
    expect(result.filter((e) => e.verb === ELEMENT_CREATE_OF_TYPE)).toEqual([
      { verb: ELEMENT_CREATE_OF_TYPE, state: 'Deny', scope: ELEMENT_TYPE_SCOPE, isPriorityOverride: false },
    ]);
  });
});

describe('entriesFromNextBase', () => {
  it('flattens the merged create cell and carries entries for other verbs over from the fresh read', () => {
    // nextBase only knows the create verb, so anything else stored must come from the fresh read
    // or the next save, which rebuilds the whole entry list, deletes it from the server.
    const fresh = [stored(ELEMENT_CREATE_OF_TYPE, 'Allow'), stored('Umb.Something.Else', 'Deny')];
    const nextBase = new Map<string, ReadonlyArray<CellEntry>>([
      [ELEMENT_CREATE_OF_TYPE, [{ state: 'Deny', scope: ELEMENT_TYPE_SCOPE, isPriorityOverride: false }]],
    ]);

    const result = entriesFromNextBase(TYPE_KEY, ROLE, nextBase, fresh);

    expect(result.filter((e) => e.verb === ELEMENT_CREATE_OF_TYPE).map((e) => e.state)).toEqual(['Deny']);
    expect(result.some((e) => e.verb === 'Umb.Something.Else')).toBe(true);
    expect(result).toHaveLength(2);
  });

  it('does not duplicate the create entry from the fresh read when nextBase supplies it', () => {
    const fresh = [stored(ELEMENT_CREATE_OF_TYPE, 'Allow')];
    const nextBase = new Map<string, ReadonlyArray<CellEntry>>([[ELEMENT_CREATE_OF_TYPE, []]]);

    expect(entriesFromNextBase(TYPE_KEY, ROLE, nextBase, fresh)).toEqual([]);
  });
});
