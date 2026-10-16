import type {
  EffectivePermission,
  StartNodeAccess,
  StartNodeSubjectState,
  StartNodeTree,
  StartNodesResponse,
} from '../models/permission.models.js';

/**
 * Display decisions for start nodes, kept out of the Lit elements so they can be tested without a DOM.
 * The server decides reachability; this module only turns its answers into what the viewers show.
 */

/** A localization key plus its arguments. */
export interface TermRef {
  key: string;
  args: string[];
}

/** The marker a viewer row shows for start nodes. */
export type StartNodeBadge = 'startNode' | 'pathOnly' | 'notVisible' | null;

/**
 * True when start nodes stop the subject from reaching the node, whatever the permissions say.
 * @param access The node's start-node access; null or undefined while it is still loading.
 * @returns Whether the node's cells render as blocked.
 */
export function isBlocked(access: StartNodeAccess | null | undefined): boolean {
  return access === 'PathOnly' || access === 'NotVisible';
}

/**
 * Picks the start-node marker for a row.
 * @param nodeKey The row's node key.
 * @param access The node's start-node access.
 * @param startNodes The subject's start nodes, or null when not loaded.
 * @returns The badge to show, or null for none.
 */
export function rowBadge(
  nodeKey: string,
  access: StartNodeAccess | null | undefined,
  startNodes: StartNodesResponse | null,
): StartNodeBadge {
  if (access === 'PathOnly') return 'pathOnly';
  if (access === 'NotVisible') return 'notVisible';
  const key = nodeKey.toLowerCase();
  if (startNodes?.startNodes.some((n) => n.key.toLowerCase() === key)) return 'startNode';
  return null;
}

/** One start node in effect for the subject, with who set it. */
export interface StartNodeLine {
  /** The start node's key. */
  key: string;
  /** The start node's name. */
  name: string;
  /** True when the start node is set on the user account itself. */
  fromUser: boolean;
  /** The names of the user groups that set this start node. */
  groups: string[];
}

/**
 * The sentence that opens the start-node card: whether start nodes limit the subject at all.
 * @param state The subject's start-node state.
 * @param subjectKind Whether the subject is a user or a single user group.
 * @param tree Which start nodes the card is about.
 * @returns The localization key.
 */
export function stripIntroTerm(
  state: StartNodeSubjectState,
  subjectKind: 'user' | 'role',
  tree: StartNodeTree = 'content',
): string {
  if (state === 'NotApplicable') return 'uap_startNodes_introNotApplicable';
  const prefix = tree === 'library' ? 'uap_startNodes_library_' : 'uap_startNodes_';
  const suffix = subjectKind === 'role' ? 'Role' : 'User';
  switch (state) {
    case 'Restricted':
      return `${prefix}introRestricted${suffix}`;
    case 'RootAccess':
      return `${prefix}introRoot${suffix}`;
    default:
      return `${prefix}introNoAccess${suffix}`;
  }
}

/**
 * The start nodes that actually limit the subject, each with the sources that set it. Configured
 * start nodes that Umbraco dropped (covered, replaced, ignored, none) change nothing, so they are left out.
 * @param data The subject's start nodes.
 * @returns One line per effective start node, in the server's order.
 */
export function startNodeLines(data: StartNodesResponse): StartNodeLine[] {
  return data.startNodes.map((node) => {
    const key = node.key.toLowerCase();
    const setters = data.sources.filter(
      (s) => s.status === 'InUse' && s.startNodeKey?.toLowerCase() === key,
    );
    return {
      key: node.key,
      name: node.name,
      fromUser: setters.some((s) => s.kind === 'User'),
      groups: setters
        .filter((s) => s.kind === 'UserGroup')
        .map((s) => s.userGroupName ?? s.userGroupAlias ?? ''),
    };
  });
}

/**
 * Who set one effective start node.
 * @param line The start-node line.
 * @returns The localization key and its arguments, or null when no source matched.
 */
export function startNodeSourceTerm(line: StartNodeLine): TermRef | null {
  const groups = line.groups.join(', ');
  if (line.fromUser && line.groups.length > 0) return { key: 'uap_startNodes_sourceUserAndGroups', args: [groups] };
  if (line.fromUser) return { key: 'uap_startNodes_sourceUser', args: [] };
  if (line.groups.length > 0) return { key: 'uap_startNodes_sourceGroups', args: [groups] };
  return null;
}

/**
 * The banner of the reasoning dialog for a blocked cell.
 * @param access A blocked start-node access.
 * @param subjectWide True when the block is for the whole subject (Library Insert Viewer: no Library access).
 * @returns The localization key.
 */
export function blockedBannerTerm(access: StartNodeAccess, subjectWide = false): string {
  if (subjectWide) return 'uap_startNodes_library_blockedNoAccess';
  return access === 'PathOnly' ? 'uap_startNodes_blockedPathOnly' : 'uap_startNodes_blockedNotVisible';
}

/**
 * The heading above the greyed preview of the permissions in the blocked dialog.
 * @param subjectWide True when the block is for the whole subject.
 * @returns The localization key.
 */
export function blockedHeadingTerm(subjectWide = false): string {
  return subjectWide ? 'uap_startNodes_library_ifAccessHeading' : 'uap_startNodes_ifInsideHeading';
}

/**
 * The info note of the reasoning dialog for a blocked cell: the subject's start nodes. The "no access
 * at all" note is only for the NoAccess state; an empty list for RootAccess or NotApplicable (for
 * example a node deleted after the tree loaded) gets the plain note.
 * @param startNodes The subject's start nodes, or null when they could not be loaded.
 * @param tree Which start nodes they are.
 * @returns The localization key and its arguments.
 */
export function blockedNoteTerm(startNodes: StartNodesResponse | null, tree: StartNodeTree = 'content'): TermRef {
  const prefix = tree === 'library' ? 'uap_startNodes_library_' : 'uap_startNodes_';
  if (!startNodes) return { key: 'uap_startNodes_blockedNoListNote', args: [] };
  if (startNodes.state === 'NoAccess') return { key: `${prefix}blockedNoAccessNote`, args: [] };
  if (startNodes.startNodes.length === 0) return { key: 'uap_startNodes_blockedNoListNote', args: [] };
  return { key: `${prefix}blockedNote`, args: [startNodes.startNodes.map((n) => n.name).join(', ')] };
}

/**
 * The extra card line for the Library Insert Viewer: a restricted subject can only create inside its
 * Library start nodes, not at the Library root.
 * @param state The subject's Library start-node state; null while loading.
 * @returns The localization key, or null when no note applies.
 */
export function createNoteTerm(state: StartNodeSubjectState | null | undefined): string | null {
  return state === 'Restricted' ? 'uap_startNodes_library_createInsideNote' : null;
}

/** What a viewer cell shows. */
export type CellMode = 'na' | 'loading' | 'blocked' | 'value';

/**
 * Decides what a viewer cell shows. A verb that cannot apply to the node kind stays N/A even on a
 * blocked row, because the verb would never be available there, start nodes or not.
 * @param applicable Whether the verb applies to the node kind.
 * @param loaded Whether the row's effective permissions have arrived.
 * @param access The row's start-node access.
 * @returns The cell mode.
 */
export function cellMode(applicable: boolean, loaded: boolean, access: StartNodeAccess | null | undefined): CellMode {
  if (!applicable) return 'na';
  if (!loaded) return 'loading';
  if (isBlocked(access)) return 'blocked';
  return 'value';
}

/**
 * A stand-in permission for a blocked cell that has none. The reasoning dialog draws its banner and
 * note only when it is given a permission, and the blocked explanation never reads the allow/deny
 * value, so a node that vanished after the tree loaded (or a doc type missing from a stale list)
 * still shows why it is blocked.
 * @param verb The verb the cell stands for.
 * @returns A deny with no reasoning.
 */
export function blockedPlaceholderPermission(verb: string): EffectivePermission {
  return { verb, isAllowed: false, isExplicit: false, reasoning: [] };
}
