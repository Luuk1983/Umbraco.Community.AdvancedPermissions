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
}

/** A content tree node with stored permission entries for a given role. */
export interface TreeNode {
  key: string;
  name: string;
  icon: string | null;
  hasChildren: boolean;
  entries: PermissionEntry[];
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
}

/** Fully resolved permission for a single verb at a node, with reasoning. */
export interface EffectivePermission {
  verb: string;
  isAllowed: boolean;
  isExplicit: boolean;
  reasoning: ReasoningStep[];
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

/** A pending verb-level change for a node in the Security Editor. */
export interface PendingVerbChange {
  /** Entries to set for this verb. Empty array means "clear/inherit". */
  entries: Array<{ state: PermissionState; scope: PermissionScope }>;
}
