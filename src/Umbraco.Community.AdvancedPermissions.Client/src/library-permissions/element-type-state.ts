import { VIRTUAL_ROOT_NODE_KEY } from '../models/permission.models.js';
import type { PermissionScope } from '../models/permission.models.js';
import type {
  DocTypeEditorNode,
  DocTypePermissionEntry,
  SaveDocTypePermissionItem,
} from '../models/doc-type-permission.models.js';
import type { CellEntry } from '../live/conflict.js';
import { EMPTY_SET_STAMP } from '../doc-type-permissions/doc-type-concurrency.js';

/** The element-type create-of-type verb stored in the document-type table. */
export const ELEMENT_CREATE_OF_TYPE = 'Umb.Element.CreateOfType';

/**
 * The scope every element-type rule is written with. Library element-type filtering is
 * section-global (Umbraco supplies no parent context for Library creates), so a rule lives on the
 * virtual root and covers everything beneath it.
 */
export const ELEMENT_TYPE_SCOPE: PermissionScope = 'ThisNodeAndDescendants';

// The empty-set stamp is defined once, in the document-type family's concurrency module, because
// this editor writes through the same table and the same endpoints and must agree with it. It is
// pinned there against the real SHA-256, and again in this module's tests. What it is for:
//
// The editor's read only reports nodes that have entries. A type with no stored rule has no bucket
// and so no stamp on the wire, but it is still a state that can change under the editor: two people
// open the same group, one sets Allow and the other Deny. If "no bucket" were mapped to "no stamp"
// the save would omit the concurrency check, and the second write would silently replace the first.
// Sending the empty-set stamp instead makes the server compare against what is really stored, so
// the second save is refused. It is distinct from an empty string, which the API layer uses for the
// one different case of a read that came back with no `ETag` at all.
export { EMPTY_SET_STAMP };

/** The editor's tri-state choice for one element type. */
export type TypeChoice = 'inherit' | 'allow' | 'deny';

/** Tri-state choice plus priority override for a single element type. */
export interface TypeState {
  /** Inherit (no rule, creatable by default), Allow or Deny. */
  state: TypeChoice;
  /** Whether the rule is a priority override. Meaningless, and ignored, for `inherit`. */
  isPriorityOverride: boolean;
}

/** What the server holds for one (user group, element type) pair at the virtual root. */
export interface StoredElementType {
  /** Every stored entry for the pair, including any for verbs this editor has no column for. */
  entries: DocTypePermissionEntry[];
  /** The concurrency stamp of `entries`, sent back on save. */
  stamp: string;
}

/**
 * Picks the virtual-root bucket out of a `GetForEditor` answer and gives it a real stamp.
 *
 * Element-type rules are section-global, so only the virtual-root bucket is ever this editor's
 * business. A type with nothing stored has no bucket at all; it gets {@link EMPTY_SET_STAMP} rather
 * than an empty stamp, so its first save is concurrency-checked like any other.
 * @param nodes The per-node buckets the server returned for one (user group, element type) pair.
 * @returns The stored entries and the stamp to send back when saving them.
 */
export function storedFromEditorNodes(nodes: readonly DocTypeEditorNode[]): StoredElementType {
  const bucket = nodes.find((n) => n.nodeKey.toLowerCase() === VIRTUAL_ROOT_NODE_KEY);
  return bucket ? { entries: bucket.entries, stamp: bucket.stamp } : { entries: [], stamp: EMPTY_SET_STAMP };
}

/**
 * Reduces stored entries to the create cell the grid shows and the comparison uses.
 * @param entries Every stored entry for the pair.
 * @returns The create entries as comparable cell entries; empty when no rule is stored.
 */
export function cellOfEntries(entries: ReadonlyArray<DocTypePermissionEntry>): CellEntry[] {
  return entries
    .filter((e) => e.verb === ELEMENT_CREATE_OF_TYPE)
    .map((e) => ({ state: e.state, scope: e.scope, isPriorityOverride: e.isPriorityOverride }));
}

/**
 * Reads the tri-state the grid displays from what is stored.
 * @param entries Every stored entry for the pair.
 * @returns `inherit` when no create rule is stored, otherwise the rule's state and override flag.
 */
export function stateOfStored(entries: ReadonlyArray<DocTypePermissionEntry>): TypeState {
  const createEntry = entries.find((e) => e.verb === ELEMENT_CREATE_OF_TYPE);
  return createEntry
    ? { state: createEntry.state === 'Deny' ? 'deny' : 'allow', isPriorityOverride: createEntry.isPriorityOverride }
    : { state: 'inherit', isPriorityOverride: false };
}

/**
 * Turns the editor's tri-state into the cell it would store, for comparison with a stored cell.
 * @param state The choice.
 * @returns An empty cell for `inherit`, otherwise one entry with the section-global scope.
 */
export function cellOfState(state: TypeState): CellEntry[] {
  if (state.state === 'inherit') return [];
  return [
    {
      state: state.state === 'allow' ? 'Allow' : 'Deny',
      scope: ELEMENT_TYPE_SCOPE,
      isPriorityOverride: state.isPriorityOverride,
    },
  ];
}

/**
 * Builds the complete entry list to save for one pair: the stored entries for every verb this
 * editor has no column for, plus the create rule the user chose.
 *
 * A save replaces the pair's whole entry list. Sending only the create rule would delete anything
 * else stored under the pair, so those entries are carried through untouched.
 * @param storedEntries Every stored entry for the pair, as last read.
 * @param state The choice for the create verb.
 * @returns The entries to send.
 */
export function entriesForSave(
  storedEntries: ReadonlyArray<DocTypePermissionEntry>,
  state: TypeState,
): SaveDocTypePermissionItem[] {
  const kept: SaveDocTypePermissionItem[] = storedEntries
    .filter((e) => e.verb !== ELEMENT_CREATE_OF_TYPE)
    .map((e) => ({ verb: e.verb, state: e.state, scope: e.scope, isPriorityOverride: e.isPriorityOverride }));
  const create: SaveDocTypePermissionItem[] = cellOfState(state).map((c) => ({
    verb: ELEMENT_CREATE_OF_TYPE,
    state: c.state,
    scope: c.scope,
    isPriorityOverride: c.isPriorityOverride,
  }));
  return [...kept, ...create];
}

/**
 * Flattens a per-verb `nextBase` merge (as `reconcileNode` returns it) back into the entry list a
 * stored pair holds. The synthesised `id` is display-only, the same convention the other editors
 * use when they fabricate entries client-side.
 *
 * Entries for verbs `nextBase` has no key for are carried over from the fresh read rather than
 * dropped. `nextBase` only knows the create verb, and a save rebuilds the pair's whole entry list
 * from what is held here, so an entry lost here would be deleted from the server by the next save.
 * @param contentTypeKey The element type these entries belong to.
 * @param roleAlias The user group these entries belong to.
 * @param nextBase The per-verb entries to flatten.
 * @param freshEntries The pair's freshly-read entries, the source for verbs `nextBase` lacks.
 * @returns The flat entry list.
 */
export function entriesFromNextBase(
  contentTypeKey: string,
  roleAlias: string,
  nextBase: ReadonlyMap<string, ReadonlyArray<CellEntry>>,
  freshEntries: ReadonlyArray<DocTypePermissionEntry>,
): DocTypePermissionEntry[] {
  const result: DocTypePermissionEntry[] = [];
  for (const [verb, cellEntries] of nextBase) {
    for (const e of cellEntries) {
      result.push({
        id: `${contentTypeKey}-${verb}-${result.length}`,
        nodeKey: VIRTUAL_ROOT_NODE_KEY,
        contentTypeKey,
        roleAlias,
        verb,
        state: e.state,
        scope: e.scope,
        isPriorityOverride: e.isPriorityOverride,
      });
    }
  }
  for (const e of freshEntries) {
    if (!nextBase.has(e.verb)) result.push(e);
  }
  return result;
}
