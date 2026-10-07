import { describe, expect, it } from 'vitest';
import { createPermissionCache } from './permission-cache.js';

/**
 * A fetcher that records every key it is asked for and answers from a fixed map, so a test can
 * count round trips and decide what the "server" says per key.
 * @param answers The answer per key; a key missing from the map answers an empty list.
 * @returns The fetcher plus the keys it was called with, in order.
 */
function recordingFetcher(answers: Record<string, string[]>): {
  fetch: (key: string) => Promise<string[]>;
  calls: string[];
} {
  const calls: string[] = [];
  return {
    calls,
    fetch: (key: string) => {
      calls.push(key);
      return Promise.resolve(answers[key] ?? []);
    },
  };
}

/**
 * A clock the test moves by hand.
 * @returns The clock function and a way to advance it.
 */
function manualClock(): { now: () => number; advance: (ms: number) => void } {
  let time = 1_000;
  return { now: () => time, advance: (ms: number) => { time += ms; } };
}

const isKnownNode = (verbs: string[]): boolean => verbs.length > 0;

describe('createPermissionCache', () => {
  it('shares one request between callers asking for the same node at the same time', async () => {
    // Ten action conditions for one document must cost one API call, not ten.
    const { fetch, calls } = recordingFetcher({ a: ['Read'] });
    const cache = createPermissionCache(fetch, { ttlMs: 30_000, isCacheable: isKnownNode });

    const results = await Promise.all([cache.get('a'), cache.get('a'), cache.get('a')]);

    expect(calls).toEqual(['a']);
    expect(results).toEqual([['Read'], ['Read'], ['Read']]);
  });

  it('answers from the cache within the time-to-live', async () => {
    const clock = manualClock();
    const { fetch, calls } = recordingFetcher({ a: ['Read'] });
    const cache = createPermissionCache(fetch, { ttlMs: 30_000, isCacheable: isKnownNode, now: clock.now });

    await cache.get('a');
    clock.advance(29_999);
    await cache.get('a');

    expect(calls).toEqual(['a']);
  });

  it('asks the server again once the time-to-live has passed', async () => {
    const clock = manualClock();
    const { fetch, calls } = recordingFetcher({ a: ['Read'] });
    const cache = createPermissionCache(fetch, { ttlMs: 30_000, isCacheable: isKnownNode, now: clock.now });

    await cache.get('a');
    clock.advance(30_000);
    await cache.get('a');

    expect(calls).toEqual(['a', 'a']);
  });

  it('does not keep an answer the caller marks as not cacheable', async () => {
    // Issue #58: a new document's draft key is unknown to the server, which answers "no
    // permissions". The document keeps that key once saved, so a remembered answer would make the
    // freshly saved document read-only until the entry expired.
    const { fetch, calls } = recordingFetcher({});
    const cache = createPermissionCache(fetch, { ttlMs: 30_000, isCacheable: isKnownNode });

    expect(await cache.get('draft')).toEqual([]);
    await cache.get('draft');

    expect(calls).toEqual(['draft', 'draft']);
  });

  it('still shares an uncacheable request while it is in flight', async () => {
    const { fetch, calls } = recordingFetcher({});
    const cache = createPermissionCache(fetch, { ttlMs: 30_000, isCacheable: isKnownNode });

    await Promise.all([cache.get('draft'), cache.get('draft')]);

    expect(calls).toEqual(['draft']);
  });

  it('asks the server again after a failed request', async () => {
    let attempts = 0;
    const cache = createPermissionCache(
      (key: string) => {
        attempts += 1;
        return attempts === 1 ? Promise.reject(new Error('offline')) : Promise.resolve([key]);
      },
      { ttlMs: 30_000, isCacheable: isKnownNode },
    );

    await expect(cache.get('a')).rejects.toThrow('offline');
    expect(await cache.get('a')).toEqual(['a']);
    expect(attempts).toBe(2);
  });

  it('forgets everything when cleared', async () => {
    const { fetch, calls } = recordingFetcher({ a: ['Read'] });
    const cache = createPermissionCache(fetch, { ttlMs: 30_000, isCacheable: isKnownNode });

    await cache.get('a');
    cache.clear();
    await cache.get('a');

    expect(calls).toEqual(['a', 'a']);
  });
});
