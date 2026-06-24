import { marked } from 'marked';

/**
 * Resolves the help-docs locale folder for the current backoffice culture.
 * The backoffice sets `<html lang>` to the active culture; anything starting
 * with "nl" maps to Dutch, everything else falls back to English.
 *
 * @returns The locale folder name to load Markdown docs from.
 */
export function helpLocale(): 'en' | 'nl' {
  const lang = (document.documentElement.lang || 'en').toLowerCase();
  return lang.startsWith('nl') ? 'nl' : 'en';
}

/**
 * Renders first-party Markdown to an HTML string. The content is authored in
 * this repository, so it is trusted and rendered without sanitization.
 *
 * @param md The raw Markdown source.
 * @returns The rendered HTML string (empty string for empty input).
 */
export function renderMarkdown(md: string): string {
  if (!md) return '';
  return marked.parse(md, { async: false }) as string;
}
