import { UMB_CONFIRM_MODAL, umbOpenModal } from '@umbraco-cms/backoffice/modal';
import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import type { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import { isLeavingLocation } from './navigation.js';

/**
 * Asks the user whether they really want to throw away their unsaved permission changes.
 *
 * The single source of the question, so leaving the page, switching user group or document type,
 * and clearing the selection all put the same words in front of the user. Uses Umbraco's public
 * confirm modal rather than its discard-changes modal: that one has fixed, generic wording
 * ("navigate away from this page") and accepts no data, so it cannot say that it is the permission
 * edits on screen that are about to be lost.
 * @param host The element opening the modal.
 * @param localize The host's localization controller, used to resolve the prompt text.
 * @returns `true` if the user chose to discard, `false` if they chose to keep editing or dismissed
 * the dialog.
 */
export async function confirmDiscardUnsavedChanges(
  host: UmbControllerHost,
  localize: UmbLocalizationController,
): Promise<boolean> {
  try {
    await umbOpenModal(host, UMB_CONFIRM_MODAL, {
      data: {
        headline: localize.term('uap_discardPromptTitle'),
        content: localize.term('uap_discardPromptBody'),
        cancelLabel: localize.term('uap_discardPromptStay'),
        confirmLabel: localize.term('uap_discardPromptConfirm'),
        color: 'danger',
      },
    });
    return true;
  } catch {
    // The modal rejects when it is cancelled or dismissed; both mean "stay".
    return false;
  }
}

/** What the guard needs from the editor it protects. */
export interface UapUnsavedChangesGuardOptions {
  /** Whether the editor currently holds edits that have not been saved. */
  hasChanges: () => boolean;
  /** Asks the user whether to discard; resolves `true` to proceed. */
  confirm: () => Promise<boolean>;
  /** Throws away the pending edits. Runs after the user confirmed leaving, before navigating. */
  onDiscard: () => void;
}

/**
 * Stops a router navigation, or a browser close or reload, from silently throwing away an editor's
 * unsaved changes.
 *
 * This mirrors how Umbraco's own entity workspaces do it (`entity-detail-workspace-base.ts`),
 * with one deliberate difference: the listeners are attached and detached by explicit calls from
 * the element's `connectedCallback` / `disconnectedCallback`, not in a constructor. A core
 * workspace context is disposed together with its host; these elements mount and unmount
 * repeatedly, and a listener left on `window` would keep a detached editor vetoing navigation
 * forever.
 *
 * It does not cover changes made inside the page (switching group, clearing the selection); those
 * are not router navigations, so no event announces them and the editor asks for itself.
 */
export class UapUnsavedChangesGuard {
  readonly #options: UapUnsavedChangesGuardOptions;

  /**
   * Set only while the guard is replaying a navigation the user has already approved. See
   * `#confirmThenNavigate` for why it must exist.
   */
  #allowNavigateAway = false;

  /** True while the prompt is open, so a second navigation attempt cannot stack a second prompt. */
  #prompting = false;

  /** The editor's own URL when it mounted; the reference point for deciding whether a navigation leaves. */
  #entryHref = '';

  /**
   * Creates a guard. It does nothing until {@link attach} is called.
   * @param options The editor-specific behaviour this guard delegates to.
   */
  constructor(options: UapUnsavedChangesGuardOptions) {
    this.#options = options;
  }

  /** Starts guarding. Call from `connectedCallback`. */
  attach(): void {
    this.#entryHref = window.location.href;
    this.#allowNavigateAway = false;
    window.addEventListener('willchangestate', this.#onWillChangeState);
    window.addEventListener('beforeunload', this.#onBeforeUnload);
  }

  /** Stops guarding and clears all state. Call from `disconnectedCallback`. */
  detach(): void {
    window.removeEventListener('willchangestate', this.#onWillChangeState);
    window.removeEventListener('beforeunload', this.#onBeforeUnload);
    this.#allowNavigateAway = false;
    this.#prompting = false;
  }

  /**
   * Reacts to Umbraco's router announcing, synchronously and before it acts, that the URL is about
   * to change. Cancelling the event cancels the navigation.
   * @param e The `willchangestate` event, carrying the target `url`.
   */
  readonly #onWillChangeState = (e: Event): void => {
    // The replay of an approved navigation passes straight through.
    if (this.#allowNavigateAway) return;
    if (!this.#options.hasChanges()) return;

    const detail = (e as CustomEvent<{ url?: string | null; eventName?: string }>).detail;
    const isPop = detail?.eventName === 'popstate';
    const target = detail?.url;

    // history.forward()/go() announce no target, so there is nothing to replay after confirming.
    // Known gap: nothing in this package or the backoffice calls them.
    if (target == null || target === '') return;
    if (!isLeavingLocation(this.#entryHref, target)) return;

    // Events are synchronous and the prompt is not, so the navigation has to be vetoed right now,
    // before we know the answer, and re-issued later if the answer is yes.
    e.preventDefault();
    if (this.#prompting) return;

    void this.#confirmThenNavigate(target, isPop);
  };

  /**
   * Asks the user, then either replays the vetoed navigation or leaves them where they were.
   * @param target The URL the vetoed navigation was heading for.
   * @param isPop True when the navigation came from the browser's back/forward buttons.
   */
  async #confirmThenNavigate(target: string, isPop: boolean): Promise<void> {
    this.#prompting = true;
    let confirmed = false;
    try {
      confirmed = await this.#options.confirm();
    } finally {
      this.#prompting = false;
    }

    if (!confirmed) {
      // A back/forward press has already moved the address bar by the time we hear about it;
      // vetoing only stops the router reacting. Put the URL back so it matches what is on screen.
      if (isPop) history.pushState({}, '', this.#entryHref);
      return;
    }

    this.#options.onDiscard();

    // Replaying the navigation means calling history.pushState, which announces itself with
    // another `willchangestate` - this same handler, seeing the same unsaved-changes situation,
    // would veto it and prompt again, forever. The flag tells that second pass "already approved".
    // pushState dispatches synchronously, so the flag can be lowered again in `finally` rather than
    // left raised; a raised flag on an editor that survived the navigation would disable the
    // guard for good. This is the spot someone will want to simplify away: do not.
    this.#allowNavigateAway = true;
    try {
      history.pushState({}, '', target);
    } finally {
      this.#allowNavigateAway = false;
    }
  }

  /**
   * Browser close, reload and address-bar navigation. Only the browser's native dialog is
   * possible here, and its wording cannot be customised.
   * @param e The `beforeunload` event.
   */
  readonly #onBeforeUnload = (e: BeforeUnloadEvent): void => {
    if (!this.#options.hasChanges()) return;
    e.preventDefault();
    e.returnValue = '';
  };
}
