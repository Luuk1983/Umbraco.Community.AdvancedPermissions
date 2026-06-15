import { UapElementPermissionConditionBase } from './element-permission-condition.base.js';
import { ELEMENT_ENTITY_TYPE } from '../models/element-permission.models.js';

/**
 * Replacement for Umbraco's built-in <c>UmbElementUserPermissionCondition</c>. Resolves the current
 * user's effective element permissions via the package's own endpoint (the element verbs are already
 * canonical, so no verb mapping is needed). Registered under the native condition alias in entrypoint.ts.
 */
export class UapElementUserPermissionCondition extends UapElementPermissionConditionBase<'Umb.Condition.UserPermission.Element'> {
  /** @inheritdoc */
  protected override get entityType(): string {
    return ELEMENT_ENTITY_TYPE;
  }

  /** @inheritdoc */
  protected override mapVerb(verb: string): string {
    return verb;
  }
}

export { UapElementUserPermissionCondition as api };
