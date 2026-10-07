/** Options for {@link createPermissionCache}. */
export interface PermissionCacheOptions<T> {
  /** How long a stored answer is reused, in milliseconds. */
  ttlMs: number;
  /**
   * Decides whether an answer may be kept once it has arrived. An answer that is not cacheable is
   * still shared by every caller that asked while the request was in flight, but the next caller
   * asks the server again.
   */
  isCacheable: (value: T) => boolean;
  /** The clock, injectable for tests. Defaults to {@link Date.now}. */
  now?: () => number;
}

/** A per-node cache of permission answers. */
export interface PermissionCache<T> {
  /**
   * Returns the answer for a node, from the cache when a fresh one is stored, otherwise from the
   * fetcher.
   * @param key The node key.
   * @returns The answer for that node.
   */
  get(key: string): Promise<T>;
  /** Forgets every stored answer, so the next request for any node asks the server again. */
  clear(): void;
}

/** One stored answer and when it was asked for. */
interface CacheEntry<T> {
  /** The in-flight or settled request. */
  promise: Promise<T>;
  /** When the request was made, used for the time-to-live. */
  timestamp: number;
}

/**
 * Creates a per-node cache in front of a permission fetcher. It stores the promise rather than the
 * value, so callers asking for the same node at the same time share one request (ten action
 * conditions for one document cost one API call).
 * @param fetch Fetches the answer for one node key.
 * @param options The time-to-live, the cacheability test and an optional clock.
 * @returns The cache.
 */
export function createPermissionCache<T>(
  fetch: (key: string) => Promise<T>,
  options: PermissionCacheOptions<T>,
): PermissionCache<T> {
  const entries = new Map<string, CacheEntry<T>>();
  const now = options.now ?? Date.now;

  return {
    get(key: string): Promise<T> {
      const existing = entries.get(key);
      if (existing && now() - existing.timestamp < options.ttlMs) {
        return existing.promise;
      }

      const forget = (): void => {
        // Only our own entry: a newer request for the same key may already have replaced it.
        if (entries.get(key)?.promise === promise) {
          entries.delete(key);
        }
      };

      const promise = fetch(key).then(
        (value) => {
          // Issue #58: the server answers "no permissions" for a node it does not know yet, such
          // as a new document's draft key. The document keeps that key once saved, so keeping the
          // answer would make the freshly saved document read-only until the entry expired.
          if (!options.isCacheable(value)) {
            forget();
          }
          return value;
        },
        (err: unknown) => {
          // Remove the failed entry so the next evaluation retries.
          forget();
          throw err;
        },
      );

      entries.set(key, { promise, timestamp: now() });
      return promise;
    },
    clear(): void {
      entries.clear();
    },
  };
}
