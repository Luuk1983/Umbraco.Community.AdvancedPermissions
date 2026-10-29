import { toCanonicalElementVerb } from '../models/element-permission.models.js';

/** The verbs a permission condition requires, in the shape the native condition configs use. */
export interface VerbRequirement {
  /** Every one of these verbs must be allowed. */
  allOf?: Array<string>;
  /** At least one of these verbs must be allowed. */
  oneOf?: Array<string>;
}

/**
 * Checks a node's allowed verbs against a requirement, the way Umbraco's native permission conditions do:
 * allOf and oneOf must both hold when both are given, and a requirement with neither permits.
 * @param allowed The verbs allowed at the node.
 * @param requirement The required verbs.
 * @param mapVerb Translates each required verb before it is looked up; identity by default.
 * @returns Whether the requirement is met.
 */
export function satisfiesVerbs(
  allowed: ReadonlySet<string>,
  requirement: VerbRequirement,
  mapVerb: (verb: string) => string = (verb) => verb,
): boolean {
  const allOfPermitted = requirement.allOf?.length
    ? requirement.allOf.every((verb) => allowed.has(mapVerb(verb)))
    : true;
  const oneOfPermitted = requirement.oneOf?.length
    ? requirement.oneOf.some((verb) => allowed.has(mapVerb(verb)))
    : true;
  return allOfPermitted && oneOfPermitted;
}

/**
 * Decides Umbraco's "element or element folder" requirement (the Library Create actions) against one
 * node's allowed canonical verbs. Element verbs are checked as they are; folder verbs are first mapped to
 * the canonical element verbs the package stores. A requirement that is not configured does not count, so
 * with neither configured nothing is permitted, as in the native condition.
 * @param allowed The canonical verbs allowed at the node.
 * @param element The element requirement, if configured.
 * @param folder The folder requirement, if configured.
 * @returns Whether either configured requirement is met.
 */
export function satisfiesElementOrFolder(
  allowed: ReadonlySet<string>,
  element: VerbRequirement | undefined,
  folder: VerbRequirement | undefined,
): boolean {
  return (element !== undefined && satisfiesVerbs(allowed, element))
    || (folder !== undefined && satisfiesVerbs(allowed, folder, toCanonicalElementVerb));
}
