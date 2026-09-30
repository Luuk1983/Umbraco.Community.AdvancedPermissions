import { UmbControllerBase } from '@umbraco-cms/backoffice/class-api';
import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { UMB_MANAGEMENT_API_SERVER_EVENT_CONTEXT } from '@umbraco-cms/backoffice/management-api';
import { createCoalescer } from './coalesce.js';
import { settleAfterMinimum, UAP_MIN_REFRESH_MS } from './refresh-phase.js';
import type { UapRefreshPhase } from './refresh-phase.js';

/**
 * The event sources this package publishes. Must match `AdvancedPermissionsServerEvents` on the
 * server exactly: the hub routes to a SignalR group named by the source string, so a mismatch
 * delivers nothing and nothing about that failure looks like a failure.
 */
export const UAP_EVENT_SOURCES = {
  /** Permission entries for a content node changed. */
  nodePermissions: 'AdvancedPermissions:NodePermissions',
  /** Permission entries for a library (element) node changed. */
  elementPermissions: 'AdvancedPermissions:ElementPermissions',
  /** Document-type create permissions changed. */
  docTypePermissions: 'AdvancedPermissions:DocTypePermissions',
  /** Element-type create permissions changed. */
  elementTypePermissions: 'AdvancedPermissions:ElementTypePermissions',
  /** Something changed that shifts effective permissions without this package's store being touched. */
  access: 'AdvancedPermissions:Access',
} as const;

/** One of the sources this package publishes. */
export type UapEventSource = (typeof UAP_EVENT_SOURCES)[keyof typeof UAP_EVENT_SOURCES];

/**
 * The event type this package's server events carry. Must match
 * `AdvancedPermissionsServerEvents.EventType.Updated` on the server exactly — same reasoning as
 * {@link UAP_EVENT_SOURCES}: a mismatch delivers nothing and nothing about that failure looks like
 * a failure.
 */
export const UAP_EVENT_TYPE_UPDATED = 'Updated';

/**
 * Connects Umbraco's live change notifications to one Advanced Permissions surface.
 *
 * Consumes the built-in management-API server-event context rather than opening a connection of
 * this package's own. The hub is already there, already authenticated and already held open by the
 * backoffice for its own cache invalidation; a second socket would buy nothing and would need its
 * own authorization, reconnect and backoff to get wrong.
 *
 * What arrives is a hint, never a statement: `UmbManagementApiServerEventModel` carries a source, a
 * type and one key, with no payload, so the host always answers by refetching rather than by
 * believing. The keys are passed on anyway, because a surface can use them to decide whether it is
 * showing anything affected at all.
 *
 * Filtering is done through the context's own `byEventSourcesAndEventTypes`, not by subscribing to
 * the raw `events` stream and checking sources by hand: it is the platform's intended API for this,
 * so it keeps working if the context's internals change, and filtering on event type as well as
 * source means that if this package ever sends a second event type, a surface that does not care
 * about it is not woken up by it.
 *
 * Bursts are coalesced before the host hears about them — see {@link createCoalescer}.
 *
 * The same coalesced path serves the manual refresh control ({@link UapSecurityEventsController.refresh}),
 * so a click can never run alongside a live-triggered refetch; it queues behind it exactly as a
 * second event would. The controller also owns the control's phase (idle, refreshing, just
 * updated or failed), because only it sees both kinds of trigger: a surface that tracked the phase itself
 * would show a spinner for clicks but not for live events, or the other way round.
 */
export class UapSecurityEventsController extends UmbControllerBase {
  /** Keys seen since the host was last called, so a burst arrives as one set. */
  #pending = new Set<string>();

  /** Requests a run, collapsing anything that arrives while one is in flight. */
  #request: () => Promise<void>;

  /** The most recent promise `#request` handed out; a new identity means a new run began. */
  #latestRun: Promise<void> | undefined = undefined;

  /** The tracker for the refresh currently showing as `refreshing`, if any. */
  #tracking: Promise<void> | undefined = undefined;

  /** Tells the host the control's phase changed. */
  #onPhaseChange: (phase: UapRefreshPhase) => void;

  /**
   * @param host The element this controller belongs to.
   * @param onChanged Called with the keys that changed. Its promise is awaited, which is what lets
   * the coalescer know a refetch is still running. Should reject when the refetch failed, so the
   * control does not claim the data was updated.
   * @param onPhaseChange Called whenever the refresh control's phase changes.
   * @param sources Which of this package's event sources to listen to. Defaults to all of them.
   *
   * A surface should name the sources whose data it actually shows. The server deliberately splits
   * document-type and element-type writes across two sources so those two editors do not wake for
   * each other's changes, and gives the library family its own — subscribing to everything throws
   * that away and costs a refetch per unrelated write.
   *
   * The default is every source rather than none, because the failure modes are not symmetric: an
   * extra wake costs one refetch that reconciliation turns into a no-change, whereas a missing one
   * leaves the screen silently wrong, which is the failure this whole feature exists to prevent.
   */
  constructor(
    host: UmbControllerHost,
    onChanged: (keys: ReadonlySet<string>) => Promise<void>,
    onPhaseChange: (phase: UapRefreshPhase) => void,
    sources: ReadonlyArray<UapEventSource> = Object.values(UAP_EVENT_SOURCES),
  ) {
    super(host);
    this.#onPhaseChange = onPhaseChange;

    this.#request = createCoalescer(async () => {
      // Taken and cleared before awaiting, so keys arriving during the refetch belong to the next
      // pass rather than being dropped as already handled.
      const keys = this.#pending;
      this.#pending = new Set<string>();
      await onChanged(keys);
    });

    this.consumeContext(UMB_MANAGEMENT_API_SERVER_EVENT_CONTEXT, (context) => {
      if (!context) return;
      this.observe(
        context.byEventSourcesAndEventTypes([...sources], [UAP_EVENT_TYPE_UPDATED]),
        (event) => {
          if (!event) return;
          this.#pending.add(event.key);
          void this.#trigger();
        },
        'uapObserveSecurityEvents',
      );
    });
  }

  /**
   * Refreshes on request — the manual counterpart of a live event. Goes through the same
   * coalescer, so clicking while a live refetch is running queues one follow-up instead of
   * overlapping it, and mashing the button cannot stack requests.
   * @returns A promise that resolves when the refresh, and any follow-up it triggered, is done.
   * Never rejects: failure is shown by the control's `failed` phase, not by an exception.
   */
  refresh(): Promise<void> {
    return this.#trigger();
  }

  /**
   * Starts a coalesced run and puts the control into `refreshing` for at least
   * {@link UAP_MIN_REFRESH_MS}, then into `updated`, or into `failed` if the run failed. Neither is timed
   * here: `uui-button` clears its own success and failed states, and the next refresh replaces the
   * phase anyway.
   *
   * Only one tracker exists at a time. A trigger arriving while one is active is folded into the
   * coalesced run it is already waiting on; a run that begins after the tracked one finished but
   * before the minimum elapsed is picked up by the loop in the tracker, so the control never
   * reports "updated" while a refetch is still in flight.
   * @returns The tracker, which never rejects.
   */
  #trigger(): Promise<void> {
    // Attached to the promise straight away so a failed run is never an unhandled rejection,
    // whichever caller ends up awaiting it.
    this.#latestRun = this.#request();
    this.#latestRun.catch(() => undefined);
    if (this.#tracking) return this.#tracking;

    this.#onPhaseChange('refreshing');

    /** Waits until no newer run has started, then reports whether the last one succeeded. */
    const drain = async (): Promise<void> => {
      let observed: Promise<void> | undefined;
      let failed = false;
      do {
        observed = this.#latestRun;
        try {
          await observed;
          failed = false;
        } catch {
          // Only the newest run's outcome counts: an earlier failure is superseded by a later run.
          failed = true;
        }
      } while (observed !== this.#latestRun);
      if (failed) throw new Error('refresh failed');
    };

    this.#tracking = settleAfterMinimum(drain(), UAP_MIN_REFRESH_MS).then((succeeded) => {
      this.#tracking = undefined;
      this.#onPhaseChange(succeeded ? 'updated' : 'failed');
    });
    return this.#tracking;
  }
}
