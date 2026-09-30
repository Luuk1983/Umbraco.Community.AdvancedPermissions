import type { PermissionScope, PermissionState } from '../models/permission.models.js';

/**
 * The verdict for one cell — one node and one verb — when the server reports that something
 * changed.
 *
 * There is deliberately no "own write" verdict. `ServerEvent` carries a source, a type and one
 * key, with no user and no client identity, so this editor's own save cannot be told from a
 * colleague's — or from the same person's other tab — by inspection. The tempting substitute is
 * value equality ("the server holds what I hold, so I wrote it"), and it must not come back: a
 * permission cell has only about seven possible values (inherit, allow or deny, by scope and
 * priority), so somebody else landing on exactly the value this editor has pending is routine, not
 * a coincidence, and treating it as authorship silently swallows a real conflict. That heuristic
 * came from a sibling project comparing whole documents, where accidental equality is effectively
 * impossible; it does not transfer to a cell.
 *
 * The echo of this editor's own save is handled by the callers instead, by not reconciling at all
 * while a save is in flight.
 */
export type CellVerdict =
  /** The server still holds what was loaded. A duplicate, stale or reverted event. Nothing to say. */
  | 'no-change'
  /** The server moved and the editor holds nothing of its own here, so take the server's value in place. */
  | 'refresh'
  /** The server moved and so did the editor — even to the same value. A person has to know. */
  | 'conflict';

/** One stored permission entry, reduced to the fields that decide whether two cells differ. */
export interface CellEntry {
  /** Allow or Deny. */
  state: PermissionState;
  /** How far the entry reaches. */
  scope: PermissionScope;
  /** Whether the entry is a priority override. */
  isPriorityOverride: boolean;
}

/** The three versions a verdict is decided from. A side that has not loaded yet is undefined. */
export interface CellComparison {
  /** What the server said when the editor loaded. */
  base: ReadonlyArray<CellEntry> | undefined;
  /** What the editor is holding: `base` with its pending change applied. */
  mine: ReadonlyArray<CellEntry> | undefined;
  /** What the server holds now. */
  theirs: ReadonlyArray<CellEntry> | undefined;
}

/**
 * Reduces a cell to a string equal for two cells that mean the same thing.
 *
 * Sorted, because nothing promises the order entries come back in, and two cells differing only
 * in that are the same cell.
 * @param entries The cell's entries.
 * @returns A comparable string.
 */
function signature(entries: ReadonlyArray<CellEntry>): string {
  return entries
    .map((e) => `${e.state}|${e.scope}|${e.isPriorityOverride ? '1' : '0'}`)
    .sort()
    .join('\u001e');
}

/**
 * Decides what a change on the server means for one cell of an editor holding unsaved work.
 *
 * Three questions, in this order: did the stored value move away from what was loaded, and if so
 * does the editor hold a change of its own here. Whether the *values* agree is never asked, for
 * the reasons on {@link CellVerdict}.
 *
 * `no-change` is only about this one comparison. A cell that reads `no-change` may still have been
 * flagged by an earlier pass and reverted since; keeping that flag is the caller's job (see
 * `reconcileNode`), because this function sees a single snapshot and cannot know the history.
 * @param comparison The three versions; see {@link CellComparison}.
 * @returns The verdict.
 */
export function classifyCell({ base, mine, theirs }: CellComparison): CellVerdict {
  // A side that has not arrived makes every comparison unknowable, and doing nothing is the only
  // safe answer to a question that cannot be asked.
  if (base === undefined || mine === undefined || theirs === undefined) return 'no-change';

  const baseSignature = signature(base);

  if (signature(theirs) === baseSignature) return 'no-change';
  if (signature(mine) === baseSignature) return 'refresh';
  return 'conflict';
}
