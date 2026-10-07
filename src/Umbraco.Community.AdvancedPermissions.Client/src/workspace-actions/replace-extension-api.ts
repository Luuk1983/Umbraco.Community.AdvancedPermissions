/**
 * The parts of an extension manifest this module reads or writes. Every other field (type, kind,
 * weight, meta, conditions, ...) is carried over untouched.
 */
export interface ReplaceableManifest {
  /** The extension's registry alias. */
  alias: string;
  /** The extension's display name. */
  name: string;
}

/**
 * The parts of Umbraco's extension registry this module uses. Taken as an interface so the swap
 * can be unit tested without the backoffice runtime.
 */
export interface ReplaceableRegistry<TManifest extends ReplaceableManifest> {
  /**
   * Gets the registered manifest for an alias.
   * @param alias The alias to look up.
   * @returns The manifest, or undefined when nothing is registered under the alias.
   */
  getByAlias(alias: string): TManifest | undefined;

  /**
   * Removes the manifest registered under an alias.
   * @param alias The alias to remove.
   */
  unregister(alias: string): void;

  /**
   * Registers a manifest.
   * @param manifest The manifest to register.
   */
  register(manifest: TManifest): void;
}

/**
 * Swaps the api of a registered extension for the package's own, keeping the alias and every other
 * manifest field (kind, weight, meta, conditions) exactly as the native registration has them.
 *
 * Inheriting the native manifest rather than redeclaring it keeps placement, label and conditions
 * in step with whichever Umbraco version is installed, and keeping the alias keeps every extension
 * that targets it (workspace action menu items via forWorkspaceActions) attached.
 * @typeParam TManifest The registry's manifest type.
 * @param registry The extension registry.
 * @param alias The alias of the extension whose api is replaced.
 * @param replacement The package's name and api for the extension.
 * @param replacement.name The display name of the replacement registration.
 * @param replacement.api The package's api (class or loader).
 * @returns True when the api was replaced; false when no native extension was registered.
 */
export function replaceExtensionApi<TManifest extends ReplaceableManifest>(
  registry: ReplaceableRegistry<TManifest>,
  alias: string,
  replacement: { name: string; api: unknown },
): boolean {
  const native = registry.getByAlias(alias);
  if (!native) {
    console.warn(`[AdvancedPermissions] Cannot replace '${alias}': it is not registered.`);
    return false;
  }

  registry.unregister(alias);
  registry.register({ ...native, name: replacement.name, api: replacement.api });
  return true;
}
