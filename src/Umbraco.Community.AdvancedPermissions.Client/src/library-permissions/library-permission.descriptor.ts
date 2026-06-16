import {
  ELEMENT_VERB_READ,
  ELEMENT_VERB_CREATE,
  ELEMENT_VERB_UPDATE,
  ELEMENT_VERB_DELETE,
  ELEMENT_VERB_PUBLISH,
  ELEMENT_VERB_UNPUBLISH,
  ELEMENT_VERB_DUPLICATE,
  ELEMENT_VERB_MOVE,
  ELEMENT_VERB_ROLLBACK,
  ELEMENT_ONLY_VERBS,
} from '../models/element-permission.models.js';

/**
 * Which scope sides of a permission cell can be set for a given verb and node kind. This is the core of
 * the generic, reusable applicability model: an editor consults it per (verb, node-kind) to drive both
 * the cell rendering (N/A halves) and the scope dialog (which sides are editable). A future entity with
 * a different verb set only needs its own descriptor.
 */
export interface VerbApplicability {
  /** Whether the verb can be set on the node itself. */
  nodeApplicable: boolean;
  /** Whether the verb can be set on the node's descendants. */
  descApplicable: boolean;
}

/** Metadata for a single verb column in the library editor. */
export interface VerbMeta {
  /** The canonical verb (e.g. <c>Umb.Element.Read</c>). */
  verb: string;
  /** Short display name shown in the column header. */
  displayName: string;
  /** Logical grouping, mirroring Umbraco's native grouping of the element actions. */
  group: 'general' | 'structure' | 'administration';
}

/** The nine canonical element verbs, in editor column order, with display names and groups. */
export const LIBRARY_VERB_METAS: readonly VerbMeta[] = [
  { verb: ELEMENT_VERB_READ, displayName: 'Read', group: 'general' },
  { verb: ELEMENT_VERB_CREATE, displayName: 'Create', group: 'general' },
  { verb: ELEMENT_VERB_UPDATE, displayName: 'Update', group: 'general' },
  { verb: ELEMENT_VERB_DELETE, displayName: 'Delete', group: 'general' },
  { verb: ELEMENT_VERB_PUBLISH, displayName: 'Publish', group: 'general' },
  { verb: ELEMENT_VERB_UNPUBLISH, displayName: 'Unpublish', group: 'general' },
  { verb: ELEMENT_VERB_DUPLICATE, displayName: 'Duplicate', group: 'structure' },
  { verb: ELEMENT_VERB_MOVE, displayName: 'Move', group: 'structure' },
  { verb: ELEMENT_VERB_ROLLBACK, displayName: 'Rollback', group: 'administration' },
];

const ELEMENT_ONLY = new Set<string>(ELEMENT_ONLY_VERBS);

/**
 * Resolves which scope sides apply for a verb on a library node, per the design matrix:
 *
 * - Folder, shared verbs (Read/Create/Update/Delete/Move): both sides — the folder itself and the items inside.
 * - Folder, element-only verbs (Publish/Unpublish/Duplicate/Rollback): descendants only — the folder
 *   isn't an element, but the rule still applies to the elements inside it.
 * - Element (leaf), Create: not applicable — a leaf element can't have children.
 * - Element (leaf), every other verb: the node itself only — leaves have no descendants.
 *
 * @param verb The canonical element verb.
 * @param isFolder Whether the node is an element folder (container) rather than an element.
 * @returns The applicable sides; when both are false the cell is fully N/A.
 */
export function libraryApplicability(verb: string, isFolder: boolean): VerbApplicability {
  if (isFolder) {
    if (ELEMENT_ONLY.has(verb)) {
      return { nodeApplicable: false, descApplicable: true };
    }
    return { nodeApplicable: true, descApplicable: true };
  }

  // Leaf element.
  if (verb === ELEMENT_VERB_CREATE) {
    return { nodeApplicable: false, descApplicable: false };
  }
  return { nodeApplicable: true, descApplicable: false };
}
