/**
 * The registry alias of the document user-permission condition. Umbraco registers its native
 * condition under it; this package's entrypoint swaps in the Advanced Permissions replacement under
 * the same alias, so anything resolved by this alias gets the package's per-node verdict.
 */
export const DOCUMENT_PERMISSION_CONDITION_ALIAS = 'Umb.Condition.UserPermission.Document';

/**
 * The constructor arguments a document user-permission condition takes (after its host).
 */
export interface DocumentPermissionConditionArgs {
  /** The condition config: the verbs that must all be granted. */
  config: { alias: string; allOf: Array<string> };
  /** Called by the condition whenever its permitted state changes. */
  onChange: (permitted: boolean) => void;
}

/**
 * The shape of Umbraco's <c>createExtensionApiByAlias</c>, narrowed to what the gate passes. Taken
 * as a parameter so the gate stays free of runtime backoffice imports and can be unit tested.
 */
export type CreateApiByAlias = (
  host: never,
  alias: string,
  constructorArgs: [DocumentPermissionConditionArgs],
) => Promise<unknown>;

/**
 * Something that can be switched between clickable and not — a workspace action.
 */
export interface Toggleable {
  /** Makes the action clickable. */
  enable(): void;
  /** Makes the action unclickable. */
  disable(): void;
}

/**
 * Enables or disables an action according to the document user-permission condition, resolved by
 * its registry alias rather than by importing a specific condition class.
 *
 * Umbraco's native Save and Publish action constructs <c>UmbDocumentUserPermissionCondition</c>
 * directly, so a condition replaced in the registry never reaches it and the button follows the
 * native (fallback-permission) check instead. Resolving by alias — as Umbraco's own document
 * workspace context does for its read-only rules — lets the registered replacement decide.
 * The action is disabled up front and stays disabled if the condition cannot be created: the
 * condition only reports changes, so deny-by-default is the safe starting state.
 * @param createApiByAlias Umbraco's <c>createExtensionApiByAlias</c> (injected for testability).
 * @param host The controller host the condition is attached to; it is destroyed with the host.
 * @param action The action to enable and disable.
 * @param allOf The verbs that must all be granted for the action to be enabled.
 * @returns A promise settling once the condition has been created (or failed to be).
 */
export async function gateOnDocumentPermission(
  createApiByAlias: CreateApiByAlias,
  host: unknown,
  action: Toggleable,
  allOf: Array<string>,
): Promise<void> {
  action.disable();

  try {
    await createApiByAlias(host as never, DOCUMENT_PERMISSION_CONDITION_ALIAS, [
      {
        config: { alias: DOCUMENT_PERMISSION_CONDITION_ALIAS, allOf },
        onChange: (permitted) => (permitted ? action.enable() : action.disable()),
      },
    ]);
  } catch (error: unknown) {
    console.error('[AdvancedPermissions] Could not resolve the document permission condition.', error);
  }
}
