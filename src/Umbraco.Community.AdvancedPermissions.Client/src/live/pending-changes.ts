import type { CellEntry } from './conflict.js';
import { cellsEqual } from './conflict.js';

/** One node's pending edits: verb to the entries the user wants stored for it. */
export type PendingNodeEdits<E extends CellEntry> = Map<string, E[]>;

/**
 * Records the value the user just applied to one cell of a node editor that has several verbs.
 *
 * An applied value equal to what is stored is not an edit, and is not recorded (an earlier edit to
 * the cell is dropped): the dialog's Apply is unconditional, so a user can apply the value a cell
 * already holds, or change a cell and change it back. Either would otherwise leave a pending entry
 * equal to the baseline, which is invisible until a colleague changes the cell: the next refresh
 * adopts their value and stamp, and the stale entry then goes out on save under a matching stamp,
 * deleting their change with no flag and no dialog.
 *
 * The exception is a flagged cell. Its baseline is frozen at the pre-conflict value, so an applied
 * value equal to it is the user choosing to overwrite the colleague's change, and the entry has to
 * stay for the conflict to have anything to be about.
 * @param pending The pending map, never modified.
 * @param nodeKey The node the cell belongs to.
 * @param verb The cell's verb.
 * @param applied The entries the dialog produced.
 * @param stored What is stored for the cell (its baseline).
 * @param flagged Whether the cell is already flagged as conflicted.
 * @returns A new pending map, without an entry for a node that has no pending verb left.
 */
export function withVerbEdit<E extends CellEntry>(
  pending: ReadonlyMap<string, ReadonlyMap<string, ReadonlyArray<E>>>,
  nodeKey: string,
  verb: string,
  applied: ReadonlyArray<E>,
  stored: ReadonlyArray<CellEntry>,
  flagged: boolean,
): Map<string, PendingNodeEdits<E>> {
  const next = new Map<string, PendingNodeEdits<E>>();
  for (const [key, verbs] of pending) next.set(key, new Map([...verbs].map(([v, e]) => [v, [...e]])));

  const forNode = next.get(nodeKey) ?? new Map<string, E[]>();
  if (!flagged && cellsEqual(applied, stored)) forNode.delete(verb);
  else forNode.set(verb, [...applied]);

  if (forNode.size === 0) next.delete(nodeKey);
  else next.set(nodeKey, forNode);
  return next;
}

/**
 * Records the value the user just applied to a node's only cell, for an editor with one verb.
 *
 * Same rule as {@link withVerbEdit}: a value equal to what is stored is not an edit, unless the
 * cell is flagged.
 * @param pending The pending map, never modified.
 * @param nodeKey The node.
 * @param applied The entries the dialog produced.
 * @param stored What is stored for the node (its baseline).
 * @param flagged Whether the cell is already flagged as conflicted.
 * @returns A new pending map.
 */
export function withCellEdit<E extends CellEntry>(
  pending: ReadonlyMap<string, ReadonlyArray<E>>,
  nodeKey: string,
  applied: ReadonlyArray<E>,
  stored: ReadonlyArray<CellEntry>,
  flagged: boolean,
): Map<string, E[]> {
  const next = new Map(pending as ReadonlyMap<string, E[]>);
  if (!flagged && cellsEqual(applied, stored)) next.delete(nodeKey);
  else next.set(nodeKey, [...applied]);
  return next;
}

/**
 * Removes the given verbs from a node's pending edits.
 * @param pending The pending map, never modified.
 * @param nodeKey The node.
 * @param verbs The verbs whose pending entries to remove.
 * @returns A new pending map, without an entry for a node that has no pending verb left.
 */
export function withoutVerbs<E extends CellEntry>(
  pending: ReadonlyMap<string, ReadonlyMap<string, ReadonlyArray<E>>>,
  nodeKey: string,
  verbs: ReadonlyArray<string>,
): Map<string, PendingNodeEdits<E>> {
  const forNode = pending.get(nodeKey);
  const next = new Map<string, PendingNodeEdits<E>>();
  for (const [key, v] of pending) next.set(key, new Map([...v].map(([verb, e]) => [verb, [...e]])));
  if (!forNode || !verbs.some((v) => forNode.has(v))) return next;

  const remaining = next.get(nodeKey)!;
  for (const verb of verbs) remaining.delete(verb);
  if (remaining.size === 0) next.delete(nodeKey);
  return next;
}
