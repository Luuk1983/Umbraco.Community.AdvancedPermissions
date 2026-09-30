/**
 * The concurrency stamp of a set with nothing in it: the lowercase-hex SHA-256 of the empty
 * string, which is what the server's `PermissionStamp` produces for zero entries.
 *
 * The document-type read endpoint only returns a bucket for a node that has something stored, so a
 * node with nothing stored arrives with no stamp at all. That must never be read as "no stamp
 * available": a save that sends no `expectedStamp` makes the server skip the check, so the first
 * write to an empty node, or to a node a colleague has just cleared, would overwrite somebody
 * else's without a dialog. Sending this value instead makes the server compare against what it
 * holds and refuse if the node has stopped being empty.
 *
 * Defined once, here, and pinned against the real hash by `stamp.test.ts`, so the editors cannot
 * drift from the server's canonical form without a test failing.
 */
export const EMPTY_SET_STAMP = 'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855';

/**
 * The stamp to hold for a node, given what the server returned for it.
 *
 * "The read returned nothing for this node" and "the read gave no stamp" are different things, and
 * only the second is unknowable. A node the read has no bucket for is one with nothing stored, which
 * is a fully known state with a fully known stamp, {@link EMPTY_SET_STAMP}. Returning `''` here, as
 * this used to, made the first save on any node without a rule skip the server's concurrency check.
 * @param bucket The node's bucket from a read, or `undefined` when the read had none for it.
 * @returns The bucket's own stamp, or {@link EMPTY_SET_STAMP} when there is no bucket.
 */
export function stampOf(bucket: { stamp: string } | undefined): string {
  return bucket?.stamp ?? EMPTY_SET_STAMP;
}

/**
 * The `expectedStamp` to send when saving a node.
 *
 * The one place `''` is given its meaning: a stamp that is empty is one a read could not supply
 * because the response carried no `ETag` at all, so there is nothing to compare against and the
 * field is omitted. It is never what a node with no stored rule holds; that is
 * {@link EMPTY_SET_STAMP}, a real value, and is sent like any other so the server checks it.
 * @param stamp The stamp the editor holds for the node.
 * @returns The stamp to send, or `undefined` to omit the field when there is genuinely none.
 */
export function expectedStampFor(stamp: string): string | undefined {
  return stamp === '' ? undefined : stamp;
}
