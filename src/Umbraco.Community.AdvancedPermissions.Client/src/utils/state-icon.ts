/**
 * Maps a permission state class to its glyph (✓ / ✗ / —).
 * Centralised so the editor, viewer, and shared components all render the same icon set.
 *
 * @param cls The state class to render.
 * @returns The single-character glyph for that state.
 */
export function stateIcon(cls: 'allow' | 'deny' | 'inherit' | 'na' | string): string {
  if (cls === 'allow') return '✓'; // ✓
  if (cls === 'deny') return '✗'; // ✗
  return '—'; // — (inherit / na / unknown)
}
