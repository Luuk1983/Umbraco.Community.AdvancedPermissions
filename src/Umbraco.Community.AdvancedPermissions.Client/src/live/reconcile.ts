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
  /**
   * The verbs on this node that are already flagged from an earlier pass and not yet resolved by
   * the user. Required rather than optional so a caller cannot forget it and quietly lose the
   * stickiness this module exists to provide; pass an empty set when there are none.
   */
  conflicted: ReadonlySet<string>;
}

/** The verdict for one node, going out of {@link reconcileNode}. */
export interface ReconcileNodeResult {
  /**
   * Every verb that is flagged after this pass: the ones passed in as already conflicted, plus any
   * this pass found. Never smaller than the input — a flag leaves only by the user's own action.
   */
  conflictedVerbs: string[];
  /**
   * The `base` to compare against on the next event: the freshly-read value for every verb that
   * came through clean, the previous `base` — unchanged — for every verb that is flagged, whether
   * it was flagged by this pass or an earlier one.
   */
  nextBase: Map<string, ReadonlyArray<CellEntry>>;
  /**
   * Whether the node's concurrency stamp may adopt the freshly-read value. Only true when no
   * verb on this node is flagged, counting earlier passes' flags; see {@link reconcileNode} for
   * why a flagged verb holds it back.
   */
  adoptStamp: boolean;
  /**
   * The verbs whose pending entry is stale bookkeeping and must be dropped by the caller: the
   * verdict was `refresh` (the server moved, the editor's value is the one that was loaded) for a
   * verb the editor holds a pending entry for, and that verb is not flagged.
   *
   * Such an entry equals the old baseline, so the user holds nothing of their own there. Left in
   * place after the server's value is adopted, it would still be what the grid renders and what a
   * save sends, under a stamp that by then matches: the colleague's change deleted with no flag and
   * no dialog. Reported here, in the one pure module every editor shares, rather than rediscovered
   * per editor. A flagged verb never appears: its pending entry is what the conflict is about, and
   * only the user resolves it.
   */
  stalePendingVerbs: string[];
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
 * level, the withheld stamp) stays frozen until the user resolves it.
 *
 * Flags are sticky. A verb once flagged stays flagged whatever this pass finds, including when the
 * server is reverted to what was loaded or lands on the editor's own pending value: somebody else
 * wrote to a cell being edited, and that news must not go away by itself. Only the user acting on
 * it (loading the stored value, saving, discarding) removes it, and those live with the caller.
 * The baseline freeze and the withheld stamp follow the sticky set, not merely this pass's
 * findings, otherwise a later `no-change` pass would advance the stamp underneath a flag nobody
 * has resolved and the save that follows would pass its concurrency check with no dialog.
 *
 * A verb whose pending entry equals what was loaded, while the server moved, is a `refresh` and not
 * a conflict � but the entry it leaves behind is stale. It is reported in `stalePendingVerbs` for the
 * caller to drop, never for this module to hide: the module is pure and does not own the pending map.
 *
 * A verb the editor has not touched and that was never flagged has nothing to protect, so it
 * always takes the fresh value — leaving it stale would show two vintages of the same node in one
 * grid.
 * @param input The node's base/pending/theirs/conflicted state; see {@link ReconcileNodeInput}.
 * @returns The verdict; see {@link ReconcileNodeResult}.
 */
export function reconcileNode({ base, pending, theirs, conflicted }: ReconcileNodeInput): ReconcileNodeResult {
  const flagged = new Set<string>();
  const stale: string[] = [];
  const nextBase = new Map<string, ReadonlyArray<CellEntry>>();

  for (const [verb, baseCell] of base) {
    const theirCell = theirs.get(verb) ?? [];
    const mine = pending?.get(verb) ?? baseCell;

    const verdict = classifyCell({ base: baseCell, mine, theirs: theirCell });
    if (verdict === 'conflict' || conflicted.has(verb)) {
      flagged.add(verb);
      nextBase.set(verb, baseCell);
    } else {
      nextBase.set(verb, theirCell);
      // Only when there is an entry to drop, and never for a flagged verb (handled above).
      if (verdict === 'refresh' && pending?.has(verb)) stale.push(verb);
    }
  }

  // A flagged verb the caller no longer supplies a base for cannot be re-evaluated, but it must
  // not vanish: only the user resolves a flag.
  for (const verb of conflicted) flagged.add(verb);

  return { conflictedVerbs: [...flagged], nextBase, adoptStamp: flagged.size === 0, stalePendingVerbs: stale };
}
