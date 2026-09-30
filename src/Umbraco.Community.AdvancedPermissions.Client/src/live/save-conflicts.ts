import type { CellEntry } from './conflict.js';
import { reconcileNode } from './reconcile.js';

/** One node the server refused to save, with everything needed to decide what the refusal means. */
export interface SaveConflictNode {
  /** The editor's own key for the node; opaque to this module. */
  key: string;
  /** What the editor has loaded for each verb of the node (the baseline). Every compared verb needs a key. */
  base: ReadonlyMap<string, ReadonlyArray<CellEntry>>;
  /** The verbs the editor holds an unsaved change for. */
  pending: ReadonlyMap<string, ReadonlyArray<CellEntry>> | undefined;
  /** What the server reported as stored, per verb, in the refusal. */
  theirs: ReadonlyMap<string, ReadonlyArray<CellEntry>>;
  /** The verbs already flagged on this node from an earlier pass, not yet resolved by the user. */
  flagged: ReadonlySet<string>;
  /** The stamp the editor sent for this node and the server refused. */
  heldStamp: string;
  /** The stamp of what the server holds now, as reported in the refusal. */
  currentStamp: string;
}

/** What a refusal means for one node. */
export interface SaveConflictPlanNode {
  /** The node's key, as passed in. */
  key: string;
  /** The verbs both sides changed, plus any already flagged. The ones the user must decide on. */
  conflictedVerbs: string[];
  /** The verbs whose pending entry is the loaded value and so is stale; see `ReconcileNodeResult`. */
  stalePendingVerbs: string[];
  /** The per-verb entries the node should hold afterwards, as `reconcileNode` returns them. */
  nextBase: Map<string, ReadonlyArray<CellEntry>>;
  /**
   * The stamp the node may adopt, or `undefined` when a verb on it is flagged and the stamp has to
   * be held back so a plain save of it is refused again.
   */
  adoptedStamp: string | undefined;
  /** The stamp of what the server holds, kept so a confirmed overwrite can be sent under it. */
  currentStamp: string;
}

/** What to do about a refused save. */
export interface SaveConflictPlan {
  /** One entry per refused node, in the order given. */
  nodes: SaveConflictPlanNode[];
  /**
   * Whether the editor should apply the plan and save again without asking: nothing anywhere is
   * genuinely contested, and at least one node's stamp moved, so the retry is under a different
   * stamp from the one that was just refused.
   */
  retry: boolean;
  /**
   * Whether the refusal makes no sense: nothing is contested and no stamp moved. Retrying would be
   * refused identically, so the editor should report a failure instead of looping.
   */
  stalled: boolean;
}

/**
 * Works out what a refused batch save means, verb by verb, so that only cells somebody actually
 * contested reach the user.
 *
 * The server refuses a node as a whole: its stamp covers every verb stored there, so a colleague
 * changing verb B is enough to refuse a save that only edits verb A. Treating every pending verb on
 * a refused node as a conflict, as the editors used to, claims a conflict on A with nothing stored
 * differently, and "Load stored values" then throws away an edit nobody contested. This runs the
 * same {@link reconcileNode} a live event does, with the editor's loaded values as the baseline and
 * the refusal's `currentEntries` as the server's, so a save-time refusal and a live event can never
 * disagree about what counts as a conflict.
 *
 * When nothing is contested, the refusal is bookkeeping: the node adopts the stored values and the
 * fresh stamp, and the save is retried. When something is, the user decides, and the nodes that are
 * not contested still adopt their fresh stamp so the decision is made against current values.
 * @param nodes The refused nodes.
 * @returns The plan; see {@link SaveConflictPlan}.
 */
export function planSaveConflicts(nodes: ReadonlyArray<SaveConflictNode>): SaveConflictPlan {
  const planned: SaveConflictPlanNode[] = nodes.map((n) => {
    const result = reconcileNode({ base: n.base, pending: n.pending, theirs: n.theirs, conflicted: n.flagged });
    return {
      key: n.key,
      conflictedVerbs: result.conflictedVerbs,
      stalePendingVerbs: result.stalePendingVerbs,
      nextBase: result.nextBase,
      adoptedStamp: result.adoptStamp ? n.currentStamp : undefined,
      currentStamp: n.currentStamp,
    };
  });

  const contested = planned.some((n) => n.conflictedVerbs.length > 0);
  const stampMoved = nodes.some((n) => n.currentStamp !== n.heldStamp);

  return {
    nodes: planned,
    retry: !contested && stampMoved,
    stalled: !contested && !stampMoved,
  };
}
