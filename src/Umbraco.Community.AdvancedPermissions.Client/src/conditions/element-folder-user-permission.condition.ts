import { UapElementPermissionConditionBase } from './element-permission-condition.base.js';
import { ELEMENT_FOLDER_ENTITY_TYPE, toCanonicalElementVerb } from '../models/element-permission.models.js';

/**
 * Replacement for Umbraco's built-in <c>UmbElementFolderUserPermissionCondition</c>. The package stores
 * folder permissions against the canonical <c>Umb.Element.*</c> verbs, so this condition maps each
 * configured <c>Umb.ElementContainer.*</c> verb to its canonical equivalent before checking the resolved
 * permission set. Registered under the native condition alias in entrypoint.ts.
 */
export class UapElementFolderUserPermissionCondition extends UapElementPermissionConditionBase<'Umb.Condition.UserPermission.ElementFolder'> {
  /** @inheritdoc */
  protected override get entityType(): string {
    return ELEMENT_FOLDER_ENTITY_TYPE;
  }

  /** @inheritdoc */
  protected override mapVerb(verb: string): string {
    return toCanonicalElementVerb(verb);
  }
}

export { UapElementFolderUserPermissionCondition as api };
