import type { CellEntry } from './conflict.js';
import { classifyCell } from './conflict.js';

/**
 * Per-verb state for one node, going into {@link reconcileNode}.
 *
 * `base` and `theirs` are expected to carry an entry for every verb the caller cares about — a
 * verb with nothing stored still needs a key, mapped to an empty array, so it is compared rather
 * than skipped. `pending` only carries the verbs the editor has an unsaved change for; a verb
 * absent from it has none, and its "mine" is `base`, unchanged.
 */
export interface ReconcileNodeInput {
  /** What was loaded — or last resolved — for each verb, before this event. */
  base: ReadonlyMap<string, ReadonlyArray<CellEntry>>;
  /** What the editor is holding for verbs it has a pending change for. */
  pending: ReadonlyMap<string, ReadonlyArray<CellEntry>> | undefined;
  /** What is stored right now, freshly read. */
  theirs: ReadonlyMap<string, ReadonlyArray<CellEntry>>;
}

/** The verdict for one node, going out of {@link reconcileNode}. */
export interface ReconcileNodeResult {
  /** Verbs where the editor's pending change and the server's new value disagree. */
  conflictedVerbs: string[];
  /**
   * The `base` to compare against on the next event: the freshly-read value for every verb that
   * came through clean, the previous `base` — unchanged — for every verb that conflicted.
   */
  nextBase: Map<string, ReadonlyArray<CellEntry>>;
  /**
   * Whether the node's concurrency stamp may adopt the freshly-read value. Only true when no
   * verb on this node conflicted; see the module doc for why a conflicted verb holds it back.
   */
  adoptStamp: boolean;
}

/**
 * Works out, verb by verb, what a live change means for one node an editor is holding unsaved
 * work against.
 *
 * The one rule that makes this safe: a conflicted verb's `base` must not advance to the
 * freshly-read value. If it did, the very next event — anywhere in the tree, since the
 * live-events controller ignores which key changed and reconciles every loaded node on every
 * event — would see the server's still-unresolved value as `base` again. `classifyCell` checks
 * `theirs === base` before ever looking at `mine`, so that second pass would answer `no-change`
 * and the conflict would silently stop being reported, even though the editor's pending change
 * and the server's stored value still disagree exactly as before. A save made after that point
 * would pass its concurrency check — because the node's stamp would have been allowed to adopt
 * the same "clean" verdict — and overwrite someone else's work with no dialog. That is exactly
 * the failure this feature exists to prevent, so a conflicted verb's `base` (and, at the node
 * level, the withheld stamp) stays frozen for as long as the disagreement remains real.
 *
 * A verb the editor has not touched has nothing to protect, so it always takes the fresh value —
 * leaving it stale would show two vintages of the same node in one grid.
 * @param input The node's base/pending/theirs state; see {@link ReconcileNodeInput}.
 * @returns The verdict; see {@link ReconcileNodeResult}.
 */
export function reconcileNode({ base, pending, theirs }: ReconcileNodeInput): ReconcileNodeResult {
  const conflictedVerbs: string[] = [];
  const nextBase = new Map<string, ReadonlyArray<CellEntry>>();

  for (const [verb, baseCell] of base) {
    const theirCell = theirs.get(verb) ?? [];
    const pendingCell = pending?.get(verb);
    const mine = pendingCell ?? baseCell;

    if (classifyCell({ base: baseCell, mine, theirs: theirCell }) === 'conflict') {
      conflictedVerbs.push(verb);
      nextBase.set(verb, baseCell);
    } else {
      nextBase.set(verb, theirCell);
    }
  }

  return { conflictedVerbs, nextBase, adoptStamp: conflictedVerbs.length === 0 };
}
