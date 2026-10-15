import type { ReactiveController } from '@umbraco-cms/backoffice/external/lit';
import type { StartNodesResponse } from '../models/permission.models.js';

/** Who to load start nodes for: a user or a user group (role). */
export type StartNodeSubject = { userKey: string } | { roleAlias: string };

/** Fetches a subject's start nodes; rejects on failure. Injected so the loader needs no backoffice imports. */
export type StartNodesFetcher = (subject: StartNodeSubject, signal: AbortSignal) => Promise<StartNodesResponse>;

/** The part of a Lit host the loader needs: somewhere to register and a way to ask for a re-render. */
export interface StartNodesLoaderHost {
  /** Registers the loader as a reactive controller of the host. */
  addController(controller: ReactiveController): void;
  /** Asks the host to re-render. */
  requestUpdate(): void;
}

/**
 * Loads a subject's start nodes for a viewer's strip and blocked-cell dialog, and owns the
 * data/loading/error state the strip renders. Both viewers share it so the abort and loading
 * rules live in one place.
 *
 * The strip is independent of the grid: a failure here only sets {@link error}, it never affects
 * how the grid loads.
 */
export class StartNodesLoader implements ReactiveController {
  /** The subject's start nodes; null until loaded, while a foreground load runs, or after {@link clear}. */
  data: StartNodesResponse | null = null;
  /** True while a foreground load for a newly selected subject is in flight. */
  loading = false;
  /** True when the latest load failed. */
  error = false;

  #host: StartNodesLoaderHost;
  #fetch: StartNodesFetcher;
  /** Identifies the newest load; an older load that finishes late must not touch the state. */
  #latest = 0;

  /**
   * @param host The element to re-render when the state changes.
   * @param fetcher Fetches the start nodes for a subject.
   */
  constructor(host: StartNodesLoaderHost, fetcher: StartNodesFetcher) {
    this.#host = host;
    this.#fetch = fetcher;
    host.addController(this);
  }

  /**
   * Loads the subject's start nodes.
   * @param subject The user or user group to load for.
   * @param signal Aborts when the selection changes; an aborted load discards its result.
   * @param background True for a live refresh: keep the current data until the new data arrives.
   * @returns True when the load succeeded or was superseded or aborted; false when it failed.
   */
  async load(subject: StartNodeSubject, signal: AbortSignal, background = false): Promise<boolean> {
    const id = ++this.#latest;
    if (!background) {
      this.data = null;
      this.loading = true;
    }
    this.error = false;
    this.#host.requestUpdate();

    try {
      const result = await this.#fetch(subject, signal);
      if (signal.aborted || id !== this.#latest) return true;
      this.data = result;
      return true;
    } catch {
      if (signal.aborted || id !== this.#latest) return true;
      this.error = true;
      return false;
    } finally {
      // Only the newest load owns the flag, so loading never sticks: an older load that finishes
      // late leaves it alone, and the newest always clears it, aborted or not.
      if (id === this.#latest) this.loading = false;
      this.#host.requestUpdate();
    }
  }

  /** Discards any load still in flight when the host leaves the page, so a late answer cannot land on a detached element. */
  hostDisconnected(): void {
    this.#latest++;
  }

  /** Forgets everything, e.g. when the selection is cleared. Any load still in flight is discarded. */
  clear(): void {
    this.#latest++;
    this.data = null;
    this.loading = false;
    this.error = false;
    this.#host.requestUpdate();
  }
}
