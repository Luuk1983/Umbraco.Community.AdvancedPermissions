import { describe, expect, it } from 'vitest';
import { createCoalescer } from './coalesce.js';

/**
 * A promise plus the function that settles it, so a test controls when work finishes.
 * @returns The promise and its resolver.
 */
function deferred(): { promise: Promise<void>; resolve: () => void } {
  let resolve!: () => void;
  const promise = new Promise<void>((r) => { resolve = r; });
  return { promise, resolve };
}

describe('createCoalescer', () => {
  it('runs immediately when nothing is in flight', async () => {
    let runs = 0;
    const run = createCoalescer(async () => { runs += 1; });
    await run();
    expect(runs).toBe(1);
  });

  it('collapses a burst arriving during one run into exactly one re-run', async () => {
    // Publishing with descendants or emptying the recycle bin delivers one event per affected
    // node. Without this, a surface refetches once per event, each time against data already
    // stale by the time the first answer came back.
    let runs = 0;
    const gate = deferred();
    const run = createCoalescer(async () => {
      runs += 1;
      if (runs === 1) await gate.promise;
    });

    const first = run();
    void run();
    void run();
    void run();
    gate.resolve();
    await first;

    expect(runs).toBe(2);
  });

  it('runs again normally once the queue has drained', async () => {
    let runs = 0;
    const run = createCoalescer(async () => { runs += 1; });
    await run();
    await run();
    expect(runs).toBe(2);
  });

  it('runs a third time when a request arrives during the re-run', async () => {
    // Without re-checking after every run, a request that arrives while the re-run itself is in
    // flight sets the queued flag, but nothing ever looks at it again: `finally` clears it
    // unconditionally and the request is silently dropped. In the real system that is a
    // permission change that never gets refetched — the screen stays wrong with nothing in the
    // console or the UI to say so.
    let runs = 0;
    const gate1 = deferred();
    const gate2 = deferred();
    let signalJob2Started!: () => void;
    const job2Started = new Promise<void>((resolve) => { signalJob2Started = resolve; });

    const run = createCoalescer(async () => {
      runs += 1;
      if (runs === 1) {
        await gate1.promise;
      } else if (runs === 2) {
        signalJob2Started();
        await gate2.promise;
      }
    });

    const first = run();
    void run(); // arrives during the first run, queuing the re-run
    gate1.resolve();
    await job2Started; // the re-run (run 2) is now in flight
    void run(); // arrives during the re-run itself, and must queue a third run
    gate2.resolve();
    await first;

    expect(runs).toBe(3);
  });

  it('clears the in-flight flag when the work throws', async () => {
    // A rejected refetch that left the flag set would wedge the surface permanently: every later
    // event would queue behind a run that had already finished.
    let runs = 0;
    const run = createCoalescer(async () => {
      runs += 1;
      throw new Error('network');
    });

    await expect(run()).rejects.toThrow('network');
    await expect(run()).rejects.toThrow('network');
    expect(runs).toBe(2);
  });
});
