import { UapElementPermissionConditionBase, type UapElementPermissionConditionConfig } from './element-permission-condition.base.js';
import { satisfiesVerbs } from './element-verbs.js';
import { ELEMENT_ENTITY_TYPE } from '../models/element-permission.models.js';

/**
 * Replacement for Umbraco's built-in <c>UmbElementUserPermissionCondition</c>. Resolves the current
 * user's effective element permissions per node (the element verbs are already canonical, so no verb
 * mapping is needed). Registered under the native condition alias in entrypoint.ts.
 */
export class UapElementUserPermissionCondition extends UapElementPermissionConditionBase<
  UapElementPermissionConditionConfig<'Umb.Condition.UserPermission.Element'>
> {
  /** @inheritdoc */
  protected override appliesTo(entityType: string): boolean {
    return entityType === ELEMENT_ENTITY_TYPE;
  }

  /** @inheritdoc */
  protected override isPermitted(allowed: ReadonlySet<string>): boolean {
    return satisfiesVerbs(allowed, this.config);
  }
}

export { UapElementUserPermissionCondition as api };
