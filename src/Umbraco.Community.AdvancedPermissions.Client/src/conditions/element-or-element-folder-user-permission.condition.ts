import type { UmbConditionConfigBase } from '@umbraco-cms/backoffice/extension-api';
import { UapElementPermissionConditionBase } from './element-permission-condition.base.js';
import { satisfiesElementOrFolder, type VerbRequirement } from './element-verbs.js';

/** Config of Umbraco's "element or element folder" permission condition. */
type UapElementOrElementFolderPermissionConditionConfig =
  UmbConditionConfigBase<'Umb.Condition.UserPermission.ElementOrElementFolder'> & {
    /** Element verbs; met when the user has them at the node. */
    element?: VerbRequirement;
    /** Folder (container) verbs; met when the user has them at the node. */
    folder?: VerbRequirement;
  };

/**
 * Replacement for Umbraco's built-in <c>UmbElementOrElementFolderUserPermissionCondition</c>, which gates
 * the Library Create actions (tree, collection) on "may create an element OR a folder here".
 *
 * The native condition constructs the native element and folder conditions directly instead of resolving
 * them by alias, so the package's replacements for those never decide it — the same bypass as the native
 * Save and publish action (#61). This replacement decides both requirements against the node's resolved
 * permissions in one request. In the package's model both requirements resolve to the same canonical
 * verbs on the same node, so the entity type does not matter. Registered under the native alias in
 * entrypoint.ts.
 */
export class UapElementOrElementFolderUserPermissionCondition extends UapElementPermissionConditionBase<UapElementOrElementFolderPermissionConditionConfig> {
  /** @inheritdoc */
  protected override appliesTo(): boolean {
    return true;
  }

  /** @inheritdoc */
  protected override isPermitted(allowed: ReadonlySet<string>): boolean {
    return satisfiesElementOrFolder(allowed, this.config.element, this.config.folder);
  }
}

export { UapElementOrElementFolderUserPermissionCondition as api };
