import { describe, expect, it } from 'vitest';
import en from './en.js';

/**
 * Every language file must declare the start-node function terms with the same parameters, in the
 * same order, as `en.ts`. `localize.term` passes arguments by position, so a file that declares
 * `(subject, nodePath, verb)` where English has `(subject, verb, nodePath)` prints the verb where the
 * node belongs. Word order inside the sentence is free; the parameter list is not.
 */

/** Every language file except English, loaded eagerly. */
const locales = import.meta.glob<{ default: { uap: Record<string, unknown> } }>('./*.ts', { eager: true });

/**
 * Reads the parameter names of a localization function term.
 * @param fn The term.
 * @returns The parameter names, in declaration order.
 */
function parameterNames(fn: (...args: string[]) => string): string[] {
  const source = fn.toString();
  const list = source.slice(source.indexOf('(') + 1, source.indexOf(')'));
  return list.split(',').map((p) => p.trim()).filter(Boolean);
}

/** The English start-node function terms and their parameter names. */
const englishFunctions = Object.entries(en.uap)
  .filter(([key, value]) => key.startsWith('startNodes_') && typeof value === 'function')
  .map(([key, value]) => [key, parameterNames(value as (...args: string[]) => string)] as const);

describe('start-node terms in every language', () => {
  it('has English function terms to compare against', () => {
    expect(englishFunctions.length).toBeGreaterThan(0);
  });

  for (const [path, module] of Object.entries(locales)) {
    if (path.endsWith('.test.ts')) continue;
    it(`${path} declares each start-node function term with the English parameters`, () => {
      for (const [key, params] of englishFunctions) {
        const term = module.default.uap[key];
        expect(typeof term, `${path} ${key}`).toBe('function');
        expect(parameterNames(term as (...args: string[]) => string), `${path} ${key}`).toEqual(params);
      }
    });
  }
});
