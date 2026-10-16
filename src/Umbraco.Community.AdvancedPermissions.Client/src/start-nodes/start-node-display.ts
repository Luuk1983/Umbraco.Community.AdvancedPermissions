import type {
  EffectivePermission,
  StartNodeAccess,
  StartNodeSubjectState,
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
 * @returns The localization key.
 */
export function stripIntroTerm(state: StartNodeSubjectState, subjectKind: 'user' | 'role'): string {
  const suffix = subjectKind === 'role' ? 'Role' : 'User';
  switch (state) {
    case 'Restricted':
      return `uap_startNodes_introRestricted${suffix}`;
    case 'RootAccess':
      return `uap_startNodes_introRoot${suffix}`;
    case 'NoAccess':
      return `uap_startNodes_introNoAccess${suffix}`;
    default:
      return 'uap_startNodes_introNotApplicable';
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
 * @returns The localization key.
 */
export function blockedBannerTerm(access: StartNodeAccess): string {
  return access === 'PathOnly' ? 'uap_startNodes_blockedPathOnly' : 'uap_startNodes_blockedNotVisible';
}

/**
 * The info note of the reasoning dialog for a blocked cell: the subject's start nodes. The "no content
 * access at all" note is only for the NoAccess state; an empty list for RootAccess or NotApplicable
 * (for example a node deleted after the tree loaded) gets the plain note.
 * @param startNodes The subject's start nodes, or null when they could not be loaded.
 * @returns The localization key and its arguments.
 */
export function blockedNoteTerm(startNodes: StartNodesResponse | null): TermRef {
  if (!startNodes) return { key: 'uap_startNodes_blockedNoListNote', args: [] };
  if (startNodes.state === 'NoAccess') return { key: 'uap_startNodes_blockedNoAccessNote', args: [] };
  if (startNodes.startNodes.length === 0) return { key: 'uap_startNodes_blockedNoListNote', args: [] };
  return { key: 'uap_startNodes_blockedNote', args: [startNodes.startNodes.map((n) => n.name).join(', ')] };
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
