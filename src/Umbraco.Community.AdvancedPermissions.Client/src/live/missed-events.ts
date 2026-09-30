/**
 * Remembers that a live event was set aside, so it can be answered later instead of lost.
 *
 * An editor does not reconcile while its own save is in flight: the write raises an event of its
 * own, the event carries no identity, and reconciling against it would flag the user's own save as
 * a conflict. But setting an event aside is not the same as answering it. The save's reload reads
 * the server at one moment, and a colleague's write can land after that read and before the save
 * finishes; the event for it arrives while saving, is set aside, and the coalescer that delivered
 * it has already settled, so nothing would ever ask again and the grid would stay stale until some
 * unrelated event arrived.
 *
 * Any number of events set aside during one save are owed exactly one replay, because a replay is a
 * full reconcile that answers all of them.
 */
export interface MissedEvents {
  /** Records that an event, or a read made on its behalf, was set aside and is owed an answer. */
  defer(): void;
  /**
   * Whether anything is owed an answer, clearing the record.
   * @returns `true` when at least one event was deferred since the last call.
   */
  take(): boolean;
}

/**
 * Creates an empty record of set-aside events.
 * @returns A fresh {@link MissedEvents}.
 */
export function createMissedEvents(): MissedEvents {
  let missed = false;
  return {
    defer: () => {
      missed = true;
    },
    take: () => {
      const owed = missed;
      missed = false;
      return owed;
    },
  };
}
