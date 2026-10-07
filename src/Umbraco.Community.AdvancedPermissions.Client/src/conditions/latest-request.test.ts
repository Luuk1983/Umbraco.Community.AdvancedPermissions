import { describe, expect, it } from 'vitest';
import { createLatestRequest } from './latest-request.js';

describe('createLatestRequest', () => {
  it('reports a request as current while no newer one has started', () => {
    const latest = createLatestRequest();

    const isCurrent = latest.begin();

    expect(isCurrent()).toBe(true);
  });

  it('reports an older request as stale once a newer one has started', () => {
    // Issue #58: a condition asks about a new document's draft key (slow, over the network) and
    // then about its parent (instant, from the cache). The draft's late "no permissions" answer
    // must not overwrite the parent's answer.
    const latest = createLatestRequest();

    const draftQuestion = latest.begin();
    const parentQuestion = latest.begin();

    expect(draftQuestion()).toBe(false);
    expect(parentQuestion()).toBe(true);
  });

  it('keeps separate trackers independent', () => {
    const first = createLatestRequest();
    const second = createLatestRequest();

    const isCurrent = first.begin();
    second.begin();

    expect(isCurrent()).toBe(true);
  });
});
