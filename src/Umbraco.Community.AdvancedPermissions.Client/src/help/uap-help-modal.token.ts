import { UmbModalToken } from '@umbraco-cms/backoffice/modal';
import type { HelpDocId } from './help-content.js';

/** Input for the help modal. All fields optional so any caller can open it (e.g. an inline ⓘ). */
export interface HelpModalData {
  /** Headline shown at the top of the modal (usually the surface name). */
  headline?: string;
  /** The per-page how-to document to show in "About this page". Omit to hide that tab. */
  howToDoc?: HelpDocId;
  /** Optional concept anchor id to scroll to on open (selects the Concepts tab). */
  scrollToConcept?: string;
}

/** The help modal returns no value (dismiss-only). */
export type HelpModalValue = undefined;

/** Modal token for the Advanced Permissions help modal. */
export const UAP_HELP_MODAL = new UmbModalToken<HelpModalData, HelpModalValue>(
  'UAP.Modal.Help',
  { modal: { type: 'sidebar', size: 'medium' } },
);
