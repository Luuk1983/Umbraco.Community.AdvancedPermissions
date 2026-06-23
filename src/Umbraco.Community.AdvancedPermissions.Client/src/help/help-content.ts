/// <reference types="vite/client" />
import { helpLocale } from './markdown.js';

/** Identifies a long-form help document (a per-page how-to or the shared concepts reference). */
export type HelpDocId =
  | 'concepts'
  | 'content-permissions'
  | 'access-viewer'
  | 'doc-type-permissions'
  | 'insert-options-viewer'
  | 'library-permissions'
  | 'element-type-permissions'
  | 'library-access-viewer'
  | 'library-insert-viewer';

/** Help configuration for a single editor/viewer surface. */
export interface SurfaceHelp {
  /** Localization key for the always-visible one-line description. */
  descriptionKey: string;
  /** The per-page how-to document shown in the modal's "About this page" section. */
  howToDoc?: HelpDocId;
}

/** Maps a workspace entityType to its help configuration. */
export const SURFACE_HELP: Record<string, SurfaceHelp> = {
  'uap-permissions-editor': {
    descriptionKey: 'uap_help_contentPermissions_description',
    howToDoc: 'content-permissions',
  },
  'uap-access-viewer': {
    descriptionKey: 'uap_help_accessViewer_description',
    howToDoc: 'access-viewer',
  },
  'uap-doc-type-permissions': {
    descriptionKey: 'uap_help_docTypePermissions_description',
    howToDoc: 'doc-type-permissions',
  },
  'uap-doc-type-create-audit': {
    descriptionKey: 'uap_help_insertOptions_description',
    howToDoc: 'insert-options-viewer',
  },
  'uap-library-permissions': {
    descriptionKey: 'uap_help_libraryPermissions_description',
    howToDoc: 'library-permissions',
  },
  'uap-element-type-permissions': {
    descriptionKey: 'uap_help_elementTypePermissions_description',
    howToDoc: 'element-type-permissions',
  },
  'uap-library-access-viewer': {
    descriptionKey: 'uap_help_libraryAccessViewer_description',
    howToDoc: 'library-access-viewer',
  },
  'uap-library-insert-viewer': {
    descriptionKey: 'uap_help_libraryInsertViewer_description',
    howToDoc: 'library-insert-viewer',
  },
};

/** The shared concepts document id, shown in the modal's "Concepts" section on every surface. */
export const CONCEPTS_DOC: HelpDocId = 'concepts';

/**
 * Lazily-loaded raw Markdown keyed by path relative to this file. Vite turns each
 * `.md` file into its own chunk loaded on demand (not bundled into initial load).
 */
const docModules = import.meta.glob('../../help-docs/**/*.md', {
  query: '?raw',
  import: 'default',
}) as Record<string, () => Promise<string>>;

/**
 * Loads the raw Markdown for a document in the current backoffice locale,
 * falling back to English when a localized file is missing.
 *
 * @param doc The document id to load.
 * @returns The raw Markdown source, or an empty string if not found.
 */
export async function loadHelpDoc(doc: HelpDocId): Promise<string> {
  const locale = helpLocale();
  const localized = `../../help-docs/${locale}/${doc}.md`;
  const fallback = `../../help-docs/en/${doc}.md`;
  const loader = docModules[localized] ?? docModules[fallback];
  if (!loader) return '';
  return loader();
}
