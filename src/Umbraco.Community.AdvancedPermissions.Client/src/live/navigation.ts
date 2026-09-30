/**
 * Decides whether a router navigation would take the user off the page they are editing.
 *
 * Kept pure (no `window`, no `history`) so the one decision that determines whether somebody's
 * unsaved work is protected can be tested without a browser. Only the path and origin count: a
 * query-string or hash change leaves the editor mounted with its pending edits intact, so
 * prompting for it would nag the user about something that loses nothing.
 *
 * An unparseable target is reported as leaving. The two ways to be wrong are not equal: a needless
 * prompt costs a click, a missing one costs the user's work.
 * @param entryHref The full URL of the editor when it mounted. Deliberately not `location.href`
 * read at event time: for a back-button navigation the browser has already moved `location` by
 * the time the router hears about it, so comparing against it would always say "same place".
 * @param targetUrl The URL being navigated to, absolute or relative to `entryHref`.
 * @returns `true` when the target is a different page from the one the editor is on.
 */
export function isLeavingLocation(entryHref: string, targetUrl: string): boolean {
  try {
    const entry = new URL(entryHref);
    const target = new URL(targetUrl, entry);
    return entry.origin !== target.origin || trimTrailingSlash(entry.pathname) !== trimTrailingSlash(target.pathname);
  } catch {
    return true;
  }
}

/**
 * Removes a trailing slash so `/a/b` and `/a/b/` compare equal.
 * @param path A URL pathname.
 * @returns The path without a trailing slash, except for the root path itself.
 */
function trimTrailingSlash(path: string): string {
  return path.length > 1 && path.endsWith('/') ? path.slice(0, -1) : path;
}
