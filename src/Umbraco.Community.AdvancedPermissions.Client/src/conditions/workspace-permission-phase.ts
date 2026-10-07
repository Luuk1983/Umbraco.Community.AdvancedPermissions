/** The verbs a permission condition is configured with. */
export interface PermissionCheckConfig {
  /** Every one of these verbs must be allowed. */
  allOf?: Array<string>;
  /** At least one of these verbs must be allowed. */
  oneOf?: Array<string>;
}

/** The verb the workspace checks while a new document is being created. */
const CREATE_VERB = 'Umb.Document.Create';

/** The verb the workspace checks once the document exists. */
const UPDATE_VERB = 'Umb.Document.Update';

/**
 * Decides whether one of the document workspace's own read-only checks no longer applies.
 *
 * Every time the workspace switches between "new" and "existing" it starts another check — Create
 * while new, Update once saved — but it never stops the previous one, which keeps re-evaluating
 * against whatever document the workspace now shows. A Create check asked about a saved document,
 * or an Update check asked about a document still being created, answers the wrong question and
 * can make the workspace read-only (issue #58). Only applies to checks the workspace itself owns;
 * the caller establishes that.
 * @param config The verbs the check was configured with.
 * @param isNew Whether the workspace is creating a new document; undefined while not yet known.
 * @returns True when the check should stop restricting the workspace.
 */
export function isWorkspaceCheckOutOfPhase(config: PermissionCheckConfig, isNew: boolean | undefined): boolean {
  if (isNew === undefined || config.oneOf?.length || config.allOf?.length !== 1) {
    return false;
  }

  const verb = config.allOf[0];
  return isNew ? verb === UPDATE_VERB : verb === CREATE_VERB;
}
