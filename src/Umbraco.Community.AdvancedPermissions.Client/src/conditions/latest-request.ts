/** Tracks which of a series of async requests is the newest. */
export interface LatestRequest {
  /**
   * Marks the start of a new request, making every earlier one stale.
   * @returns A check that is true for as long as this request is still the newest.
   */
  begin(): () => boolean;
}

/**
 * Creates a tracker that lets an async evaluation tell whether a newer evaluation started while it
 * was waiting.
 * @returns The tracker.
 */
export function createLatestRequest(): LatestRequest {
  let newest = 0;
  return {
    begin(): () => boolean {
      const mine = ++newest;
      return () => mine === newest;
    },
  };
}
