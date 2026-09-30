import { UmbControllerBase } from '@umbraco-cms/backoffice/class-api';
import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { UMB_MANAGEMENT_API_SERVER_EVENT_CONTEXT } from '@umbraco-cms/backoffice/management-api';
import { createCoalescer } from './coalesce.js';

/**
 * The event sources this package publishes. Must match `AdvancedPermissionsServerEvents` on the
 * server exactly: the hub routes to a SignalR group named by the source string, so a mismatch
 * delivers nothing and nothing about that failure looks like a failure.
 */
export const UAP_EVENT_SOURCES = {
  /** Permission entries for a content node changed. */
  nodePermissions: 'AdvancedPermissions:NodePermissions',
  /** Document-type create permissions changed. */
  docTypePermissions: 'AdvancedPermissions:DocTypePermissions',
  /** Something changed that shifts effective permissions without this package's store being touched. */
  access: 'AdvancedPermissions:Access',
} as const;

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
 */
export class UapSecurityEventsController extends UmbControllerBase {
  /** Keys seen since the host was last called, so a burst arrives as one set. */
  #pending = new Set<string>();

  /** Requests a run, collapsing anything that arrives while one is in flight. */
  #request: () => Promise<void>;

  /**
   * @param host The element this controller belongs to.
   * @param onChanged Called with the keys that changed. Its promise is awaited, which is what lets
   * the coalescer know a refetch is still running.
   */
  constructor(host: UmbControllerHost, onChanged: (keys: ReadonlySet<string>) => Promise<void>) {
    super(host);

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
        context.byEventSourcesAndEventTypes(Object.values(UAP_EVENT_SOURCES), [UAP_EVENT_TYPE_UPDATED]),
        (event) => {
          if (!event) return;
          this.#pending.add(event.key);
          void this.#request();
        },
        'uapObserveSecurityEvents',
      );
    });
  }
}
