import type { CellEntry } from '../live/conflict.js';
import { classifyCell } from '../live/conflict.js';

/**
 * Reduces a cell to a string that is equal for two cells that mean the same thing.
 *
 * Sorted, because nothing promises the order entries come back in, and two cells differing only in
 * that are the same cell. Deliberately the same notion of equality `classifyCell` uses; it is
 * repeated here only because that module keeps its own signature private.
 * @param entries The cell's entries.
 * @returns A comparable string.
 */
function signatureOf(entries: ReadonlyArray<CellEntry>): string {
  return entries
    .map((e) => `${e.state}|${e.scope}|${e.isPriorityOverride ? '1' : '0'}`)
    .sort()
    .join('\u001e');
}

/**
 * Whether two cells hold the same permission.
 *
 * Used to refuse to record a no-op edit. The scope dialog's Apply is unconditional, so applying the
 * value a cell already has (or changing it and changing it back) would otherwise leave a pending
 * entry equal to the stored one, and that entry is poison: when a colleague then changes the cell,
 * reconciliation sees "the editor holds nothing of its own here", adopts the colleague's value and
 * stamp, and the leftover pending entry is what the next save sends — silently reverting their
 * change under a stamp that now matches.
 * @param a One cell.
 * @param b The other cell.
 * @returns `true` when they carry the same entries, in any order.
 */
export function sameCell(a: ReadonlyArray<CellEntry>, b: ReadonlyArray<CellEntry>): boolean {
  return signatureOf(a) === signatureOf(b);
}

/** What {@link staleRefreshVerbs} needs to know about one node. */
export interface StaleRefreshInput {
  /** What was loaded — or last resolved — for each verb, before this event. */
  base: ReadonlyMap<string, ReadonlyArray<CellEntry>>;
  /** The editor's pending change for each verb it has one for. */
  pending: ReadonlyMap<string, ReadonlyArray<CellEntry>>;
  /** What is stored right now, freshly read. */
  theirs: ReadonlyMap<string, ReadonlyArray<CellEntry>>;
  /** The verbs flagged as conflicts, already or by this very pass. */
  flagged: ReadonlySet<string>;
}

/**
 * Finds the pending entries that reconciliation has just made stale, so the caller can drop them.
 *
 * A `refresh` verdict means the server moved and the editor holds nothing of its own on that cell:
 * the pending value equals the loaded one. Reconciliation then adopts the server's value and its
 * stamp as the new baseline, which leaves the pending entry claiming an edit the user never made.
 * The grid keeps rendering it and the next save sends it, under a stamp that now matches, so the
 * colleague's change is deleted with no flag and no dialog. Dropping the entry is the whole fix.
 *
 * A flagged verb is never returned: a conflict is the user's to resolve, whatever the verdict.
 * @param input The node's base, pending, theirs and flagged verbs; see {@link StaleRefreshInput}.
 * @returns The verbs whose pending entry should be removed.
 */
export function staleRefreshVerbs({ base, pending, theirs, flagged }: StaleRefreshInput): string[] {
  const stale: string[] = [];
  for (const [verb, mine] of pending) {
    if (flagged.has(verb)) continue;
    const verdict = classifyCell({ base: base.get(verb), mine, theirs: theirs.get(verb) });
    if (verdict === 'refresh') stale.push(verb);
  }
  return stale;
}
