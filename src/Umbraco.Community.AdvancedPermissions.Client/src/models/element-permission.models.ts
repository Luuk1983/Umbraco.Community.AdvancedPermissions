import type { TreeNode, TreeNodeState } from './permission.models.js';

// ── Entity types (mirror Umbraco 18 packages/elements/entity.ts) ──────────────

/** Backoffice entity type for a library element (a reusable block). */
export const ELEMENT_ENTITY_TYPE = 'element';

/** Backoffice entity type for a library element folder (container). */
export const ELEMENT_FOLDER_ENTITY_TYPE = 'element-folder';

// ── Canonical element verbs (the nine Umb.Element.* the package stores) ───────

export const ELEMENT_VERB_READ = 'Umb.Element.Read';
export const ELEMENT_VERB_CREATE = 'Umb.Element.Create';
export const ELEMENT_VERB_UPDATE = 'Umb.Element.Update';
export const ELEMENT_VERB_DELETE = 'Umb.Element.Delete';
export const ELEMENT_VERB_PUBLISH = 'Umb.Element.Publish';
export const ELEMENT_VERB_UNPUBLISH = 'Umb.Element.Unpublish';
export const ELEMENT_VERB_DUPLICATE = 'Umb.Element.Duplicate';
export const ELEMENT_VERB_MOVE = 'Umb.Element.Move';
export const ELEMENT_VERB_ROLLBACK = 'Umb.Element.Rollback';

/** The nine canonical element verbs, in display order. */
export const ELEMENT_VERBS: readonly string[] = [
  ELEMENT_VERB_READ,
  ELEMENT_VERB_CREATE,
  ELEMENT_VERB_UPDATE,
  ELEMENT_VERB_DELETE,
  ELEMENT_VERB_PUBLISH,
  ELEMENT_VERB_UNPUBLISH,
  ELEMENT_VERB_DUPLICATE,
  ELEMENT_VERB_MOVE,
  ELEMENT_VERB_ROLLBACK,
];

/** The four element-only verbs that have no folder (container) counterpart. */
export const ELEMENT_ONLY_VERBS: readonly string[] = [
  ELEMENT_VERB_PUBLISH,
  ELEMENT_VERB_UNPUBLISH,
  ELEMENT_VERB_DUPLICATE,
  ELEMENT_VERB_ROLLBACK,
];

/**
 * Maps each native element-folder (container) verb to the canonical Umb.Element.* verb the package
 * stores and resolves. Mirrors the backend `ElementContainerVerbToCanonical`. The folder condition uses
 * this to translate its `Umb.ElementContainer.*` config verbs before checking the resolved (canonical)
 * permission set.
 */
export const ELEMENT_CONTAINER_VERB_TO_CANONICAL: Readonly<Record<string, string>> = {
  'Umb.ElementContainer.Read': ELEMENT_VERB_READ,
  'Umb.ElementContainer.Create': ELEMENT_VERB_CREATE,
  'Umb.ElementContainer.Update': ELEMENT_VERB_UPDATE,
  'Umb.ElementContainer.Delete': ELEMENT_VERB_DELETE,
  'Umb.ElementContainer.Move': ELEMENT_VERB_MOVE,
};

/**
 * Translates a verb to its canonical element verb. Container verbs map via
 * {@link ELEMENT_CONTAINER_VERB_TO_CANONICAL}; element verbs (and anything else) pass through unchanged.
 */
export function toCanonicalElementVerb(verb: string): string {
  return ELEMENT_CONTAINER_VERB_TO_CANONICAL[verb] ?? verb;
}

// ── Tree node shapes ──────────────────────────────────────────────────────────

/**
 * A library tree node (element or folder) with stored permission entries for a given role. Extends the
 * shared {@link TreeNode} with the kind flag that drives per-node-kind verb applicability in the editor.
 */
export interface ElementTreeNode extends TreeNode {
  /** Whether this node is an element folder (container) rather than an element. */
  isFolder: boolean;
}

/** An {@link ElementTreeNode} augmented with client-side expand/load state. */
export interface ElementTreeNodeState extends TreeNodeState {
  isFolder: boolean;
  children?: ElementTreeNodeState[];
}
