import { describe, expect, it } from 'vitest';
import { collectStamps, readConflicts, stampFromETag } from './concurrency.js';

describe('readConflicts', () => {
  it('returns the conflicts array from a ProblemDetails-with-extension body', () => {
    const conflicts = [{ nodeKey: 'n', roleAlias: 'r', currentEntries: [], currentStamp: 's' }];
    const body = { type: 'Conflict', title: 'Conflict', status: 409, conflicts };
    expect(readConflicts(body, 'Permission')).toEqual(conflicts);
  });

  // The Umbraco interceptor rewrites a body that is not ProblemDetails-like, dropping `conflicts`.
  // Returning { ok: false, conflicts: undefined } once caused a TypeError deep inside a loop.
  it.each([
    ['a rewritten generic body', { type: 'Error', title: 'Something went wrong', status: 409 }],
    ['undefined', undefined],
    ['null', null],
    ['a string', 'Conflict'],
    ['conflicts that is not an array', { conflicts: 'nope' }],
  ])('throws a clear error for %s', (_label, body) => {
    expect(() => readConflicts(body, 'Permission')).toThrow(/Permission save was refused with 409.*"conflicts" array/s);
  });

  it('names the family in the error', () => {
    expect(() => readConflicts({}, 'Library permission')).toThrow(/^Library permission save/);
  });
});

describe('collectStamps', () => {
  it('keys each stamp by the supplied composite key', () => {
    const stamps = collectStamps(
      [
        { nodeKey: 'a', roleAlias: 'x', stamp: '1' },
        { nodeKey: 'b', roleAlias: 'x', stamp: '2' },
      ],
      (s) => `${s.nodeKey}|${s.roleAlias}`,
    );
    expect(stamps.get('a|x')).toBe('1');
    expect(stamps.get('b|x')).toBe('2');
    expect(stamps.size).toBe(2);
  });

  it('treats an absent body as no stamps', () => {
    expect(collectStamps(undefined, () => 'k').size).toBe(0);
    expect(collectStamps(null, () => 'k').size).toBe(0);
  });
});

describe('stampFromETag', () => {
  const headers = (value: string | null): Pick<Headers, 'get'> => ({ get: () => value });

  it('strips the quotes of a strong entity-tag', () => {
    expect(stampFromETag(headers('"abc123"'))).toBe('abc123');
  });

  it('leaves an unquoted value alone', () => {
    expect(stampFromETag(headers('abc123'))).toBe('abc123');
  });

  it('returns an empty stamp when there is no ETag', () => {
    expect(stampFromETag(headers(null))).toBe('');
  });
});
