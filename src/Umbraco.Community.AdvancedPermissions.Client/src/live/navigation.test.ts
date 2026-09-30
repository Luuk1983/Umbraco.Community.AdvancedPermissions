import { describe, expect, it } from 'vitest';
import { isLeavingLocation } from './navigation.js';

const EDITOR = 'https://site.test/umbraco/section/advanced-permissions/workspace/uap-permissions-editor';

describe('isLeavingLocation', () => {
  it('is true for a different path on the same origin', () => {
    expect(isLeavingLocation(EDITOR, '/umbraco/section/content')).toBe(true);
  });

  it('is true for an absolute URL on a different path', () => {
    expect(isLeavingLocation(EDITOR, 'https://site.test/umbraco/section/media')).toBe(true);
  });

  it('is false when the target is the same path', () => {
    expect(isLeavingLocation(EDITOR, '/umbraco/section/advanced-permissions/workspace/uap-permissions-editor')).toBe(false);
  });

  it('ignores a query string or hash change on the same path', () => {
    expect(isLeavingLocation(EDITOR, `${EDITOR}?tab=2`)).toBe(false);
    expect(isLeavingLocation(EDITOR, `${EDITOR}#top`)).toBe(false);
  });

  it('treats a trailing slash as the same location', () => {
    expect(isLeavingLocation(EDITOR, `${EDITOR}/`)).toBe(false);
  });

  it('is true for a different origin even when the path matches', () => {
    expect(isLeavingLocation(EDITOR, 'https://other.test/umbraco/section/advanced-permissions/workspace/uap-permissions-editor')).toBe(true);
  });

  it('is true when the target cannot be parsed, because staying silent would risk losing work', () => {
    expect(isLeavingLocation(EDITOR, 'http://[bad')).toBe(true);
  });
});
