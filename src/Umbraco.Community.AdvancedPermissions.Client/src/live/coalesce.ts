/**
 * Wraps an async job so at most one run is in flight at a time, and every request for a run —
 * however many arrive, and whenever they arrive relative to a run already happening — is answered
 * by a run that starts after it, no matter how many times it is asked for. No request is ever
 * silently dropped: one that arrives during the first run, during the re-run that answers it, or
 * during any re-run after that, is guaranteed a further run once the one in flight finishes.
 *
 * Bulk operations on the server deliver one event per affected entity — publishing with
 * descendants, emptying the recycle bin, deleting a user group that touched a hundred nodes — and
 * a surface that refetched per event would spend one request each, every one of them answering a
 * question that was already out of date when it was asked. Collapsing a burst into "run now, then
 * once more for everything that arrived while it ran, repeating for as long as more keeps
 * arriving" answers every request while never running more than one job at a time.
 * @param job The work to run.
 * @returns A function that requests a run.
 */
export function createCoalescer(job: () => Promise<void>): () => Promise<void> {
  let running: Promise<void> | undefined;
  let queued = false;

  /**
   * Runs the job, then keeps re-running it for as long as a request arrived while the previous
   * run was in flight.
   * @returns A promise settling when the run, and every follow-up it triggers, is done.
   */
  const start = async (): Promise<void> => {
    try {
      await job();
      // Re-checked after every run, not just the first. A request arriving during the re-run
      // sets `queued` again, and without this loop that flag would be cleared by `finally` with
      // nothing scheduled to act on it — the request would be silently dropped, which is the one
      // failure this module exists to rule out.
      while (queued) {
        queued = false;
        await job();
      }
    } finally {
      // In `finally`, because a rejected job that left these set would wedge the surface: every
      // later request would queue behind a run that had already finished.
      running = undefined;
      queued = false;
    }
  };

  return () => {
    if (running) {
      queued = true;
      return running;
    }

    running = start();
    return running;
  };
}
