import { classifyCell } from '../live/conflict.js';
import type { CellEntry } from '../live/conflict.js';

/**
 * The concurrency stamp of a set with nothing in it: the lowercase-hex SHA-256 of the empty
 * string, which is what the server's `PermissionStamp` produces for zero entries.
 *
 * `GetForEditor` only returns a bucket for a node that has something stored, so a node with
 * nothing stored arrives with no stamp at all. That must never be read as "no stamp available":
 * a save that sends no `expectedStamp` makes the server skip the check, so the first write to an
 * empty node - or to a node a colleague has just cleared - would overwrite somebody else's
 * without a dialog. Sending this value instead makes the server compare against what it holds and
 * refuse if the node has stopped being empty.
 *
 * Defined once, here, and pinned against the real hash by `doc-type-concurrency.test.ts`, so the
 * editor cannot drift from the server's canonical form without a test failing.
 */
export const EMPTY_SET_STAMP = 'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855';

/**
 * Builds the key a conflict flag is stored under. All three parts are there on purpose: a
 * document-type permission belongs to a node, a user group *and* a document type, and a flag that
 * named only the node would be matched by another document type's cell for the same node.
 *
 * The document type is lower-cased because the key reaches the editor from three places (the
 * picker's stored selection, the tree state and the server's conflict report) and a Guid's casing
 * is not something to depend on for an equality check that guards against lost writes.
 * @param nodeKey The local node key (`'virtual-root'` for the virtual root).
 * @param contentTypeKey The document type's key.
 * @param verb The verb.
 * @returns The key, `nodeKey|contentTypeKey|verb`.
 */
export function conflictKey(nodeKey: string, contentTypeKey: string, verb: string): string {
  return `${nodeKey}|${contentTypeKey.toLowerCase()}|${verb}`;
}

/**
 * Splits a key built by {@link conflictKey} back into its three parts.
 * @param key The key.
 * @returns The node key, the (lower-cased) document type key and the verb.
 */
export function parseConflictKey(key: string): { nodeKey: string; contentTypeKey: string; verb: string } {
  const [nodeKey = '', contentTypeKey = '', verb = ''] = key.split('|');
  return { nodeKey, contentTypeKey, verb };
}

/**
 * Whether two cells hold the same permission, whatever order their entries are listed in.
 *
 * Deliberately built on `classifyCell` rather than a second comparison of its own: it is the one
 * definition of "the same cell" that reconciliation uses, so an edit judged a no-op here is judged
 * unchanged there too.
 * @param a One cell.
 * @param b The other cell.
 * @returns `true` if they mean the same thing.
 */
export function cellsEqual(a: ReadonlyArray<CellEntry>, b: ReadonlyArray<CellEntry>): boolean {
  // With `mine` equal to `base`, the verdict is 'no-change' exactly when `theirs` equals `base`.
  return classifyCell({ base: a, mine: a, theirs: b }) === 'no-change';
}

/**
 * Whether a pending edit is stale bookkeeping: the server moved, and the edit is the value that
 * was loaded, so the user holds nothing of their own here.
 *
 * Such an entry must be dropped when the server's value is adopted. Left in place it would still
 * be what the grid renders and what a save sends, under a stamp that by then matches, silently
 * reverting the other person's write - the verdict `refresh` promises the user has nothing to lose,
 * and an entry saying otherwise breaks that promise.
 * @param base What was loaded (or last resolved) for the cell.
 * @param pending The user's pending value for it.
 * @param theirs What the server holds now.
 * @returns `true` if the pending entry should be dropped.
 */
export function isStalePending(
  base: ReadonlyArray<CellEntry>,
  pending: ReadonlyArray<CellEntry>,
  theirs: ReadonlyArray<CellEntry>,
): boolean {
  return classifyCell({ base, mine: pending, theirs }) === 'refresh';
}
