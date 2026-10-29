import { describe, expect, it } from 'vitest';
import { satisfiesElementOrFolder, satisfiesVerbs } from './element-verbs.js';

const allowed = new Set(['Umb.Element.Read', 'Umb.Element.Create']);

describe('satisfiesVerbs', () => {
  it('permits when every allOf verb is allowed', () => {
    expect(satisfiesVerbs(allowed, { allOf: ['Umb.Element.Read', 'Umb.Element.Create'] })).toBe(true);
  });

  it('denies when one allOf verb is missing', () => {
    expect(satisfiesVerbs(allowed, { allOf: ['Umb.Element.Read', 'Umb.Element.Delete'] })).toBe(false);
  });

  it('permits when one oneOf verb is allowed', () => {
    expect(satisfiesVerbs(allowed, { oneOf: ['Umb.Element.Delete', 'Umb.Element.Create'] })).toBe(true);
  });

  it('denies when no oneOf verb is allowed', () => {
    expect(satisfiesVerbs(allowed, { oneOf: ['Umb.Element.Delete', 'Umb.Element.Move'] })).toBe(false);
  });

  it('requires both allOf and oneOf when both are given', () => {
    expect(satisfiesVerbs(allowed, { allOf: ['Umb.Element.Read'], oneOf: ['Umb.Element.Move'] })).toBe(false);
    expect(satisfiesVerbs(allowed, { allOf: ['Umb.Element.Read'], oneOf: ['Umb.Element.Create'] })).toBe(true);
  });

  it('permits when no verbs are required, like the native conditions', () => {
    expect(satisfiesVerbs(allowed, {})).toBe(true);
  });

  it('maps each required verb before checking it', () => {
    const toCanonical = (verb: string): string => verb.replace('ElementContainer', 'Element');
    expect(satisfiesVerbs(allowed, { allOf: ['Umb.ElementContainer.Create'] }, toCanonical)).toBe(true);
  });
});

describe('satisfiesElementOrFolder', () => {
  it('permits when the element requirement is met', () => {
    expect(satisfiesElementOrFolder(allowed, { allOf: ['Umb.Element.Create'] }, { allOf: ['Umb.ElementContainer.Delete'] })).toBe(true);
  });

  it('permits when the folder requirement is met, checked as canonical verbs', () => {
    expect(satisfiesElementOrFolder(allowed, { allOf: ['Umb.Element.Delete'] }, { allOf: ['Umb.ElementContainer.Create'] })).toBe(true);
  });

  it('denies when neither requirement is met', () => {
    expect(satisfiesElementOrFolder(allowed, { allOf: ['Umb.Element.Delete'] }, { allOf: ['Umb.ElementContainer.Move'] })).toBe(false);
  });

  it('ignores a requirement that is not configured', () => {
    expect(satisfiesElementOrFolder(allowed, undefined, { allOf: ['Umb.ElementContainer.Create'] })).toBe(true);
    expect(satisfiesElementOrFolder(allowed, { allOf: ['Umb.Element.Move'] }, undefined)).toBe(false);
  });

  it('denies when nothing is configured, like the native condition', () => {
    expect(satisfiesElementOrFolder(allowed, undefined, undefined)).toBe(false);
  });
});
