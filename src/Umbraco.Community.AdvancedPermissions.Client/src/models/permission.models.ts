/** State of a single permission entry. */
export type PermissionState = 'Allow' | 'Deny';

/** Scope of a single permission entry relative to the node it is set on. */
export type PermissionScope = 'ThisNodeOnly' | 'ThisNodeAndDescendants' | 'DescendantsOnly';

/**
 * Sentinel GUID used as the NodeKey for virtual-root (default) permission entries.
 * Replaces null to make the column non-nullable and eliminate the risk of accidental null matches.
 */
export const VIRTUAL_ROOT_NODE_KEY = 'ffffffff-ffff-ffff-ffff-ffffffffffff';

/** A stored permission entry returned from the API. */
export interface PermissionEntry {
  id: string;
  nodeKey: string;
  roleAlias: string;
  verb: string;
  state: PermissionState;
  scope: PermissionScope;
  /**
   * Priority-override flag. When true on an entry that applies at the node being resolved
   * (depth 0), the resolver considers only flagged entries on that node when aggregating
   * across the user's user groups — a CSS `!important`-style escape hatch for cross-role
   * Explicit Deny. Node-local, verb-local: does NOT inherit and has NO effect on other
   * verbs or other nodes.
   */
  isPriorityOverride: boolean;
}

/** A content tree node with stored permission entries for a given role. */
export interface TreeNode {
  key: string;
  name: string;
  icon: string | null;
  hasChildren: boolean;
  entries: PermissionEntry[];
  /** The concurrency stamp of this node's entries, sent back on save. */
  stamp: string;
}

/** A tree node augmented with client-side expand/load state. */
export interface TreeNodeState extends TreeNode {
  children?: TreeNodeState[];
  expanded: boolean;
  loading: boolean;
}

/** A single step in the reasoning chain explaining an effective permission result. */
export interface ReasoningStep {
  contributingRole: string;
  state: string;
  isExplicit: boolean;
  sourceNodeKey: string;
  sourceScope: string | null;
  isFromGroupDefault: boolean;
  /** True if this contribution came from an entry flagged with priority override. */
  isPriorityOverride: boolean;
}

/** Fully resolved permission for a single verb at a node, with reasoning. */
export interface EffectivePermission {
  verb: string;
  isAllowed: boolean;
  isExplicit: boolean;
  reasoning: ReasoningStep[];
  /**
   * True when the priority-override shortcircuit fired — at least one applicable entry on
   * the resolved node carried the override flag and the resolver considered only flagged
   * entries when aggregating across the user's user groups.
   */
  wasPriorityOverrideActive?: boolean;
  /**
   * Reasoning entries that would have applied under normal resolution but were suppressed
   * by the priority override path. Empty/undefined when no override fired.
   */
  suppressedReasoning?: ReasoningStep[];
}

/** Collection of effective permissions for all verbs at a specific node. */
export interface EffectivePermissions {
  nodeKey: string;
  permissions: EffectivePermission[];
}

/** A permission verb with its display name. */
export interface VerbInfo {
  verb: string;
  displayName: string;
}

/** A role (user group or $everyone) available for permission assignment. */
export interface RoleInfo {
  alias: string;
  name: string;
  isEveryone: boolean;
}

/** A user item for display in the user picker. */
export interface UserItem {
  unique: string;
  name: string;
  avatarUrls: string[];
}

/** A node in the inheritance path from root to target. */
export interface PathNode {
  key: string;
  name: string;
  icon: string | null;
}

/** Response from the permissions-for-path endpoint. */
export interface PathEntriesResponse {
  path: PathNode[];
  entries: PermissionEntry[];
}

/**
 * Stored entries for a node+role pair, together with their concurrency stamp.
 * The stamp comes from the response's `ETag` header rather than the JSON body — see
 * `getPermissionsWithStamp` in the API layer for why. An empty `stamp` means the server sent no
 * `ETag`, and the caller must treat that as "no stamp available" rather than a real value.
 */
export interface PermissionEntriesWithStamp {
  entries: PermissionEntry[];
  stamp: string;
}

/** A pending verb-level change for a node in the Security Editor. */
export interface PendingVerbChange {
  /** Entries to set for this verb. Empty array means "clear/inherit". */
  entries: Array<{ state: PermissionState; scope: PermissionScope; isPriorityOverride: boolean }>;
}

/** One node-and-user-group pair in a batch save, with the stamp the client read. */
export interface BatchSaveNode {
  /** The content node key, or VIRTUAL_ROOT_NODE_KEY for the virtual root. */
  nodeKey: string;
  /** The user group alias. */
  roleAlias: string;
  /** The replacement entries. An empty list removes all entries for the pair. */
  entries: Array<{ verb: string; state: PermissionState; scope: PermissionScope; isPriorityOverride: boolean }>;
  /** The stamp this client was given when it read these entries. */
  expectedStamp: string | undefined;
}

/** One pair's new stamp after a successful batch save. */
export interface BatchSavedStamp {
  /** The content node key. */
  nodeKey: string;
  /** The user group alias. */
  roleAlias: string;
  /** The stamp of what was just written. */
  stamp: string;
}

/** One pair the server refused because its stored entries moved. */
export interface BatchSaveConflict {
  /** The content node key. */
  nodeKey: string;
  /** The user group alias. */
  roleAlias: string;
  /** What is stored right now — what the dialog shows as "stored". */
  currentEntries: PermissionEntry[];
  /** The stamp of `currentEntries`, so a resolved conflict can retry without a further read. */
  currentStamp: string;
}

/** The result of a batch save: either it wrote, or it named what moved. */
export type BatchSaveResult =
  | { ok: true; stamps: ReadonlyMap<string, string> }
  | { ok: false; conflicts: BatchSaveConflict[] };
