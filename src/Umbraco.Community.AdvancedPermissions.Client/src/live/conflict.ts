import type { PermissionScope, PermissionState } from '../models/permission.models.js';

/**
 * The verdict for one cell — one node and one verb — when the server reports that something
 * changed.
 *
 * The decision is made from three snapshots rather than from anything the event says about
 * itself, because the event says almost nothing: `ServerEvent` carries a source, a type and one
 * key, with no user and no client identity, so this editor's own save is indistinguishable from a
 * colleague's by inspection. Asking the data instead answers for every write path there is —
 * including the same person saving from another browser tab — because it never tries to enumerate
 * them.
 */
export type CellVerdict =
  /** The server already holds what the editor holds. This editor wrote it. Do nothing. */
  | 'own-write'
  /** Nothing moved relative to what was loaded. A duplicate or stale event. Do nothing. */
  | 'no-change'
  /** The editor holds nothing of its own here, so take the server's value in place. */
  | 'refresh'
  /** Both sides moved, differently. A person has to decide. */
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
 * The order of the checks is load-bearing. `own-write` is asked first because when a save is
 * landing all three comparisons are true at once, and only that answer is safe: the server matches
 * what the editor holds, and a moment later the loaded baseline will match it too. Asking
 * `refresh` first would reload over a save that is still settling.
 * @param comparison The three versions; see {@link CellComparison}.
 * @returns The verdict.
 */
export function classifyCell({ base, mine, theirs }: CellComparison): CellVerdict {
  // A side that has not arrived makes every comparison unknowable, and doing nothing is the only
  // safe answer to a question that cannot be asked.
  if (base === undefined || mine === undefined || theirs === undefined) return 'no-change';

  const theirSignature = signature(theirs);
  const mySignature = signature(mine);

  if (theirSignature === mySignature) return 'own-write';
  if (theirSignature === signature(base)) return 'no-change';
  if (mySignature === signature(base)) return 'refresh';
  return 'conflict';
}
