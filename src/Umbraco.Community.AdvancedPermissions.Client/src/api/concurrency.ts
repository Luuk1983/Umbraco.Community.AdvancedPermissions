// Shared plumbing for the three permission families' optimistic-concurrency calls (content,
// library element and document type). The families differ only in which endpoint they hit and how
// a saved row is keyed; the two response conventions below are identical, and both were expensive
// to get right, so they live in one place rather than being re-derived per family.

/**
 * Reads the conflict list off a `409 Conflict` body, or throws if it is not there.
 *
 * The server sends the conflicts as an extension member of a ProblemDetails. It has to be one:
 * Umbraco's HTTP client installs an interceptor that replaces any error body which does not satisfy
 * `isProblemDetailsLike` (`type`, `title` and `status`) with a generic one, and that would drop the
 * `conflicts` member. If that ever happens again, this fails with a message that says so, rather
 * than the caller receiving `{ ok: false, conflicts: undefined }` and dying on a bare "not iterable"
 * deep inside a loop, which is exactly what happened in the first implementation.
 *
 * @typeparam TConflict The conflict item shape of the family being saved.
 * @param error The error body the generated client produced for the `409` response.
 * @param subject What was being saved, for the error message (e.g. `'Permission'`).
 * @returns The conflicting items the server named.
 * @throws {Error} When the body carries no `conflicts` array.
 */
export function readConflicts<TConflict>(error: unknown, subject: string): TConflict[] {
  const conflicts = (error as { conflicts?: unknown } | null | undefined)?.conflicts;
  if (!Array.isArray(conflicts)) {
    throw new Error(
      `${subject} save was refused with 409 but the response carried no "conflicts" array. ` +
        'The response body was probably rewritten by the Umbraco interceptor because it no longer ' +
        'looks like a ProblemDetails (type, title and status).',
    );
  }
  return conflicts as TConflict[];
}

/**
 * Indexes the stamps a successful batch save returned by a caller-defined composite key, so the
 * caller can look up the new stamp for each thing it saved.
 *
 * @typeparam TSaved The saved-row shape of the family being saved; must carry its new `stamp`.
 * @param saved The rows the server returned; treated as empty when the body is absent.
 * @param keyOf Builds the lookup key for a row (e.g. `nodeKey|roleAlias`).
 * @returns The new stamp for each saved row, keyed by `keyOf`.
 */
export function collectStamps<TSaved extends { stamp: string }>(
  saved: readonly TSaved[] | null | undefined,
  keyOf: (row: TSaved) => string,
): Map<string, string> {
  const stamps = new Map<string, string>();
  for (const row of saved ?? []) stamps.set(keyOf(row), row.stamp);
  return stamps;
}

/**
 * Reads a concurrency stamp from a response's `ETag` header, stripping the surrounding quotes.
 *
 * The server writes the stamp as a quoted strong entity-tag (RFC 9110), so the raw header value is
 * `"abc123..."` including the quotes. They must be stripped: a save compares `expectedStamp`
 * against the unquoted stamp computed from the stored entries, so leaving them in would make the
 * comparison fail every time, not just sometimes.
 *
 * @param headers The response headers.
 * @returns The bare stamp, or `''` when the server sent no `ETag`. Callers must treat an empty
 * stamp as "no stamp available" and omit `expectedStamp` accordingly, rather than sending `''`,
 * which would be a guaranteed false conflict.
 */
export function stampFromETag(headers: Pick<Headers, 'get'>): string {
  const rawETag = headers.get('ETag');
  return rawETag ? rawETag.replace(/^"|"$/g, '') : '';
}
