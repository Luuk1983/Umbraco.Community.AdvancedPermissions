import { describe, expect, it } from 'vitest';
import { EMPTY_SET_STAMP, expectedStampFor, stampOf } from './stamp.js';

describe('EMPTY_SET_STAMP', () => {
  it('is the SHA-256 of the empty string, which is what the server stamps an empty set with', async () => {
    const digest = await globalThis.crypto.subtle.digest('SHA-256', new Uint8Array());
    const hex = [...new Uint8Array(digest)].map((b) => b.toString(16).padStart(2, '0')).join('');

    expect(EMPTY_SET_STAMP).toBe(hex);
  });
});

describe('stampOf', () => {
  it('gives a node the read returned no bucket for the real empty-set stamp, not an empty string', () => {
    // C1: the first save on a node with no stored rule must still be concurrency-checked.
    expect(stampOf(undefined)).toBe(EMPTY_SET_STAMP);
  });

  it('keeps the stamp the server sent for a node it did return', () => {
    expect(stampOf({ stamp: 'abc123' })).toBe('abc123');
  });
});

describe('expectedStampFor', () => {
  it('sends a stamp for a node that had no stored rule, so the server checks the save', () => {
    expect(expectedStampFor(stampOf(undefined))).toBe(EMPTY_SET_STAMP);
  });

  it('omits the stamp only when there genuinely is none (an absent ETag)', () => {
    expect(expectedStampFor('')).toBeUndefined();
  });

  it('passes a real stamp through unchanged', () => {
    expect(expectedStampFor('abc123')).toBe('abc123');
  });
});
