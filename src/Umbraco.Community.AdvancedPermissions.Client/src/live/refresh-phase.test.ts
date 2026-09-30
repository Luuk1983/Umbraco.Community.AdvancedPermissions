import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { buttonStateFor, settleAfterMinimum } from './refresh-phase.js';

describe('settleAfterMinimum', () => {
  beforeEach(() => { vi.useFakeTimers(); });
  afterEach(() => { vi.useRealTimers(); });

  /**
   * Tracks whether a promise has settled without awaiting it.
   * @param p The promise to watch.
   * @returns An object whose `done` flips once the promise settles.
   */
  function watch(p: Promise<unknown>): { done: boolean } {
    const state = { done: false };
    void p.then(() => { state.done = true; });
    return state;
  }

  it('holds for the minimum when the job finishes sooner', async () => {
    const state = watch(settleAfterMinimum(Promise.resolve(), 1000));
    await vi.advanceTimersByTimeAsync(999);
    expect(state.done).toBe(false);
    await vi.advanceTimersByTimeAsync(1);
    expect(state.done).toBe(true);
  });

  it('holds for the minimum when the job fails fast, and reports the failure', async () => {
    const outcome = settleAfterMinimum(Promise.reject(new Error('nope')), 1000);
    const state = watch(outcome);
    await vi.advanceTimersByTimeAsync(999);
    expect(state.done).toBe(false);
    await vi.advanceTimersByTimeAsync(1);
    expect(await outcome).toBe(false);
  });

  it('does not add time when the job outlasts the minimum', async () => {
    let finish!: () => void;
    const job = new Promise<void>((resolve) => { finish = resolve; });
    const outcome = settleAfterMinimum(job, 1000);
    const state = watch(outcome);
    await vi.advanceTimersByTimeAsync(5000);
    expect(state.done).toBe(false);
    finish();
    expect(await outcome).toBe(true);
  });
});

describe('buttonStateFor', () => {
  it('shows no state while idle, so the button looks like an ordinary button', () => {
    expect(buttonStateFor('idle')).toBeUndefined();
  });

  it('maps refreshing to the waiting spinner', () => {
    expect(buttonStateFor('refreshing')).toBe('waiting');
  });

  it('maps updated to success', () => {
    expect(buttonStateFor('updated')).toBe('success');
  });

  it('maps failed to failed, so a failed refresh is not silent', () => {
    expect(buttonStateFor('failed')).toBe('failed');
  });
});
