import type { UUIButtonState } from '@umbraco-cms/backoffice/external/uui';

/**
 * Where a surface's refresh control is in its life: waiting to be used, working, confirming that
 * the last refresh landed, or reporting that it did not.
 *
 * `updated` and `failed` are not timed by this package. The control maps them onto `uui-button`'s
 * `success` and `failed` states, which the button clears by itself after a couple of seconds, so a
 * host may leave the phase where it is until the next refresh begins.
 */
export type UapRefreshPhase = 'idle' | 'refreshing' | 'updated' | 'failed';

/**
 * Translates a refresh phase into the state `uui-button` should show.
 *
 * Kept as a pure function, away from the element, so the mapping can be tested without a DOM. The
 * button renders its state in an absolutely positioned layer over an invisible label, which is what
 * lets a phase change without the button changing width.
 * @param phase The phase the host reports.
 * @returns The `uui-button` state; `undefined` means "no state", the ordinary look of the button.
 */
export function buttonStateFor(phase: UapRefreshPhase): UUIButtonState {
  switch (phase) {
    case 'refreshing':
      return 'waiting';
    case 'updated':
      return 'success';
    case 'failed':
      return 'failed';
    default:
      return undefined;
  }
}

/**
 * The shortest time the refreshing state stays on screen. A refresh that answers in 80ms would
 * otherwise show a spinner for a single frame, which reads as a glitch rather than as "something
 * happened". Applies to failures as well as successes, so the outcome never appears sooner than this.
 */
export const UAP_MIN_REFRESH_MS = 1000;

/**
 * Waits for a job to settle and for a minimum time to pass, whichever is later, and reports
 * whether the job succeeded.
 *
 * Built on `allSettled` rather than `all` on purpose: `all` rejects the instant the job does, so a
 * request that fails fast would drop the spinner immediately, and the minimum duration — the very
 * thing this exists to guarantee — would hold on success but not on failure. Never rejects.
 * @param job The work being waited on.
 * @param minMs The minimum time to wait, in milliseconds.
 * @returns `true` if the job fulfilled, `false` if it rejected.
 */
export async function settleAfterMinimum(job: Promise<unknown>, minMs: number): Promise<boolean> {
  const wait = new Promise<void>((resolve) => setTimeout(resolve, minMs));
  const [result] = await Promise.allSettled([job, wait]);
  return result.status === 'fulfilled';
}
